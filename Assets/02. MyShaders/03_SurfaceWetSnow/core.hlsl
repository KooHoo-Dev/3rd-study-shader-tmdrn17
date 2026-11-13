#ifndef SURFACE_WET_SNOW_CORE_INCLUDED
#define SURFACE_WET_SNOW_CORE_INCLUDED

#define SURFACE_NOISE_AMOUNT 0.6
#define SURFACE_GLINT_CELLS 90.0

float surfaceHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float surfaceValueNoise(float3 p)
{
    float3 cell = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float x00 = lerp(surfaceHash(cell), surfaceHash(cell + float3(1,0,0)), f.x);
    float x10 = lerp(surfaceHash(cell + float3(0,1,0)), surfaceHash(cell + float3(1,1,0)), f.x);
    float x01 = lerp(surfaceHash(cell + float3(0,0,1)), surfaceHash(cell + float3(1,0,1)), f.x);
    float x11 = lerp(surfaceHash(cell + float3(0,1,1)), surfaceHash(cell + float3(1,1,1)), f.x);
    return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
}

float surfaceNoise(float3 p)
{
    float n = surfaceValueNoise(p) * 0.6;
    n += surfaceValueNoise(p * 2.03 + float3(11.7,3.1,7.9)) * 0.28;
    n += surfaceValueNoise(p * 4.01 + float3(5.3,17.2,2.4)) * 0.12;
    return saturate((n - 0.2) / 0.6);
}

float surfaceCoverMask(float up, float coverage)
{
    float soft = max(_Softness, 0.001);
    float edge = lerp(1.35 + soft, -1.35 - soft, saturate(coverage));
    return smoothstep(edge - soft, edge + soft, up);
}

float surfaceSpecular(float3 n, float3 h, float ndotl, float power)
{
    return pow(saturate(dot(n, h)), power) * step(0.0, ndotl);
}

void surfaceFootprint(
    float3 positionWS,
    float3 baseNormalWS,
    float snowMask,
    out float amount,
    out float3 adjustedNormal,
    out float rim
)
{
    float2 worldSize = max(_FootprintWorldMinSize.zw, float2(0.001, 0.001));
    float2 uv = (positionWS.xz - _FootprintWorldMinSize.xy) / worldSize;
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

    amount = 0.0;
    adjustedNormal = baseNormalWS;
    rim = 0.0;

    if (inside > 0.5 && snowMask > 0.001)
    {
        float center = SAMPLE_TEXTURE2D(_FootprintTex, sampler_FootprintTex, uv).r;
        float2 texelWorld = max(_FootprintTexelWorldSize.xy, float2(0.001, 0.001));
        float2 texelUV = texelWorld / worldSize;

        float leftValue = SAMPLE_TEXTURE2D(_FootprintTex, sampler_FootprintTex, uv - float2(texelUV.x, 0)).r;
        float rightValue = SAMPLE_TEXTURE2D(_FootprintTex, sampler_FootprintTex, uv + float2(texelUV.x, 0)).r;
        float downValue = SAMPLE_TEXTURE2D(_FootprintTex, sampler_FootprintTex, uv - float2(0, texelUV.y)).r;
        float upValue = SAMPLE_TEXTURE2D(_FootprintTex, sampler_FootprintTex, uv + float2(0, texelUV.y)).r;

        float2 gradient = float2(
            (rightValue - leftValue) / (2.0 * texelWorld.x),
            (upValue - downValue) / (2.0 * texelWorld.y)
        );

        float topFacing = smoothstep(0.25, 0.8, baseNormalWS.y);
        amount = saturate(center) * snowMask * topFacing;

        float3 pressedNormal = normalize(float3(
            gradient.x * _FootprintDepth,
            1.0,
            gradient.y * _FootprintDepth
        ));
        adjustedNormal = normalize(lerp(baseNormalWS, pressedNormal, snowMask * topFacing));
        rim = saturate(length(gradient) * 0.07) * snowMask * topFacing;
    }
}

float4 sgEffect(SgInput i)
{
    float3 n = normalize(i.normalWS);
    float3 h = normalize(i.lightDirWS + i.viewWS);
    float ndotl = dot(n, i.lightDirWS);

    float coverage = saturate(_Coverage + _Storm * 0.4);
    float noise = surfaceNoise(i.positionOS * _NoiseScale);
    float mask = surfaceCoverMask(n.y + (noise - 0.5) * SURFACE_NOISE_AMOUNT, coverage);

    float3 albedo = SG_TEX(_StoneTex, i.uv * float2(_TilingU, _TilingV)).rgb;
    float3 dry = albedo * i.lighting + i.lightColor * surfaceSpecular(n, h, ndotl, 12.0) * 0.04;

    float3 wetAlbedo = lerp(albedo, albedo * albedo * 0.8, _WetDarkness) * lerp(1.0, 0.4, _WetDarkness);
    float wetPower = exp2(lerp(5.0, 10.0, _Gloss));
    float wetStrength = lerp(0.3, 3.0, _Gloss);
    float3 ambient = max(i.lighting - i.lightColor * saturate(ndotl), float3(0,0,0));
    float fresnel = pow(1.0 - saturate(dot(n, i.viewWS)), 4.0);
    float3 wet = wetAlbedo * i.lighting
        + i.lightColor * surfaceSpecular(n, h, ndotl, wetPower) * wetStrength
        + ambient * fresnel * _Gloss;

    float3 grain = floor(i.positionOS * SURFACE_GLINT_CELLS);
    float3 jitter = float3(
        surfaceHash(grain),
        surfaceHash(grain + float3(17,5,3)),
        surfaceHash(grain + float3(7,29,13))
    ) - 0.5;
    float3 glintN = normalize(n + jitter * 0.9);
    float glintPick = step(0.8, surfaceHash(grain + float3(3,11,23)));
    float glint = glintPick * surfaceSpecular(glintN, h, ndotl, 300.0) * lerp(0.0, 6.0, _Gloss);
    float3 snow = _SnowColor.rgb * i.lighting
        + i.lightColor * (surfaceSpecular(n, h, ndotl, 10.0) * 0.08 + glint);

    float isSnow = step(0.5, _Mode);
    float footprintAmount;
    float3 footprintNormal;
    float footprintRim;
    surfaceFootprint(i.positionWS, n, mask * isSnow, footprintAmount, footprintNormal, footprintRim);

    float3 pressedHalf = normalize(i.lightDirWS + i.viewWS);
    float pressedNdotL = dot(footprintNormal, i.lightDirWS);
    float3 pressedLighting = i.lightColor * saturate(pressedNdotL) + SampleSH(footprintNormal);
    float3 pressedSnow = _SnowColor.rgb * pressedLighting
        + i.lightColor * (surfaceSpecular(footprintNormal, pressedHalf, pressedNdotL, 10.0) * 0.08 + glint);
    pressedSnow *= lerp(1.0, 1.0 - _FootprintDarkening, footprintAmount);
    pressedSnow = lerp(pressedSnow, pressedSnow * 1.08, saturate(footprintRim * _FootprintRim));

    float3 cover = lerp(wet, pressedSnow, isSnow);
    return float4(lerp(dry, cover, mask), 1.0);
}

#endif
