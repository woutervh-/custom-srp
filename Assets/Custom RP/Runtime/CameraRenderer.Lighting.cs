using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string lightingBufferName = "Lighting";

    CommandBuffer lightingBuffer = new CommandBuffer
    {
        name = lightingBufferName
    };

    Vector4[] colors;
    Vector4[] positions;
    Vector4[] spotDirections;
    Vector4[] attenuations;

    ComputeBuffer colorsBuffer;
    ComputeBuffer positionsBuffer;
    ComputeBuffer spotDirectionsBuffer;
    ComputeBuffer attenuationsBuffer;
    ComputeBuffer lightIndicesBuffer;

    void SetLightingInput(ref ScriptableRenderContext context, ref CullingResults cullingResults)
    {
        ShaderInput.SetLightsCount(lightingBuffer, cullingResults.lightAndReflectionProbeIndexCount);

        colorsBuffer = CreateBuffer(colors);
        ShaderInput.SetLightsColors(lightingBuffer, colorsBuffer);

        positionsBuffer = CreateBuffer(positions);
        ShaderInput.SetLightsPositions(lightingBuffer, positionsBuffer);

        spotDirectionsBuffer = CreateBuffer(spotDirections);
        ShaderInput.SetLightsSpotDirections(lightingBuffer, spotDirectionsBuffer);

        attenuationsBuffer = CreateBuffer(attenuations);
        ShaderInput.SetLightsAttenuations(lightingBuffer, attenuationsBuffer);

        if (cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            lightIndicesBuffer = new ComputeBuffer(cullingResults.lightAndReflectionProbeIndexCount, 4);
            cullingResults.FillLightAndReflectionProbeIndices(lightIndicesBuffer);
            ShaderInput.SetLightIndices(lightingBuffer, lightIndicesBuffer);
        }

        SubmitBuffer(ref context, lightingBuffer);
    }

    void SetupLights(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, float shadowDistance)
    {
        if (cullingResults.visibleLights.Length >= 1 && cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            positions = new Vector4[cullingResults.visibleLights.Length];
            colors = new Vector4[cullingResults.visibleLights.Length];
            attenuations = new Vector4[cullingResults.visibleLights.Length];
            spotDirections = new Vector4[cullingResults.visibleLights.Length];

            for (int i = 0; i < cullingResults.visibleLights.Length; i++)
            {
                VisibleLight visibleLight = cullingResults.visibleLights[i];

                switch (cullingResults.visibleLights[i].lightType)
                {
                    case LightType.Directional:
                        SetupDirectionalLight(i, ref visibleLight);
                        break;
                    case LightType.Point:
                        SetupPointLight(i, ref visibleLight);
                        break;
                    case LightType.Spot:
                        SetupSpotLight(i, ref visibleLight);
                        break;
                }
            }
        }
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        colors[index] = visibleLight.finalColor;
        positions[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        spotDirections[index] = Vector4.zero;
        attenuations[index] = Vector4.zero;
        attenuations[index].w = 1f;
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        colors[index] = visibleLight.finalColor;
        positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        spotDirections[index] = Vector4.zero;
        attenuations[index] = Vector4.zero;
        attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        attenuations[index].w = 1f;
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
        float outerCos = Mathf.Cos(outerRad);
        float outerTan = Mathf.Tan(outerRad);
        float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
        float angleRange = Mathf.Max(innerCos - outerCos, 0.00001f);

        colors[index] = visibleLight.finalColor;
        positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        spotDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        attenuations[index] = Vector4.zero;
        attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        attenuations[index].z = 1f / angleRange;
        attenuations[index].w = -outerCos / angleRange;
    }
}