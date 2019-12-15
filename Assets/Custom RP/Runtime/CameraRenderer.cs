using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string cameraBufferName = "Render Camera";
    const string shadowBufferName = "Render Shadows";

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");
    static int shadowMapsId = Shader.PropertyToID("_ShadowMaps");

    CommandBuffer cameraBuffer = new CommandBuffer
    {
        name = cameraBufferName
    };

    CommandBuffer shadowBuffer = new CommandBuffer
    {
        name = shadowBufferName
    };

    RenderTexture shadowMaps;
    ShadowRenderer shadowRenderer = new ShadowRenderer();
    Lighting lighting = new Lighting();

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, int shadowMapSize)
    {
        PrepareBuffer(camera);
        PrepareForSceneWindow(camera);

        CullingResults? cullingResultMaybe = Cull(context, camera);
        if (!cullingResultMaybe.HasValue)
        {
            return;
        }
        CullingResults cullingResults = cullingResultMaybe.Value;

        lighting.Setup(context, cullingResults);

        if (cullingResults.visibleLights.Length > 0)
        {
            RenderShadows(context, cullingResults, shadowMapSize);
        }

        Setup(context, camera);
        DrawVisibleGeometry(context, camera, cullingResults, useDynamicBatching, useGPUInstancing);
        DrawUnsupportedShaders(context, camera, cullingResults);
        DrawGizmos(context, camera);
        Submit(context);

        RenderTexture.ReleaseTemporary(shadowMaps);
    }

    void Setup(ScriptableRenderContext context, Camera camera)
    {
        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        cameraBuffer.BeginSample(SampleName);
        ExecuteBuffer(context);
    }

    void Submit(ScriptableRenderContext context)
    {
        cameraBuffer.EndSample(SampleName);
        ExecuteBuffer(context);
        context.Submit();
    }

    void ExecuteBuffer(ScriptableRenderContext context)
    {
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();
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

    void RenderShadows(ScriptableRenderContext context, CullingResults cullingResults, int shadowMapSize)
    {
        shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMaps.dimension = TextureDimension.Tex2DArray;
        shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
        shadowMaps.filterMode = FilterMode.Bilinear;
        shadowMaps.wrapMode = TextureWrapMode.Clamp;

        shadowRenderer.Render(context, cullingResults, shadowBuffer, shadowMaps, lighting.shadowData);
    }
}
