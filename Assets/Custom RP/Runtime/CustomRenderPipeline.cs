using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching;
    bool useGPUInstancing;
    int shadowMapSize;
    float shadowDistance;
    int shadowCascades;
    Vector3 shadowCascadesSplit;
    CameraRendererV2 cameraRenderer = new CameraRendererV2();

    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, int shadowMapSize, float shadowDistance, int shadowCascades, Vector3 shadowCascadesSplit)
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowMapSize = shadowMapSize;
        this.shadowDistance = shadowDistance;
        this.shadowCascades = shadowCascades;
        this.shadowCascadesSplit = shadowCascadesSplit;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        GraphicsSettings.lightsUseLinearIntensity = true;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras)
        {
            cameraRenderer.Render(ref context, camera, useDynamicBatching, useGPUInstancing, shadowMapSize, shadowDistance, shadowCascades, shadowCascadesSplit);
        }
    }
}
