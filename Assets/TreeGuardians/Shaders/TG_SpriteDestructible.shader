// Tree Guardians — castle wall shader: sprite + world-space destruction mask + blendable silhouette.
// _MaskTex (R8, 1 = intact, 0 = hole) is mapped from world position through _MaskRect (xmin, ymin, 1/w, 1/h),
// so every part of one castle shares a single mask painted by DestructibleMask. Holes are cut out (alpha 0)
// with a scorched rim; _Silhouette tints the remaining wall flat (used while the player is aiming).
Shader "Tree Guardians/2D/Sprite Destructible"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Destruction Mask (R: 1 = intact)", 2D) = "white" {}
        _MaskRect ("Mask Rect (xmin, ymin, 1/w, 1/h)", Vector) = (0, 0, 1, 1)
        _UseMask ("Use Mask", Float) = 0
        _EdgeColor ("Hole Edge Color", Color) = (0.16, 0.09, 0.05, 1)
        _SilhouetteColor ("Silhouette Color", Color) = (0.05, 0.04, 0.07, 1)
        _Silhouette ("Silhouette (0-1)", Range(0, 1)) = 0
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

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

            #pragma vertex DestructibleVertex
            #pragma fragment DestructibleFragment

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
                float2 worldXY : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half4 _SilhouetteColor;
                float4 _MaskRect;
                half _UseMask;
                half _Silhouette;
            CBUFFER_END

            Varyings DestructibleVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldXY = TransformObjectToWorld(input.positionOS).xy;
                return o;
            }

            half4 DestructibleFragment(Varyings input) : SV_Target
            {
                half4 col = CommonUnlitFragment(input, input.color);
                if (_UseMask > 0.5h)
                {
                    float2 muv = (input.worldXY - _MaskRect.xy) * _MaskRect.zw;
                    half m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, saturate(muv)).r;
                    half solid = smoothstep(0.45h, 0.55h, m);   // inside the hole -> transparent
                    half rim = smoothstep(0.55h, 0.95h, m);     // scorched edge around the hole
                    col.rgb = lerp(_EdgeColor.rgb, col.rgb, rim);
                    col.a *= solid;
                }
                half s = saturate(_Silhouette);
                col.rgb = lerp(col.rgb, _SilhouetteColor.rgb, s);
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
