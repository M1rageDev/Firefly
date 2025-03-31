Texture2D _AirstreamTex;
SamplerState sampler_AirstreamTex;

Texture2D _NoiseTex;
SamplerState sampler_NoiseTex;

Texture2D _DitherTex;
SamplerState sampler_DitherTex;

float _EntryStrength;

float3 _ModelScale;
float3 _EnvelopeScaleFactor;
float3 _Velocity;
float4x4 _AirstreamVP;

// Gets the scaled entry speed
float Shadow(float3 airstreamNDC, float bias, float shadowStrength)
{
	if (airstreamNDC.x < -1.0f || airstreamNDC.x > 1.0f || airstreamNDC.y < -1.0f || airstreamNDC.y > 1.0f) 
	{
		return 1;
	}
		
	float2 UV = airstreamNDC.xy * 0.5 + 0.5;
	float3 lpos = airstreamNDC;
	
	#if defined(UNITY_REVERSED_Z)
		
	#else
		lpos.z = -lpos.z * 0.5 + 0.5;
	#endif
	
	#if UNITY_UV_STARTS_AT_TOP
		UV.y = 1 - UV.y;
	#endif
	
	lpos.x = lpos.x / 2 + 0.5;
	lpos.y = lpos.y / -2 + 0.5;
	lpos.z -= bias;
		
	float sum = 0;
	float x, y;
	for (y = -2; y <= 2; y += 1.0f)
	{
		for (x = -2; x <= 2; x += 1.0f)
		{
			float sampled = _AirstreamTex.SampleLevel(sampler_AirstreamTex, UV + float2(x * 1/512, y * 1/512), 0);
			
			#if defined(UNITY_REVERSED_Z)
				
			#else
				sampled = 1 - sampled;
			#endif
			
			if (sampled <= lpos.z) sum += 1;
		}
	}
	float shadowFac = sum / 25.0f;
	return saturate(shadowFac + 1 - shadowStrength);
}

float3 TransformObjectToWorld(float3 v) 
{
	return mul(unity_ObjectToWorld, float4(v, 1.0));
}

float3 TransformWorldToObject(float3 v) 
{
	return mul(unity_WorldToObject, float4(v, 1.0));
}

float3 GetAirstreamNDC(float3 positionOS)
{
	float3 positionWS = TransformObjectToWorld(positionOS);

	float4 airstreamPosition = mul(_AirstreamVP, float4(positionWS, 1));
	return airstreamPosition.xyz / airstreamPosition.w;
}

// Samples the noise map at a given UV and channel
float Noise(float2 uv, int channel)
{
	return _NoiseTex.SampleLevel(sampler_NoiseTex, uv + float2(_Time.x*14, _Time.x*7), 0)[channel];
}

float NoiseStatic(float2 uv, int channel)
{
	return _NoiseTex.SampleLevel(sampler_NoiseTex, uv, 0)[channel];
}

float Fresnel(float3 normal, float3 viewDir, float power) 
{
	return pow((1.0 - saturate(dot(normalize(normal), normalize(viewDir)))), power);
}