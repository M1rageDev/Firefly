struct VS_INPUT
{
	float4 position : POSITION;
	float3 normal : NORMAL;
};

struct FS_INPUT
{
	float4 position : SV_POSITION;
	float3 normal : NORMAL;
				
	float4 screenPos : TEXCOORD2;
	float3 worldPosition : TEXCOORD3;
	float4 airstreamNDC : TEXCOORD4;
	float3 viewDir : TEXCOORD5;
};

float _Hdr;
int _UnityEditor;
			
float4 _GlowColor;
float4 _HotGlowColor;
float4 _TertiaryColor;

float _GlowMultiplier;

float _BlueMultiplier;
			
float _FxState;

float4 GetEntryColor(float4 heat, float4 blue, float strength)
{
	float whiteRatio = strength / 0.25;

	float4 col = lerp(heat, blue, strength);

	if (whiteRatio < 2)
	{
		float t = saturate(whiteRatio - 1);

		col = lerp(1, col, t + _FxState);
	}

	return col;
}

FS_INPUT vert(VS_INPUT IN)
{
	FS_INPUT OUT;

	float3 scaledPos = IN.position.xyz * _EnvelopeScaleFactor;

	// World space normal
	OUT.normal = UnityObjectToWorldNormal(IN.normal);

	// Position in clip space
	OUT.position = UnityObjectToClipPos(scaledPos);

	// Screen POSITION
	OUT.screenPos = ComputeScreenPos(OUT.position);
	OUT.screenPos = OUT.screenPos / OUT.screenPos.w;
				
	// Calculating the clip position on the airstream camera
	OUT.worldPosition = TransformObjectToWorld(IN.position.xyz);
	float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.worldPosition, 1));
	OUT.airstreamNDC = airstreamPosition / airstreamPosition.w;
				
	// View dir
	OUT.viewDir = WorldSpaceViewDir(IN.position);

	return OUT;
}

half4 frag(FS_INPUT IN) : SV_Target
{
	// Occlusion
	float shadow = Shadow(IN.airstreamNDC, -0.003, 1);

	// Acquiring the alpha
	float entrySpeed = saturate(_EntryStrength / 4000);
	float whiteRatio = saturate(entrySpeed / 0.42);  // white when below 1680
	float faintRatio = saturate(entrySpeed / 0.5) / 2;

	float rawDot = saturate(dot(IN.normal, _Velocity));
	float velDot = saturate(rawDot + 0.2);

	float alpha = saturate(shadow * whiteRatio * _GlowMultiplier * velDot * _FxState);
				
	// Entry heat color
	float3 entryHeat = GetEntryColor(_GlowColor, _HotGlowColor, (entrySpeed + _BlueMultiplier)) * shadow * velDot * lerp(0, 4, faintRatio);
				
	// Output
	//clip(DitherTexture(alpha, IN.screenPos.xy));
	entryHeat = lerp(entryHeat * alpha, entryHeat, _Hdr);  // perform alpha blending before the color is clamped to SDR
	return float4(entryHeat, alpha);
}