/* Filename: KifsFractal.shader
 * Creator: Bryan Dunphy
 * Purpose: Raymarch a Kaleidoscopic Iterated Function System (KIFS) fractal,
 *          mirroring the setup used in Raymarch.shader but replacing the torus SDF.
 */

Shader "Unlit/KifsFractal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FractalScale ("Fractal Scale", Float) = 0.4
        _BoundingRadius ("Bounding Radius", Float) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define MAX_STEPS 60
            #define MAX_DIST 22
            #define SURF_DIST 4e-4
            #define SUN_DIR float3(0.5, 0.8, 0.0)

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FractalScale;
            float _BoundingRadius;

            float3 ReflectPlane(float3 p, float3 n)
            {
                float d = dot(p, n);
                return d < 0.0 ? p - 2.0 * d * n : p;
            }

            float3 TetraFold(float3 p)
            {
                const float3 n0 = float3(0.57735026919, 0.57735026919, 0.57735026919);
                const float3 n1 = float3(-0.57735026919, 0.57735026919, 0.57735026919);
                const float3 n2 = float3(0.57735026919, -0.57735026919, 0.57735026919);
                const float3 n3 = float3(0.57735026919, 0.57735026919, -0.57735026919);

                p = ReflectPlane(p, n0);
                p = ReflectPlane(p, n1);
                p = ReflectPlane(p, n2);
                p = ReflectPlane(p, n3);
                return p;
            }

            float sdTetrahedron(float3 p)
            {
                const float invSqrt3 = 0.57735026919;
                float d0 = (-1.0 - (p.x + p.y + p.z)) * invSqrt3;
                float d1 = (p.x + p.y - p.z - 1.0) * invSqrt3;
                float d2 = (p.x - p.y + p.z - 1.0) * invSqrt3;
                float d3 = (-p.x + p.y + p.z - 1.0) * invSqrt3;
                return max(max(d0, d1), max(d2, d3));
            }

            float FractalDistance(float3 p, out float orbitTrap)
            {
                const int iterations = 9;
                const float foldScale = 2.0;
                const float bailout = 16.0;
                float scale = max(_FractalScale, 1e-3);
                float3 z = p / scale;
                float accumulatedScale = 1.0;
                float minTrap = 1e9;

                float wobble = sin(_Time.y * 0.6);
                float3 animatedShift = 1.0 + wobble * float3(0.35, 0.25, 0.2);

                UNITY_LOOP
                for (int i = 0; i < iterations; ++i)
                {
                    z = TetraFold(z);
                    z = z * foldScale - animatedShift;
                    accumulatedScale *= foldScale;
                    float r2 = dot(z, z);
                    minTrap = min(minTrap, r2);
                    if (r2 > bailout)
                    {
                        break;
                    }
                }

                float invAccum = 1.0 / max(accumulatedScale, 1e-3);
                orbitTrap = saturate(sqrt(minTrap) * invAccum);
                float dist = sdTetrahedron(z) * invAccum;
                return dist * scale;
            }

            float FractalDistance(float3 p)
            {
                float unused;
                return FractalDistance(p, unused);
            }

            float GetDist(float3 p)
            {
                float fractal = FractalDistance(p);
                float bounding = length(p) - _BoundingRadius;
                return max(fractal, bounding);
            }

            float Raymarch(float3 ro, float3 rd)
            {
                float dO = SURF_DIST * 8.0;
                UNITY_LOOP
                for (int i = 0; i < MAX_STEPS; ++i)
                {
                    float3 p = ro + dO * rd;
                    float dS = GetDist(p);
                    dO += dS;
                    if (dS < SURF_DIST || dO > MAX_DIST)
                    {
                        break;
                    }
                }

                return dO;
            }

            float3 GetNormal(float3 p)
            {
                float2 e = float2(1e-2, 0);
                float d = GetDist(p);
                float3 n = float3(
                    d - GetDist(p - e.xyy),
                    d - GetDist(p - e.yxy),
                    d - GetDist(p - e.yyx)
                );
                return normalize(n);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0));
                o.hitPos = v.vertex;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ro = i.ro;
                float3 rd = normalize(i.hitPos - ro);

                float d = Raymarch(ro, rd);

                if (d >= MAX_DIST)
                {
                    discard;
                }

                float3 p = ro + rd * d;
                float surfaceCheck = GetDist(p);
                if (surfaceCheck > SURF_DIST * 4.0)
                {
                    discard;
                }
                float trap;
                FractalDistance(p, trap);
                float3 n = GetNormal(p);

                const half3 pink = half3(0.898h, 0.035h, 0.498h);
                const half3 turquoise = half3(0.035h, 0.404h, 0.545h);
                half wobble = half(sin(_Time.y * 0.7));
                half blendFactor = saturate(half(0.35) + half(0.55) * (half(1.0) - half(trap)) + wobble * half(0.1));
                half3 baseColor = lerp(turquoise, pink, blendFactor);

                half sun = saturate(dot(n, SUN_DIR));
                half3 lighting = sun * half3(1.2h, 1.1h, 1.0h) + half3(0.18h, 0.22h, 0.26h);

                return fixed4(baseColor * lighting, 1.0h);
            }
            ENDCG
        }
    }
}
