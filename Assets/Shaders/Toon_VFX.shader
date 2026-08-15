Shader "Custom/Toon_VFX"
{
    // Transparent sibling of Custom/Toon_Mobile for particle/mesh VFX (trails, impact bursts,
    // embers). Same toon shading math so VFX reads as part of the same visual language as the
    // rest of the scene, but defaults to alpha-blended/no ZWrite and drops the ShadowCaster,
    // DepthOnly and Outline passes - VFX meshes don't need to cast/receive shadows or get an
    // inverted-hull outline, and skipping those passes keeps per-particle draw cost down.
    Properties
    {
        _BaseMap ("Albedo (optional)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _UseVertexColor ("Use Vertex Color", Float) = 1

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

        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.6

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        [Header(Transparency)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Cull [_Cull]

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
                // No shadow-coord sampling here (unlike Toon_Mobile) - VFX meshes don't receive
                // shadows, and skipping TransformWorldToShadowCoord/the shadow multi_compiles
                // saves work on what's usually the most overdraw-heavy material in the scene.
                Light mainLight = GetMainLight();

                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = texColor.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, albedo * IN.color.rgb, _UseVertexColor);

                float NdotL = dot(normalWS, mainLight.direction);
                float lightTerm = NdotL;

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

                // Vertex color alpha always folds in, independent of _UseVertexColor (which only
                // gates the RGB tint) - this is how particle Color over Lifetime/Start Color
                // alpha drives fade in/out.
                half alpha = texColor.a * _BaseColor.a * IN.color.a;
                return half4(toonColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
