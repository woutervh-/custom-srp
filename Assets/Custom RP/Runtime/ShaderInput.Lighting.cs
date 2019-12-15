using UnityEngine;
using UnityEngine.Rendering;

public static partial class ShaderInput
{
    static int lightsCountId = Shader.PropertyToID("_LightsCount");
    static int lightsPositionsId = Shader.PropertyToID("_LightsPositions");
    static int lightsColorsId = Shader.PropertyToID("_LightsColors");
    static int lightsAttenuationsId = Shader.PropertyToID("_LightsAttenuations");
    static int lightsSpotDirectionsId = Shader.PropertyToID("_LightsSpotDirections");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");

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
}
