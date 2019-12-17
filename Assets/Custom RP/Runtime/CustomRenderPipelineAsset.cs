using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    public enum ShadowCascades
    {
        Zero = 0,
        Four = 4
    }

    [SerializeField]
    bool useDynamicBatching = true;
    [SerializeField]
    bool useGPUInstancing = true;
    [SerializeField]
    bool useSRPBatcher = true;
    [SerializeField]
    ShadowMapSize shadowMapSize = ShadowMapSize._1024;
    [SerializeField]
    float shadowDistance = 100f;
    [SerializeField]
    ShadowCascades shadowCascades = ShadowCascades.Four;
    [SerializeField, HideInInspector]
    Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);

    public enum ShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, (int)shadowMapSize, shadowDistance, (int)shadowCascades, fourCascadesSplit);
    }
}
