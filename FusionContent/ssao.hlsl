﻿
#if 0
$ubershader 	HEMISPHERE S_4|S_8|S_16|S_32|S_64
$ubershader 	HBAO S_4|S_8|S_16|S_32|S_64
$ubershader 	BLANK
#endif

#ifdef S_4
#define NUM_SAMPLES 4
#endif

#ifdef S_8
#define NUM_SAMPLES 8
#endif

#ifdef S_16
#define NUM_SAMPLES 16
#endif

#ifdef S_32
#define NUM_SAMPLES 32
#endif

#ifdef S_64
#define NUM_SAMPLES 64
#endif

struct PARAMS {
	float4x4	ProjMatrix;
	float4x4	View;
	float4x4	ViewProjection;
	float4x4	InvViewProjection;
	float4x4	InvProj;
	float 		TraceStep;
	float 		DecayRate;
	float		MaxSampleRadius;
	float		MaxDepthJump;
};

Texture2D		DepthTexture 		: register(t0);
Texture2D		NormalsTexture 		: register(t1);
Texture2D		RandomTexture 		: register(t2);
SamplerState	LinearSampler		: register(s0);
	
cbuffer PARAMS 		: register(b0) { 
	PARAMS Params 	: packoffset(c0); 
};


float4 FSQuad( uint VertexID ) 
{
	return float4((VertexID == 0) ? 3.0f : -1.0f, (VertexID == 2) ? 3.0f : -1.0f, 1.0f, 1.0f);
}

float2 FSQuadUV ( uint VertexID )
{
	return float2((VertexID == 0) ? 2.0f : -0.0f, 1-((VertexID == 2) ? 2.0f : -0.0f));
}

/*-----------------------------------------------------------------------------
	SSAO
-----------------------------------------------------------------------------*/


float4 VSMain(uint VertexID : SV_VertexID, out float2 uv : TEXCOORD0 ) : SV_POSITION
{
	uv = FSQuadUV( VertexID );
	return FSQuad( VertexID );

}

// linearize depth taken from depth buffer
float LinearZ( float depth, float4x4 proj )
{
	return abs(proj._43 / ( proj._33 + depth ));
}


#ifdef HEMISPHERE

float4 PSMain(float4 position : SV_POSITION, float2 uv : TEXCOORD0 ) : SV_Target
{
	uint width;
	uint height;
	DepthTexture.GetDimensions( width, height );
	uint xpos = position.x;
	uint ypos = position.y;


	float pixelDepth = DepthTexture.Load( int3(xpos,ypos,0) ).r;
	float4 pixelProjPos = float4
	(
		((position.x/(float)width)*2 - 1),
		((position.y/(float)height)*(-2) + 1),
		pixelDepth,
		1
	);

  // choose sampling radius depending on distance from camera:
  float samplingRadius = LinearZ( pixelDepth, Params.ProjMatrix ) * Params.MaxSampleRadius * 0.1f;
  samplingRadius = samplingRadius > Params.MaxSampleRadius ? Params.MaxSampleRadius : samplingRadius;


	float4 pixelWorldPos = mul( pixelProjPos, Params.InvViewProjection );
		pixelWorldPos /= pixelWorldPos.w;

	float3	normalWorld = normalize(NormalsTexture.Load( int3(xpos,ypos,0) )*2 - 1 ).xyz;
	float3	randDirWorld = normalize(RandomTexture.Load( int3(xpos % 64, ypos % 64, 0) )*2 - 1 ).xyz;

	float	occlusion	=	0;
	for ( uint i = 0; i < NUM_SAMPLES; ++i )
	{
		float3 sampleDirWorld	=	( RandomTexture.Load( int3((i*117)%64, (i*113)%64, 0) )*2 - 1 ).xyz;

		sampleDirWorld =	reflect( sampleDirWorld, randDirWorld );
		sampleDirWorld	=	faceforward( sampleDirWorld, sampleDirWorld, -normalWorld );

		float4 sampleWorldPos = pixelWorldPos + float4( sampleDirWorld*samplingRadius, 0 );
		float4 sampleProjPos = mul( sampleWorldPos, Params.ViewProjection );
		sampleProjPos	=	sampleProjPos / sampleProjPos.w;
		sampleProjPos.xy=	sampleProjPos.xy * float2(0.5,-0.5) + float2(0.5f,0.5f);

		float sampleGeometryDepth = LinearZ( DepthTexture.Sample( LinearSampler, sampleProjPos.xy ).r, Params.ProjMatrix );
		float sampleDepth = LinearZ( sampleProjPos.z, Params.ProjMatrix );

//		float sampleGeometryDepth = DepthTexture.Sample( LinearSampler, sampleProjPos.xy ).r;
//		float sampleDepth = sampleProjPos.z;

		float deltaDepth = sampleGeometryDepth - sampleDepth;
		occlusion += deltaDepth > 0 ? 1 : 0;
		occlusion += deltaDepth < -Params.MaxDepthJump ? 1 : 0;
	}
	occlusion /= NUM_SAMPLES;
	return occlusion;
//	return mul ( float4(normalWorld, 0), Params.View );
}


#endif // HEMISPHERE


#ifdef HBAO


#define NUM_STEPS 6				// number of steps along a direction
#define TOTAL_SAMPLES ( NUM_STEPS*NUM_SAMPLES )


float cosAng( float2 a, float2 b )
{
	a = normalize(a);
	b = normalize(b);
	return dot( a, b );
}


float tanToSin( float tan )
{
	return (tan / sqrt( 1 + tan*tan ));
}



float2 ProjToTex( float2 projCoord )
{
	float2 texCoord = float2( (projCoord.x + 1.0f)*0.5f, (1.0f - projCoord.y)*0.5f );
	return texCoord;
}

float2 TexToProj( float2 texCoord )
{
	float2 projCoord = float2( 2.0f*texCoord.x - 1.0f, -2.0f*texCoord.y + 1.0f );
	return projCoord;
}



float4 texToWorld( float2 texCoord )
{
	float2 proj2 = TexToProj( texCoord );
	float depthProj = DepthTexture.Sample( LinearSampler, texCoord ).r;
	float4 proj4 = float4( proj2, depthProj, 1 );
	float4 world4 = mul( proj4, Params.InvViewProjection );
	world4 /= world4.w;
	return world4;
}



float project( float2 what, float2 toWhat )
{
	return dot( what, toWhat ) / length(toWhat);
}



float3 getTangentVector( float2 sampleDirView, float3 normalView )
{
	//// incorrect:
	//float sinT = project( sampleDirView, normalView.xy );
	//float absSinT = abs(sinT);
	//float sign = absSinT < 0.0000001f ? 1.0f : sinT / absSinT;
	//float sinTSq = sinT * sinT;
	//float ellRad = sqrt( 1 + (normalView.z*normalView.z - 1)*sinTSq );
	//return float3( sampleDirView * ellRad, -sign * sqrt( 1 - ellRad*ellRad ) );

	// correct:
	float SDotNXY = dot( sampleDirView, normalView.xy );
	float normFactor = 1.0f / sqrt( normalView.z*normalView.z + SDotNXY*SDotNXY );
	return float3(	sampleDirView.x * normalView.z * normFactor,
					sampleDirView.y * normalView.z * normFactor,
					-SDotNXY * normFactor );
}



// -World suffix	: in world coordinates
// -View suffix		: in view coordinates (world coords rotated by View matrix)
// -Proj suffix		: in proj coordinates (from -1 to 1) (view coordinates transformad by Proj matrix, then divided by .w) 
// -Tex suffix		: in texture coordinates (from 0 to 1)

float4 PSMain(float4 position : SV_POSITION, float2 uv : TEXCOORD0 ) : SV_Target
{
	uint width;
	uint height;
	DepthTexture.GetDimensions( width, height );
	uint xpos = position.x;
	uint ypos = position.y;
	

	/// finding current normal in view coordinates: //////////////////////////////////////////////////
	float3 normalWorld = normalize( NormalsTexture.Load( int3( xpos, ypos, 0 ) ).xyz * 2 - 1 );
	float3 normalView = normalize( mul ( float4( normalWorld, 0 ), Params.View ).xyz );
	//////////////////////////////////////////////////////////////////////////////////////////////////


	/// finding texture coordinates of the current pixel: ////////////////////////////////////////////
	float2 currentPixelPosTex = float2( xpos/(float)width, ypos/(float)height );
	//////////////////////////////////////////////////////////////////////////////////////////////////


	/// get curent pixel depth in projection coordinates from the depth dexture: /////////////////////
	float currentPixelDepthProj = DepthTexture.Sample( LinearSampler, currentPixelPosTex ).r;
	//////////////////////////////////////////////////////////////////////////////////////////////////


	/// finding current pixel view coordinates: //////////////////////////////////////////////////////
	float4 currentPixelPosProj = float4( TexToProj(currentPixelPosTex), currentPixelDepthProj, 1 );
	float4 currentPixelPosView = mul( currentPixelPosProj, Params.InvProj );
	currentPixelPosView /= currentPixelPosView.w;
	//////////////////////////////////////////////////////////////////////////////////////////////////


	/// get distance from camera to current pixel: ///////////////////////////////////////////////////
	float currentPixelDepthView = - currentPixelPosView.z;
	//////////////////////////////////////////////////////////////////////////////////////////////////


	/// choose sampling radius depending on distance from camera: ////////////////////////////////////
	float samplingRadius = LinearZ( currentPixelDepthProj, Params.ProjMatrix ) * Params.MaxSampleRadius * 0.1f;
	samplingRadius = samplingRadius > Params.MaxSampleRadius ? Params.MaxSampleRadius : samplingRadius;
	float samplingRadiusSq = samplingRadius * samplingRadius;
	//////////////////////////////////////////////////////////////////////////////////////////////////


	float occlusion = 0;

	for ( uint i = 0; i < NUM_SAMPLES; ++i )
	{
		float angle = i * 2 * 3.14159265 / (float)NUM_SAMPLES;
		float3 sampleStepView	= float3( sin(angle), cos(angle), 0 );
//		float3 sampleStepView = float3(normalize( ( RandomTexture.Load( int3(i%64, 3*i%64, 0) )*2 - 1 ).xy ), 0);


		// rotate randomly: ///////////////////////////////////////////////////////////////////////////////
		float randomAngle = RandomTexture.Load( int3(13*ypos%64, 17*xpos%64, 0) ).z*0;
		float tempX = cos(randomAngle)*sampleStepView.x + sin(randomAngle)*sampleStepView.y;
		sampleStepView.y = -sin(randomAngle)*sampleStepView.x + cos(randomAngle)*sampleStepView.y;
		sampleStepView.x = tempX;
		///////////////////////////////////////////////////////////////////////////////////////////////////


		/// restore tangent vector from normal vector and sample direction: ///////////////////////////////
		float3 tangentVectorView = getTangentVector( sampleStepView.xy, normalView );
		///////////////////////////////////////////////////////////////////////////////////////////////////


		/// scale sampleStepView according to sampling radius: ////////////////////////////////////////////
		sampleStepView = tangentVectorView * samplingRadius;
		///////////////////////////////////////////////////////////////////////////////////////////////////

		float4 lastSamplePositionView = currentPixelPosView + float4( sampleStepView, 0 );

		float4 lastSamplePositionProj = mul(lastSamplePositionView, Params.ProjMatrix);
		lastSamplePositionProj /= lastSamplePositionProj.w;

		float2 lastSamplePositionTex = ProjToTex( lastSamplePositionProj.xy );
		float2 sampleStepTex = lastSamplePositionTex - currentPixelPosTex;


		sampleStepView /= (float)NUM_STEPS;
		sampleStepTex /= (float)NUM_STEPS;


		float tanFlatHoriz = tangentVectorView.z / length(tangentVectorView.xy);
		float sinFlatHoriz = tanToSin( tanFlatHoriz );

		float tanTrueHoriz = tanFlatHoriz;
		float sinTrueHoriz = sinFlatHoriz;


		for (float step = 1.0f; step <= NUM_STEPS; step += 1.0f)
		{
			float4 samplePositionView = currentPixelPosView + float4(sampleStepView*step, 0);
			//float4 samplePositionProj = mul(samplePositionView, Params.ProjMatrix);
			//samplePositionProj /= samplePositionProj.w;

	//		float2 samplePositionTex = float2((samplePositionProj.x + 1.0f) * 0.5f,
	//												(1.0f - samplePositionProj.y) * 0.5f);

			float2 samplePositionTex = currentPixelPosTex + sampleStepTex*step;

			float sampleDepthProj = DepthTexture.Sample( LinearSampler, samplePositionTex ).r;

	//		float4 sampleTruePositionProj = samplePositionProj;
	//		sampleTruePositionProj.z = sampleDepthProj;


	//		float4 sampleTruePositionView = mul(sampleTruePositionProj, Params.InvProj);
	//		sampleTruePositionView /= sampleTruePositionView.w;

			float4 sampleTruePositionView = samplePositionView;
			sampleTruePositionView.z = LinearZ( sampleDepthProj, Params.ProjMatrix );


			float4 horizVectorView = sampleTruePositionView - currentPixelPosView;
			tanTrueHoriz = horizVectorView.z / length(horizVectorView.xy);
			float horizVectorSq = dot( horizVectorView, horizVectorView );

			if ( tanTrueHoriz > tanFlatHoriz && samplingRadiusSq >= horizVectorSq )
			{
				sinTrueHoriz = tanToSin( tanTrueHoriz );
				float weight = 1.0f - horizVectorSq / samplingRadiusSq;

			//	occlusion += 1.0f;
				occlusion += ( sinTrueHoriz - sinFlatHoriz ) * weight * 1.5f;
				tanFlatHoriz = tanTrueHoriz;
				sinFlatHoriz = sinTrueHoriz;
			}
	//		normalizationTerm += 1.0f;
		}

	}
	float reverseOcclusion = 1.0f - (occlusion / (float)TOTAL_SAMPLES);
	return reverseOcclusion * reverseOcclusion;
}


#endif // HBAO


#ifdef BLANK

float4 PSMain(float4 position : SV_POSITION, float2 uv : TEXCOORD0 ) : SV_Target
{
	return float4(1,1,1,1);
}
#endif // BLANK
