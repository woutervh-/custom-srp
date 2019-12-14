using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string bufferName = "Render Camera";

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

    CommandBuffer buffer = new CommandBuffer
    {
        name = CameraRenderer.bufferName
    };

    // Lighting lighting = new Lighting();
    LightingBuffer lighting = new LightingBuffer();

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing)
    {
        this.PrepareBuffer(camera);
        this.PrepareForSceneWindow(camera);

        CullingResults? cullingResultMaybe = this.Cull(context, camera);
        if (!cullingResultMaybe.HasValue)
        {
            return;
        }
        CullingResults cullingResults = cullingResultMaybe.Value;

        this.Setup(context, camera);
        this.lighting.Setup(context, cullingResults);
        this.DrawVisibleGeometry(context, camera, cullingResults, useDynamicBatching, useGPUInstancing);
        this.DrawUnsupportedShaders(context, camera, cullingResults);
        this.DrawGizmos(context, camera);
        this.Submit(context);
    }

    void Setup(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        this.buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        this.buffer.BeginSample(SampleName);
        this.ExecuteBuffer(context);
    }

    void Submit(ScriptableRenderContext context)
    {
        this.buffer.EndSample(SampleName);
        this.ExecuteBuffer(context);
        context.Submit();
    }

    void ExecuteBuffer(ScriptableRenderContext context)
    {
        context.ExecuteCommandBuffer(this.buffer);
        this.buffer.Clear();
    }

    void DrawVisibleGeometry(ScriptableRenderContext context, Camera camera, CullingResults cullingResults, bool useDynamicBatching, bool useGPUInstancing)
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

    CullingResults? Cull(ScriptableRenderContext context, Camera camera)
    {
        ScriptableCullingParameters cullingParameters;
        if (camera.TryGetCullingParameters(out cullingParameters))
        {
            return context.Cull(ref cullingParameters);
        }
        return null;
    }
}
