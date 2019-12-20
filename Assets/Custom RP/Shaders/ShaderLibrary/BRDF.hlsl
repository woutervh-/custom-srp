#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

#define MIN_REFLECTIVITY 0.04

struct BRDF {
    float3 diffuse;
    float3 specular;
    float roughness;
};

float OneMinusReflectivity (float metallic) {
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

float SpecularStrength (Surface surface, BRDF brdf, Light light) {
    float3 lightDirection = normalize(GetLightVector(surface, light));
    float3 h = SafeNormalize(lightDirection + surface.viewDirection);
    float nh = saturate(dot(surface.normal, h));
    float nh2 = nh * nh;
    float lh = saturate(dot(lightDirection, h));
    float lh2 = lh * lh;
    float r = brdf.roughness;
    float r2 = r * r;
    float d = nh2 * (r2 - 1.0) + 1.00001;
    float d2 = d * d;
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

float3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

BRDF GetBRDF (Surface surface) {
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);

    BRDF brdf;
    brdf.diffuse = surface.color * oneMinusReflectivity;
    #if defined(_PREMULTIPLY_ALPHA)
        brdf.diffuse *= surface.alpha;
    #endif
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
    brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    return brdf;
}

#endif
