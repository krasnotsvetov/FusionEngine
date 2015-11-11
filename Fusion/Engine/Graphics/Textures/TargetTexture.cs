﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Drivers.Graphics;

namespace Fusion.Engine.Graphics {
	
	/// <summary>
	/// Represents texture that could be used as target for rendering.
	/// </summary>
	public class TargetTexture : Texture {


		/// <summary>
		/// Gets target texture's format
		/// </summary>
		public TargetFormat Format { get; private set; }

		RenderTarget2D	renderTarget;

		
		/// <summary>
		/// Create target texture with specified size and format
		/// </summary>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="format"></param>
		public TargetTexture ( GraphicsEngine ge, int width, int height, TargetFormat format )
		{
			this.Width	=	width;
			this.Height	=	height;
			this.Format	=	format;

			var clrFrmt	=	 ColorFormat.Unknown;

			switch (format) {
				case TargetFormat.LowDynamicRange  : clrFrmt = ColorFormat.Rgba8;	break;
				case TargetFormat.HighDynamicRange : clrFrmt = ColorFormat.Rgba16F;	break;
				default: throw new ArgumentException("format");
			}

			renderTarget	=	new RenderTarget2D( ge.Device, clrFrmt, width, height ); 
			Srv	=	renderTarget;
		}	
		
	}
}
