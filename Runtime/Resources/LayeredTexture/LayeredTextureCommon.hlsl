#ifndef LAYERED_TEXTURE_COMMON_INCLUDED
#define LAYERED_TEXTURE_COMMON_INCLUDED

#define LT_BLEND_NORMAL 0
#define LT_BLEND_REPLACE 1
#define LT_BLEND_ADD 2
#define LT_BLEND_MULTIPLY 3
#define LT_BLEND_MIN 4
#define LT_BLEND_MAX 5

#define LT_WRITE_R 1
#define LT_WRITE_G 2
#define LT_WRITE_B 4
#define LT_WRITE_A 8

float4 LT_Blend(float4 previous, float4 candidate, uint blendMode)
{
    if (blendMode == LT_BLEND_ADD)
        return previous + candidate;

    if (blendMode == LT_BLEND_MULTIPLY)
        return previous * candidate;

    if (blendMode == LT_BLEND_MIN)
        return min(previous, candidate);

    if (blendMode == LT_BLEND_MAX)
        return max(previous, candidate);

    return candidate;
}

float4 LT_ApplyLayer(float4 previous, float4 candidate, float mask, float opacity, uint blendMode, uint writeMask)
{
    float influence = saturate(mask) * saturate(opacity);
    float4 blended = LT_Blend(previous, candidate, blendMode);
    float4 result = lerp(previous, blended, influence);

    result.r = (writeMask & LT_WRITE_R) != 0 ? result.r : previous.r;
    result.g = (writeMask & LT_WRITE_G) != 0 ? result.g : previous.g;
    result.b = (writeMask & LT_WRITE_B) != 0 ? result.b : previous.b;
    result.a = (writeMask & LT_WRITE_A) != 0 ? result.a : previous.a;

    return saturate(result);
}

#endif
