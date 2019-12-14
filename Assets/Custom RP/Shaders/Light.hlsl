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
    float4x4 _WorldToShadowMatrix;
CBUFFER_END

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);

struct Light {
    float4 position;
    float4 color;
    float4 attenuation;
    float4 spotDirection;
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

Light GetLight (int index) {
    Light light;
    light.position = _LightsPositions[index];
    light.color = _LightsColors[index];
    light.attenuation = _LightsAttenuations[index];
    light.spotDirection = _LightsSpotDirections[index];
    return light;
}

#endif
