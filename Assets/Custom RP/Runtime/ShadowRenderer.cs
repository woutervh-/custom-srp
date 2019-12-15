using UnityEngine;
using UnityEngine.Rendering;

public class ShadowRenderer
{
    const string bufferName = "Render Shadows";
    const string SampleName = bufferName;
    const string shadowsSoftKeyword = "_SHADOWS_SOFT";

    static int shadowMapsId = Shader.PropertyToID("_ShadowMaps");
    static int worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowDataId = Shader.PropertyToID("_ShadowData");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");

    ComputeBuffer worldToShadowMatricesBuffer;
    ComputeBuffer shadowDataBuffer;

    public void Render(ScriptableRenderContext context, CullingResults cullingResults, CommandBuffer buffer, RenderTexture shadowMaps, Vector4[] shadowData)
    {
        Setup(context, buffer, shadowMaps);

        Matrix4x4[] worldToShadowMatrices = new Matrix4x4[cullingResults.visibleLights.Length];

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            CoreUtils.SetRenderTarget(buffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);

            if (shadowData[i].x <= 0f)
            {
                continue;
            }

            Matrix4x4 viewMatrix;
            Matrix4x4 projectionMatrix;
            ShadowSplitData splitData;
            if (!cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData))
            {
                shadowData[i].x = 0f;
                continue;
            }

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalFloat(shadowBiasId, cullingResults.visibleLights[i].light.shadowBias);
            context.ExecuteCommandBuffer(buffer);
            buffer.Clear();

            ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, i);
            context.DrawShadows(ref shadowSettings);

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
            worldToShadowMatrices[i] = scaleOffset * (projectionMatrix * viewMatrix);
        }

        CoreUtils.SetKeyword(buffer, shadowsSoftKeyword, cullingResults.visibleLights[0].light.shadows == LightShadows.Soft);
        buffer.SetGlobalTexture(shadowMapsId, shadowMaps);
        float invShadowMapSize = 1f / shadowMaps.width;
        buffer.SetGlobalVector(shadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, shadowMaps.width, shadowMaps.width));

        if (worldToShadowMatricesBuffer != null)
        {
            worldToShadowMatricesBuffer.Release();
        }
        worldToShadowMatricesBuffer = new ComputeBuffer(cullingResults.visibleLights.Length, 4 * 4 * 4);
        worldToShadowMatricesBuffer.SetData(worldToShadowMatrices);

        if (shadowDataBuffer != null)
        {
            shadowDataBuffer.Release();
        }
        shadowDataBuffer = new ComputeBuffer(cullingResults.visibleLights.Length, 4 * 4);
        shadowDataBuffer.SetData(shadowData);

        buffer.SetGlobalBuffer(worldToShadowMatricesId, worldToShadowMatricesBuffer);
        buffer.SetGlobalBuffer(shadowDataId, shadowDataBuffer);

        Submit(context, buffer);
    }

    void Setup(ScriptableRenderContext context, CommandBuffer buffer, RenderTexture shadowMap)
    {
        buffer.BeginSample(SampleName);
        ExecuteBuffer(context, buffer);
        buffer.Clear();
    }

    void Submit(ScriptableRenderContext context, CommandBuffer buffer)
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer(context, buffer);
        context.Submit();
    }

    void ExecuteBuffer(ScriptableRenderContext context, CommandBuffer buffer)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
