#ifndef LIT_LIGHT_INCLUDED
#define LIT_LIGHT_INCLUDED

CBUFFER_START(_CustomLight)
    StructuredBuffer<float4> _LightsPositions;
    StructuredBuffer<float4> _LightsColors;
    StructuredBuffer<float4> _LightsAttenuations;
    StructuredBuffer<float4> _LightsSpotDirections;
    StructuredBuffer<int> _LightsIndices;
CBUFFER_END

struct Light {
    int index;
    float4 position;
    float4 direction;
    float4 color;
    float4 attenuation;
    float4 spotDirection;
};

int GetLightIndex(uint i) {
    uint offset = unity_LightData.x;
    return _LightsIndices[offset + i];
}

int GetLightsCount() {
    return unity_LightData.y;
}

float3 GetLightVector (Surface surface, Light light) {
    return light.position.xyz - surface.worldPosition * light.position.w;
}

Light GetLight (int index) {
    Light light;
    light.index = index;
    light.position = _LightsPositions[index];
    light.color = _LightsColors[index];
    light.attenuation = _LightsAttenuations[index];
    light.spotDirection = _LightsSpotDirections[index];
    return light;
}

#endif
