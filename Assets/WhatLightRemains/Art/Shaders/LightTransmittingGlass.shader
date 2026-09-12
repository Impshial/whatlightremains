Shader "What Light Remains/Light-Transmitting Glass"
{
    Properties
    {
        [MainTexture] _BaseMap("Imperfection Detail", 2D) = "white" {}
        [Normal] _BumpMap("Thickness Normal", 2D) = "bump" {}
        [HDR] _Tint("Imperfection Tint", Color) = (0.22, 0.30, 0.34, 1)
        _GlassTint("Glass Tint", Color) = (0.10, 0.14, 0.16, 1)
        _ImperfectionStrength("Imperfection Strength", Range(0, 0.1)) = 0.004
        _NormalDetailStrength("Thickness Normal Strength", Range(0, 2)) = 1
        _DetailContrast("Detail Contrast", Range(0, 32)) = 4
        _DetailThreshold("Detail Threshold", Range(0, 0.25)) = 0.02
        _HighPassMip("High-pass Mip", Range(1, 8)) = 5
        _DistortionPixels("Distortion Pixels", Range(0, 6)) = 2.5
        _DistortionBlend("Distortion Blend", Range(0, 1)) = 0.55
        _DistortionMip("Distortion Mip", Range(0, 8)) = 4
        _Translucency("Subtle Translucency", Range(0, 1)) = 0.05
        [HideInInspector] _WLRDeleteTintColor("Delete Selection Tint", Color) = (1, 0.025, 0.015, 1)
        [HideInInspector] _WLRDeleteTintStrength("Delete Selection Tint Strength", Range(0, 1)) = 0

        [HideInInspector] _SrcBlend("", Float) = 1
        [HideInInspector] _DstBlend("", Float) = 10
        [HideInInspector] _ZWrite("", Float) = 0
        [HideInInspector] _Cull("", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            // Premultiplied partial replacement mixes a subtly offset opaque-scene sample
            // over the original framebuffer. With zero offset the scene is unchanged except
            // for sparse additive imperfections; there is no reflective or Lit response.
            Blend [_SrcBlend] [_DstBlend]
            ColorMask RGB
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                half4 _Tint;
                half4 _GlassTint;
                half _ImperfectionStrength;
                half _NormalDetailStrength;
                half _DetailContrast;
                half _DetailThreshold;
                half _HighPassMip;
                half _DistortionPixels;
                half _DistortionBlend;
                half _DistortionMip;
                half _Translucency;
                half4 _WLRDeleteTintColor;
                half _WLRDeleteTintStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 detailUv = TRANSFORM_TEX(input.uv, _BaseMap);
                half3 fineDetail = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, detailUv).rgb;
                half3 broadDetail = SAMPLE_TEXTURE2D_LOD(
                    _BaseMap,
                    sampler_BaseMap,
                    detailUv,
                    _HighPassMip).rgb;

                const half3 luminanceWeights = half3(0.2126h, 0.7152h, 0.0722h);
                half highFrequency = saturate(
                    (abs(dot(fineDetail - broadDetail, luminanceWeights)) - _DetailThreshold)
                    * _DetailContrast);

                float2 normalUv = TRANSFORM_TEX(input.uv, _BumpMap);
                half3 normalTs = UnpackNormalScale(
                    SAMPLE_TEXTURE2D_LOD(
                        _BumpMap,
                        sampler_BumpMap,
                        normalUv,
                        _DistortionMip),
                    _NormalDetailStrength);

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 distortionOffset = normalTs.xy
                    * _DistortionPixels
                    * _CameraOpaqueTexture_TexelSize.xy;
                half3 refractedScene = SampleSceneColor(screenUv + distortionOffset);

                half3 additiveDetail = max(_Tint.rgb, 0.0h)
                    * highFrequency
                    * _ImperfectionStrength;
                half distortionBlend = saturate(_DistortionBlend);
                half translucency = saturate(_Translucency);

                // Premultiplied composition preserves most of the framebuffer, replaces a
                // stronger portion with the offset opaque-scene sample, and adds only a very
                // small body tint so the pane reads as glass instead of a mirror or open void.
                half replacementWeight = 1.0h - (1.0h - distortionBlend) * (1.0h - translucency);
                half3 glassContribution = refractedScene * distortionBlend * (1.0h - translucency)
                    + max(_GlassTint.rgb, 0.0h) * translucency
                    + additiveDetail;
                // Delete selection changes only the pane's transmitted color. Keeping the
                // replacement weight unchanged preserves the authored pane opacity.
                glassContribution = lerp(
                    glassContribution,
                    max(_WLRDeleteTintColor.rgb, 0.0h) * replacementWeight,
                    saturate(_WLRDeleteTintStrength));
                return half4(glassContribution, replacementWeight);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
