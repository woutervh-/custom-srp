#ifndef SHADOWS_V2_INCLUDED
#define SHADOWS_V2_INCLUDED

#if defined(_CASCADES)
    float GetCascadedShadow () {

    }
#else
    float GetShadow () {
        
    }
#endif

float GetShadowAttenuation (Light light, float3 worldPosition) {
    return 1;
}

#endif
