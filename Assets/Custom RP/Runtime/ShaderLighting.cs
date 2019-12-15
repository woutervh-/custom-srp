using System;
using UnityEngine;
using UnityEngine.Rendering;

public static class ShaderLighting
{
    static void SetupDirectionalLight(LightingValues values, int index, ref VisibleLight visibleLight)
    {
        values.colors[index] = visibleLight.finalColor;
        values.positions[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        values.spotDirections[index] = Vector4.zero;
        values.attenuations[index] = Vector4.zero;
        values.attenuations[index].w = 1f;
    }

    static void SetupPointLight(LightingValues values, int index, ref VisibleLight visibleLight)
    {
        values.colors[index] = visibleLight.finalColor;
        values.positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        values.spotDirections[index] = Vector4.zero;
        values.attenuations[index] = Vector4.zero;
        values.attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        values.attenuations[index].w = 1f;
    }

    static void SetupSpotLight(LightingValues values, int index, ref VisibleLight visibleLight)
    {
        float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
        float outerCos = Mathf.Cos(outerRad);
        float outerTan = Mathf.Tan(outerRad);
        float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
        float angleRange = Mathf.Max(innerCos - outerCos, 0.00001f);

        values.colors[index] = visibleLight.finalColor;
        values.positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        values.spotDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        values.attenuations[index] = Vector4.zero;
        values.attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        values.attenuations[index].z = 1f / angleRange;
        values.attenuations[index].w = -outerCos / angleRange;
    }

    public static LightingValues CreateLightingValues(ref CullingResults cullingResults)
    {
        LightingValues values = new LightingValues();
        values.shadowData = new Vector4[cullingResults.visibleLights.Length];
        values.positions = new Vector4[cullingResults.visibleLights.Length];
        values.colors = new Vector4[cullingResults.visibleLights.Length];
        values.attenuations = new Vector4[cullingResults.visibleLights.Length];
        values.spotDirections = new Vector4[cullingResults.visibleLights.Length];

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];

            switch (cullingResults.visibleLights[i].lightType)
            {
                case LightType.Directional:
                    SetupDirectionalLight(values, i, ref visibleLight);
                    break;
                case LightType.Point:
                    SetupPointLight(values, i, ref visibleLight);
                    break;
                case LightType.Spot:
                    SetupSpotLight(values, i, ref visibleLight);
                    break;
            }

            values.shadowData[i] = Vector4.zero;
            Bounds shadowBounds;
            if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out shadowBounds))
            {
                values.shadowData[i].x = visibleLight.light.shadowStrength;
                values.shadowData[i].y = visibleLight.light.shadows == LightShadows.Soft ? 1f : 0f;
            }
        }

        return values;
    }

    public class LightingValues
    {
        public Vector4[] shadowData;
        public Vector4[] positions;
        public Vector4[] colors;
        public Vector4[] attenuations;
        public Vector4[] spotDirections;
    }

    public class LightingBuffers : IDisposable
    {
        public ComputeBuffer shadowDataBuffer;
        public ComputeBuffer positionsBuffer;
        public ComputeBuffer colorsBuffer;
        public ComputeBuffer attenuationsBuffer;
        public ComputeBuffer spotDirectionsBuffer;

        public LightingBuffers(LightingValues lightingValues)
        {
            shadowDataBuffer = new ComputeBuffer(lightingValues.shadowData.Length, 4 * 4);
            shadowDataBuffer.SetData(lightingValues.shadowData);
            positionsBuffer = new ComputeBuffer(lightingValues.positions.Length, 4 * 4);
            positionsBuffer.SetData(lightingValues.positions);
            colorsBuffer = new ComputeBuffer(lightingValues.colors.Length, 4 * 4);
            colorsBuffer.SetData(lightingValues.colors);
            attenuationsBuffer = new ComputeBuffer(lightingValues.attenuations.Length, 4 * 4);
            attenuationsBuffer.SetData(lightingValues.attenuations);
            spotDirectionsBuffer = new ComputeBuffer(lightingValues.spotDirections.Length, 4 * 4);
            spotDirectionsBuffer.SetData(lightingValues.spotDirections);
        }

        public void Dispose()
        {
            shadowDataBuffer.Release();
            positionsBuffer.Release();
            colorsBuffer.Release();
            attenuationsBuffer.Release();
            spotDirectionsBuffer.Release();
        }
    }
}
