#ifndef CUSTOM_SHADOW_PASS_INCLUDED
#define CUSTOM_SHADOW_PASS_INCLUDED

#include "Common.hlsl"

CBUFFER_START(_ShadowBuffer)
    float _ShadowBias;
CBUFFER_END

struct Attributes {
	float3 positionOS : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float4 positionCS : SV_POSITION;
};

Varyings ShadowPassVertex (Attributes input) {
    Varyings output;

    UNITY_SETUP_INSTANCE_ID(input);

    output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
    #if UNITY_REVERSED_Z
        output.positionCS.z -= _ShadowBias;
        output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z += _ShadowBias;
        output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    
    return output;
}

float4 ShadowPassFragment (Varyings input) : SV_TARGET {
    return float4(0, 0, 0, 0);
}

#endif
