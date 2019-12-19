using System;
using UnityEditor;
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

    void RenderShadows(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShaderLighting.LightingValues lightingValues, int shadowMapSize)
    {
        shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMaps.dimension = TextureDimension.Tex2DArray;
        shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
        shadowMaps.filterMode = FilterMode.Bilinear;
        shadowMaps.wrapMode = TextureWrapMode.Clamp;

        shadowsBuffer.BeginSample(shadowsBuffer.name);
        SubmitBuffer(ref context, shadowsBuffer);

        bool hasSoftShadows = false;
        bool hasHardShadows = false;
        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            CommandBuffer lightShadowBuffer = new CommandBuffer
            {
                name = cullingResults.visibleLights[i].light.name
            };

            if (lightingValues.shadowData[i].x <= 0f)
            {
                continue;
            }

            if (lightingValues.shadowData[i].y <= 0f)
            {
                hasHardShadows = true;
            }
            else
            {
                hasSoftShadows = true;
            }

            CoreUtils.SetRenderTarget(lightShadowBuffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);

            lightShadowBuffer.BeginSample(lightShadowBuffer.name);
            SubmitBuffer(ref context, lightShadowBuffer);

            for (int j = 0; j < lightingValues.cascadeData[i].x; j++)
            {
                lightShadowBuffer.SetViewport(new Rect(0f, 0f, shadowMapSize, shadowMapSize));
                lightShadowBuffer.EnableScissorRect(new Rect(4f, 4f, shadowMapSize - 8f, shadowMapSize - 8f));
                lightShadowBuffer.SetViewProjectionMatrices(lightingValues.cascades[i].viewMatrices[j], lightingValues.cascades[i].projectionMatrices[j]);
                ShaderInput.SetShadowBias(lightShadowBuffer, cullingResults.visibleLights[i].light.shadowBias);
                SubmitBuffer(ref context, lightShadowBuffer);

                ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, i);
                shadowSettings.splitData = lightingValues.cascades[i].splitData[j];
                context.DrawShadows(ref shadowSettings);
            }

            lightShadowBuffer.EndSample(lightShadowBuffer.name);
            SubmitBuffer(ref context, lightShadowBuffer);
            lightShadowBuffer.Release();
        }

        ShaderInput.SetSoftShadows(shadowsBuffer, hasSoftShadows);
        ShaderInput.SetHardShadows(shadowsBuffer, hasHardShadows);
        ShaderInput.SetShadowMaps(shadowsBuffer, shadowMaps);
        ShaderInput.SetShadowMapsSize(shadowsBuffer, new Vector4(1f / shadowMaps.width, 1f / shadowMaps.width, shadowMaps.width, shadowMaps.width));
        shadowsBuffer.EndSample(shadowsBuffer.name);
        shadowsBuffer.DisableScissorRect();
        SubmitBuffer(ref context, shadowsBuffer);
    }
}