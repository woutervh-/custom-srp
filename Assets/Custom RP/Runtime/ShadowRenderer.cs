using UnityEngine;
using UnityEngine.Rendering;

public class ShadowRenderer
{
    const string bufferName = "Render Shadows";
    const string SampleName = bufferName;
    const string shadowsSoftKeyword = "_SHADOWS_SOFT";

    static int shadowMapId = Shader.PropertyToID("_ShadowMap");
    static int worldToShadowMatrixId = Shader.PropertyToID("_WorldToShadowMatrix");
    static int shadowBiasId = Shader.PropertyToID("_ShadowBias");
    static int shadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    static int shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");

    public void Render(ScriptableRenderContext context, CullingResults cullingResults, CommandBuffer buffer, RenderTexture shadowMap)
    {
        Setup(context, buffer, shadowMap);

        Matrix4x4 viewMatrix;
        Matrix4x4 projectionMatrix;
        ShadowSplitData splitData;
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(0, out viewMatrix, out projectionMatrix, out splitData);

        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalFloat(shadowBiasId, cullingResults.visibleLights[0].light.shadowBias);
        buffer.SetGlobalFloat(shadowStrengthId, cullingResults.visibleLights[0].light.shadowStrength);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();

        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, 0);
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
        Matrix4x4 worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
        buffer.SetGlobalMatrix(worldToShadowMatrixId, worldToShadowMatrix);
        buffer.SetGlobalTexture(shadowMapId, shadowMap);
        float invShadowMapSize = 1f / shadowMap.width;
        buffer.SetGlobalVector(shadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, shadowMap.width, shadowMap.width));

        CoreUtils.SetKeyword(buffer, shadowsSoftKeyword, cullingResults.visibleLights[0].light.shadows == LightShadows.Soft);

        Submit(context, buffer);
    }

    void Setup(ScriptableRenderContext context, CommandBuffer buffer, RenderTexture shadowMap)
    {
        CoreUtils.SetRenderTarget(buffer, shadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);
        buffer.BeginSample(SampleName);
        ExecuteBuffer(context, buffer);
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
