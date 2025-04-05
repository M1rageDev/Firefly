Shader "Firefly/Particles"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (0, 0, 0)

		_EmissionMap ("Emission Texture", 2D) = "white" {}
		[HDR] _EmissionColor ("Emission", Color) = (0, 0, 0)

		_Blending ("Blend: Additive<->Multiply", Range(0, 1)) = 0

		_AirstreamBlending ("Airstream Blending", Range(0, 1)) = 1
		_AirstreamTex ("Airstream Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		LOD 100
		BlendOp Add
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "CommonFunctions.cginc"

			struct VS_INPUT
			{
				float4 position : POSITION;
				float3 uv : TEXCOORD0;
				float center : TEXCOORD1;
				half4 color : COLOR;
			};

			struct FS_INPUT
			{
				float2 uv : TEXCOORD0;
				float4 position : SV_POSITION;
				half4 color : COLOR;
				
				float shadow : TEXCOORD1;
				float lifetime : TEXCOORD2;
			};

			Texture2D _MainTex;
			SamplerState sampler_MainTex;

			Texture2D _EmissionMap;
			SamplerState sampler_EmissionMap;

			half4 _Color;
			half4 _EmissionColor;

			float _AirstreamBlending;

			int _Blending;

			FS_INPUT vert (VS_INPUT v)
			{
				FS_INPUT o;
				o.position = UnityObjectToClipPos(v.position);
				o.uv = v.uv;
				o.color = v.color;

				// calculate world space and airstream camera space coordinates for occlusion
				float3 worldPosition = TransformObjectToWorld(v.position);
				float4 airstreamPosition = mul(_AirstreamVP, float4(worldPosition, 1));
				float4 airstreamNDC = airstreamPosition / airstreamPosition.w;

				// sample the shadowmap for occlusion
				o.shadow = saturate(1 - Shadow(airstreamNDC.xyz, -0.003, 1));

				// pass the lifetime to the fragment shader
				o.lifetime = v.uv.z;

				return o;
			}

			half4 frag (FS_INPUT i) : SV_Target
			{
				// sample main texture and blend it with color
				half4 col = _MainTex.Sample(sampler_MainTex, i.uv);
				col *= _Color;
				col.a *= i.color.a;

				// change alpha based on occlusion and lifetime
				// particles are less affected by the occlusion the longer they exist
				col.a *= lerp(1.0, lerp(i.shadow, 1.0, saturate(i.lifetime)), _AirstreamBlending);

				// blend texture color with particle color
				half3 additive = col.rgb + i.color.rgb;  // additive blending
				half3 multiplicative = col.rgb * i.color.rgb;  // multiplicative blending

				// perform the proper color blending, additive or multiplicative
				col.rgb = lerp(additive, multiplicative, _Blending);

				// emission
				half3 emission = _EmissionMap.Sample(sampler_EmissionMap, i.uv).rgb;
				col.rgb += emission * _EmissionColor;

				return col;
			}
			ENDHLSL
		}
	}
}
