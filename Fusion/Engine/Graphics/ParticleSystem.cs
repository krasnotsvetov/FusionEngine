﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Core.Mathematics;
using Fusion.Core;
using Fusion.Engine.Common;
using Fusion.Drivers.Graphics;
using System.Runtime.InteropServices;



namespace Fusion.Engine.Graphics {

	/// <summary>
	/// Represents particle rendering and simulation system.
	/// 1. http://www.gdcvault.com/play/1014347/HALO-REACH-Effects
	/// 2. Gareth Thomas Compute-based GPU Particle
	/// </summary>
	public class ParticleSystem : DisposableBase {

		readonly Game Game;
		readonly RenderSystem rs;
		Ubershader		shader;
		StateFactory	factory;
		RenderWorld	viewLayer;

		const int BlockSize				=	256;
		const int MaxInjectingParticles	=	1024;
		const int MaxSimulatedParticles =	384 * 1024;
		const int MaxImages				=	512;

		bool toMuchInjectedParticles = false;

		int					injectionCount = 0;
		Particle[]			injectionBufferCPU = new Particle[MaxInjectingParticles];
		StructuredBuffer	injectionBuffer;
		StructuredBuffer	simulationBuffer;
		StructuredBuffer	deadParticlesIndices;
		ConstantBuffer		paramsCB;
		ConstantBuffer		imagesCB;

		enum Flags {
			INJECTION	=	0x1,
			SIMULATION	=	0x2,
			DRAW		=	0x4,
			INITIALIZE	=	0x8,
		}


		//	row_major float4x4 View;       // Offset:    0
		//	row_major float4x4 Projection; // Offset:   64
		//	int MaxParticles;              // Offset:  128
		//	float DeltaTime;               // Offset:  132
		[StructLayout(LayoutKind.Explicit, Size=256)]
		struct Params {
			[FieldOffset(  0)] public Matrix	View;
			[FieldOffset( 64)] public Matrix	Projection;
			[FieldOffset(128)] public Vector4	CameraForward;
			[FieldOffset(144)] public Vector4	CameraRight;
			[FieldOffset(160)] public Vector4	CameraUp;
			[FieldOffset(176)] public Vector4	Gravity;
			[FieldOffset(192)] public int		MaxParticles;
			[FieldOffset(196)] public float		DeltaTime;
			[FieldOffset(200)] public uint		DeadListSize;

		} 

		Random rand = new Random();


		/// <summary>
		/// Gets and sets overall particle gravity.
		/// Default -9.8.
		/// </summary>
		public Vector3	Gravity { get; set; }
		


		/// <summary>
		/// Sets and gets images for particles.
		/// This property must be set before particle injection.
		/// </summary>
		public TextureAtlas Images { 
			get {
				return images;
			}
			set {
				if (value.Count>MaxImages) {
					throw new ArgumentOutOfRangeException("Number of subimages in texture atlas is greater than " + MaxImages.ToString() );
				}
				images = value;
			}
		}

		TextureAtlas images = null;


		/// <summary>
		/// 
		/// </summary>
		/// <param name="rs"></param>
		internal ParticleSystem ( RenderSystem rs, RenderWorld viewLayer )
		{
			this.rs			=	rs;
			this.Game		=	rs.Game;
			this.viewLayer	=	viewLayer;

			Gravity	=	Vector3.Down * 9.80665f;

			paramsCB		=	new ConstantBuffer( Game.GraphicsDevice, typeof(Params) );
			imagesCB		=	new ConstantBuffer( Game.GraphicsDevice, typeof(Vector4), MaxImages );

			injectionBuffer			=	new StructuredBuffer( Game.GraphicsDevice, typeof(Particle),	MaxInjectingParticles, StructuredBufferFlags.None );
			simulationBuffer		=	new StructuredBuffer( Game.GraphicsDevice, typeof(Particle),	MaxSimulatedParticles, StructuredBufferFlags.None );
			deadParticlesIndices	=	new StructuredBuffer( Game.GraphicsDevice, typeof(uint),		MaxSimulatedParticles, StructuredBufferFlags.Append );

			rs.Game.Reloading += LoadContent;
			LoadContent(this, EventArgs.Empty);

			//	initialize dead list :
			var device = Game.GraphicsDevice;

			device.SetCSRWBuffer( 1, deadParticlesIndices, 0 );
			device.PipelineState	=	factory[ (int)Flags.INITIALIZE ];
			device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );
		}



		/// <summary>
		/// Loads content
		/// </summary>
		void LoadContent ( object sender, EventArgs args )
		{
			shader	=	rs.Game.Content.Load<Ubershader>("particles.hlsl");
			factory	=	shader.CreateFactory( typeof(Flags), (ps,i) => EnumAction( ps, (Flags)i ) );
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {	

				paramsCB.Dispose();

				injectionBuffer.Dispose();
				simulationBuffer.Dispose();
				deadParticlesIndices.Dispose();
			}
			base.Dispose( disposing );
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="ps"></param>
		/// <param name="flag"></param>
		void EnumAction ( PipelineState ps, Flags flag )
		{
			ps.BlendState			=	BlendState.AlphaBlend;
			ps.DepthStencilState	=	DepthStencilState.Readonly;
			ps.Primitive			=	Primitive.PointList;
		}



		/// <summary>
		/// 
		/// </summary>
		public TextureAtlas TextureAtlas {
			get; set;
		}


		/// <summary>
		/// Injects hard particle.
		/// </summary>
		/// <param name="particle"></param>
		public void InjectParticle ( Particle particle )
		{
			if (Images==null) {
				throw new InvalidOperationException("Images must be set");
			}

			if (injectionCount>=MaxInjectingParticles) {
				toMuchInjectedParticles = true;
				return;
			}

			if (particle.LifeTime<=0) {
				return;
			}

			toMuchInjectedParticles = false;

			injectionBufferCPU[ injectionCount ] = particle;
			injectionCount ++;
		}



		/// <summary>
		/// Makes all particles wittingly dead
		/// </summary>
		void ClearParticleBuffer ()
		{
			//for (int i=0; i<MaxInjectingParticles; i++) {
			//	injectionBufferCPU[i].Timing.X = -999999;
			//}
			injectionCount = 0;
		}



		/// <summary>
		/// Updates particle properties.
		/// </summary>
		/// <param name="gameTime"></param>
		internal void Simulate ( GameTime gameTime )
		{
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		internal void Render ( GameTime gameTime, Camera camera, StereoEye stereoEye, HdrFrame viewFrame )
		{
			var device	=	Game.GraphicsDevice;

			device.ResetStates();

			device.SetTargets( viewFrame.DepthBuffer, viewFrame.HdrBuffer );
			device.SetViewport( 0,0,viewFrame.HdrBuffer.Width, viewFrame.HdrBuffer.Height );

			int	w	=	device.DisplayBounds.Width;
			int h	=	device.DisplayBounds.Height;

			//
			//	Setup images :
			//
			if (Images!=null) {
				imagesCB.SetData( Images.GetNormalizedRectangles( MaxImages ) );
			}
			

			//
			//	Setup parameters :
			//	
			Params param = new Params();
			param.View			=	viewLayer.Camera.GetViewMatrix( stereoEye );
			param.Projection	=	viewLayer.Camera.GetProjectionMatrix( stereoEye );
			param.MaxParticles	=	0;
			param.DeltaTime		=	gameTime.ElapsedSec;
			param.CameraForward	=	new Vector4( viewLayer.Camera.GetCameraMatrix( StereoEye.Mono ).Forward	, 0 );
			param.CameraRight	=	new Vector4( viewLayer.Camera.GetCameraMatrix( StereoEye.Mono ).Right	, 0 );
			param.CameraUp		=	new Vector4( viewLayer.Camera.GetCameraMatrix( StereoEye.Mono ).Up		, 0 );
			param.Gravity		=	new Vector4( this.Gravity, 0 );
			param.MaxParticles	=	injectionCount;

			paramsCB.SetData( param );

			//	set DeadListSize to prevent underflow:
			deadParticlesIndices.CopyStructureCount( paramsCB, Marshal.OffsetOf( typeof(Params), "DeadListSize").ToInt32() );


			device.ComputeShaderConstants[0]	= paramsCB ;
			device.VertexShaderConstants[0]		= paramsCB ;
			device.GeometryShaderConstants[0]	= paramsCB ;
			device.PixelShaderConstants[0]		= paramsCB ;
			
			device.ComputeShaderConstants[1]	= imagesCB ;
			device.VertexShaderConstants[1]		= imagesCB ;
			device.GeometryShaderConstants[1]	= imagesCB ;
			device.PixelShaderConstants[1]		= imagesCB ;
			
			device.PixelShaderSamplers[0]	= SamplerState.LinearWrap ;


			//
			//	Inject :
			//
			injectionBuffer.SetData( injectionBufferCPU );

			device.ComputeShaderResources[1]	= injectionBuffer ;
			device.SetCSRWBuffer( 0, simulationBuffer,		0 );
			device.SetCSRWBuffer( 1, deadParticlesIndices, -1 );

			device.PipelineState	=	factory[ (int)Flags.INJECTION ];
			
			//	GPU time ???? -> 0.0046
			device.Dispatch( MathUtil.IntDivUp( MaxInjectingParticles, BlockSize ) );

			ClearParticleBuffer();

			//
			//	Simulate :
			//
			bool skipSim = Game.InputDevice.IsKeyDown(Fusion.Drivers.Input.Keys.O);

			if (!skipSim) {
	
				device.SetCSRWBuffer( 0, simulationBuffer,		0 );
				device.SetCSRWBuffer( 1, deadParticlesIndices, -1 );

				param.MaxParticles	=	MaxSimulatedParticles;
				paramsCB.SetData( param );
				device.ComputeShaderConstants[0] = paramsCB ;

				device.PipelineState	=	factory[ (int)Flags.SIMULATION ];
	
				/// GPU time : 1.665 ms	 --> 0.38 ms
				device.Dispatch( MathUtil.IntDivUp( MaxSimulatedParticles, BlockSize ) );//*/
			}


			//
			//	Render
			//
			device.PipelineState	=	factory[ (int)Flags.DRAW ];
			device.SetCSRWBuffer( 0, null );	
			device.PixelShaderResources[0]		=	Images==null? null : Images.Texture.Srv;
			device.GeometryShaderResources[1]	=	simulationBuffer ;
			device.GeometryShaderResources[2]	=	simulationBuffer ;

			//	GPU time : 0.81 ms	-> 0.91 ms
			device.Draw( MaxSimulatedParticles, 0 );


			if (rs.Config.ShowParticles) {
				rs.Counters.DeadParticles	=	deadParticlesIndices.GetStructureCount();
			}
		}
	}
}
