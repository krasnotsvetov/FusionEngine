﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using SharpDX;
using Fusion;
using Fusion.Core.Mathematics;
using Fusion.Drivers.Graphics;
using Fusion.Core;
using Fusion.Core.Content;


namespace Fusion.Engine.Graphics {
	
	public class GeometryCache : DisposableBase {

		/// <summary>
		/// Gets total geometry clip length
		/// </summary>
		public TimeSpan	Length { 
			get;
			private set;
		}

		/// <summary>
		/// Gets total number of vertices
		/// </summary>
		public int VertexCount {
			get;
			private set;
		}


		/// <summary>
		/// Gets total number of indices
		/// </summary>
		public int IndexCount {
			get;
			private set;
		}


		/// <summary>
		/// Gets material references
		/// </summary>
		public IEnumerable<MaterialRef> Materials {
			get;
			private set;
		}


		internal VertexBuffer VertexBuffer { 
			get { 
				return vertexBuffer; 
			} 
		}

		internal IndexBuffer IndexBuffer { 
			get { 
				return indexBuffer; 
			}
		}

		public bool					IsSkinned		{ get; private set; }

		VertexBuffer vertexBuffer;
		IndexBuffer	 indexBuffer;


		/// <summary>
		/// 
		/// </summary>
		/// <param name="rs"></param>
		/// <param name="stream"></param>
		public GeometryCache ( RenderSystem rs, Stream stream )
		{
		}
		


	}
}
