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
    Vector2Int[] shadowCascadeData;
    List<Matrix4x4> worldToShadowMatrices;
    List<ShadowSplitData> splitDatas;

    ComputeBuffer shadowDataBuffer;
    ComputeBuffer shadowCascadesBuffer;
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

        worldToShadowMatricesBuffer = CreateBuffer(worldToShadowMatrices.ToArray());
        ShaderInput.SetWorldToShadowMatrices(shadowsBuffer, worldToShadowMatricesBuffer);

        SubmitBuffer(ref context, shadowsBuffer);
    }

    static Matrix4x4 CreateWorldToShadowMatrix(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
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

    const int CASCADE_COUNT = 1;

    void RenderShadows(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize)
    {
        shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMaps.dimension = TextureDimension.Tex2DArray;
        shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
        shadowMaps.filterMode = FilterMode.Bilinear;
        shadowMaps.wrapMode = TextureWrapMode.Clamp;


        hasSoftShadows = false;
        hasHardShadows = false;
        shadowData = new Vector4[cullingResults.visibleLights.Length];
        shadowCascadeData = new Vector2Int[cullingResults.visibleLights.Length];
        worldToShadowMatrices = new List<Matrix4x4>();
        splitDatas = new List<ShadowSplitData>();

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            shadowData[i] = Vector4.zero;
            shadowCascadeData[i] = Vector2Int.zero;

            Bounds shadowBounds;
            if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(i, out shadowBounds))
            {
                CoreUtils.SetRenderTarget(shadowsBuffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);

                for (int j = 0; j < CASCADE_COUNT; j++)
                {
                    Matrix4x4 viewMatrix;
                    Matrix4x4 projectionMatrix;
                    ShadowSplitData splitData;

                    bool validShadows = false;
                    if (visibleLight.lightType == LightType.Directional)
                    {

                        validShadows = cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right, shadowMapSize, visibleLight.light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
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
                        shadowCascadeData[i].x = CASCADE_COUNT;
                        shadowCascadeData[i].y = worldToShadowMatrices.Count;
                        worldToShadowMatrices.Add(CreateWorldToShadowMatrix(viewMatrix, projectionMatrix));
                        splitDatas.Add(splitData);

                        shadowsBuffer.SetViewport(new Rect(0f, 0f, shadowMapSize, shadowMapSize));
                        shadowsBuffer.EnableScissorRect(new Rect(4f, 4f, shadowMapSize - 8f, shadowMapSize - 8f));
                        shadowsBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                        ShaderInput.SetShadowBias(shadowsBuffer, visibleLight.light.shadowBias);
                        SubmitBuffer(ref context, shadowsBuffer);

                        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, i);
                        shadowSettings.splitData = splitData;
                        context.DrawShadows(ref shadowSettings);
                    }
                }
            }
        }

        shadowsBuffer.DisableScissorRect();
        SubmitBuffer(ref context, shadowsBuffer);
    }
}
