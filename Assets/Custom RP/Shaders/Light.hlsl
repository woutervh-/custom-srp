#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

CBUFFER_START(_CustomLight)
    int _LightsCount;
    StructuredBuffer<float4> _LightsPositions;
    StructuredBuffer<float4> _LightsColors;
    StructuredBuffer<float4> _LightsAttenuations;
    StructuredBuffer<float4> _LightsSpotDirections;
    StructuredBuffer<int> _LightsIndices;
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
    StructuredBuffer<float4x4> _WorldToShadowMatrices;
    StructuredBuffer<float4> _ShadowData;
    StructuredBuffer<int2> _ShadowCascades;
    float4 _ShadowMapSize;
CBUFFER_END

TEXTURE2D_ARRAY_SHADOW(_ShadowMaps);
SAMPLER_CMP(sampler_ShadowMaps);

struct Light {
    int index;
    float4 position;
    float4 color;
    float4 attenuation;
    float4 spotDirection;
    float4 shadowData;
    int2 cascadeData;
};

int GetLightIndex(uint i) {
    uint offset = unity_LightData.x;
    return _LightsIndices[offset + i];
}

int GetLightsCount() {
    return min(_LightsCount, unity_LightData.y);
}

float3 GetLightVector (Surface surface, Light light) {
    return light.position.xyz - surface.worldPosition * light.position.w;
}

float3 GetLightDirection (Surface surface, Light light) {
    return normalize(light.position.xyz - surface.worldPosition * light.position.w);
}

float4x4 GetWorldToShadowMatrix(Light light, int cascadeIndex) {
    return _WorldToShadowMatrices[light.cascadeData.y + cascadeIndex];
}

Light GetLight (int index) {
    Light light;
    light.index = index;
    light.position = _LightsPositions[index];
    light.color = _LightsColors[index];
    light.attenuation = _LightsAttenuations[index];
    light.spotDirection = _LightsSpotDirections[index];
    light.shadowData = _ShadowData[index];
    light.cascadeData = _ShadowCascades[index];
    return light;
}

#endif
