#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float GetShadowAttenuation (float3 worldPos) {
    float4 shadowPos = mul(_WorldToShadowMatrix, float4(worldPos, 1.0));
    shadowPos.xyz /= shadowPos.w;
    return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
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

    float shadowAttenuation = GetShadowAttenuation(surface.worldPosition);

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
