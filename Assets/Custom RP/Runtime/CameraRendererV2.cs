using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRendererV2
{
    static Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);
    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");

#if UNITY_EDITOR
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
#endif

    Vector4[] colors;
    Vector4[] positions;
    Vector4[] spotDirections;
    Vector4[] attenuations;

    ComputeBuffer colorsBuffer;
    ComputeBuffer positionsBuffer;
    ComputeBuffer spotDirectionsBuffer;
    ComputeBuffer attenuationsBuffer;
    ComputeBuffer lightIndicesBuffer;

    ShadowData shadowData;

    CommandBuffer buffer = new CommandBuffer
    {
        name = "Command Buffer"
    };

    public void Render(ref ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, int shadowMapSize, float shadowDistance, int shadowCascades, Vector3 shadowCascadesSplit)
    {
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
        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);
        CullingResults cullingResults = context.Cull(ref cullingParameters);

        SetupShadows(ref context, ref cullingResults, shadowMapSize);
        RenderShadows(ref context, ref cullingResults, shadowMapSize);
        SetupLights(ref context, ref cullingResults);
        ApplyLights(ref context, ref cullingResults);

        context.SetupCameraProperties(camera);
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(
            flags <= CameraClearFlags.Depth,
            flags == CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear
        );
        DrawVisibleGeometry(ref context, camera, ref cullingResults, useDynamicBatching, useGPUInstancing);

#if UNITY_EDITOR
        DrawUnsupportedShaders(ref context, camera, ref cullingResults);
        DrawGizmos(ref context, camera);
#endif
        context.Submit();

        CleanupBuffers();
    }

    void CleanupBuffers()
    {
        if (shadowData != null && shadowData.shadowMaps != null)
        {
            RenderTexture.ReleaseTemporary(shadowData.shadowMaps);
        }
        if (colorsBuffer != null)
        {
            colorsBuffer.Release();
        }
        if (positionsBuffer != null)
        {
            positionsBuffer.Release();
        }
        if (spotDirectionsBuffer != null)
        {
            spotDirectionsBuffer.Release();
        }
        if (attenuationsBuffer != null)
        {
            attenuationsBuffer.Release();
        }
        if (lightIndicesBuffer != null)
        {
            lightIndicesBuffer.Release();
        }
    }

    void SetupShadows(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize)
    {
        shadowData = new ShadowData();

        if (cullingResults.visibleLights.Length <= 0)
        {
            shadowData.lights = null;
            shadowData.shadowMaps = null;
            return;
        }

        shadowData.lights = new ShadowLight[cullingResults.visibleLights.Length];
        shadowData.shadowMaps = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowData.shadowMaps.dimension = TextureDimension.Tex2DArray;
        shadowData.shadowMaps.volumeDepth = cullingResults.visibleLights.Length;
        shadowData.shadowMaps.filterMode = FilterMode.Bilinear;
        shadowData.shadowMaps.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            switch (cullingResults.visibleLights[i].lightType)
            {
                case LightType.Directional:
                    SetupDirectionalShadow(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                    break;
                // case LightType.Point:
                //     SetupPointShadow(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                //     break;
                case LightType.Spot:
                    SetupSpotShadow(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                    break;
            }
        }
    }

    void RenderShadows(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize)
    {
        if (shadowData == null)
        {
            return;
        }

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            switch (cullingResults.visibleLights[i].lightType)
            {
                case LightType.Directional:
                    RenderDirectionalShadow(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                    break;
                // case LightType.Point:
                //     RenderPointLight(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                //     break;
                case LightType.Spot:
                    RenderSpotShadow(ref context, ref cullingResults, shadowMapSize, i, ref visibleLight);
                    break;
            }
        }
    }

    void SetupDirectionalShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, int index, ref VisibleLight visibleLight)
    {
        Bounds shadowBounds;
        if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(index, out shadowBounds))
        {
            ShadowLight shadowLight = new ShadowLight();
            shadowLight.cascades = new ShadowCascade[4];

            int tileSize = shadowMapSize / 2;
            for (int j = 0; j < 4; j++)
            {
                Matrix4x4 viewMatrix;
                Matrix4x4 projectionMatrix;
                ShadowSplitData splitData;

                if (cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(index, j, 4, fourCascadesSplit, tileSize, visibleLight.light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData))
                {
                    ShadowCascade shadowCascade = new ShadowCascade();
                    shadowCascade.viewMatrix = viewMatrix;
                    shadowCascade.projectionMatrix = projectionMatrix;
                    shadowCascade.splitData = splitData;
                    shadowLight.cascades[j] = shadowCascade;
                }
            }

            shadowData.lights[index] = shadowLight;
        }
    }

    void RenderDirectionalShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, int index, ref VisibleLight visibleLight)
    {
        if (shadowData.lights[index] == null)
        {
            return;
        }

        CoreUtils.SetRenderTarget(buffer, shadowData.shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, index);
        SubmitBuffer(ref context, buffer);

        int tileSize = shadowMapSize / 2;
        for (int j = 0; j < 4; j++)
        {
            if (shadowData.lights[index].cascades[j] == null)
            {
                continue;
            }

            Vector2Int tileOffset = new Vector2Int(j % 2, j / 2);
            Rect tileViewport = new Rect(tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);

            buffer.SetViewport(new Rect(tileViewport));
            buffer.EnableScissorRect(new Rect(tileViewport.x + 4f, tileViewport.y + 4f, tileSize - 8f, tileSize - 8f));
            buffer.SetViewProjectionMatrices(shadowData.lights[index].cascades[j].viewMatrix, shadowData.lights[index].cascades[j].projectionMatrix);
            ShaderInput.SetShadowBias(buffer, visibleLight.light.shadowBias);
            SubmitBuffer(ref context, buffer);

            ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, index);
            shadowSettings.splitData = shadowData.lights[index].cascades[j].splitData;
            context.DrawShadows(ref shadowSettings);
        }

        buffer.DisableScissorRect();
        SubmitBuffer(ref context, buffer);
    }

    void SetupSpotShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, int index, ref VisibleLight visibleLight)
    {
        Bounds shadowBounds;
        if (visibleLight.light.shadows != LightShadows.None && cullingResults.GetShadowCasterBounds(index, out shadowBounds))
        {
            ShadowLight shadowLight = new ShadowLight();
            shadowLight.cascades = new ShadowCascade[1];

            Matrix4x4 viewMatrix;
            Matrix4x4 projectionMatrix;
            ShadowSplitData splitData;

            if (cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(index, out viewMatrix, out projectionMatrix, out splitData))
            {
                ShadowCascade shadowCascade = new ShadowCascade();
                shadowCascade.viewMatrix = viewMatrix;
                shadowCascade.projectionMatrix = projectionMatrix;
                shadowCascade.splitData = splitData;
                shadowLight.cascades[0] = shadowCascade;
            }

            shadowData.lights[index] = shadowLight;
        }
    }

    void RenderSpotShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, int shadowMapSize, int index, ref VisibleLight visibleLight)
    {
        if (shadowData.lights[index] == null)
        {
            return;
        }

        CoreUtils.SetRenderTarget(buffer, shadowData.shadowMaps, ClearFlag.Depth, 0, CubemapFace.Unknown, index);
        SubmitBuffer(ref context, buffer);

        if (shadowData.lights[index].cascades[0] == null)
        {
            return;
        }

        Rect tileViewport = new Rect(0, 0, shadowMapSize, shadowMapSize);
        buffer.SetViewport(new Rect(tileViewport));
        buffer.SetViewProjectionMatrices(shadowData.lights[index].cascades[0].viewMatrix, shadowData.lights[index].cascades[0].projectionMatrix);
        ShaderInput.SetShadowBias(buffer, visibleLight.light.shadowBias);
        SubmitBuffer(ref context, buffer);

        ShadowDrawingSettings shadowSettings = new ShadowDrawingSettings(cullingResults, index);
        shadowSettings.splitData = shadowData.lights[index].cascades[0].splitData;
        context.DrawShadows(ref shadowSettings);
    }

#if UNITY_EDITOR
    void DrawUnsupportedShaders(ref ScriptableRenderContext context, Camera camera, ref CullingResults cullingResults)
    {
        Material errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        DrawingSettings drawingSettings = new DrawingSettings()
        {
            sortingSettings = new SortingSettings(camera),
            overrideMaterial = errorMaterial
        };
        for (int i = 0; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void DrawGizmos(ref ScriptableRenderContext context, Camera camera)
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }
#endif

    void DrawVisibleGeometry(ref ScriptableRenderContext context, Camera camera, ref CullingResults cullingResults, bool useDynamicBatching, bool useGPUInstancing)
    {
        SortingSettings sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        DrawingSettings drawingSettings = new DrawingSettings()
        {
            sortingSettings = sortingSettings,
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.LightData

        };
        drawingSettings.SetShaderPassName(0, unlitShaderTagId);
        drawingSettings.SetShaderPassName(1, litShaderTagId);
        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void ApplyLights(ref ScriptableRenderContext context, ref CullingResults cullingResults)
    {
        colorsBuffer = CreateBuffer(colors);
        ShaderInput.SetLightsColors(buffer, colorsBuffer);

        positionsBuffer = CreateBuffer(positions);
        ShaderInput.SetLightsPositions(buffer, positionsBuffer);

        spotDirectionsBuffer = CreateBuffer(spotDirections);
        ShaderInput.SetLightsSpotDirections(buffer, spotDirectionsBuffer);

        attenuationsBuffer = CreateBuffer(attenuations);
        ShaderInput.SetLightsAttenuations(buffer, attenuationsBuffer);

        if (cullingResults.lightAndReflectionProbeIndexCount >= 1)
        {
            lightIndicesBuffer = new ComputeBuffer(cullingResults.lightAndReflectionProbeIndexCount, 4);
            cullingResults.FillLightAndReflectionProbeIndices(lightIndicesBuffer);
            ShaderInput.SetLightIndices(buffer, lightIndicesBuffer);
        }

        SubmitBuffer(ref context, buffer);
    }

    void SetupLights(ref ScriptableRenderContext context, ref CullingResults cullingResults)
    {
        if (cullingResults.visibleLights.Length <= 0)
        {
            positions = null;
            colors = null;
            attenuations = null;
            spotDirections = null;
            return;
        }
        positions = new Vector4[cullingResults.visibleLights.Length];
        colors = new Vector4[cullingResults.visibleLights.Length];
        attenuations = new Vector4[cullingResults.visibleLights.Length];
        spotDirections = new Vector4[cullingResults.visibleLights.Length];

        for (int i = 0; i < cullingResults.visibleLights.Length; i++)
        {
            VisibleLight visibleLight = cullingResults.visibleLights[i];
            switch (cullingResults.visibleLights[i].lightType)
            {
                case LightType.Directional:
                    SetupDirectionalLight(i, ref visibleLight);
                    break;
                case LightType.Point:
                    SetupPointLight(i, ref visibleLight);
                    break;
                case LightType.Spot:
                    SetupSpotLight(i, ref visibleLight);
                    break;
            }
        }
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        colors[index] = visibleLight.finalColor;
        positions[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        spotDirections[index] = Vector4.zero;
        attenuations[index] = Vector4.zero;
        attenuations[index].w = 1f;
    }

    void SetupPointLight(int index, ref VisibleLight visibleLight)
    {
        colors[index] = visibleLight.finalColor;
        positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        spotDirections[index] = Vector4.zero;
        attenuations[index] = Vector4.zero;
        attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        attenuations[index].w = 1f;
    }

    void SetupSpotLight(int index, ref VisibleLight visibleLight)
    {
        float outerRad = Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle;
        float outerCos = Mathf.Cos(outerRad);
        float outerTan = Mathf.Tan(outerRad);
        float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
        float angleRange = Mathf.Max(innerCos - outerCos, 0.00001f);

        colors[index] = visibleLight.finalColor;
        positions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        spotDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        attenuations[index] = Vector4.zero;
        attenuations[index].x = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        attenuations[index].z = 1f / angleRange;
        attenuations[index].w = -outerCos / angleRange;
    }

    class ShadowCascade
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public ShadowSplitData splitData;
    }

    class ShadowLight
    {
        public ShadowCascade[] cascades;
    }

    class ShadowData
    {
        public RenderTexture shadowMaps;
        public ShadowLight[] lights;
    }
}
