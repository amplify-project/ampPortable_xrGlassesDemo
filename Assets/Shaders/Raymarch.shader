/* Filename: Raymarch.shader
 * Creator: Bryan Dunphy
 * Date: 09/06/23
 * Purpose: This file implements a raymarcher with signed distance
 *          fields. The set up of the raymarching algorithm is
 *          adapted from The Art of Code: https://bit.ly/3J6OShb. 
 */

Shader "Unlit/Raymarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            #define MAX_STEPS 100
            #define MAX_DIST 100
            #define SURF_DIST 1e-4
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
    
                // For stereo rendering
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            // This function bends the SDF along the x and z axes
            float3 Bend(float3 p)
            {
                float k = sin(_Time.y * 2);
                int xDimension = step(0, k);
                int zDimension = step(k, 0);
                float cX = cos(k * p.x);
                float sX = sin(k * p.x);
                float cZ = cos(k * p.z);
                float sZ = sin(k * p.z);
    
                float c = (cX * xDimension) + (cZ * zDimension);
                float s = (sX * xDimension) + (sZ * zDimension);
                
                float2x2 m = float2x2(c, s, -s, c);
                float2 matMul = mul(m, p.xy);
                float3 q = float3(matMul.x, matMul.y, p.z);
                return q;
            }

            // This is an SDF for a torus
            float TorusSDF(float3 p)
            {
                return length(float2(length(p.xz) - 0.25, p.y)) - 0.03;
            }

            // This function returns the distance to the scene objects
            float GetDist(float3 p)
            {  
                float3 bendRes = Bend(p);
    
                // taurus SDF
                float d = TorusSDF(bendRes);
    
                return d;
            }

            // This function performs the raymarch algorithm
            float Raymarch(float3 ro, float3 rd)
            {
                float dO = 0;
                float dS;
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = ro + dO * rd;
                    dS = GetDist(p);
                    dO += dS;
                    if (dS < SURF_DIST || dO > MAX_DIST)
                        break;
                }
    
                return dO;
            }

            // This function returns the normal at the point of intersection
            float3 GetNormal(float3 p)
            {
                float2 e = float2(1e-2, 0);
                float3 n = GetDist(p) - float3(
                    GetDist(p - e.xyy),
                    GetDist(p - e.yxy),
                    GetDist(p - e.yyx)
                );
                return normalize(n);
            }

            // This function returns texture coordinates for the torus
            float2 TorusUV(float3 position)
            {
                float u = atan2(position.z, position.x) / (2.0 * 3.1415926) + 0.5;
                float v = atan2(position.y, length(position.xz) - 1.0) / (2.0 * 3.1415926) + 0.5;
                return float2(u, v);
            }

            v2f vert(appdata v)
            {
                v2f o;
                            UNITY_SETUP_INSTANCE_ID(v);
                            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    
                            // world space
                o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1));
                            // object space
                o.hitPos = v.vertex;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // map texture coords and move origin
                // to centre
                float2 uv = i.uv - 0.5; 
                // get the ray origin
                float3 ro = i.ro;
                // get the ray direction
                float3 rd = normalize(i.hitPos - ro);
                
                // perform raymarch
                float d = Raymarch(ro, rd);
    
                fixed4 col = 0;
                // if the ray doesn't hit anything discard it
                if (d >= MAX_DIST)
                {
                    discard;
                }
                // otherwise calculate the colour at the point of intersection
                else 
                {
                    float3 p = ro + rd * d;
                    float3 n = GetNormal(p);
                    float2 uv = TorusUV(p - float3(0, 0, 0));
        
                    // Transmixr colours
                    fixed3 pink = fixed3(229, 9, 127);
                    fixed3 turquoise = fixed3(9, 103, 139);
                    fixed3 blend = lerp(normalize(turquoise), normalize(pink), clamp(n.x * sin(_Time.y * 2), 0.25, 1));
                    col = fixed4(blend, 1);
        
                    // global lighting
                    float sun = clamp(dot(n, SUN_DIR), 0.0, 1.0);
                    float sky = clamp(0.5 + 0.5 * n.y, 0.0, 1.0);
                    float ind = clamp(dot(n, normalize(SUN_DIR * float3(-1.0, -1.0, 0.0))), 0.0, 1.0);
		    
                    float3 lightRig = sun * float3(1.64, 1.27, 0.99);
                    lightRig += sky * float3(0.32, 0.4, 0.56);
                    lightRig += ind * float3(0.4, 0.28, 0.2);
		    
                    col = col * float4(lightRig, 0);
                } 
    
                return col;
            }
            ENDCG
        }
    }
}
