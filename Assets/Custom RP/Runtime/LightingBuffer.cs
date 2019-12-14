using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class LightingBuffer
{
    const string bufferName = "Lighting";

    static int lightsPositionsId = Shader.PropertyToID("_LightsPositions");
    static int lightsColorsId = Shader.PropertyToID("_LightsColors");
    static int lightsAttenuationsId = Shader.PropertyToID("_LightsAttenuations");
    static int lightsSpotDirectionsId = Shader.PropertyToID("_LightsSpotDirections");
    static int lightsIndicesId = Shader.PropertyToID("_LightsIndices");
    static int lightsCountId = Shader.PropertyToID("_LightsCount");

    Vector4[] lightsPositions;
    Vector4[] lightsColors;
    Vector4[] lightsAttenuations;
    Vector4[] lightsSpotDirections;
    ComputeBuffer lightsPositionsBuffer;
    ComputeBuffer lightsColorsBuffer;
    ComputeBuffer lightsAttenuationsBuffer;
    ComputeBuffer lightsSpotDirectionsBuffer;
    ComputeBuffer lightsIndices;

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults)
    {
        buffer.BeginSample(bufferName);
        SetupLights(cullingResults);
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        lightsColors[index] = visibleLight.finalColor;
        lightsPositions[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        lightsSpotDirections[index] = Vector4.zero;
        lightsAttenuations[index] = Vector4.zero;
        lightsAttenuations[index].w = 1f;
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        lightsColors[index] = visibleLight.finalColor;
        lightsPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        lightsPositions[index].w = 1f;
        lightsSpotDirections[index] = Vector4.zero;
        lightsAttenuations[index] = Vector4.zero;
        lightsAttenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        lightsAttenuations[index].w = 1f;
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
        float outerCos = Mathf.Cos(outerRad);
        float outerTan = Mathf.Tan(outerRad);
        float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
        float angleRange = Mathf.Max(innerCos - outerCos, 0.00001f);

        lightsColors[index] = visibleLight.finalColor;
        lightsPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        lightsPositions[index].w = 1f;
        lightsSpotDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        lightsAttenuations[index] = Vector4.zero;
        lightsAttenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        lightsAttenuations[index].z = 1f / angleRange;
        lightsAttenuations[index].w = -outerCos / angleRange;
    }

    void SetupLights(CullingResults cullingResults)
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
        lightsPositions = new Vector4[visibleLights.Length];
        lightsColors = new Vector4[visibleLights.Length];
        lightsAttenuations = new Vector4[visibleLights.Length];
        lightsSpotDirections = new Vector4[visibleLights.Length];

        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)
            {
                this.SetupDirectionalLight(i, ref visibleLight);
            }
            else if (visibleLight.lightType == LightType.Point)
            {
                this.SetupPointLight(i, ref visibleLight);
            }
            else if (visibleLight.lightType == LightType.Spot)
            {
                this.SetupSpotLight(i, ref visibleLight);
            }
        }

        if (lightsPositionsBuffer != null)
        {
            lightsPositionsBuffer.Release();
        }
        lightsPositionsBuffer = new ComputeBuffer(visibleLights.Length, 4 * 4);
        lightsPositionsBuffer.SetData(lightsPositions);

        if (lightsColorsBuffer != null)
        {
            lightsColorsBuffer.Release();
        }
        lightsColorsBuffer = new ComputeBuffer(visibleLights.Length, 4 * 4);
        lightsColorsBuffer.SetData(lightsColors);

        if (lightsAttenuationsBuffer != null)
        {
            lightsAttenuationsBuffer.Release();
        }
        lightsAttenuationsBuffer = new ComputeBuffer(visibleLights.Length, 4 * 4);
        lightsAttenuationsBuffer.SetData(lightsAttenuations);

        if (lightsSpotDirectionsBuffer != null)
        {
            lightsSpotDirectionsBuffer.Release();
        }
        lightsSpotDirectionsBuffer = new ComputeBuffer(visibleLights.Length, 4 * 4);
        lightsSpotDirectionsBuffer.SetData(lightsSpotDirections);

        buffer.SetGlobalInt(lightsCountId, cullingResults.lightAndReflectionProbeIndexCount);
        buffer.SetGlobalBuffer(lightsPositionsId, lightsPositionsBuffer);
        buffer.SetGlobalBuffer(lightsColorsId, lightsColorsBuffer);
        buffer.SetGlobalBuffer(lightsAttenuationsId, lightsAttenuationsBuffer);
        buffer.SetGlobalBuffer(lightsSpotDirectionsId, lightsSpotDirectionsBuffer);

        if (lightsIndices != null)
        {
            lightsIndices.Release();
        }
        if (cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            lightsIndices = new ComputeBuffer(cullingResults.lightAndReflectionProbeIndexCount, 4);
            cullingResults.FillLightAndReflectionProbeIndices(lightsIndices);
            buffer.SetGlobalBuffer(lightsIndicesId, lightsIndices);
        }
    }
}
