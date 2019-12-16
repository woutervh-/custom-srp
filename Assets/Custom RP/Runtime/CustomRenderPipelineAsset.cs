using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    public enum ShadowCascades
    {
        Zero = 0,
        Two = 2,
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
    float twoCascadesSplit = 0.25f;
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
        Vector3 shadowCascadesSplit = shadowCascades == ShadowCascades.Four ? fourCascadesSplit : new Vector3(twoCascadesSplit, 0f, 0f);
        return new CustomRenderPipeline(useDynamicBatching, useGPUInstancing, useSRPBatcher, (int)shadowMapSize, shadowDistance, (int)shadowCascades, shadowCascadesSplit);
    }
}
