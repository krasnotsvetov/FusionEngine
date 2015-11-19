﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Core.Mathematics;
using Fusion.Core;
using Fusion.Drivers.Graphics;
using Fusion.Engine.Common;
using Fusion.Engine.Graphics.GIS;
using Fusion.Engine.Graphics.GIS.DataSystem.MapSources.Projections;

namespace Fusion.Engine.Graphics {
	public class ViewLayer {
		
		readonly GameEngine		GameEngine;
		readonly GraphicsEngine	ge;

		/// <summary>
		/// Indicates whether view should be drawn.
		/// Default value is True.
		/// </summary>
		public bool Visible {
			get; set;
		}

		/// <summary>
		/// Indicates whether view should be drawn.
		/// </summary>
		public int Order {
			get; set;
		}

		/// <summary>
		/// Gets and sets view's camera.
		/// This value is already initialized when View object is created.
		/// </summary>
		public Camera Camera {
			get; set;
		}

		/// <summary>
		/// Gets and sets view target.
		/// Null value indicates that view will be rendered to backbuffer.
		/// Default value is null.
		/// </summary>
		public TargetTexture Target {
			get; set;
		}

		/// <summary>
		/// Indicated whether target buffer should be cleared before rendering.
		/// </summary>
		public bool Clear {	
			get; set;
		}

		/// <summary>
		/// Gets and sets clear color
		/// </summary>
		public Color4 ClearColor {
			get; set;
		}

		/// <summary>
		/// Gets and sets view light set.
		/// This value is already initialized when View object is created.
		/// </summary>
		public LightSet LightSet {
			get; private set;
		}

		/// <summary>
		/// Gets collection of sprite layers.
		/// </summary>
		public ICollection<SpriteLayer>	SpriteLayers {
			get; private set;
		}


		public ICollection<Instance> Instances {
			get; private set;
		}


		public ICollection<Gis.GisLayer> GisLayers;


		/// <summary>
		/// Creates view's instance.
		/// </summary>
		/// <param name="ge"></param>
		public ViewLayer ( GameEngine gameEngine )
		{
			GameEngine	=	gameEngine;
			this.ge		=	gameEngine.GraphicsEngine;

			Visible		=	true;
			Order		=	0;

			Camera		=	new Camera();
			Target		=	null;

			SpriteLayers	=	new List<SpriteLayer>();
			Instances		=	new List<Instance>();
			LightSet		=	new LightSet( gameEngine.GraphicsEngine );
			GisLayers		=	new List<Gis.GisLayer>();
		}



		/// <summary>
		/// Renders view
		/// </summary>
		internal void RenderView ( GameTime gameTime, StereoEye stereoEye )
		{
			var targetRT = (Target!=null) ? Target.RenderTarget : ge.Device.BackbufferColor;

			//	clear target buffer if necassary :
			if (Clear) {
				ge.Device.Clear( targetRT.Surface, ClearColor );
			}


			var viewport	=	new Viewport( 0,0, targetRT.Width, targetRT.Height );


			ge.LightRenderer.ClearHdrBuffer();

			if (Instances.Any()) {
				//	clear g-buffer :
				ge.LightRenderer.ClearGBuffer();

				//	render shadows :
				ge.LightRenderer.RenderShadows( Camera, Instances );
			
				//	render g-buffer :
				ge.SceneRenderer.RenderGBuffer( Camera, stereoEye, Instances, viewport );

				//	render sky :
				//ge.Sky.Render( Camera, stereoEye, gameTime, ge.LightRenderer.DepthBuffer.Surface, ge.LightRenderer.HdrBuffer.Surface );

				//	render lights :
				ge.LightRenderer.RenderLighting( Camera, stereoEye, LightSet, GameEngine.GraphicsEngine.WhiteTexture, viewport );
			}

			//	apply tonemapping and bloom :
			ge.HdrFilter.Render( gameTime, targetRT.Surface, ge.LightRenderer.HdrBuffer, viewport );

			GameEngine.GraphicsDevice.RestoreBackbuffer();
			if (GisLayers.Any()) {
				var tiles = GisLayers.First() as TilesGisLayer;
				if(tiles != null)
					tiles.Update(gameTime);
			}
			ge.Gis.Draw(gameTime, stereoEye, GisLayers);

			//	draw sprites :
			ge.SpriteEngine.DrawSprites( gameTime, stereoEye, SpriteLayers );
		}
	}
}
