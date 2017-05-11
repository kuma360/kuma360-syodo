Shader "Unlit/PaperShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }


    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            struct v2f
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (v2f v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
            	float P = 0;
            	int M = 4;

            	//ピクセルを円形にぼかす。
            	for(int I=-M;I<M;++I) {
	            	for(int J=-M;J<M;++J) {
	            		float D = tex2D(_MainTex,i.uv+float2(I/1334.0,J/750.0)).a;
	            		float L = distance(float2(I/1024.0,J/1024.0),float2(0,0)) ;
	            		if(L<.004f) {
            				P += D ;
	            		}
	            	}
            	}

            	return float4(1,1,1,1) - float4(P,P,P,1);
            }
            ENDCG
        }
    }
}