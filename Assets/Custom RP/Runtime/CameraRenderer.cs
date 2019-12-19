using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer : IDisposable
{
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

        RenderShadows(ref context, ref cullingResults, shadowMapSize);
        SetupShadowInput(ref context);
        SetupLights(ref context, ref cullingResults, shadowMapSize, shadowDistance);
        SetLightingInput(ref context, ref cullingResults);

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
        SubmitBuffer(ref context, cameraBuffer);
        DrawVisibleGeometry(ref context, camera, ref cullingResults, useDynamicBatching, useGPUInstancing);
        SubmitBuffer(ref context, cameraBuffer);

#if UNITY_EDITOR
        DrawUnsupportedShaders(ref context, camera, ref cullingResults);
        DrawGizmos(ref context, camera);
#endif
        context.Submit();
        cameraBuffer.Release();

        Cleanup();
    }

    void Cleanup()
    {
        if (shadowDataBuffer != null)
        {
            shadowDataBuffer.Release();
            shadowDataBuffer = null;
        }
        if (shadowCascadesBuffer != null)
        {
            shadowCascadesBuffer.Release();
            shadowCascadesBuffer = null;
        }
        if (worldToShadowMatricesBuffer != null)
        {
            worldToShadowMatricesBuffer.Release();
            worldToShadowMatricesBuffer = null;
        }
        if (colorsBuffer != null)
        {
            colorsBuffer.Release();
            colorsBuffer = null;
        }
        if (positionsBuffer != null)
        {
            positionsBuffer.Release();
            positionsBuffer = null;
        }
        if (spotDirectionsBuffer != null)
        {
            spotDirectionsBuffer.Release();
            spotDirectionsBuffer = null;
        }
        if (attenuationsBuffer != null)
        {
            attenuationsBuffer.Release();
            attenuationsBuffer = null;
        }
        if (lightIndicesBuffer != null)
        {
            lightIndicesBuffer.Release();
            lightIndicesBuffer = null;
        }
        if (shadowMaps != null)
        {
            shadowMaps.Release();
            shadowMaps = null;
        }
    }

    public void Dispose()
    {
        lightingBuffer.Dispose();
        shadowsBuffer.Dispose();
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
