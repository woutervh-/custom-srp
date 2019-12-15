using UnityEngine;
using UnityEngine.Rendering;

public static partial class ShaderInput
{
    static int lightsCountId = Shader.PropertyToID("_LightsCount");
    static int lightsIndicesId = Shader.PropertyToID("_LightsIndices");
    static int lightsPositionsId = Shader.PropertyToID("_LightsPositions");
    static int lightsColorsId = Shader.PropertyToID("_LightsColors");
    static int lightsAttenuationsId = Shader.PropertyToID("_LightsAttenuations");
    static int lightsSpotDirectionsId = Shader.PropertyToID("_LightsSpotDirections");
    static int shadowMapsId = Shader.PropertyToID("_ShadowMaps");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");

    public static void SetLightsCount(CommandBuffer buffer, int count)
    {
        buffer.SetGlobalInt(lightsCountId, count);
    }

    public static void SetLightsPositions(CommandBuffer buffer, ComputeBuffer positions)
    {
        buffer.SetGlobalBuffer(lightsPositionsId, positions);
    }

    public static void SetLightsColors(CommandBuffer buffer, ComputeBuffer colors)
    {
        buffer.SetGlobalBuffer(lightsColorsId, colors);
    }

    public static void SetLightsAttenuations(CommandBuffer buffer, ComputeBuffer attenuations)
    {
        buffer.SetGlobalBuffer(lightsAttenuationsId, attenuations);
    }

    public static void SetLightsSpotDirections(CommandBuffer buffer, ComputeBuffer spotDirections)
    {
        buffer.SetGlobalBuffer(lightsSpotDirectionsId, spotDirections);
    }

    public static void SetShadowData(CommandBuffer buffer, ComputeBuffer shadowData)
    {
        buffer.SetGlobalBuffer(shadowDataId, shadowData);
    }

    public static void SetShadowBias(CommandBuffer buffer, float shadowBias)
    {
        buffer.SetGlobalFloat(shadowBiasId, shadowBias);
    }

    public static void SetWorldToShadowMatrices(CommandBuffer buffer, ComputeBuffer worldToShadowMatrices)
    {
        buffer.SetGlobalBuffer(worldToShadowMatricesId, worldToShadowMatrices);
    }

    public static void SetShadowMaps(CommandBuffer buffer, RenderTexture shadowMaps)
    {
        buffer.SetGlobalTexture(shadowMapsId, shadowMaps);
    }

    public static void SetShadowMapsSize(CommandBuffer buffer, Vector4 shadowMapSize)
    {
        buffer.SetGlobalVector(shadowMapSizeId, shadowMapSize);
    }

    public static void SetLightIndices(CommandBuffer buffer, ComputeBuffer lightsIndices)
    {
        buffer.SetGlobalBuffer(lightsIndicesId, lightsIndices);
    }
}
