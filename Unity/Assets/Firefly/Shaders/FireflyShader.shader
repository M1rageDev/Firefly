Shader "Firefly/Firefly"
{
	Properties
	{
		[Header(Textures)]
		_AirstreamTex ("Airstream Texture", 2D) = "" {}
		_NoiseTex ("Noise Texture", 2D) = "" {}
		_DitherTex ("Dither Texture", 2D) = "" {}

		[Space]
		[Header(Values)]
		_TrailAlphaMultiplier ("Trail Alpha Multiplier", Float) = 1
		_BlueMultiplier ("Blue Multiplier", Float) = 0.1
		_OpacityMultiplier ("Opacity Multiplier", Float) = 1
		_WrapFresnelModifier ("Wrap layer fresnel modifier", Float) = 0
		_StreakProbability ("Streak Probability", Float) = 0.1
		_StreakThreshold ("Streak Threshold", Float) = -0.2
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
		LOD 100

		HLSLINCLUDE
		#include "UnityCG.cginc"
		#include "CommonFunctions.cginc"
		ENDHLSL

		Pass
		{
			Name "Glow Pass"

			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha
			
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "EffectPasses/GlowPass.cginc"

			ENDHLSL
		}
		
		Pass
		{
			Name "Effects Pass"

			ZWrite Off
			ZTest LEqual
			Blend SrcAlpha One
			Cull Off
			
			HLSLPROGRAM
			
			#pragma require geometry

			#pragma vertex gs_vert
			#pragma geometry gs_geom
			#pragma fragment gs_frag

			#include "EffectPasses/MainPass.cginc"
			
			ENDHLSL
		}
		
		Pass
		{
			Name "Bowshock Pass"
			
			ZWrite Off
			Cull Off
			Blend SrcAlpha One
			ZTest LEqual
			
			HLSLPROGRAM
			
			#pragma require geometry

			#pragma vertex gs_vert
			#pragma geometry gs_geom
			#pragma fragment gs_frag
			
			#include "EffectPasses/BowshockPass.cginc"
			
			ENDHLSL
		}
	}
}