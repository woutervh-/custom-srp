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

    void SubmitBuffer(ref ScriptableRenderContext context, CommandBuffer buffer)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

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

        SetupLights(ref context, ref cullingResults, shadowMapSize, shadowDistance);

        CommandBuffer buffer = new CommandBuffer
        {
            name = camera.name
        };

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        buffer.BeginSample(buffer.name);
        SubmitBuffer(ref context, buffer);
        DrawVisibleGeometry(ref context, camera, ref cullingResults, useDynamicBatching, useGPUInstancing);
        buffer.EndSample(buffer.name);
        SubmitBuffer(ref context, buffer);

#if UNITY_EDITOR
        DrawUnsupportedShaders(ref context, camera, ref cullingResults);
        DrawGizmos(ref context, camera);
#endif
        context.Submit();
        buffer.Release();

        Cleanup();
    }

    void Cleanup()
    {
        if (lightingBuffers != null)
        {
            lightingBuffers.Dispose();
        }
        if (shadowMaps != null)
        {
            shadowMaps.Release();
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
