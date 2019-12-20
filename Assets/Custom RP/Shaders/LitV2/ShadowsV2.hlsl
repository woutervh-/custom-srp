#ifndef SHADOWS_V2_INCLUDED
#define SHADOWS_V2_INCLUDED

CBUFFER_START(_CustomShadows)
    StructuredBuffer<float4x4> _WorldToShadowMatrices;
    StructuredBuffer<int2> _CascadeData;
CBUFFER_END

TEXTURE2D_ARRAY_SHADOW(_ShadowMaps);
SAMPLER_CMP(sampler_ShadowMaps);

float HardShadowAttenuation (Light light, float3 shadowPosition) {
    return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_ShadowMaps, sampler_ShadowMaps, shadowPosition, light.index);
}

float InsideCascadeCullingSphere (Light light, int cascadeIndex, float3 worldPosition) {
    float4 sphere = GetCullingSphere(light, cascadeIndex);
    return dot(worldPosition - sphere.xyz, worldPosition - sphere.xyz) < sphere.w;
}

#if defined(_CASCADES)
    float4x4 GetWorldToShadowMatrixCascades (Light light, float3 worldPosition) {
        // float4 cascadeFlags = float4(
    	// 	InsideCascadeCullingSphere(light, 0, worldPosition),
		//     InsideCascadeCullingSphere(light, 1, worldPosition),
		//     InsideCascadeCullingSphere(light, 2, worldPosition),
		//     InsideCascadeCullingSphere(light, 3, worldPosition)
	    // );
        float4 cascadeFlags = float4(0, 1, 0, 0);
        cascadeFlags.yzw = saturate(cascadeFlags.yzw - cascadeFlags.xyz);
        float cascadeIndex = dot(cascadeFlags, float4(0, 1, 2, 3));

        return _WorldToShadowMatrices[_CascadeData[light.index].x + cascadeIndex];
    }
#else
    float4x4 GetWorldToShadowMatrix (Light light, float3 worldPosition) {
        return _WorldToShadowMatrices[_CascadeData[light.index].x];
    }
#endif

float GetShadowAttenuation (Light light, float3 worldPosition) {
    float4x4 worldToShadowMatrix = GetWorldToShadowMatrix(light, worldPosition);
    float4 shadowPosition = mul(worldToShadowMatrix, float4(worldPosition, 1.0));
    shadowPosition.xyz /= shadowPosition.w;

    return HardShadowAttenuation(light, shadowPosition.xyz);
}

#endif
