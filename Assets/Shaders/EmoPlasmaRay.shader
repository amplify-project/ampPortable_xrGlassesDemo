Shader "Unlit/EmoPlasmaRay"
{
    Properties
    {
        _BaseColor     ("Base Color", Color)                          = (1.0, 1.0, 1.0, 1.0)
        _ColorInner    ("Inner Color", Color)                         = (1.0, 0.5, 0.18, 1.0)
        _ColorOuter    ("Outer Color", Color)                         = (0.13, 0.40, 0.95, 1.0)

        _BoundsRadius  ("Bounds Radius", Float)                       = 0.8
        _StepSize      ("Primary Step Size", Range(0.005, 0.1))       = 0.04
        _PlasmaScale   ("Noise Scale", Float)                         = 3.0
        _WarpStrength  ("Warp Strength", Float)                       = 0.65
        _FlowSpeed     ("Flow Speed", Float)                          = 1.25

        _Brightness    ("Brightness", Range(0, 6))                    = 1.0
        _Saturation    ("Saturation", Range(0, 3.5))                  = 1.0

        _Curvature     ("Curvature (Round vs Angular)", Range(0, 1))  = 0.5
        _Asymmetry     ("Asymmetry (Order vs Chaos)", Range(0, 1))    = 0.0
        _SurfaceSharpness ("Surface Sharpness", Range(0, 2))          = 0.2

        _Turbulence    ("Turbulence", Range(0, 1))                    = 0.2
        _PulseFrequency("Pulse Frequency", Range(0, 10))              = 2.0
        _PulseAmplitude("Pulse Amplitude", Range(0, 1))               = 0.3

        _Smoothness    ("Smoothness", Range(0, 1))                    = 0.8
        _NoiseScale    ("Secondary Noise Scale", Range(0, 2))         = 0.5
        _NoiseContrast ("Noise Contrast", Range(0, 2))                = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 250
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ColorInner;
                float4 _ColorOuter;
                float  _BoundsRadius;
                float  _StepSize;
                float  _PlasmaScale;
                float  _WarpStrength;
                float  _FlowSpeed;
                float  _Brightness;
                float  _Saturation;
                float  _Curvature;
                float  _Asymmetry;
                float  _SurfaceSharpness;
                float  _Turbulence;
                float  _PulseFrequency;
                float  _PulseAmplitude;
                float  _Smoothness;
                float  _NoiseScale;
                float  _NoiseContrast;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                return output;
            }

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (1.0 + _NoiseContrast));
            }

            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = hash(i + float3(0, 0, 0));
                float n100 = hash(i + float3(1, 0, 0));
                float n010 = hash(i + float3(0, 1, 0));
                float n110 = hash(i + float3(1, 1, 0));
                float n001 = hash(i + float3(0, 0, 1));
                float n101 = hash(i + float3(1, 0, 1));
                float n011 = hash(i + float3(0, 1, 1));
                float n111 = hash(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            float fbm(float3 p)
            {
                float f = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    f += amplitude * noise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return f;
            }

            float3 applySaturation(float3 rgb, float saturation)
            {
                float luminance = dot(rgb, float3(0.299, 0.587, 0.114));
                return lerp(float3(luminance, luminance, luminance), rgb, saturation);
            }

            float EvaluateDensity(float3 posWS, float3 center, float radius, float time)
            {
                float3 rel = posWS - center;
                float distNorm = saturate(length(rel) / radius);

                float curvPower = lerp(0.35, 4.0, _Curvature);
                float curvatureTerm = pow(1.0 - distNorm, curvPower);

                float3 flowPos = rel * (_PlasmaScale + _NoiseScale);
                flowPos += _WarpStrength * fbm(flowPos * 0.5);
                flowPos += time * (_FlowSpeed + 0.5);

                float noiseTerm = fbm(flowPos * (1.0 + _Turbulence * 3.0));

                float asym = sin(dot(rel, float3(12.3, 7.7, 5.5)) + time * (1.0 + _Asymmetry * 8.0)) * _Asymmetry;
                float baseDensity = saturate(noiseTerm * curvatureTerm + asym * 0.5);
                float sharpPow = lerp(2.5, 0.4, saturate(_SurfaceSharpness));
                float density = pow(baseDensity, sharpPow);

                float pulse = 1.0 + _PulseAmplitude * sin(time * _PulseFrequency * 6.28318);
                return saturate(density * pulse);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 center = mul(GetObjectToWorldMatrix(), float4(0.0, 0.0, 0.0, 1.0)).xyz;
                float radius = max(_BoundsRadius, 1e-3);

                float3 ro = _WorldSpaceCameraPos;
                float3 rd = normalize(i.positionWS - ro);

                float3 oc = ro - center;
                float b = dot(oc, rd);
                float c = dot(oc, oc) - radius * radius;
                float h = b * b - c;
                if (h < 0.0)
                    discard;

                float sqrtH = sqrt(h);
                float tNear = max(-b - sqrtH, 0.0);
                float tFar = max(-b + sqrtH, 0.0);
                if (tFar <= tNear)
                    discard;

                const int MAX_STEPS = 64;
                float adaptiveStep = lerp(_StepSize * 1.5, _StepSize * 0.5, _Smoothness);
                float thickness = min(tFar - tNear, radius * 2.0);
                float approxSteps = thickness / max(adaptiveStep, 0.0001f);
                int steps = (int)min(max(approxSteps, 12.0f), float(MAX_STEPS));
                float stepScale = thickness / max((float)steps, 1.0f);

                float transmittance = 1.0;
                float3 accumColor = 0;
                float time = _Time.y;

                for (int step = 0; step < MAX_STEPS; step++)
                {
                    if (step >= steps || transmittance <= 0.01)
                        break;

                    float t = tNear + stepScale * (step + 0.5);
                    float3 samplePos = ro + rd * t;
                    float density = EvaluateDensity(samplePos, center, radius, time);

                    float alpha = density * stepScale * lerp(1.0, 2.0, _SurfaceSharpness);
                    alpha = saturate(alpha);

                    float3 sampleColor = lerp(_ColorInner.rgb, _ColorOuter.rgb, density);
                    sampleColor = applySaturation(sampleColor * _Brightness, _Saturation);

                    accumColor += transmittance * alpha * sampleColor;
                    transmittance *= (1.0 - alpha);
                }

                float3 finalColor = accumColor + transmittance * (_BaseColor.rgb * 0.05);
                float finalAlpha = saturate(1.0 - transmittance);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
