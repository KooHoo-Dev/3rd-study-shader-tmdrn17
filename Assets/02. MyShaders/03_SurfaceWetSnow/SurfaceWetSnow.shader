Shader "MyShaders/SurfaceWetSnow"
{
    Properties
    {
        [NoScaleOffset] _StoneTex ("Base Texture", 2D) = "white" {}
        _TilingU ("Texture Tiling U", Range(1, 20)) = 6
        _TilingV ("Texture Tiling V", Range(1, 20)) = 3
        [Enum(Wet, 0, Snow, 1)] _Mode ("Mode", Float) = 1
        _Coverage ("Snow / Wet Coverage", Range(0, 1)) = 0.5
        _Softness ("Coverage Edge Softness", Range(0.01, 0.5)) = 0.08
        _NoiseScale ("Noise Scale", Range(1, 20)) = 6
        _WetDarkness ("Wet Darkness", Range(0, 1)) = 0.8
        _Gloss ("Gloss", Range(0, 1)) = 0.7
        _SnowColor ("Snow Color", Color) = (0.8039, 0.8392, 0.8941, 1)
        _Storm ("Storm Boost", Range(0, 1)) = 0
        _FootprintDepth ("Footprint Indent Strength", Range(0.005, 0.15)) = 0.045
        _FootprintDarkening ("Compressed Snow Darkening", Range(0, 1)) = 0.32
        _FootprintRim ("Raised Snow Rim", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_StoneTex);
        SAMPLER(sampler_StoneTex);

        // Global footprint map written by SnowFootprintManager.
        TEXTURE2D(_FootprintTex);
        SAMPLER(sampler_FootprintTex);
        float4 _FootprintWorldMinSize;   // minX, minZ, sizeX, sizeZ
        float4 _FootprintTexelWorldSize; // texelWorldX, texelWorldZ

        CBUFFER_START(UnityPerMaterial)
            float4 _StoneTex_TexelSize;
            float _TilingU;
            float _TilingV;
            float _Mode;
            float _Coverage;
            float _Softness;
            float _NoiseScale;
            float _WetDarkness;
            float _Gloss;
            float4 _SnowColor;
            float _Storm;
            float _FootprintDepth;
            float _FootprintDarkening;
            float _FootprintRim;
        CBUFFER_END

        #define SG_TEX(name, uv) SAMPLE_TEXTURE2D(name, sampler##name, uv)

        struct SgInput
        {
            float2 uv;
            float3 positionOS;
            float3 positionWS;
            float3 normalWS;
            float3 viewWS;
            float3 lightDirWS;
            float3 lightColor;
            float3 lighting;
            float time;
        };

        #include "core.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionOS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float3 normalWS : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionOS = v.positionOS.xyz;
            o.uv = v.uv;
            return o;
        }

        SgInput makeInput(Varyings v, float3 lightDir, float3 lightColor, float3 lighting)
        {
            SgInput i;
            i.uv = v.uv;
            i.positionOS = v.positionOS;
            i.positionWS = v.positionWS;
            i.normalWS = normalize(v.normalWS);
            i.viewWS = GetWorldSpaceNormalizeViewDir(v.positionWS);
            i.lightDirWS = lightDir;
            i.lightColor = lightColor;
            i.lighting = lighting;
            i.time = _Time.y;
            return i;
        }

        float effectAlpha(Varyings v)
        {
            return sgEffect(makeInput(v, float3(0,1,0), float3(1,1,1), float3(1,1,1))).a;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            float4 frag(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float3 normalWS = normalize(v.normalWS);
                Light light = GetMainLight(
                    TransformWorldToShadowCoord(v.positionWS),
                    v.positionWS,
                    half4(1,1,1,1)
                );
                float3 lightColor = light.color * light.shadowAttenuation;
                float3 lighting = lightColor * saturate(dot(normalWS, light.direction)) + SampleSH(normalWS);
                float4 c = sgEffect(makeInput(v, light.direction, lightColor, lighting));
                return float4(c.rgb, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragClip
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;

            Varyings vertShadow(Attributes v)
            {
                Varyings o = vert(v);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - o.positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 clipPos = TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, lightDir));
                o.positionCS = ApplyShadowClamping(clipPos);
                return o;
            }

            float4 fragClip(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                clip(effectAlpha(v) - 0.5);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            #pragma multi_compile_instancing
            float4 fragDepth(Varyings v) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                clip(effectAlpha(v) - 0.5);
                return v.positionCS.z;
            }
            ENDHLSL
        }
    }
}
