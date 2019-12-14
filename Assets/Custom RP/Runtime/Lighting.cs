using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const int maxLightCount = 4;
    const string bufferName = "Lighting";

    static int lightCountId = Shader.PropertyToID("_LightCount");
    static int lightColorsId = Shader.PropertyToID("_LightColors");
    static int lightDirectionsId = Shader.PropertyToID("_LightDirections");
    static int lightPositionsId = Shader.PropertyToID("_LightPositions");
    static int lightAttenuationsId = Shader.PropertyToID("_LightAttenuations");

    static Vector4[] lightColors = new Vector4[maxLightCount];
    static Vector4[] lightDirections = new Vector4[maxLightCount];
    static Vector4[] lightPositions = new Vector4[maxLightCount];
    static Vector4[] lightAttenuations = new Vector4[maxLightCount];

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults)
    {
        buffer.BeginSample(bufferName);
        this.SetupLights(cullingResults);
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        lightColors[index] = visibleLight.finalColor;
        lightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        lightDirections[index].w = 1f;
        lightPositions[index] = Vector4.zero;
        lightAttenuations[index] = Vector4.zero;
        lightAttenuations[index].w = 1f;
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        lightColors[index] = visibleLight.finalColor;
        lightDirections[index] = Vector4.zero;
        lightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        lightPositions[index].w = 1f;
        lightAttenuations[index] = Vector4.zero;
        lightAttenuations[index].x = 1f / (visibleLight.range * visibleLight.range);
        lightAttenuations[index].w = 1f;
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
        float outerCos = Mathf.Cos(outerRad);
        float outerTan = Mathf.Tan(outerRad);
        float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
        float angleRange = Mathf.Max(innerCos - outerCos, 0f);

        lightColors[index] = visibleLight.finalColor;
        lightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        lightDirections[index].w = 0f;
        lightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        lightPositions[index].w = 1f;
        lightAttenuations[index] = Vector4.zero;
        lightAttenuations[index].y = 1f;
        lightAttenuations[index].z = 1f / angleRange;
        lightAttenuations[index].w = -outerCos / angleRange;
    }

    void SetupLights(CullingResults cullingResults)
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        int lightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                this.SetupDirectionalLight(lightCount++, ref visibleLight);
            }
            else if (visibleLight.lightType == LightType.Point)
            {
                this.SetupPointLight(lightCount++, ref visibleLight);
            }
            else if (visibleLight.lightType == LightType.Spot)
            {
                this.SetupSpotLight(lightCount++, ref visibleLight);
            }
            if (lightCount >= maxLightCount)
            {
                break;
            }
        }

        buffer.SetGlobalInt(lightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(lightColorsId, lightColors);
        buffer.SetGlobalVectorArray(lightDirectionsId, lightDirections);
        buffer.SetGlobalVectorArray(lightPositionsId, lightPositions);
        buffer.SetGlobalVectorArray(lightAttenuationsId, lightAttenuations);
    }
}
