////////////////////////////////////////////////////////////////////////////////
//           P R E C I P I T A T I O N   O B J E C T   S H A D E R            //
////////////////////////////////////////////////////////////////////////////////

////////////////////    G L O B A L   V A L U E S    ///////////////////////////

float4x4 worldViewProjection;  // model -> world -> view -> projection
float4x4 invView;              // inverse view

float3 LightVector; // Direction vector to sun, used for day-night darkening

float particleSize;

float2 cameraTileXZ;
float currentTime;

static float2 texCoords[4] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 1) };
static float2 offsets[4] = { float2(-0.5f, 0.5f), float2(0.5f, 0.5f), float2(0.5f, -0.5f), float2(-0.5f, -0.5f) };

texture precipitation_Tex;

sampler PrecipitationSamp = sampler_state
{
	texture = (precipitation_Tex);
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
	MipLodBias = 0;
	AddressU = Wrap;
	AddressV = Wrap;
};

////////////////////    V E R T E X   I N P U T S    ///////////////////////////

struct VERTEX_INPUT
{
	float4 StartPosition_StartTime : POSITION0;
	float4 EndPosition_EndTime : POSITION1;
	float4 TileXZ_Vertex : POSITION2;
};

////////////////////    V E R T E X   O U T P U T S    /////////////////////////

struct VERTEX_OUTPUT
{
	float4 Position	: POSITION;
	float2 TexCoord : TEXCOORD0;
};

////////////////////    V E R T E X   S H A D E R S    /////////////////////////

// Precipitation vertex shader - calculates raindrop/snowflake positions.
VERTEX_OUTPUT VSPrecipitation(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;
	
	float age = (currentTime - In.StartPosition_StartTime.w) / (In.EndPosition_EndTime.w - In.StartPosition_StartTime.w);
	int vertIdx = (int)In.TileXZ_Vertex.z;
	float3 right = invView[0].xyz;
	float3 up = normalize(In.StartPosition_StartTime.xyz - In.EndPosition_EndTime.xyz);
	
	In.StartPosition_StartTime.xyz = lerp(In.StartPosition_StartTime.xyz, In.EndPosition_EndTime.xyz, age);
	In.StartPosition_StartTime.xz += (cameraTileXZ - In.TileXZ_Vertex.xy) * float2(-2048, 2048);
	In.StartPosition_StartTime.xyz += right * offsets[vertIdx].x * particleSize;
	In.StartPosition_StartTime.xyz += up * offsets[vertIdx].y * particleSize;
	
	Out.Position = mul(float4(In.StartPosition_StartTime.xyz, 1), worldViewProjection);
	Out.TexCoord = texCoords[vertIdx];
	
	return Out;
}

////////////////////    P I X E L   S H A D E R S    ///////////////////////////

// This function dims the lighting at night, with a transition period as the sun rises/sets.
void _PSApplyDay2Night(inout float4 Color)
{
	// The following constants define the beginning and the end conditions of the day-night transition
	const float startNightTrans = 0.1; // The "NightTrans" values refer to the Y postion of LightVector
	const float finishNightTrans = -0.1;
	const float minDarknessCoeff = 0.15;

	// Internal variables
	// The following two are used to interpoate between day and night lighting (y = mx + b)
	// Can't use lerp() here, as overall dimming action is too complex
	float slope = (1.0 - minDarknessCoeff) / (startNightTrans - finishNightTrans); // "m"
	float incpt = 1.0 - slope * startNightTrans; // "b"
	// This is the return value used to darken scenery
	float adjustment;

    if (LightVector.y < finishNightTrans)
      adjustment = minDarknessCoeff;
    else if (LightVector.y > startNightTrans)
      adjustment = 0.9; // Scenery is fully lit during the day
    else
      adjustment = slope * LightVector.y + incpt;

	Color.rgb *= adjustment;
}

float4 PSPrecipitation(in VERTEX_OUTPUT In) : COLOR0
{
	float4 color = tex2D(PrecipitationSamp, In.TexCoord);
	_PSApplyDay2Night(color);
	return color;
}

////////////////////    T E C H N I Q U E S    /////////////////////////////////

technique Precipitation
{
	pass Pass_0
	{
		VertexShader = compile vs_5_0 VSPrecipitation();
		PixelShader = compile ps_5_0 PSPrecipitation();
	}
}
