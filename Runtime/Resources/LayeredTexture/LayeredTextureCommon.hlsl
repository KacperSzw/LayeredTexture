#ifndef LAYERED_TEXTURE_COMMON_INCLUDED
#define LAYERED_TEXTURE_COMMON_INCLUDED

#define LT_BLEND_NORMAL 0
#define LT_BLEND_REPLACE 1
#define LT_BLEND_ADD 2
#define LT_BLEND_MULTIPLY 3
#define LT_BLEND_MIN 4
#define LT_BLEND_MAX 5
#define LT_BLEND_SUBTRACT 6

#define LT_WRITE_R 1
#define LT_WRITE_G 2
#define LT_WRITE_B 4
#define LT_WRITE_A 8

#define LT_MASK_GRAYSCALE 0
#define LT_MASK_R 1
#define LT_MASK_G 2
#define LT_MASK_B 3
#define LT_MASK_A 4

float4 LT_Swizzle(float4 value, uint4 swizzle)
{
    return float4(
        value[(int)swizzle.x],
        value[(int)swizzle.y],
        value[(int)swizzle.z],
        value[(int)swizzle.w]);
}

float4 LT_Blend(float4 previous, float4 candidate, uint blendMode)
{
    if (blendMode == LT_BLEND_ADD)
        return previous + candidate;

    if (blendMode == LT_BLEND_SUBTRACT)
        return previous - candidate;

    if (blendMode == LT_BLEND_MULTIPLY)
        return previous * candidate;

    if (blendMode == LT_BLEND_MIN)
        return min(previous, candidate);

    if (blendMode == LT_BLEND_MAX)
        return max(previous, candidate);

    return candidate;
}

float LT_MaskValue(float4 sample, uint usage, uint invert, float opacity)
{
    float value = dot(sample.rgb, float3(0.2126, 0.7152, 0.0722));

    if (usage == LT_MASK_R)
        value = sample.r;
    else if (usage == LT_MASK_G)
        value = sample.g;
    else if (usage == LT_MASK_B)
        value = sample.b;
    else if (usage == LT_MASK_A)
        value = sample.a;

    if (invert != 0)
        value = 1.0 - value;

    return saturate(value) * saturate(opacity);
}

float4 LT_ApplyLayer(
    float4 previous,
    float4 candidate,
    float mask,
    float opacity,
    uint blendMode,
    uint4 swizzle,
    uint writeMask,
    uint rawPreview)
{
    candidate = LT_Swizzle(candidate, swizzle);

    if (rawPreview != 0)
        return saturate(candidate);

    float influence = saturate(mask) * saturate(opacity);
    float4 blended = LT_Blend(previous, candidate, blendMode);
    float4 result = saturate(lerp(previous, blended, influence));

    result.r = (writeMask & LT_WRITE_R) != 0 ? result.r : previous.r;
    result.g = (writeMask & LT_WRITE_G) != 0 ? result.g : previous.g;
    result.b = (writeMask & LT_WRITE_B) != 0 ? result.b : previous.b;
    result.a = (writeMask & LT_WRITE_A) != 0 ? result.a : previous.a;

    return result;
}

#endif
