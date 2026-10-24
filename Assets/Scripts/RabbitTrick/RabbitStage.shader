Shader "Sleight/Rabbit Stage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Magician ("Colour magician silhouette", Float) = 0
        _Unlit ("Ignore spotlight", Float) = 0
        _UseSpriteColor ("Use SpriteRenderer tint", Float) = 1
        _HideLegs ("Hide silhouette legs (drawn separately)", Float) = 0
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Magician;
                float _Unlit;
                float _UseSpriteColor;
                float _HideLegs;
            CBUFFER_END
            float4 _RabbitSpot;
            float _RabbitSpotTopRadius;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 world:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                if (_UseSpriteColor > 0.5) input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output;
                output.world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.world);
                output.uv = input.uv;
                output.color = input.color * _Color;
                // Unity's normal SpriteRenderer draw path sends tint/alpha separately from vertex COLOR.
                // Immediate Camera.Render previews can bake it into vertices instead, hiding a missing multiplier.
                if (_UseSpriteColor > 0.5) output.color *= unity_SpriteColor;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 rgb = tex.rgb * input.color.rgb;
                if (_Magician > 0.5)
                {
                    if (input.uv.y > 0.32 && input.uv.y < 0.72 && abs(input.uv.x - 0.5) > 0.18) tex.a = 0;
                    if (_HideLegs > 0.5 && input.uv.y < 0.34) tex.a = 0;   // 腿由 RabbitStageView 单独画，走路会动
                    half3 fabric = half3(0.43, 0.16, 0.36);
                    if (input.uv.y < 0.34) fabric = half3(0.13, 0.17, 0.26);
                    else if (input.uv.y > 0.86) fabric = half3(0.17, 0.12, 0.27);
                    else if (input.uv.y > 0.72) fabric = half3(0.95, 0.66, 0.45);
                    else if (abs(input.uv.x - 0.5) < 0.075 && input.uv.y > 0.48) fabric = half3(0.96, 0.87, 0.70);
                    if (input.uv.y > 0.87 && input.uv.y < 0.9) fabric = half3(0.86, 0.49, 0.22);
                    rgb = fabric * (0.85 + 0.15 * tex.r);
                }
                float height = saturate((input.world.y - _RabbitSpot.y) / max(0.01, _RabbitSpot.z - _RabbitSpot.y));
                float radius = lerp(_RabbitSpot.w, _RabbitSpotTopRadius, height);
                float light = 1.0 - smoothstep(radius - 0.07, radius, abs(input.world.x - _RabbitSpot.x));
                light *= step(_RabbitSpot.y, input.world.y) * step(input.world.y, _RabbitSpot.z);
                float3 illumination = lerp(float3(0.32,0.34,0.42), float3(1.38,1.22,0.94), light);
                rgb *= lerp(illumination, float3(1,1,1), _Unlit);
                return half4(rgb, tex.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
