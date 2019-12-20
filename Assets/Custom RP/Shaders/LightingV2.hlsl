#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

float3 GetIncomingLight (Surface surface, Light light) {
    float3 lightVector = GetLightVector(surface, light);
    float3 lightDirection = normalize(lightVector);
    float diffuse = saturate(dot(surface.normal, lightDirection));

    float distanceSqr = max(dot(lightVector, lightVector), 0.00001);
    float rangeFade = distanceSqr * light.attenuation.x;
    rangeFade = saturate(1.0 - rangeFade * rangeFade);
    rangeFade *= rangeFade;

    float spotFade = dot(light.spotDirection.xyz, lightDirection);
    spotFade = saturate(spotFade * light.attenuation.z + light.attenuation.w);
    spotFade *= spotFade;

    diffuse *= spotFade * rangeFade / distanceSqr;

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
