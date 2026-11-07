// Arena sky dome: a soft two-stop vertical gradient, horizon color at and below
// the horizon, top color overhead. Drawn after the camera's skybox and placed at the far plane,
// so it covers the skybox and every other surface stays in front of it.
Shader "RacecarSim/ArenaSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.52, 0.87, 1, 1)
        _HorizonColor ("Horizon", Color) = (0.64, 0.95, 0.95, 1)
        _Height ("Gradient height (0 to 1 of the dome)", Range(0.05, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Transparent-1" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            float _Height;

            struct v2f
            {
                float4 position : SV_POSITION;
                float height : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.position = UnityObjectToClipPos(vertex);
                // Just in front of the far plane (the skybox depth), behind all scenery
                #if defined(UNITY_REVERSED_Z)
                o.position.z = o.position.w * 1e-5;
                #else
                o.position.z = o.position.w * (1 - 1e-5);
                #endif
                o.height = normalize(vertex.xyz).y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = smoothstep(0, _Height, i.height);
                return lerp(_HorizonColor, _TopColor, t);
            }
            ENDCG
        }
    }
}
