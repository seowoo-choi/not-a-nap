Shader "Hidden/NotANap/BabyMouthWarp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MouthCenter ("Mouth center", Vector) = (.5,.5,0,0)
        _MouthRadius ("Mouth radius", Vector) = (.06,.04,0,0)
        _MouthStrength ("Horizontal expansion", Float) = .28
        _MouthAngle ("Mouth angle", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float2 _MouthCenter;
            float2 _MouthRadius;
            float _MouthStrength;
            float _MouthAngle;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float sine = sin(_MouthAngle);
                float cosine = cos(_MouthAngle);
                float2 delta = i.uv - _MouthCenter;
                float2 local = float2(cosine * delta.x - sine * delta.y,
                                      sine * delta.x + cosine * delta.y);
                float2 normalized = local / max(_MouthRadius, float2(.0001, .0001));
                float distanceSquared = dot(normalized, normalized);
                float falloff = saturate(1.0 - distanceSquared);
                falloff *= falloff;
                local.x /= 1.0 + _MouthStrength * falloff;
                delta = float2(cosine * local.x + sine * local.y,
                               -sine * local.x + cosine * local.y);
                return tex2D(_MainTex, _MouthCenter + delta) * _Color;
            }
            ENDCG
        }
    }
}
