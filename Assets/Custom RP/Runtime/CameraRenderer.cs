using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer : IDisposable
{
    const string lightBufferName = "Lighting";
    const string shadowBufferName = "Render Shadows";

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

#if UNITY_EDITOR
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
#endif

    CommandBuffer lightingBuffer = new CommandBuffer
    {
        name = lightBufferName
    };

    CommandBuffer shadowBuffer = new CommandBuffer
    {
        name = shadowBufferName
    };

    public void Render(ref ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, int shadowMapSize, float shadowDistance, int shadowCascades, Vector3 shadowCascadesSplit)
    {
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        ScriptableCullingParameters cullingParameters;
        if (!camera.TryGetCullingParameters(out cullingParameters))
        {
            return;
        }
        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);

        CullingResults cullingResults = context.Cull(ref cullingParameters);

        RenderTexture shadowMaps = null;
        ShaderLighting.LightingBuffers lightingBuffers = null;
        ShaderInput.SetLightsCount(lightingBuffer, cullingResults.lightAndReflectionProbeIndexCount);
        if (cullingResults.visibleLights.Length >= 1 && cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            lightingBuffer.BeginSample(lightingBuffer.name);
            ShaderLighting.LightingValues lightingValues = ShaderLighting.CreateLightingValues(ref cullingResults, shadowMapSize, shadowDistance);
            lightingBuffer.EndSample(lightingBuffer.name);

            lightingBuffers = new ShaderLighting.LightingBuffers(ref cullingResults, lightingValues);
            lightingBuffer.BeginSample(lightingBuffer.name);
            ShaderInput.SetLightsPositions(lightingBuffer, lightingBuffers.positionsBuffer);
            ShaderInput.SetLightsColors(lightingBuffer, lightingBuffers.colorsBuffer);
            ShaderInput.SetLightsAttenuations(lightingBuffer, lightingBuffers.attenuationsBuffer);
            ShaderInput.SetLightsSpotDirections(lightingBuffer, lightingBuffers.spotDirectionsBuffer);
            ShaderInput.SetLightIndices(lightingBuffer, lightingBuffers.lightIndicesBuffer);
            ShaderInput.SetShadowData(lightingBuffer, lightingBuffers.shadowDataBuffer);
            ShaderInput.SetShadowCascades(lightingBuffer, lightingBuffers.cascadeDataBuffer);
            ShaderInput.SetWorldToShadowMatrices(lightingBuffer, lightingBuffers.worldToShadowMatricesBuffer);
            lightingBuffer.EndSample(lightingBuffer.name);
            SubmitBuffer(ref context, lightingBuffer);

            shadowBuffer.BeginSample(shadowBuffer.name);
            SubmitBuffer(ref context, shadowBuffer);
            shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
            shadowMaps.dimension = TextureDimension.Tex2DArray;
            shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
            shadowMaps.filterMode = FilterMode.Bilinear;
            shadowMaps.wrapMode = TextureWrapMode.Clamp;

            bool hasSoftShadows = false;
            bool hasHardShadows = false;
            for (int i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                CommandBuffer lightShadowBuffer = new CommandBuffer
                {
                    name = cullingResults.visibleLights[i].light.name
                };

                if (lightingValues.shadowData[i].x <= 0f)
                {
                    continue;
                }

                if (lightingValues.shadowData[i].y <= 0f)
                {
                    hasHardShadows = true;
                }
                else
                {
                    hasSoftShadows = true;
                }

                CoreUtils.SetRenderTarget(lightShadowBuffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);

                lightShadowBuffer.BeginSample(lightShadowBuffer.name);
                SubmitBuffer(ref context, lightShadowBuffer);

                for (int j = 0; j < lightingValues.cascadeData[i].x; j++)
                {
                    lightShadowBuffer.SetViewport(new Rect(0f, 0f, shadowMapSize, shadowMapSize));
                    lightShadowBuffer.EnableScissorRect(new Rect(4f, 4f, shadowMapSize - 8f, shadowMapSize - 8f));
                    lightShadowBuffer.SetViewProjectionMatrices(lightingValues.cascades[i].viewMatrices[j], lightingValues.cascades[i].projectionMatrices[j]);
                    ShaderInput.SetShadowBias(lightShadowBuffer, cullingResults.visibleLights[i].light.shadowBias);
                    SubmitBuffer(ref context, lightShadowBuffer);

                    ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, i);
                    shadowSettings.splitData = lightingValues.cascades[i].splitData[j];
                    context.DrawShadows(ref shadowSettings);
                }

                lightShadowBuffer.EndSample(lightShadowBuffer.name);
                SubmitBuffer(ref context, lightShadowBuffer);
                lightShadowBuffer.Release();
            }

            ShaderInput.SetSoftShadows(shadowBuffer, hasSoftShadows);
            ShaderInput.SetHardShadows(shadowBuffer, hasHardShadows);
            ShaderInput.SetShadowMaps(shadowBuffer, shadowMaps);
            ShaderInput.SetShadowMapsSize(shadowBuffer, new Vector4(1f / shadowMaps.width, 1f / shadowMaps.width, shadowMaps.width, shadowMaps.width));
            shadowBuffer.EndSample(shadowBuffer.name);
            shadowBuffer.DisableScissorRect();
            SubmitBuffer(ref context, shadowBuffer);
        }

        CommandBuffer cameraBuffer = new CommandBuffer
        {
            name = camera.name
        };

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        cameraBuffer.BeginSample(cameraBuffer.name);
        SubmitBuffer(ref context, cameraBuffer);
        DrawVisibleGeometry(ref context, camera, ref cullingResults, useDynamicBatching, useGPUInstancing);
        cameraBuffer.EndSample(cameraBuffer.name);
        SubmitBuffer(ref context, cameraBuffer);

#if UNITY_EDITOR
        DrawUnsupportedShaders(ref context, camera, ref cullingResults);
        DrawGizmos(ref context, camera);
#endif
        context.Submit();

        cameraBuffer.Release();
        if (lightingBuffers != null)
        {
            lightingBuffers.Dispose();
        }
        if (shadowMaps != null)
        {
            shadowMaps.Release();
        }
    }

    void SubmitBuffer(ref ScriptableRenderContext context, CommandBuffer buffer)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Dispose()
    {
        lightingBuffer.Dispose();
        shadowBuffer.Dispose();
    }

    void DrawVisibleGeometry(ref ScriptableRenderContext context, Camera camera, ref CullingResults cullingResults, bool useDynamicBatching, bool useGPUInstancing)
    {
        SortingSettings sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        DrawingSettings drawingSettings = new DrawingSettings()
        {
            sortingSettings = sortingSettings,
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.LightData

        };
        drawingSettings.SetShaderPassName(0, unlitShaderTagId);
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

#if UNITY_EDITOR
    void DrawUnsupportedShaders(ref ScriptableRenderContext context, Camera camera, ref CullingResults cullingResults)
    {
        Material errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        DrawingSettings drawingSettings = new DrawingSettings()
        {
            sortingSettings = new SortingSettings(camera),
            overrideMaterial = errorMaterial
        };
        for (int i = 0; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void DrawGizmos(ref ScriptableRenderContext context, Camera camera)
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
#endif
}
