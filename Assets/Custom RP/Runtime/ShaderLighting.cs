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

    public static LightingValues CreateLightingValues(ref CullingResults cullingResults, int shadowMapSize)
    {
        LightingValues values = new LightingValues();
        values.shadowData = new Vector4[cullingResults.visibleLights.Length];
        values.positions = new Vector4[cullingResults.visibleLights.Length];
        values.colors = new Vector4[cullingResults.visibleLights.Length];
        values.attenuations = new Vector4[cullingResults.visibleLights.Length];
        values.spotDirections = new Vector4[cullingResults.visibleLights.Length];
        values.viewMatrices = new Matrix4x4[cullingResults.visibleLights.Length];
        values.projectionMatrices = new Matrix4x4[cullingResults.visibleLights.Length];
        values.worldToShadowMatrices = new Matrix4x4[cullingResults.visibleLights.Length];
        values.splitData = new ShadowSplitData[cullingResults.visibleLights.Length];

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
                Matrix4x4 viewMatrix;
                Matrix4x4 projectionMatrix;
                ShadowSplitData splitData;
                bool validShadows;

                switch (cullingResults.visibleLights[i].lightType)
                {
                    case LightType.Directional:
                        validShadows = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right, shadowMapSize, cullingResults.visibleLights[i].light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
                        break;
                    case LightType.Spot:
                        validShadows = cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
                        break;
                    default:
                        viewMatrix = Matrix4x4.identity;
                        projectionMatrix = Matrix4x4.identity;
                        splitData = new ShadowSplitData();
                        validShadows = false;
                        break;
                }

                if (validShadows)
                {
                    values.shadowData[i].x = visibleLight.light.shadowStrength;
                    values.shadowData[i].y = visibleLight.light.shadows == LightShadows.Soft ? 1f : 0f;
                    values.viewMatrices[i] = viewMatrix;
                    values.projectionMatrices[i] = projectionMatrix;
                    values.splitData[i] = splitData;

                    if (SystemInfo.usesReversedZBuffer)
                    {
                        projectionMatrix.m20 = -projectionMatrix.m20;
                        projectionMatrix.m21 = -projectionMatrix.m21;
                        projectionMatrix.m22 = -projectionMatrix.m22;
                        projectionMatrix.m23 = -projectionMatrix.m23;
                    }
                    Matrix4x4 scaleOffset = Matrix4x4.identity;
                    scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
                    scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
                    values.worldToShadowMatrices[i] = scaleOffset * (projectionMatrix * viewMatrix);
                }
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
        public Matrix4x4[] viewMatrices;
        public Matrix4x4[] projectionMatrices;
        public Matrix4x4[] worldToShadowMatrices;
        public ShadowSplitData[] splitData;
    }

    public class LightingBuffers : IDisposable
    {
        public ComputeBuffer shadowDataBuffer;
        public ComputeBuffer positionsBuffer;
        public ComputeBuffer colorsBuffer;
        public ComputeBuffer attenuationsBuffer;
        public ComputeBuffer spotDirectionsBuffer;
        public ComputeBuffer worldToShadowMatricesBuffer;
        public ComputeBuffer lightIndicesBuffer;

        public LightingBuffers(ref CullingResults cullingResults, LightingValues lightingValues)
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
            worldToShadowMatricesBuffer = new ComputeBuffer(lightingValues.worldToShadowMatrices.Length, 4 * 4 * 4);
            worldToShadowMatricesBuffer.SetData(lightingValues.worldToShadowMatrices);
            lightIndicesBuffer = new ComputeBuffer(cullingResults.lightAndReflectionProbeIndexCount, 4);
            cullingResults.FillLightAndReflectionProbeIndices(lightIndicesBuffer);
        }

        public void Dispose()
        {
            shadowDataBuffer.Release();
            positionsBuffer.Release();
            colorsBuffer.Release();
            attenuationsBuffer.Release();
            spotDirectionsBuffer.Release();
            worldToShadowMatricesBuffer.Release();
            lightIndicesBuffer.Release();
        }
    }
}
