using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    const string shadowsBufferName = "Render Shadows";

    CommandBuffer shadowsBuffer = new CommandBuffer
    {
        name = shadowsBufferName
    };

    RenderTexture shadowMaps;
    bool hasSoftShadows;
    bool hasHardShadows;
    Vector4[] shadowData;
    Vector3Int[] shadowCascadeData;
    List<Matrix4x4> worldToShadowMatrices;
    List<ShadowSplitData> splitDatas;
    List<Vector4> cullingSpheres;

    ComputeBuffer shadowDataBuffer;
    ComputeBuffer shadowCascadesBuffer;
    ComputeBuffer shadowCascadeCullingSpheresBuffer;
    ComputeBuffer worldToShadowMatricesBuffer;

    void SetupShadowInput(ref ScriptableRenderContext context)
    {
        // TODO: use splitData to compute correct cascade in shader

        ShaderInput.SetSoftShadows(shadowsBuffer, hasSoftShadows);
        ShaderInput.SetHardShadows(shadowsBuffer, hasHardShadows);
        ShaderInput.SetShadowMaps(shadowsBuffer, shadowMaps);
        ShaderInput.SetShadowMapsSize(shadowsBuffer, new Vector4(1f / shadowMaps.width, 1f / shadowMaps.height, shadowMaps.width, shadowMaps.height));

        shadowDataBuffer = CreateBuffer(shadowData);
        ShaderInput.SetShadowData(shadowsBuffer, shadowDataBuffer);

        shadowCascadesBuffer = CreateBuffer(shadowCascadeData);
        ShaderInput.SetShadowCascades(shadowsBuffer, shadowCascadesBuffer);

        shadowCascadeCullingSpheresBuffer = CreateBuffer(cullingSpheres.ToArray());
        ShaderInput.SetShadowCascadeCullingSpheres(shadowsBuffer, shadowCascadeCullingSpheresBuffer);

        worldToShadowMatricesBuffer = CreateBuffer(worldToShadowMatrices.ToArray());
        ShaderInput.SetWorldToShadowMatrices(shadowsBuffer, worldToShadowMatricesBuffer);

        SubmitBuffer(ref context, shadowsBuffer);
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

    void RenderShadows(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, float shadowDistance, int shadowCascades, Vector3 shadowCascadesSplit)
    {
        shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMaps.dimension = TextureDimension.Tex2DArray;
        shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
        shadowMaps.filterMode = FilterMode.Bilinear;
        shadowMaps.wrapMode = TextureWrapMode.Clamp;

        hasSoftShadows = false;
        hasHardShadows = false;
        shadowData = new Vector4[cullingResults.visibleLights.Length];
        shadowCascadeData = new Vector3Int[cullingResults.visibleLights.Length];
        worldToShadowMatrices = new List<Matrix4x4>();
        splitDatas = new List<ShadowSplitData>();
        cullingSpheres = new List<Vector4>();

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            shadowData[i] = Vector4.zero;
            shadowCascadeData[i] = Vector3Int.zero;

            Bounds shadowBounds;
            if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out shadowBounds))
            {
                CoreUtils.SetRenderTarget(shadowsBuffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);

                int cascadeCount = visibleLight.lightType == LightType.Directional ? shadowCascades : 1;
                Vector3 cascadeSplit = cascadeCount == 4 ? shadowCascadesSplit : Vector3.right;
                int tileSize = cascadeCount == 4 ? shadowMapSize / 2 : shadowMapSize;
                shadowCascadeData[i].x = cascadeCount;
                shadowCascadeData[i].y = worldToShadowMatrices.Count;
                shadowCascadeData[i].z = cullingSpheres.Count;
                for (int j = 0; j < cascadeCount; j++)
                {
                    Matrix4x4 viewMatrix;
                    Matrix4x4 projectionMatrix;
                    ShadowSplitData splitData;

                    bool validShadows = false;
                    if (visibleLight.lightType == LightType.Directional)
                    {

                        validShadows = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, j, cascadeCount, cascadeSplit, tileSize, visibleLight.light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
                    }
                    else
                    {
                        validShadows = cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
                    }

                    if (validShadows)
                    {
                        hasSoftShadows = hasSoftShadows || visibleLight.light.shadows == LightShadows.Soft;
                        hasHardShadows = hasHardShadows || visibleLight.light.shadows == LightShadows.Hard;
                        shadowData[i].x = visibleLight.light.shadowStrength;
                        shadowData[i].y = visibleLight.light.shadows == LightShadows.Soft ? 1f : 0f;
                        shadowData[i].z = shadowDistance * shadowDistance * 0.85f * 0.85f;
                        shadowData[i].w = shadowDistance * shadowDistance;

                        Vector2Int tileOffset = new Vector2Int(j % 2, j / 2);
                        Rect tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);

                        shadowsBuffer.SetViewport(new Rect(tileViewport));
                        shadowsBuffer.EnableScissorRect(new Rect(tileViewport.x + 4f, tileViewport.y + 4f, tileSize - 8f, tileSize - 8f));
                        shadowsBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                        ShaderInput.SetShadowBias(shadowsBuffer, visibleLight.light.shadowBias);
                        SubmitBuffer(ref context, shadowsBuffer);

                        Matrix4x4 tileMatrix = Matrix4x4.identity;
                        if (cascadeCount == 4)
                        {
                            tileMatrix.m00 = tileMatrix.m11 = 0.5f;
                            tileMatrix.m03 = tileOffset.x * 0.5f;
                            tileMatrix.m13 = tileOffset.y * 0.5f;
                        }

                        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, i);
                        shadowSettings.splitData = splitData;
                        context.DrawShadows(ref shadowSettings);

                        worldToShadowMatrices.Add(tileMatrix * CreateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix));
                        splitDatas.Add(splitData);
                        if (cascadeCount == 4)
                        {
                            Vector4 cullingSphere = splitData.cullingSphere;
                            cullingSphere.w *= cullingSphere.w;
                            cullingSpheres.Add(cullingSphere);
                        }
                    }
                }
            }
        }

        shadowsBuffer.DisableScissorRect();
        SubmitBuffer(ref context, shadowsBuffer);
    }
}
