float _FxState;
float _AngleOfAttack;
int _Hdr;
int _UnityEditor = 0;
int _VertexSamples = 3;
			
float _VelDotPower;
float _ShadowPower;
float _LengthMultiplier;

float _TrailAlphaMultiplier;
float _BlueMultiplier;
float _SideMultiplier;
float _OpacityMultiplier;
float _WrapFresnelModifier;
float _StreakProbability;
float _StreakThreshold;

float4 _SecondaryColor;
float4 _PrimaryColor;
float4 _TertiaryColor;
float4 _StreakColor;
float4 _LayerColor;
float4 _LayerStreakColor;

float2 _RandomnessFactor;  // factor deciding the streak randomness, should be high for asteroids and low for anything else

struct VS_INPUT
{
	float4 position : POSITION;
	float3 normal : NORMAL;

	float2 uv : TEXCOORD0;
};

struct GS_INPUT
{
	float4 position : POSITION;
	float3 normal : NORMAL;

	float2 uv : TEXCOORD0;

	float3 positionWS : TEXCOORD3;
	float4 airstreamNDC : TEXCOORD4;
	float3 velocityOS : TEXCOORD5;
	float3 normalOS : TEXCOORD6;
	float3 viewDir : TEXCOORD7;
}; 

struct GS_DATA
{
	float4 position : SV_POSITION;
	half4 color : COLOR;

	float3 positionWS : TEXCOORD1;
	float3 positionOS : TEXCOORD2;
	float4 screenPos : TEXCOORD4;
				
	float2 trailPos : TEXCOORD6;
				
	float layer : TEXCOORD7;
	float4 airstreamNDC : TEXCOORD8;
};

// Creates a vertex instance that can be added to a triangle stream
GS_DATA CreateVertex(float3 pos, float layer, float4 airstreamNDC, float trailPosX, float trailPosY, half4 color, half a) 
{
	GS_DATA o;

	o.position = UnityObjectToClipPos(pos);
	o.color = color;
	o.color.a = a;

	o.positionWS = TransformObjectToWorld(pos);
	o.positionOS = pos;
	o.screenPos = o.position;
				
	o.trailPos = float2(trailPosX, trailPosY);
				
	o.layer = layer;
				
	o.airstreamNDC = airstreamNDC;

	return o;
}
			
GS_INPUT gs_vert(VS_INPUT IN)
{
	GS_INPUT OUT;

	// get normal in WS and OS
	OUT.normal = UnityObjectToWorldNormal(IN.normal);
	OUT.normalOS = normalize(IN.normal);

	// scale vertex
	OUT.position = IN.position * float4(_EnvelopeScaleFactor, 1);

	OUT.uv = IN.uv;

	OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);

	// get the airstream position
	float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
	OUT.airstreamNDC = float4(airstreamPosition.xyz / airstreamPosition.w, airstreamPosition.w);

	// object space velocity
	OUT.velocityOS = normalize(UnityWorldToObjectDir(_Velocity));
				
	OUT.viewDir = WorldSpaceViewDir(IN.position);

	return OUT;
}

[maxvertexcount(12)]
void gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
	// don't draw anything if the speed is low enough
	if (GetEntrySpeed() < 50) return;
	uint i = 0;

	float entrySpeed = GetEntrySpeed() / 4000 - 0.08 * _FxState;
	float whiteRatio = entrySpeed / 0.25;  // white when below 1000
	float transparentRatio = saturate(entrySpeed / 0.025);

	// Get the occlusion for each vertex
	float3 occlusion = float3(
		Shadow(vertex[0].airstreamNDC, -0.003, 1),
		Shadow(vertex[1].airstreamNDC, -0.003, 1),
		Shadow(vertex[2].airstreamNDC, -0.003, 1)
	);
				
	// Calculate the dot product between the inverted normal and velocity
	float3 velDots = 0;
	for (i = 0; i < 3; i++) velDots[i] = dot(-vertex[i].normal, _Velocity);

	// Calculate the base length
	float baseLength = GetEntrySpeed() * 0.0013;
	float maxBaseLength = 5.2;  // 4000 * 0.0013

	// calculate the noise values for each vertex
	float3 noise = 0;
	for (i = 0; i < 3; i++) noise[i] = Noise(vertex[i].position.xy + vertex[i].uv, 1) * baseLength * Noise(vertex[i].position.xy + vertex[i].uv, 1) * 5;

	// apply noise and calculate the middle vertex's position and outward spread
	float3 effectLength = (baseLength + noise) * _LengthMultiplier;
	float3 middleLength = effectLength * 0.2;
	float middleNormalMultiplier = 0.23 + lerp(0.1 * _FxState, 0, saturate((entrySpeed - 0.2) * 4));
				
	// maximum efffect length at peak noise
	float maxEffectLength = (maxBaseLength * 6) * _LengthMultiplier; // imitating the effectLength variable, noise is at it's maximum, so 1 * maxBaseLength * 1 * 5, which can be simplified to maxBaseLength * 5
				
	// scale with model, to avoid huge trails
	effectLength *= _ModelScale.y;
	middleLength *= _ModelScale.y;

	// iterate through every vertex (probably wrong?)
	for (i = 0; i < 3; i++)
	{
		float velDot = velDots[i];
					
		// DEBUG
		//triStream.Append(CreateVertex(vertex[i].position, 0, 0, velDot, 1));
		// DEBUG

		// Only proceed if the vertex isnt occluded and it's at a high enough angle, creating a rim from which the plasma trail is created
		if (velDot > -0.1 && occlusion[i] > 0.9)
		{
			uint j = (i + 1) % 3;  // next vertex
			uint k = (j + 1) % 3;  // another vertex
						
			// calculate the triangle edge lengths, which then get used to modify the segment width and opacity
			float edgeLength_j = length(vertex[i].position - vertex[j].position);
			float edgeLength_k = length(vertex[i].position - vertex[k].position);
			float edgeLength = (edgeLength_j + edgeLength_k) / 2 * (1 / _ModelScale.y);  // world-scaled average edge length
			float edgeMul = clamp(edgeLength / 0.05, 0.1, 40);
			float sideEdgeMul = 1 - saturate(edgeLength - 0.1);
						
			// decide the second most dominant vertex
			if (occlusion[k] > occlusion[j] || velDots[k] > velDots[j]) j = k;
						
			// vector determining the side direction of the segment (the width)
			float3 sizeVector = normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));

			// vectors which determine the width of the segment at various points
			float3 side = sizeVector * 0.6 * sideEdgeMul;
			float3 middleSide = side * 1.4 * clamp(entrySpeed, 0.2, 1);
			float3 endSide = side * 2.5 * clamp(entrySpeed, 0.2, 1);
			side *= 0.3;
						
			// different noise values for the current vertex
			float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);
			float vertNoise1 = Noise(vertex[i].position.xy - _Time.y * (1 - _RandomnessFactor.x), 1 + round(_RandomnessFactor.x));
			float vertNoise2 = Noise(vertex[i].position.xy - _Time.x, 1);
						
			// value which determines the outward spread of the segment
			float normalMultiplier = 0.8 * pow(_LengthMultiplier, lerp(5, 1, saturate(_LengthMultiplier))) + lerp(2 * _LengthMultiplier * _FxState, 0, saturate((entrySpeed - 0.2) * 4));

			// color at the start of the trail, random between primary and secondary
			float4 col = lerp(_PrimaryColor, _SecondaryColor, vertNoise * 0.3);

			// color at the middle of the trail, mix of the start color and secondary
			float4 middleCol = lerp(col, _SecondaryColor, 0.5);

			// color at the end, secondary or tertiary, determined by the entry speed
			float4 endCol = lerp(_SecondaryColor, _TertiaryColor, clamp(entrySpeed, 0, 1.7));
			endCol = lerp(middleCol, endCol, _Hdr);

			// opacity of the trail
			float alpha = (0.004 * _TrailAlphaMultiplier + vertNoise * 0.004);

			// Changes colors and opacity for mach effects
			if (whiteRatio < 2)
			{
				float t = saturate(whiteRatio - 1);
							
				// reduces the FxState based on if the ship's going up or down
				// if it's going up then the state should be reduced
				float fxState = lerp(_FxState, _FxState * 0.05, sign(_Velocity.y));
				float interpolation = saturate(t + _FxState);

				// interpolate between white and the actual color 
				col = lerp(1, col, interpolation);
				middleCol = lerp(1, middleCol, interpolation);
				endCol = lerp(1, endCol, interpolation);
							
				// increase/decrease the opacity based on the angle of attack
				float aoa = pow(saturate(_AngleOfAttack / 20), 4);
				alpha *= saturate(aoa + t + 0.5);
			}
						
			// scale the width vectors and the outward spread vector
			side *= _ModelScale.x;
			middleSide *= _ModelScale.x;
			endSide *= _ModelScale.x;
			normalMultiplier *= _ModelScale.x;
						
			// make sure these don't go negative
			effectLength = abs(effectLength);
			middleLength = abs(middleLength);
						
			// make the effect transparent at extremely low speeds
			// (this basically scales the opacity with speed again)
			alpha *= transparentRatio;
						
			// calculate the normal vector of the segment
			float3 trailDir = (vertex[i].position - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed) - vertex[i].position;
			float3 normal = normalize(cross(sizeVector, trailDir));
						
			// apply a fresnel effect to soften the trail
			float vertFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 2);
			alpha *= saturate(vertFresnel + 0.5 + vertNoise * 0.3) * edgeMul;
						
			// Random streaks, either standard colored streaks or random ones for asteroids/comets
			int streakValue = 0;  // value deciding if the current segment is a streak or not
			float streakNoise = vertNoise2;
			float4 streakColor = lerp(float4(4, 1, 1, 1), float4(1, 4, 1, 1), streakNoise);
			if (vertNoise1 > 0.73 - _StreakProbability && entrySpeed > 0.5 + _StreakThreshold)
			{
				// change color to streak
				col = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
				middleCol = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
				endCol = lerp(_StreakColor, streakColor, _RandomnessFactor.x);
				effectLength *= 2;
				alpha *= 2;
							
				streakValue = 1;
			}
						
			// calculate the vertex positions at various points
			float3 vertex_b0 = vertex[i].position - side;
			float3 vertex_b1 = vertex[j].position + side;
						
			float3 vertex_m0 = vertex[i].position - middleSide - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
			float3 vertex_m1 = vertex[j].position + middleSide - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						
			float3 vertex_t0 = vertex[i].position - endSide - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed;
			float3 vertex_t1 = vertex[j].position + endSide - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed;
						
			// Limit length
			float3 m0_ndc = GetAirstreamNDC(lerp(vertex_b0, vertex_m0, lerp(0.5, 0.1, saturate(entrySpeed - 0.5))));
			float depth = Shadow(m0_ndc, -0.003, 1);
						
			if (depth < 0.9) return;

			// add the vertices to the trianglestrip
			triStream.Append(CreateVertex(vertex_b0, 0, vertex[i].airstreamNDC, 0, 0, col, alpha));
			triStream.Append(CreateVertex(vertex_b1, 0, vertex[j].airstreamNDC, 1, 0, col, alpha));
						
			triStream.Append(CreateVertex(vertex_m0, 0, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
			triStream.Append(CreateVertex(vertex_m1, 0, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
						
			triStream.Append(CreateVertex(vertex_t0, 0, vertex[i].airstreamNDC, 0, 1, endCol, 0));
			triStream.Append(CreateVertex(vertex_t1, 0, vertex[j].airstreamNDC, 1, 1, endCol, 0));

			// restart the trianglestrip, to allow for the next segment
			triStream.RestartStrip();
						
			// 
			// SECOND LAYER (WRAP)
			// 
			
			// COMMENTED OUT:
			// This likely breaks the effects on Linux, with Proton and AMD GPUs
			// Instead of doing this, we still add the vertices to the trianglestream, but marked as discarded vertices to not use the fragment shader.
			/*
			// if the state is low enough, don't draw the wrap layer at all
			if (_FxState < 0.6) 
			{
				return;
			}
			*/
						
			// clamp the entry speed to the max value
			entrySpeed = clamp(entrySpeed, 0, 1);
						
			// recalculate the fresnel value
			vertFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 1);
			vertFresnel += (1 - vertFresnel) * _WrapFresnelModifier;
						
			// modify the effect length, as well as the outward spread values to create the nice shape
			middleLength = clamp(middleLength, 0, maxEffectLength * 0.2);
			effectLength = clamp(effectLength * 3.4 * clamp(entrySpeed, 0, 0.6) * _FxState, 0, maxEffectLength * 1.6);
			normalMultiplier = -2.325 * saturate(pow(_LengthMultiplier, 3)) * _ModelScale.x;
			middleNormalMultiplier = 0.05;

			// change the opacity
			alpha *= 0.5 * vertFresnel * min(entrySpeed, 0.7) * _FxState;

			// change the color
			// the middle and end colors also get the streak colors
			col = _PrimaryColor;
			middleCol = lerp(_LayerColor, lerp(_LayerStreakColor, streakColor, _RandomnessFactor.y), streakValue);
			endCol = lerp(lerp(_LayerColor, _SecondaryColor, 0.5), lerp(_LayerStreakColor, streakColor, _RandomnessFactor.y), streakValue);

			// calculate the layer offset to move the entire thing away from the ship
			float3 layerOffset = vertex[i].velocityOS * -0.05 * _LengthMultiplier * _ModelScale.y;
						
			// calculate the vertex positions
			vertex_b0 = vertex[i].position - side + layerOffset;
			vertex_b1 = vertex[j].position + side + layerOffset;
						
			vertex_m0 = vertex[i].position - middleSide + layerOffset - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
			vertex_m1 = vertex[j].position + middleSide + layerOffset - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						
			vertex_t0 = vertex[i].position - endSide + layerOffset - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed;
			vertex_t1 = vertex[j].position + endSide + layerOffset - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed;
			
			// should we discard this batch?
			float discardWrap = (_FxState < 0.6) ? 2.0 : 0.0;

			// add the set of vertices to the tri strip
			// NOTE: the discard variable gets subtracted from the layer, to let the frag shader know if it should clip
			triStream.Append(CreateVertex(vertex_b0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 0, col, alpha));
			triStream.Append(CreateVertex(vertex_b1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 0, col, alpha));
						
			triStream.Append(CreateVertex(vertex_m0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
			triStream.Append(CreateVertex(vertex_m1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
						
			triStream.Append(CreateVertex(vertex_t0, 1 - discardWrap, vertex[i].airstreamNDC, 0, 1, endCol, 0));
			triStream.Append(CreateVertex(vertex_t1, 1 - discardWrap, vertex[j].airstreamNDC, 1, 1, endCol, 0));
						
			// restart the strip again
			triStream.RestartStrip();
		}
	}

	// DEBUG	
	//triStream.RestartStrip();
	// DEBUG
}

half4 gs_frag(GS_DATA IN) : SV_Target
{
	float4 c = IN.color;
	
	// first of all, clip the pixel if it's marked as discarded (layer < 0)
	clip(IN.layer);
	
	float entrySpeed = GetEntrySpeed() / 4000 - 0.08 * _FxState;
	float speedScalar = saturate(lerp(0, 2.5, entrySpeed));
				
	// fragment angle
	float2 circleCoord = GetAirstreamNDC(normalize(IN.positionOS));
	float angle = atan2(circleCoord.y, circleCoord.x);
				
	// position along the trail in various configurations, used to scale the noise
	float2 trailPos = 1 - IN.trailPos;
	float trailPosScalar0 = pow(trailPos.y, 2);
	float trailPosScalar1 = 0.2;
	float trailPosScalar = lerp(trailPosScalar0, trailPosScalar1, IN.layer);
	float invTrailPosScalar = 1 - trailPosScalar;
				
	// calculate the scroll and uv
	float2 scrollScale = float2(lerp(0.6, 0.1, IN.layer), lerp(-8, -0.2, IN.layer));
	float2 timeOffset = float2(_Time.y * scrollScale.x, _Time.y * scrollScale.y * (entrySpeed + 0.5));
	float2 scale0 = float2(0.1, 2);
	float2 scale1 = float2(lerp(1, 0.2, trailPosScalar0 + 0.5), lerp(1, 0.1, trailPosScalar0 + 0.5));
	float2 uv = lerp(scale0, scale1, IN.layer) * trailPos + float2(angle, 0) - timeOffset;
				
	// sample the noise, scale it with the entry speed and the position along the trail
	float noise = NoiseStatic(uv, lerp(3, 2, IN.layer)) * speedScalar * trailPosScalar;
	float noiseSign = lerp(1, -1, IN.layer);
				
	// clamp the noise so it doesn't go below 0
	noise = max(0, noise);
				
	// define the opacity for both effect layers
	float alpha0 = saturate(c.a + noise * 0.05);
	float alpha1 = saturate(c.a - (noise * c.a * 7));
				
	// opacity multiplier for both layers
	float scalar0 = (0.1 + invTrailPosScalar * 0.05);
	float scalar1 = 1 - trailPosScalar0;
				
	// set the fragment alpha to the new opacity with noise
	c.a = lerp(alpha0, alpha1, IN.layer) * lerp(scalar0, scalar1, IN.layer) * _OpacityMultiplier;
				
	// doing alpha blending before Unity can clamp the colors to LDR (if not in HDR mode)
	float c_a = saturate(c.a);
	c.rgb *= lerp(c_a * 1.3, 1.0, _Hdr);
	c.a = lerp(1, c_a, _Hdr);
				
	// dithering to reduce banding in SDR
	float DitheringGrain = 0.5 / 255.0;
	float dither = _DitherTex.SampleLevel(sampler_DitherTex, IN.screenPos.xy / _ScreenParams.xy * 500 + _Time.x, 0).r * trailPos.y;
	float3 cd = lerp(c.rgb, dither, DitheringGrain);
	c.rgb = lerp(cd + (-DitheringGrain / 4), c.rgb, _Hdr);

	return c;
}