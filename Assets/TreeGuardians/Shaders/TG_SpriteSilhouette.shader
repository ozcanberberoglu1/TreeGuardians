// Tree Guardians — 2D sprite shader with a blendable silhouette.
// _Silhouette 0 = normal sprite, 1 = flat _SilhouetteColor cut-out (alpha of the sprite is kept),
// so guardians standing in front of the tree parts stay fully visible. Driven per renderer via
// MaterialPropertyBlock from CenterTreeDisplay; works with the URP 2D Renderer (unlit, like Sprite-Unlit-Default).
Shader "Tree Guardians/2D/Sprite Silhouette"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SilhouetteColor ("Silhouette Color", Color) = (0.05, 0.04, 0.07, 1)
        _Silhouette ("Silhouette (0-1)", Range(0, 1)) = 0
        _SilhouetteAlpha ("Silhouette Alpha", Range(0, 1)) = 1
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        // Legacy sprite properties so SpriteRenderer keeps working with this material.
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex SilhouetteVertex
            #pragma fragment SilhouetteFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // Keep every material property in one cbuffer (SRP Batcher layout).
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _SilhouetteColor;
                half _Silhouette;
                half _SilhouetteAlpha;
            CBUFFER_END

            Varyings SilhouetteVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 SilhouetteFragment(Varyings input) : SV_Target
            {
                half4 col = CommonUnlitFragment(input, input.color);
                half s = saturate(_Silhouette);
                col.rgb = lerp(col.rgb, _SilhouetteColor.rgb, s);
                col.a *= lerp(1.0h, _SilhouetteAlpha, s);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
