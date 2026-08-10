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
        _NoseCenter ("Nose center", Vector) = (.5,.5,0,0)
        _NoseRadius ("Nose radius", Vector) = (.04,.03,0,0)
        _NoseStrength ("Radial expansion", Float) = 0
        _NoseAngle ("Nose angle", Float) = 0
        _SkinTint ("Skin tint", Color) = (1,1,1,1)
        _SkinStrength ("Skin tint strength", Float) = 0
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
            float2 _NoseCenter;
            float2 _NoseRadius;
            float _NoseStrength;
            float _NoseAngle;
            fixed4 _SkinTint;
            float _SkinStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 한 부위만 국소적으로 넓히는 샘플 좌표 변형.
            // scale.x/scale.y로 축별 확장량을 나눠 입은 가로로, 코는 반경으로 키운다.
            float2 WarpFeature(float2 uv, float2 center, float2 radius,
                float angle, float2 scale)
            {
                float sine = sin(angle);
                float cosine = cos(angle);
                float2 delta = uv - center;
                float2 local = float2(cosine * delta.x - sine * delta.y,
                                      sine * delta.x + cosine * delta.y);
                float2 normalized = local / max(radius, float2(.0001, .0001));
                float distanceSquared = dot(normalized, normalized);
                float falloff = saturate(1.0 - distanceSquared);
                falloff *= falloff;
                local /= 1.0 + scale * falloff;
                delta = float2(cosine * local.x + sine * local.y,
                               -sine * local.x + cosine * local.y);
                return center + delta;
            }

            // 피부만 골라내는 근사 마스크.
            // 크림색 우주복은 채도가 거의 없어 제외되고, 눈동자·머리 그림자는 어두워 제외된다.
            half SkinMask(half3 color)
            {
                half maxChannel = max(color.r, max(color.g, color.b));
                half minChannel = min(color.r, min(color.g, color.b));
                half saturation = maxChannel - minChannel;
                half warm = step(color.g, color.r);
                return warm
                    * smoothstep(.05, .16, saturation)
                    * smoothstep(.16, .36, maxChannel);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                if (_MouthStrength > 0)
                    uv = WarpFeature(uv, _MouthCenter, _MouthRadius, _MouthAngle,
                        float2(_MouthStrength, 0));
                if (_NoseStrength > 0)
                    uv = WarpFeature(uv, _NoseCenter, _NoseRadius, _NoseAngle,
                        float2(_NoseStrength, _NoseStrength));

                fixed4 source = tex2D(_MainTex, uv);
                if (_SkinStrength > 0)
                {
                    half mask = SkinMask(source.rgb) * _SkinStrength;
                    source.rgb = lerp(source.rgb, source.rgb * _SkinTint.rgb, mask);
                }
                return source * _Color;
            }
            ENDCG
        }
    }
}
