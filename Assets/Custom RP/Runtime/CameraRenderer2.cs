using System;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraRenderer2 : IDisposable
{
    const string lightBufferName = "Lighting";
    const string shadowBufferName = "Render Shadows";

    CommandBuffer lightingBuffer = new CommandBuffer
    {
        name = lightBufferName
    };

    CommandBuffer shadowBuffer = new CommandBuffer
    {
        name = shadowBufferName
    };

    public void Render(ref ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, int shadowMapSize)
    {
        // CommandBuffer cameraBuffer = new CommandBuffer
        // {
        //     name = camera.name
        // };

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
#endif

        ScriptableCullingParameters cullingParameters;
        if (!camera.TryGetCullingParameters(out cullingParameters))
        {
            return;
        }

        CullingResults cullingResults = context.Cull(ref cullingParameters);

        lightingBuffer.BeginSample(lightingBuffer.name);
        ShaderLighting.LightingValues lightingValues = ShaderLighting.CreateLightingValues(ref cullingResults);
        lightingBuffer.EndSample(lightingBuffer.name);

        using (ShaderLighting.LightingBuffers lightingBuffers = new ShaderLighting.LightingBuffers(lightingValues))
        {
            ShaderInput.SetLightsCount(lightingBuffer, cullingResults.lightAndReflectionProbeIndexCount);
            ShaderInput.SetShadowData(lightingBuffer, lightingBuffers.positionsBuffer);
            ShaderInput.SetLightsPositions(lightingBuffer, lightingBuffers.shadowDataBuffer);
            ShaderInput.SetLightsColors(lightingBuffer, lightingBuffers.colorsBuffer);
            ShaderInput.SetLightsAttenuations(lightingBuffer, lightingBuffers.attenuationsBuffer);
            ShaderInput.SetLightsSpotDirections(lightingBuffer, lightingBuffers.spotDirectionsBuffer);
            SubmitBuffer(ref context, lightingBuffer);

            if (cullingResults.visibleLights.Length > 0)
            {
                RenderTexture shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
                shadowMaps.dimension = TextureDimension.Tex2DArray;
                shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
                shadowMaps.filterMode = FilterMode.Bilinear;
                shadowMaps.wrapMode = TextureWrapMode.Clamp;

                Matrix4x4[] worldToShadowMatrices = new Matrix4x4[cullingResults.visibleLights.Length];
                for (int i = 0; i < cullingResults.visibleLights.Length; i++)
                {
                    // TODO: can it be done with shadowBuffer.SetRenderTarget?
                    // CoreUtils.SetRenderTarget(buffer, shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, i);
                }
            }
        }
    }

    void SubmitBuffer(ref ScriptableRenderContext context, CommandBuffer buffer)
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    public void Dispose()
    {
        lightingBuffer.Dispose();
        shadowBuffer.Dispose();
    }
}
