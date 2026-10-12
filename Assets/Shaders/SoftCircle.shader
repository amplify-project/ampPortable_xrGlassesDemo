Shader "Amp/Soft Circle"
{
    Properties
    {
        _BaseColor ("Circle Color", Color) = (0.96, 0.96, 0.96, 1)
        _Radius ("Radius (Half Opacity, UV Units)", Range(0.001, 0.5)) = 0.335
        _Softness ("Edge Softness (Full Width, UV Units)", Range(0, 0.5)) = 0.207
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SoftCircle"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Radius;
                float _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Use a square Quad with UVs covering 0..1. Leave room for the
                // feather: Radius + Edge Softness / 2 should be less than 0.5.
                float distanceFromCenter = length(input.uv - float2(0.5, 0.5));
                float transitionWidth = max(_Softness, max(fwidth(distanceFromCenter), 0.00001));
                float halfWidth = transitionWidth * 0.5;
                float alpha = 1.0 - smoothstep(_Radius - halfWidth, _Radius + halfWidth, distanceFromCenter);

                // Fade alpha only; the blend state applies it to RGB once.
                return half4(_BaseColor.rgb, _BaseColor.a * alpha);
            }
            ENDHLSL
        }
    }
}
