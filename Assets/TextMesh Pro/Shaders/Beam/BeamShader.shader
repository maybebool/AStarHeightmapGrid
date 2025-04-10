Shader "Unlit/BeamShader"
{
 Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Threshold("Black Key Threshold", Range(0, 1)) = 0.02
        _ScrollSpeed("Scroll Speed (X-axis)", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        LOD 100

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;   
            float  _Threshold;
            float  _ScrollSpeed;
            
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;  
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR; 
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                OUT.positionHCS = TransformObjectToHClip(float4(IN.positionOS, 1.0));
                float2 uvScaled = IN.uv * _MainTex_ST.xy;
                uvScaled.x += _MainTex_ST.z + _ScrollSpeed * _Time.x;
                uvScaled.y += _MainTex_ST.w;
                OUT.uv = uvScaled;
                OUT.color = IN.color;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half brightness = (texColor.r + texColor.g + texColor.b) * 0.3333h;
                if (brightness < _Threshold)
                {
                    texColor.a = 0;
                }
                half4 finalColor = texColor * IN.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
