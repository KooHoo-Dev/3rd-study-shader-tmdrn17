Shader "Hidden/MyShaders/FootprintStamp"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _WorldMinSize;
            float4 _StampCenter;
            float4 _StampForward;
            float4 _StampSizeStrength;
            float _Decay;
            float _StampEnabled;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float footprintShape(float2 local)
            {
                // A shoe-like sole: round toe, connecting midsole and narrower heel.
                float toe = length(float2(local.x / 0.78, (local.y - 0.50) / 0.52));
                float mid = length(float2(local.x / 0.56, local.y / 0.76));
                float heel = length(float2(local.x / 0.56, (local.y + 0.53) / 0.47));
                float distanceToSole = min(toe, min(mid, heel));
                return 1.0 - smoothstep(0.86, 1.02, distanceToSole);
            }

            float4 frag(Varyings i) : SV_Target
            {
                float previous = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).r;
                float value = previous * saturate(_Decay);

                if (_StampEnabled > 0.5)
                {
                    float2 worldXZ = _WorldMinSize.xy + i.uv * _WorldMinSize.zw;
                    float2 forward = normalize(_StampForward.xy);
                    float2 right = float2(forward.y, -forward.x);
                    float2 delta = worldXZ - _StampCenter.xy;
                    float halfWidth = max(_StampSizeStrength.x * 0.5, 0.005);
                    float halfLength = max(_StampSizeStrength.y * 0.5, 0.005);
                    float2 local = float2(dot(delta, right) / halfWidth, dot(delta, forward) / halfLength);
                    float stamp = footprintShape(local) * saturate(_StampSizeStrength.z);
                    value = max(value, stamp);
                }

                return float4(value, value, value, 1.0);
            }
            ENDHLSL
        }
    }
}
