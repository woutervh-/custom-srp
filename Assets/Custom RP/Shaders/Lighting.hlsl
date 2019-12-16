#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float HardShadowAttenuation (Light light, float3 shadowPosition) {
    return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_ShadowMaps, sampler_ShadowMaps, shadowPosition, light.index);
}

float SoftShadowAttenuation (Light light, float3 shadowPosition) {
    real tentWeights[9];
    real2 tentUVs[9];
    SampleShadow_ComputeSamples_Tent_5x5(_ShadowMapSize, shadowPosition.xy, tentWeights, tentUVs);
    float attenuation = 0;
    for (int i = 0; i < 9; i++) {
        attenuation += tentWeights[i] * SAMPLE_TEXTURE2D_ARRAY_SHADOW(_ShadowMaps, sampler_ShadowMaps, float3(tentUVs[i].xy, shadowPosition.z), light.index);
    }
    return attenuation;
}

float GetShadowAttenuation (Light light, float3 worldPosition) {
    #if !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
        return 1.0;
    #endif

    if (light.shadowData.x <= 0) {
        return 1.0;
    }

    float4 shadowPosition = mul(light.worldToShadowMatrix, float4(worldPosition, 1.0));
    shadowPosition.xyz /= shadowPosition.w;
    float attenuation;

    #if defined(_SHADOWS_HARD) && defined(_SHADOWS_SOFT)
        if (light.shadowData.y == 0) {
            attenuation = HardShadowAttenuation(light, shadowPosition.xyz);
        } else {
            attenuation = SoftShadowAttenuation(light, shadowPosition.xyz);
        }
    #elif defined(_SHADOWS_SOFT)
        attenuation = SoftShadowAttenuation(light, shadowPosition.xyz);
    #else
        attenuation = HardShadowAttenuation(light, shadowPosition.xyz);
    #endif

    return lerp(1, attenuation, light.shadowData.x);
}

float3 GetIncomingLight (Surface surface, Light light) {
    float3 lightDirection = GetLightDirection(surface, light);
    float diffuse = saturate(dot(surface.normal, lightDirection));

    float3 lightVector = GetLightVector(surface, light);
    float distanceSqr = max(dot(lightVector, lightVector), 0.00001);
    float rangeFade = distanceSqr * light.attenuation.x;
    rangeFade = saturate(1.0 - rangeFade * rangeFade);
    rangeFade *= rangeFade;

    float spotFade = dot(light.spotDirection.xyz, lightDirection);
    spotFade = saturate(spotFade * light.attenuation.z + light.attenuation.w);
    spotFade *= spotFade;

    float shadowAttenuation = GetShadowAttenuation(light, surface.worldPosition);

    diffuse *= shadowAttenuation * spotFade * rangeFade / distanceSqr;

    return diffuse * light.color.rgb;
}

float3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return GetIncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting (Surface surface, BRDF brdf) {
    float3 color = 0.0;
    for (int i = 0; i < GetLightsCount(); i++) {
        color += GetLighting(surface, brdf, GetLight(GetLightIndex(i)));
    }
    return color;
}

#endif
