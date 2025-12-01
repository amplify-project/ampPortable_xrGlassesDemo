/* Filename: PlasmaMass.shader
 * Purpose: Raymarch a dynamic volumetric plasma mass with animated density
 *          and color controlled via material properties.
 */

Shader "Unlit/PlasmaMass"
{
    Properties
    {
        _ColorInner ("Inner Color", Color) = (1.000, 0.502, 0.184, 1.000)
        _ColorOuter ("Outer Color", Color) = (0.133, 0.400, 0.949, 1.000)
        _BoundsRadius ("Bounds Radius", Float) = 0.8
        _StepSize ("Step Size", Range(0.005, 0.1)) = 0.04
        _PlasmaScale ("Noise Scale", Float) = 3.0
        _WarpStrength ("Warp Strength", Float) = 0.65
        _FlowSpeed ("Flow Speed", Float) = 1.25
        _DensityGain ("Density Gain", Float) = 1.35
        _DensityThreshold ("Density Threshold", Range(0.0, 0.8)) = 0.25
        _DensityPower ("Density Power", Float) = 1.3
        _Absorption ("Absorption", Range(0.05, 2.5)) = 0.8
        _Falloff ("Edge Falloff", Range(0.25, 1.5)) = 0.8
        _Emission ("Emission Strength", Range(0.5, 10.0)) = 2.5
        _HaloPulseSpeed ("Halo Pulse Speed", Range(0.0, 10.0)) = 0.0
        _HaloPulseAmplitude ("Halo Pulse Amplitude", Range(0.0, 2.0)) = 0.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        LOD 250

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define MAX_STEPS 16

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ro : TEXCOORD1;
                float3 hitPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ColorInner;
            float4 _ColorOuter;
            float _BoundsRadius;
            float _StepSize;
            float _PlasmaScale;
            float _WarpStrength;
            float _FlowSpeed;
            float _DensityGain;
            float _DensityThreshold;
            float _DensityPower;
            float _Absorption;
            float _Falloff;
            float _Emission;
            float _HaloPulseSpeed;
            float _HaloPulseAmplitude;

            float3 DomainWarp(float3 p)
            {
                float t = _Time.y * _FlowSpeed;
                float3 warp = sin(p.yzx * 1.21 + t) + sin(p.zxy * 1.73 - t);
                return p + warp * _WarpStrength;
            }

            float SampleDensity(float3 p)
            {
                float scale = max(_PlasmaScale, 1e-3);
                float3 q = DomainWarp(p * scale);
                float t = _Time.y * _FlowSpeed;
                float a = sin(q.x + t);
                float b = sin(q.y * 1.37 - t * 1.15);
                float c = sin(q.z * 0.93 + t * 0.85);
                float d = sin(dot(q, float3(0.910, 0.601, 1.233)) - t * 0.62);
                float baseVal = (a + b + c + d) * 0.25;
                float density = saturate((baseVal * 0.5 + 0.5 - _DensityThreshold) * _DensityGain);
                density = pow(density, max(_DensityPower, 0.01));
                return density;
            }

            float3 EvaluateColor(float density, float rim)
            {
                // Bias inner vs outer hues with a visible gradient while forcing a very bright output.
                float core = smoothstep(0.2, 0.9, density);
                float edge = saturate(rim);
                float blend = saturate(core * (1.0 - edge * 0.35) + (1.0 - edge) * 0.25);

                float3 vividInner = lerp(_ColorInner.rgb, float3(1.0, 0.95, 0.9), 0.2);
                float3 vividOuter = lerp(_ColorOuter.rgb, float3(0.9, 0.97, 1.0), 0.1);
                float3 baseColor = lerp(vividOuter, vividInner, blend);

                const float minEmission = 10.0; // Keep plasma extremely bright regardless of incoming values.
                float brightness = max(_Emission * 3.0, minEmission);
                float punch = 1.3 + density * 2.2 + (1.0 - rim) * 1.1;
                return baseColor * (brightness * punch);
            }

            bool RaySphere(float3 ro, float3 rd, float radius, out float2 hit)
            {
                float b = dot(ro, rd);
                float c = dot(ro, ro) - radius * radius;
                float h = b * b - c;
                if (h < 0.0)
                {
                    hit = 0;
                    return false;
                }
                h = sqrt(h);
                hit = float2(-b - h, -b + h);
                return hit.y > 0.0;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                o.hitPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.ro;
                float3 rd = normalize(i.hitPos - ro);

                float2 hit;
                // Enlarge bounds to include torus halo around the sphere.
                float torusMajor = _BoundsRadius * 1.1;
                float torusMinorBase = _BoundsRadius * 0.04;
                float pulsePhaseInit = _Time.y * _HaloPulseSpeed;
                float pulseInit = 1.0 + _HaloPulseAmplitude * sin(pulsePhaseInit);
                float torusMinorInit = torusMinorBase * saturate(pulseInit);
                float torusOuterRadius = torusMajor + torusMinorInit;
                float maxBounds = max(_BoundsRadius, torusOuterRadius);

                if (!RaySphere(ro, rd, maxBounds, hit))
                {
                    return 0;
                }

                float t = max(hit.x, 0.0);
                float tEnd = hit.y;
                float transmittance = 1.0;
                float3 accum = 0.0;
                float torusAlphaAccum = 0.0;

                UNITY_LOOP
                for (int step = 0; step < MAX_STEPS; ++step)
                {
                    if (t > tEnd || transmittance < 0.02)
                    {
                        break;
                    }

                    float3 samplePos = ro + rd * (t + _StepSize * 0.5);
                    float radius = length(samplePos);

                    // Pulse state shared by halo calculations.
                    float pulsePhase = _Time.y * _HaloPulseSpeed;
                    float pulseWave = sin(pulsePhase);
                    float pulse = 1.0 + _HaloPulseAmplitude * pulseWave;

                    // Plasma sphere sampling only inside its radius.
                    if (radius <= _BoundsRadius)
                    {
                        float density = SampleDensity(samplePos);
                        float rim = saturate(radius / _BoundsRadius);
                        density *= exp(-_Falloff * rim * rim);

                        if (density > 1e-4)
                        {
                            float absorb = saturate(1.0 - exp(-density * _Absorption * _StepSize));
                            float3 sampleColor = EvaluateColor(density, rim);
                            accum += transmittance * absorb * sampleColor;
                            transmittance *= (1.0 - absorb);
                        }
                    }

                    // Torus halo around the sphere (emissive only, no absorption).
                    {
                        float torusMinor = torusMinorBase * saturate(pulse);
                        float3 p = samplePos;
                        float2 q = float2(length(p.xz), p.y);
                        float torusDist = length(float2(q.x - torusMajor, q.y)) - torusMinor;
                        float torusShell = 1.0 - smoothstep(0.0, _StepSize * 3.0, torusDist);
                        if (torusShell > 1e-4)
                        {
                            float warmFactor = saturate(_HaloPulseAmplitude * 0.8);
                            float3 warmTint = float3(1.0, 0.64, 0.32);
                            float3 haloBaseColor = lerp(_ColorOuter.rgb, warmTint, warmFactor);
                            float haloIntensity = torusShell * pulse;
                            float3 haloColor = haloBaseColor * haloIntensity;
                            float haloAlpha = haloIntensity * 0.35;
                            accum += haloColor * 0.6;
                            torusAlphaAccum = max(torusAlphaAccum, haloAlpha);
                        }
                    }

                    if (radius > maxBounds)
                    {
                        break;
                    }

                    t += _StepSize;
                }

                float4 finalColor = float4(accum, saturate(1.0 - transmittance));
                finalColor.a = saturate(finalColor.a + torusAlphaAccum);
                return finalColor;
            }
            ENDCG
        }
    }
}
