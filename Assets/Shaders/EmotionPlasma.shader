Shader "Unlit/EmotionPlasma"
{
    Properties
    {
        // Original core colours & plasma controls
        _ColorInner     ("Inner Color", Color)                        = (1.000, 0.502, 0.184, 1.000)
        _ColorOuter     ("Outer Color", Color)                        = (0.133, 0.400, 0.949, 1.000)
        _BoundsRadius   ("Bounds Radius", Float)                      = 0.8
        _StepSize       ("Step Size", Range(0.005, 0.1))              = 0.04
        _PlasmaScale    ("Noise Scale", Float)                        = 3.0
        _WarpStrength   ("Warp Strength", Float)                      = 0.65
        _FlowSpeed      ("Flow Speed", Float)                         = 1.25

        // Global appearance
        _Brightness     ("Brightness", Range(0, 5))                   = 1.0   // arousal → intensity
        _Saturation     ("Saturation", Range(0, 3))                   = 1.0   // arousal/valence → vividness

        // Shape & surface “feel”
        _Curvature      ("Curvature (Round vs Angular)", Range(0, 1)) = 0.5   // valence → roundness
        _Asymmetry      ("Asymmetry (Order vs Chaos)", Range(0, 1))   = 0.0   // confusion → asymmetry
        _SurfaceSharpness("Surface Sharpness", Range(0, 2))           = 0.2   // frustration → sharpness

        // Motion / internal energy
        _Turbulence     ("Turbulence", Range(0, 1))                   = 0.2   // confusion/frustration → chaos
        _PulseFrequency ("Pulse Frequency", Range(0, 10))             = 2.0   // arousal → speed of pulsing
        _PulseAmplitude ("Pulse Amplitude", Range(0, 1))              = 0.3   // arousal → intensity of pulsing

        // Texture / noise
        _Smoothness     ("Smoothness", Range(0, 1))                   = 0.8   // valence → smooth vs rough
        _NoiseScale     ("Secondary Noise Scale", Range(0, 2))        = 0.5   // detail scale
        _NoiseContrast  ("Noise Contrast", Range(0, 2))               = 1.0   // roughness/harshness
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 worldNormal: TEXCOORD1;
            };

            // ---- Properties as uniforms ----
            float4 _ColorInner;
            float4 _ColorOuter;
            float  _BoundsRadius;
            float  _StepSize;
            float  _PlasmaScale;
            float  _WarpStrength;
            float  _FlowSpeed;

            // New affective controls
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

            v2f vert (appdata v)
            {
                v2f o;
                o.pos        = UnityObjectToClipPos(v.vertex);
                o.worldPos   = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal= UnityObjectToWorldNormal(v.normal);
                return o;
            }

            // Simple value noise + fbm

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

                float n000 = hash(i + float3(0,0,0));
                float n100 = hash(i + float3(1,0,0));
                float n010 = hash(i + float3(0,1,0));
                float n110 = hash(i + float3(1,1,0));
                float n001 = hash(i + float3(0,0,1));
                float n101 = hash(i + float3(1,0,1));
                float n011 = hash(i + float3(0,1,1));
                float n111 = hash(i + float3(1,1,1));

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
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    f += a * noise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return f;
            }

            float3 applySaturation(float3 rgb, float sat)
            {
                float l = dot(rgb, float3(0.299, 0.587, 0.114));
                return lerp(float3(l, l, l), rgb, sat);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sphere bounds in world space
                float3 center = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float3 toPos  = i.worldPos - center;
                float  r      = length(toPos);

                if (r > _BoundsRadius)
                    discard;

                float radius = max(_BoundsRadius, 1e-4);
                float tNorm  = r / radius;

                float time = _Time.y * _FlowSpeed;

                // Curvature: bias density toward the centre; higher curvature → sharper falloff
                float curvPow = lerp(0.5, 3.0, _Curvature);
                float sphereCurv = pow(saturate(1.0 - tNorm), curvPow);

                // Basic plasma position with warp/turbulence control
                float3 p = toPos * (_PlasmaScale + _NoiseScale);
                p += _WarpStrength * fbm(p * 0.5);
                p += time;

                // Turbulence increases frequency/chaos
                float turbFactor = 1.0 + _Turbulence * 2.0;
                float density = fbm(p * turbFactor);

                // Modulate by spherical curvature
                density *= sphereCurv;

                // Surface sharpness: controls how “spiky” the density is
                density = pow(saturate(density), lerp(1.0, 3.0, _SurfaceSharpness));

                // Pulsing based on arousal-type input mapped from script
                float pulse = 1.0 + _PulseAmplitude * sin(time * _PulseFrequency * 6.28318);
                density *= pulse;

                // Color gradient from inner to outer
                float3 col = lerp(_ColorInner.rgb, _ColorOuter.rgb, density);

                // Global brightness & saturation (arousal / valence)
                col *= _Brightness;
                col  = applySaturation(col, _Saturation);

                float alpha = saturate(density);

                return float4(col, alpha);
            }
            ENDCG
        }
    }
}
