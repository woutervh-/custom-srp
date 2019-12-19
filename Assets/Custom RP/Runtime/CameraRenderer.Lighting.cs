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

    ShaderLighting.LightingBuffers lightingBuffers;

    void SetupLights(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, float shadowDistance)
    {
        ShaderInput.SetLightsCount(lightingBuffer, cullingResults.lightAndReflectionProbeIndexCount);

        if (cullingResults.visibleLights.Length >= 1 && cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            ShaderLighting.LightingValues lightingValues = ShaderLighting.CreateLightingValues(ref cullingResults, shadowMapSize, shadowDistance);

            lightingBuffers = new ShaderLighting.LightingBuffers(ref cullingResults, lightingValues);
            ShaderInput.SetLightsPositions(lightingBuffer, lightingBuffers.positionsBuffer);
            ShaderInput.SetLightsColors(lightingBuffer, lightingBuffers.colorsBuffer);
            ShaderInput.SetLightsAttenuations(lightingBuffer, lightingBuffers.attenuationsBuffer);
            ShaderInput.SetLightsSpotDirections(lightingBuffer, lightingBuffers.spotDirectionsBuffer);
            ShaderInput.SetLightIndices(lightingBuffer, lightingBuffers.lightIndicesBuffer);
            ShaderInput.SetShadowData(lightingBuffer, lightingBuffers.shadowDataBuffer);
            ShaderInput.SetShadowCascades(lightingBuffer, lightingBuffers.cascadeDataBuffer);
            ShaderInput.SetWorldToShadowMatrices(lightingBuffer, lightingBuffers.worldToShadowMatricesBuffer);
            SubmitBuffer(ref context, lightingBuffer);

            RenderShadows(ref context, ref cullingResults, lightingValues, shadowMapSize);
        }
        else
        {
            SubmitBuffer(ref context, lightingBuffer);
        }
    }
}