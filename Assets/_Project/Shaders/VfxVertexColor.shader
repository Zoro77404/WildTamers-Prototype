// Unlit vertex-colored shader for the baked special-attack effects (VfxClip / VfxPlayer).
// Colors come from the mesh (Houdini "Cd"). One blend state covers every look: premultiplied alpha, where
// "Additive" 1 = pure glow (adds light) and 0 = normal see-through/solid surface.
// Recolor swaps the hue for _Tint while keeping the effect's own light and dark areas, so one effect can be
// reused for several animals in different colors. _Fade (0–1) fades the whole effect out for clean-up.
Shader "Wild Tamers/VFX Vertex Color"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Recolor ("Recolor (0 = original colors, 1 = tint color)", Range(0, 1)) = 0
        _Brightness ("Brightness", Range(0, 6)) = 1
        _Additive ("Additive (glow)", Range(0, 1)) = 1
        _Opacity ("Opacity", Range(0, 1)) = 1
        _LumaAlpha ("Dark parts see-through", Range(0, 1)) = 0
        _Shade ("Shading from normals", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Recolor;
                half _Brightness;
                half _Additive;
                half _Opacity;
                half _LumaAlpha;
                half _Shade;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                half3 normalWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                half3 color = input.color.rgb;
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));

                // Recolor: keep the brightness pattern, take the hue from the tint.
                half3 recolored = _Tint.rgb * saturate(luma * 1.6h + 0.12h) * 1.25h;
                color = lerp(color, recolored, _Recolor);

                // Soft sun-from-above shading for solid parts (rocks); 0 keeps it flat/unlit.
                half3 n = normalize(input.normalWS) * (frontFace ? 1.0h : -1.0h);
                half light = saturate(dot(n, normalize(half3(0.35h, 0.85h, -0.4h)))) * 0.6h + 0.45h;
                color *= lerp(1.0h, light, _Shade);
                color *= _Brightness;

                half alpha = _Opacity * _Fade * lerp(1.0h, saturate(luma * 2.2h), _LumaAlpha);
                color = MixFog(color, input.fogFactor);
                // Premultiplied: rgb·a always; alpha 0 = additive glow, alpha a = regular blend.
                return half4(color * alpha, alpha * (1.0h - _Additive));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
