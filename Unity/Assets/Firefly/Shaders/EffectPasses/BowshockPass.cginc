float4 _ShockwaveColor;
float _LengthMultiplier;

int _DisableBowshock;

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
	float3 airstreamNDC : TEXCOORD4;
	float3 velocityOS : TEXCOORD5;
	float3 normalOS : TEXCOORD6;
	float3 viewDir : TEXCOORD7;
}; 

struct GS_DATA
{
	float4 position : SV_POSITION;
	half4 color : COLOR;

	float3 positionWS : TEXCOORD1;
};
			
// Creates a vertex instance that can be added to a triangle stream
GS_DATA CreateVertex(float3 pos, half4 color, half a) 
{
	GS_DATA o;

	o.position = UnityObjectToClipPos(pos);
	o.color = color;
	o.color.a = a;

	o.positionWS = TransformObjectToWorld(pos);

	return o;
}

GS_INPUT gs_vert(VS_INPUT IN)
{
	GS_INPUT OUT;

	// scaled position
	OUT.position = IN.position * float4(_EnvelopeScaleFactor, 1.0);
	OUT.positionWS = TransformObjectToWorld(OUT.position.xyz);

	// world and object space normal
	OUT.normal = normalize(UnityObjectToWorldNormal(IN.normal));
	OUT.normalOS = normalize(IN.normal);

	OUT.uv = IN.uv;

	// airstream position
	float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
	OUT.airstreamNDC = airstreamPosition.xyz / airstreamPosition.w;

	// object space velocity
	OUT.velocityOS = UnityWorldToObjectDir(normalize(_Velocity));
				
	OUT.viewDir = WorldSpaceViewDir(IN.position);

	return OUT;
}

[maxvertexcount(9)]
void gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
{
	if (_EntryStrength < 1000 || _DisableBowshock > 0) return;
				
	// Initialize iterator variable
	int i = 0;
				
	// Scale the entry speed
	float clampedEntrySpeed = min(_EntryStrength, 2300);
	float entrySpeed = min(_EntryStrength / 4000, 0.57);
	float scaledEntrySpeed = lerp(0, entrySpeed + 0.55, saturate((entrySpeed - 0.32) * 2));

	// Get the occlusion for each vertex
	float3 occlusion = float3(
		Shadow(vertex[0].airstreamNDC, -0.003, 1),
		Shadow(vertex[1].airstreamNDC, -0.003, 1),
		Shadow(vertex[2].airstreamNDC, -0.003, 1)
	);
				
	// Calculate the base effect length
	float baseLength = clampedEntrySpeed * 0.0005;
				
	// Sample noise
	float3 noise = 0;
	for (i = 0; i < 3; i++) noise[i] = Noise(vertex[i].position.xy + vertex[i].uv, 1) * baseLength * Noise(vertex[i].position.xy + vertex[i].uv, 2) * 10;
				
	// Calculate the outward effect length
	float3 effectLength = (baseLength + noise * 0.3) * scaledEntrySpeed * 1.5;
	float3 middleLength = effectLength * 0.54;
				
	// Calculate the forward effect length
	float3 effectSideLength = (3 + noise) * scaledEntrySpeed * 0.9;
	float3 middleSideLength = effectSideLength * 0.45;
				
	// Offset the bowshock away from the ship
	float3 offset = (vertex[0].velocityOS * 0.4 * entrySpeed) * _ModelScale.y;
	float3 trailOffset = offset * 1.1;
				
	// Fresnel effect
	float3 vertFresnel = 0;
	for (i = 0; i < 3; i++) vertFresnel[i] = Fresnel(vertex[i].normal, vertex[i].viewDir, 1) * Fresnel(-vertex[i].normal, vertex[i].viewDir, 1) - (Fresnel(vertex[i].normal, vertex[i].viewDir, 3) - 0.3) - (1 - Fresnel(vertex[i].normal, vertex[i].viewDir, 1) - 0.8);
				
	// Dot product of normal and velocity
	float3 velDotInv = 0;
	for (i = 0; i < 3; i++) velDotInv[i] = dot(vertex[i].normal, _Velocity);
				
	// Inverted dot product of normal and velocity	
	float3 velDot = -velDotInv;
				
	// Create the "bowl"
	if (occlusion[0] > 0.9 && velDotInv[0] > 0.2)
	{
		for (i = 0; i < 3; i++)
		{
			// bowl fresnel effect
			float fresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 2);
			float fresnelInv = Fresnel(-vertex[i].normal, vertex[i].viewDir, 2);
						
			// Fresnel value to soften the edges
			float softFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 2);
						
			// bowl opacity
			float alpha = 0.85 * fresnel * scaledEntrySpeed * (1 - softFresnel);
					
			// add vertex
			triStream.Append(CreateVertex(vertex[i].position + offset, _ShockwaveColor * velDotInv[i] + fresnel * 0.6, alpha));
		}
					
		triStream.RestartStrip();
	}
				
	// Scale
	effectLength *= _ModelScale.y;
	middleLength *= _ModelScale.y;
	middleSideLength *= _ModelScale.y;
	effectSideLength *= _ModelScale.y;
				
	// make sure these values dont go negative
	effectLength = abs(effectLength);
	middleLength = abs(middleLength);
	middleSideLength = abs(middleSideLength);
	effectSideLength = abs(effectSideLength);

	// Iterate through every vertex
	for (uint i = 0; i < 2; i++)
	{
		if (occlusion[i] > 0.9 && velDot[i] > -0.4 && velDot[i] < 0 && pow(vertFresnel[i], 2) > 0.2)
		{
			uint j = (i + 1) % 3;  // next vertex
			uint k = (j + 1) % 3;  // another vertex
						
			// Get the average edge length
			float edgeLength_j = length(vertex[i].position - vertex[j].position);
			float edgeLength_k = length(vertex[i].position - vertex[k].position);
			float edgeLength = ((edgeLength_j + edgeLength_k) / 2) * (1 / _ModelScale.y);
			float edgeMul = clamp(edgeLength / 0.1, 0.1, 1);
						
			// Create the offsets which move the trail a bit inside of the bowl
			float3 offset_i = float3(vertex[i].position.x, 0, vertex[i].position.z) * 0.0;
			float3 offset_j = float3(vertex[j].position.x, 0, vertex[j].position.z) * 0.0;
						
			// Sample noise
			float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);
						
			// Create the vector which will be used to widen the trail segments
			float3 sizeVector = -normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));
						
			// Width
			float3 side = sizeVector * 0.2;  // vector pointing to the side, which allows for thicker trails
			float3 middleSide = side * 2;
			float3 endSide = side * 2;
						
			// Scale
			side *= _ModelScale.x;
			middleSide *= _ModelScale.x;
			endSide *= _ModelScale.x;
						
			// Opacity
			//float alpha = (0.05 + vertNoise * 0.009) * saturate(pow(vertFresnel[i], 1)) * 0.75 * scaledEntrySpeed;
			float alpha = 0.02 * 0.5 * scaledEntrySpeed * saturate(pow(vertFresnel[i], 3));
			float middleAlpha = alpha * 0.6;
			alpha *= edgeMul;
			middleAlpha *= edgeMul;
						
			// Define the vertex positions
			float3 vertex_b0 = vertex[i].position - offset_i + trailOffset - side;
			float3 vertex_b1 = vertex[j].position - offset_j + trailOffset + side;
						
			float3 vertex_m0 = vertex[i].position - offset_i + trailOffset - middleSide + middleLength[i] * vertex[i].normalOS - vertex[i].velocityOS * middleSideLength[i];
			float3 vertex_m1 = vertex[j].position - offset_j + trailOffset + middleSide + middleLength[j] * vertex[j].normalOS - vertex[j].velocityOS * middleSideLength[j];
						
			float3 vertex_t0 = vertex[i].position - offset_i + trailOffset - endSide + effectLength[i] * vertex[i].normalOS - vertex[i].velocityOS * effectSideLength[i];
			float3 vertex_t1 = vertex[j].position - offset_j + trailOffset + endSide + effectLength[j] * vertex[j].normalOS - vertex[j].velocityOS * effectSideLength[j];
						
			// add the vertices to the tri strip
			triStream.Append(CreateVertex(vertex_b0, _ShockwaveColor, alpha));
			triStream.Append(CreateVertex(vertex_b1, _ShockwaveColor, alpha));
						
			triStream.Append(CreateVertex(vertex_m0, _ShockwaveColor, middleAlpha));
			triStream.Append(CreateVertex(vertex_m1, _ShockwaveColor, middleAlpha));
						
			triStream.Append(CreateVertex(vertex_t0, _ShockwaveColor, 0));
			triStream.Append(CreateVertex(vertex_t1, _ShockwaveColor, 0));
						
			// Restart the strip
			triStream.RestartStrip();
		}
	}
}

half4 gs_frag(GS_DATA IN) : SV_Target
{
	float4 c = IN.color;
				
	// Doing alpha blending before Unity can clamp the colors to SDR (if not in HDR mode)
	c.rgb *= c.a;
	c.a = 1;
				
	return c;
}