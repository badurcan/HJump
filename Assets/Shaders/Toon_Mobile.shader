Shader "Custom/Toon_Mobile"
{
    Properties
    {
        _BaseMap ("Albedo (optional)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Float) = 0

        _ShadowColor ("Shadow Tint", Color) = (0.55,0.52,0.68,1)
        _ShadowThreshold ("Shadow Threshold", Range(-1,1)) = 0.15
        _ShadowSmoothness ("Shadow Softness", Range(0.001,1)) = 0.15

        _HighlightColor ("Sun Highlight Color", Color) = (1,0.92,0.75,1)
        _HighlightThreshold ("Highlight Threshold", Range(0,1)) = 0.85
        _HighlightSmoothness ("Highlight Softness", Range(0.001,1)) = 0.25
        _HighlightStrength ("Highlight Strength", Range(0,1)) = 0.25

        _RimColor ("Rim Color", Color) = (1,0.85,0.6,1)
        _RimPower ("Rim Power", Range(0.1,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,1)) = 0.25

        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.35

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        [Header(Transparency)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1

        [Header(Outline)]
        [Toggle] _EnableOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.1,0.08,0.12,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 100
        Cull [_Cull]

        // Inverted-hull outline: extrudes backfaces along the vertex normal and renders them
        // behind the front-facing surface, so only a silhouette rim shows through. One extra
        // draw call per object, no fullscreen post-processing - cheap enough for mobile and
        // works with URP's default forward renderer without a custom render feature.
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Front
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _EnableOutline;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float width = _OutlineWidth * _EnableOutline;
                float3 positionOS = IN.positionOS.xyz + normalize(IN.normalOS) * width;
                OUT.positionHCS = TransformObjectToHClip(positionOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // _EnableOutline also gates alpha, not just width, so a zero-width outline on a
                // transparent material can't still composite as a flat opaque wash behind it.
                half a = _OutlineColor.a * _EnableOutline;
                clip(a - 0.001);
                return half4(_OutlineColor.rgb, a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 color       : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _UseVertexColor;
                float4 _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSmoothness;
                float4 _HighlightColor;
                float  _HighlightThreshold;
                float  _HighlightSmoothness;
                float  _HighlightStrength;
                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;
                float  _AmbientStrength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color       = IN.color;
                OUT.fogCoord    = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, albedo * IN.color.rgb, _UseVertexColor);

                float NdotL = dot(normalWS, mainLight.direction);
                float atten = mainLight.shadowAttenuation;
                float lightTerm = NdotL * atten;

                float shadowBand = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, lightTerm);
                float highlightBand = smoothstep(_HighlightThreshold - _HighlightSmoothness, _HighlightThreshold + _HighlightSmoothness, lightTerm);

                half3 shadowTone = _ShadowColor.rgb * albedo;
                half3 litTone     = albedo * mainLight.color;
                half3 toonColor   = lerp(shadowTone, litTone, shadowBand);
                toonColor = lerp(toonColor, toonColor + _HighlightColor.rgb * _HighlightStrength, highlightBand * shadowBand);

                half3 ambientSH = SampleSH(normalWS);
                toonColor += ambientSH * albedo * _AmbientStrength;

                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float rim = 1.0 - saturate(dot(viewDir, normalWS));
                rim = pow(rim, _RimPower) * _RimStrength * shadowBand;
                toonColor += _RimColor.rgb * rim;

                toonColor = MixFog(toonColor, IN.fogCoord);

                // Vertex color alpha is always folded in (not gated by _UseVertexColor, which
                // only controls the RGB tint) so particle systems can drive fade purely via
                // Color over Lifetime/Start Color alpha without needing the color tint enabled.
                half alpha = texColor.a * _BaseColor.a * IN.color.a;
                return half4(toonColor, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            float4 ShadowPassVertex (Attributes input) : SV_POSITION
            {
                return GetShadowPositionHClip(input);
            }

            half4 ShadowPassFragment () : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment (Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
