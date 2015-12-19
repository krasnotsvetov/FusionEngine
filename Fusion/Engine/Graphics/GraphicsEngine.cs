﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Core;
using Fusion.Core.Mathematics;
using Fusion.Core.Configuration;
using Fusion.Engine.Common;
using Fusion.Drivers.Graphics;
using Fusion.Engine.Graphics.GIS;

namespace Fusion.Engine.Graphics {

	public class GraphicsEngine : GameModule {

		internal readonly GraphicsDevice Device;

		[GameModule("Sprites", "sprite", InitOrder.After)]
		public SpriteEngine	SpriteEngine { get { return spriteEngine; } }
		SpriteEngine	spriteEngine;

		[GameModule("GIS", "gis", InitOrder.After)]
		public Gis Gis { get { return gis; } }
		Gis gis;

		[GameModule("Filter", "filter", InitOrder.After)]
		public Filter Filter { get{ return filter; } }
		public Filter filter;

		[GameModule("HdrFilter", "hdr", InitOrder.After)]
		public HdrFilter HdrFilter { get{ return hdrFilter; } }
		public HdrFilter hdrFilter;
		
		[GameModule("LightRenderer", "light", InitOrder.After)]
		public LightRenderer	LightRenderer { get { return lightRenderer; } }
		public LightRenderer	lightRenderer;
		
		[GameModule("SceneRendere", "scene", InitOrder.After)]
		public SceneRenderer	SceneRenderer { get { return sceneRenderer; } }
		public SceneRenderer	sceneRenderer;
		
		[GameModule("Sky", "sky", InitOrder.After)]
		public Sky	Sky { get { return sky; } }
		public Sky	sky;
		
		[Config]
		public  GraphicsEngineConfig Config { get; private set; }


		/// <summary>
		/// Fullscreen
		/// </summary>
		public bool Fullscreen { 
			get { return Device.FullScreen; }
			set { Device.FullScreen = value; }
		}



		RenderTarget2D	hdrTarget;

		public Texture	GrayTexture { get { return grayTexture; } }
		public Texture	WhiteTexture { get { return whiteTexture; } }
		public Texture	BlackTexture { get { return blackTexture; } }
		public Texture	FlatNormalMap { get { return flatNormalMap; } }

		DynamicTexture grayTexture;
		DynamicTexture whiteTexture;
		DynamicTexture blackTexture;
		DynamicTexture flatNormalMap;

		/// <summary>
		/// Gets default material.
		/// </summary>
		public Material	DefaultMaterial { get { return defaultMaterial; } }
		Material defaultMaterial;




		/// <summary>
		/// 
		/// </summary>
		/// <param name="engine"></param>
		public GraphicsEngine ( Game Game ) : base(Game)
		{
			Config		=	new GraphicsEngineConfig();
			this.Device	=	Game.GraphicsDevice;

			viewLayers	=	new List<ViewLayer>();
			spriteEngine	=	new SpriteEngine( this );
			gis				=	new Gis(Game);
			filter			=	new Filter( Game );
			hdrFilter		=	new HdrFilter( Game );
			lightRenderer	=	new LightRenderer( Game );
			sceneRenderer	=	new SceneRenderer( Game );
			sky				=	new Sky( Game );

			Device.DisplayBoundsChanged += (s,e) => {
				var handler = DisplayBoundsChanged;
				if (handler!=null) {
					handler(s,e);
				}
			};
		}


										  
		/// <summary>
		/// Applies graphics parameters.
		/// </summary>
		/// <param name="p"></param>
		internal void ApplyParameters ( ref GraphicsParameters p )
		{
			p.Width				=	Config.Width;
			p.Height			=	Config.Height;
			p.FullScreen		=	Config.Fullscreen;
			p.StereoMode		=	(Fusion.Drivers.Graphics.StereoMode)Config.StereoMode;
			p.InterlacingMode	=	(Fusion.Drivers.Graphics.InterlacingMode)Config.InterlacingMode;
			p.UseDebugDevice	=	Config.UseDebugDevice;
			p.MsaaLevel			=	Config.MsaaEnabled ? 4 : 1;
		}



		/// <summary>
		/// Intializes graphics engine.
		/// </summary>
		public override void Initialize ()
		{
			whiteTexture	=	new DynamicTexture( this, 4,4, typeof(Color), false, false );
			whiteTexture.SetData( Enumerable.Range(0,16).Select( i => Color.White ).ToArray() );
			
			grayTexture		=	new DynamicTexture( this, 4,4, typeof(Color), false, false );
			grayTexture.SetData( Enumerable.Range(0,16).Select( i => Color.Gray ).ToArray() );

			blackTexture	=	new DynamicTexture( this, 4,4, typeof(Color), false, false );
			blackTexture.SetData( Enumerable.Range(0,16).Select( i => Color.Black ).ToArray() );

			flatNormalMap	=	new DynamicTexture( this, 4,4, typeof(Color), false, false );
			flatNormalMap.SetData( Enumerable.Range(0,16).Select( i => new Color(127,127,255,127) ).ToArray() );

			defaultMaterial	=	new Material();
			defaultMaterial.LoadGpuResources( Game.Content );
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {
				SafeDispose( ref spriteEngine );

				SafeDispose( ref whiteTexture );
				SafeDispose( ref blackTexture );
				SafeDispose( ref flatNormalMap );

				SafeDispose( ref defaultMaterial );
			}
			base.Dispose( disposing );
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="gameTime"></param>
		/// <param name="stereoEye"></param>
		internal void Draw ( GameTime gameTime, StereoEye stereoEye )
		{
			Gis.Update(gameTime);
			//GIS.Draw(gameTime, StereoEye.Mono);

			ViewLayer[] layersToDraw;

			lock (viewLayers) {
				layersToDraw = viewLayers
					.OrderBy( c1 => c1.Order )
					.Where( c2 => c2.Visible )
					.ToArray();
			}

			foreach ( var viewLayer in layersToDraw ) {
				viewLayer.RenderView( gameTime, stereoEye );
			}
		}



		/// <summary>
		/// Adds view layer.
		/// </summary>
		/// <param name="viewLayer"></param>
		public void AddLayer ( ViewLayer viewLayer )
		{
			lock (viewLayers) {
				viewLayers.Add( viewLayer );
			}
		}



		/// <summary>
		/// Removes layer.
		/// </summary>
		/// <param name="viewLayer"></param>
		/// <returns>True if element was removed. False if layer does not exist.</returns>
		public bool RemoveLayer ( ViewLayer viewLayer )
		{
			lock (viewLayers) {
				if (viewLayers.Contains(viewLayer)) {
					viewLayers.Remove( viewLayer );
					return true;
				}
			}

			return false;
		}



		/// <summary>
		/// Gets collection of Composition.
		/// </summary>
		readonly ICollection<ViewLayer> viewLayers = new List<ViewLayer>();



		/*-----------------------------------------------------------------------------------------
		 * 
		 *	Display stuff :
		 * 
		-----------------------------------------------------------------------------------------*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="path"></param>
		public void Screenshot ( string path = null )
		{
			Device.Screenshot(path);
		}
		

		/// <summary>
		/// Gets display bounds.
		/// </summary>
		public Rectangle DisplayBounds {
			get {
				return Device.DisplayBounds;
			}
		}


		/// <summary>
		/// Raises when display bound changes.
		/// DisplayBounds property is already has actual value when this event raised.
		/// </summary>
		public event EventHandler	DisplayBoundsChanged;
	}
}
