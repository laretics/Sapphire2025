////////////////////////////////////////////////////////////////////////////////
//                        L O A D I N G   S H A D E R                         //
////////////////////////////////////////////////////////////////////////////////

////////////////////    G L O B A L   V A L U E S    ///////////////////////////

float4x4 WorldViewProjection;  // model -> world -> view -> projection

float LoadingPercent;

texture LoadingTexture;

sampler LoadingSampler = sampler_state
{
	Texture = (LoadingTexture);
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

////////////////////    V E R T E X   I N P U T S    ///////////////////////////

struct VERTEX_INPUT
{
	float4 Position  : POSITION;
	float2 TexCoords : TEXCOORD0; // tex coords x, y
};

////////////////////    V E R T E X   O U T P U T S    /////////////////////////

struct VERTEX_OUTPUT
{
	float4 Position  : POSITION;  // position x, y, z, w
	float2 TexCoords : TEXCOORD0; // tex coords x, y
};

////////////////////    V E R T E X   S H A D E R S    /////////////////////////

VERTEX_OUTPUT VSLoading(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;
	
	Out.Position = mul(In.Position, WorldViewProjection);
	Out.TexCoords = In.TexCoords;
	
	return Out;
}

VERTEX_OUTPUT VSLoadingBar(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;
	
	Out.Position = mul(In.Position, WorldViewProjection);
	Out.TexCoords = In.TexCoords;
	
	return Out;
}

////////////////////    P I X E L   S H A D E R S    ///////////////////////////

float4 PSLoading(in VERTEX_OUTPUT In) : COLOR0
{
	return tex2D(LoadingSampler, In.TexCoords);
}

float4 PSLoadingBar(in VERTEX_OUTPUT In) : COLOR0
{
	const float4 ColorBack = float4(0.5, 0.5, 0.5, 1);
	const float4 ColorFore = float4(1.0, 1.0, 1.0, 1);
	if (LoadingPercent < 0 || LoadingPercent > 1) {
		float c = sin(frac(In.TexCoords.x + LoadingPercent) * 3.14159);
		return lerp(ColorFore, ColorBack, c);
	} else if (LoadingPercent - In.TexCoords.x > 0)
		return ColorFore;
	else
		return ColorBack;
}

////////////////////    T E C H N I Q U E S    /////////////////////////////////

////////////////////////////////////////////////////////////////////////////////
// IMPORTANT: ATI graphics cards/drivers do NOT like mixing shader model      //
//            versions within a technique/pass. Always use the same vertex    //
//            and pixel shader versions within each technique/pass.           //
////////////////////////////////////////////////////////////////////////////////

technique Loading {
	pass Pass_0 {
		VertexShader = compile vs_5_0 VSLoading();
		PixelShader = compile ps_5_0 PSLoading();
	}
}

technique LoadingBar {
	pass Pass_0 {
		VertexShader = compile vs_5_0 VSLoadingBar();
		PixelShader = compile ps_5_0 PSLoadingBar();
	}
}
