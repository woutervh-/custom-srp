#ifndef SHADOWS_V2_INCLUDED
#define SHADOWS_V2_INCLUDED

CBUFFER_START(_CustomShadows)
    StructuredBuffer<float4x4> _WorldToShadowMatrices;
    StructuredBuffer<float4> _ShadowSettings;
    StructuredBuffer<int3> _CascadeData;
    StructuredBuffer<float4> _CullingSpheres;
CBUFFER_END

TEXTURE2D_ARRAY_SHADOW(_ShadowMaps);
SAMPLER_CMP(sampler_ShadowMaps);

float4 GetCullingSphere (Light light, int sphereIndex) {
    return _CullingSpheres[_CascadeData[light.index].z + sphereIndex];
}

float HardShadowAttenuation (Light light, float3 shadowPosition) {
    return SAMPLE_TEXTURE2D_ARRAY_SHADOW(_ShadowMaps, sampler_ShadowMaps, shadowPosition, light.index);
}

float SoftShadowAttenuation (Light light, float3 shadowPosition) {
    real tentWeights[9];
    real2 tentUVs[9];
    SampleShadow_ComputeSamples_Tent_5x5(cascade ? _ShadowMapSize : float4(_ShadowMapSize.xy / 2, _ShadowMapSize.xy * 2), shadowPosition.xy, tentWeights, tentUVs);
    float attenuation = 0;
    for (int i = 0; i < 9; i++) {
        attenuation += tentWeights[i] * SAMPLE_TEXTURE2D_ARRAY_SHADOW(_ShadowMaps, sampler_ShadowMaps, float3(tentUVs[i].xy, shadowPosition.z), light.index);
    }
    return attenuation;
}

float InsideCascadeCullingSphere (Light light, int cascadeIndex, float3 worldPosition) {
    float4 sphere = GetCullingSphere(light, cascadeIndex);
    return dot(worldPosition - sphere.xyz, worldPosition - sphere.xyz) < sphere.w;
}

float4x4 GetWorldToShadowMatrixCascades (Light light, float3 worldPosition) {
    float4 cascadeFlags = float4(
		InsideCascadeCullingSphere(light, 0, worldPosition),
	    InsideCascadeCullingSphere(light, 1, worldPosition),
	    InsideCascadeCullingSphere(light, 2, worldPosition),
	    InsideCascadeCullingSphere(light, 3, worldPosition)
    );
    cascadeFlags.yzw = saturate(cascadeFlags.yzw - cascadeFlags.xyz);
    float cascadeIndex = dot(cascadeFlags, float4(0, 1, 2, 3));

    return _WorldToShadowMatrices[_CascadeData[light.index].x + cascadeIndex];
}

float4x4 GetWorldToShadowMatrix (Light light, float3 worldPosition) {
    return _WorldToShadowMatrices[_CascadeData[light.index].x];
}

float GetShadowAttenuation (Light light, float3 worldPosition) {
    if (_ShadowSettings[light.index].x <= 0) {
        return 1.0;
    }

    float4x4 worldToShadowMatrix;
    if (_CascadeData[light.index].y <= 0) {
        worldToShadowMatrix = GetWorldToShadowMatrix(light, worldPosition);
    } else {
        worldToShadowMatrix = GetWorldToShadowMatrixCascades(light, worldPosition);
    }

    float4 shadowPosition = mul(worldToShadowMatrix, float4(worldPosition, 1.0));
    shadowPosition.xyz /= shadowPosition.w;

    #if UNITY_REVERSED_Z
        if (shadowPosition.z < 0) {
            return 1.0;
        }
    #else
        if (shadowPosition.z > 1) {
            return 1.0;
        }
    #endif
    
    float attenuation;
    if (_ShadowSettings[light.index].y <= 0) {
        attenuation = HardShadowAttenuation(light, shadowPosition.xyz);
    } else {
        attenuation = SoftShadowAttenuation(light, shadowPosition.xyz);
    }
    return lerp(1.0, attenuation, _ShadowSettings[light.index].x);
}

#endif
