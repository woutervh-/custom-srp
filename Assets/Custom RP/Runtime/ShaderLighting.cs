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

    static Cascade[] SetupDirectionalCascades(LightingValues values, int index, ref VisibleLight visibleLight, ref CullingResults cullingResults, int shadowMapSize)
    {
        Matrix4x4 viewMatrix;
        Matrix4x4 projectionMatrix;
        ShadowSplitData splitData;

        if (cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(index, 0, 1, Vector3.right, shadowMapSize, cullingResults.visibleLights[index].light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData))
        {
            Cascade cascade = new Cascade();
            cascade.viewMatrices = new Matrix4x4[1];
            cascade.projectionMatrices = new Matrix4x4[1];
            cascade.worldToShadowMatrices = new Matrix4x4[1];
            cascade.splitData = new ShadowSplitData[1];
            cascade.viewMatrices[0] = viewMatrix;
            cascade.projectionMatrices[0] = projectionMatrix;
            cascade.worldToShadowMatrices[0] = CreateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix);
            cascade.splitData[0] = splitData;
            return new Cascade[] { cascade };
        }

        return null;
    }

    static Cascade[] SetupSpotCascades(LightingValues values, int index, ref VisibleLight visibleLight, ref CullingResults cullingResults)
    {
        Matrix4x4 viewMatrix;
        Matrix4x4 projectionMatrix;
        ShadowSplitData splitData;

        if (cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(index, out viewMatrix, out projectionMatrix, out splitData))
        {
            Cascade cascade = new Cascade();
            cascade.viewMatrices = new Matrix4x4[1];
            cascade.projectionMatrices = new Matrix4x4[1];
            cascade.worldToShadowMatrices = new Matrix4x4[1];
            cascade.splitData = new ShadowSplitData[1];
            cascade.viewMatrices[0] = viewMatrix;
            cascade.projectionMatrices[0] = projectionMatrix;
            cascade.worldToShadowMatrices[0] = CreateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix);
            cascade.splitData[0] = splitData;
            return new Cascade[] { cascade };
        }

        return null;
    }

    static Matrix4x4 CreateWorldToShadowMatrix(ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix)
    {
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
        return scaleOffset * (projectionMatrix * viewMatrix);
    }

    public static LightingValues CreateLightingValues(ref CullingResults cullingResults, int shadowMapSize, float shadowDistance)
    {
        LightingValues values = new LightingValues();
        values.shadowData = new Vector4[cullingResults.visibleLights.Length];
        values.cascadeData = new Vector2Int[cullingResults.visibleLights.Length];
        values.positions = new Vector4[cullingResults.visibleLights.Length];
        values.colors = new Vector4[cullingResults.visibleLights.Length];
        values.attenuations = new Vector4[cullingResults.visibleLights.Length];
        values.spotDirections = new Vector4[cullingResults.visibleLights.Length];
        values.cascades = new Cascade[cullingResults.visibleLights.Length];

        int cascadeIndex = 0;
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
            values.cascadeData[i] = Vector2Int.zero;
            Bounds shadowBounds;
            if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out shadowBounds))
            {
                Cascade[] cascades = null;
                switch (cullingResults.visibleLights[i].lightType)
                {
                    case LightType.Directional:
                        cascades = SetupDirectionalCascades(values, i, ref visibleLight, ref cullingResults, shadowMapSize);
                        break;
                    case LightType.Spot:
                        cascades = SetupSpotCascades(values, i, ref visibleLight, ref cullingResults);
                        break;
                }

                if (cascades != null)
                {
                    values.shadowData[i].x = visibleLight.light.shadowStrength;
                    values.shadowData[i].y = visibleLight.light.shadows == LightShadows.Soft ? 1f : 0f;
                    values.cascadeData[i].x = cascades.Length;
                    values.cascadeData[i].y = cascadeIndex;
                    values.cascades = cascades;
                    cascadeIndex += cascades.Length;
                }
            }
        }

        return values;
    }

    public class Cascade
    {
        public Matrix4x4[] viewMatrices;
        public Matrix4x4[] projectionMatrices;
        public Matrix4x4[] worldToShadowMatrices;
        public ShadowSplitData[] splitData;
    }

    public class LightingValues
    {
        public Vector4[] shadowData;
        public Vector2Int[] cascadeData;
        public Vector4[] positions;
        public Vector4[] colors;
        public Vector4[] attenuations;
        public Vector4[] spotDirections;
        public Cascade[] cascades;
    }

    public class LightingBuffers : IDisposable
    {
        public ComputeBuffer shadowDataBuffer;
        public ComputeBuffer cascadeDataBuffer;
        public ComputeBuffer positionsBuffer;
        public ComputeBuffer colorsBuffer;
        public ComputeBuffer attenuationsBuffer;
        public ComputeBuffer spotDirectionsBuffer;
        public ComputeBuffer worldToShadowMatricesBuffer;
        public ComputeBuffer lightIndicesBuffer;

        public LightingBuffers(ref CullingResults cullingResults, LightingValues lightingValues)
        {
            int worldToShadowMatricesCount = 0;
            for (int i = 0; i < lightingValues.cascades.Length; i++)
            {
                if (lightingValues.cascades[i] != null)
                {
                    worldToShadowMatricesCount += lightingValues.cascades[i].worldToShadowMatrices.Length;
                }
            }
            Matrix4x4[] worldToShadowMatrices = new Matrix4x4[worldToShadowMatricesCount];
            int worldToShadowMatricesIndex = 0;
            for (int i = 0; i < lightingValues.cascades.Length; i++)
            {
                if (lightingValues.cascades[i] != null)
                {
                    for (int j = 0; j < lightingValues.cascades[i].worldToShadowMatrices.Length; j++)
                    {
                        worldToShadowMatrices[worldToShadowMatricesIndex] = lightingValues.cascades[i].worldToShadowMatrices[j];
                        worldToShadowMatricesIndex += 1;
                    }
                }
            }

            shadowDataBuffer = new ComputeBuffer(lightingValues.shadowData.Length, 4 * 4);
            shadowDataBuffer.SetData(lightingValues.shadowData);
            cascadeDataBuffer = new ComputeBuffer(lightingValues.cascadeData.Length, 2 * 4);
            cascadeDataBuffer.SetData(lightingValues.cascadeData);
            positionsBuffer = new ComputeBuffer(lightingValues.positions.Length, 4 * 4);
            positionsBuffer.SetData(lightingValues.positions);
            colorsBuffer = new ComputeBuffer(lightingValues.colors.Length, 4 * 4);
            colorsBuffer.SetData(lightingValues.colors);
            attenuationsBuffer = new ComputeBuffer(lightingValues.attenuations.Length, 4 * 4);
            attenuationsBuffer.SetData(lightingValues.attenuations);
            spotDirectionsBuffer = new ComputeBuffer(lightingValues.spotDirections.Length, 4 * 4);
            spotDirectionsBuffer.SetData(lightingValues.spotDirections);
            if (worldToShadowMatricesCount >= 1)
            {
                worldToShadowMatricesBuffer = new ComputeBuffer(worldToShadowMatricesCount, 4 * 4 * 4);
                worldToShadowMatricesBuffer.SetData(worldToShadowMatrices);
            }
            lightIndicesBuffer = new ComputeBuffer(cullingResults.lightAndReflectionProbeIndexCount, 4);
            cullingResults.FillLightAndReflectionProbeIndices(lightIndicesBuffer);
        }

        public void Dispose()
        {
            shadowDataBuffer.Release();
            cascadeDataBuffer.Release();
            positionsBuffer.Release();
            colorsBuffer.Release();
            attenuationsBuffer.Release();
            spotDirectionsBuffer.Release();
            if (worldToShadowMatricesBuffer != null)
            {
                worldToShadowMatricesBuffer.Release();
            }
            lightIndicesBuffer.Release();
        }
    }
}
