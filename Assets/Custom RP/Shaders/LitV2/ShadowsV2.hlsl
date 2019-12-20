#ifndef SHADOWS_V2_INCLUDED
#define SHADOWS_V2_INCLUDED

CBUFFER_START(_CustomShadows)
    StructuredBuffer<float4x4> _WorldToShadowMatrices;
CBUFFER_END

TEXTURE2D_ARRAY_SHADOW(_ShadowMaps);
SAMPLER_CMP(sampler_ShadowMaps);

#if defined(_CASCADES)
    float GetShadow () {

    }
#else
    float GetShadow () {
        
    }
#endif

float GetShadowAttenuation (Light light, float3 worldPosition) {
    return 1;
}

#endif
