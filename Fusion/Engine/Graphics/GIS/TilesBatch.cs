﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Drivers.Graphics;
using Fusion.Engine.Common;
using Fusion.Engine.Graphics.GIS.DataSystem.MapSources;

namespace Fusion.Engine.Graphics.GIS
{
	public partial class TilesGisBatch : GIS.GisBatch
	{
		Ubershader		shader;
		StateFactory	factory;


		Texture2D frame;


		[Flags]
		public enum TileFlags : int
		{
			SHOW_FRAMES		= 0x0001,
		}


		public TilesGisBatch(GameEngine engine) : base(engine)
		{
			RegisterMapSources();

			CurrentMapSource = MapSources[0];

			frame	= GameEngine.Content.Load<Texture2D>("redframe.tga");
			shader	= GameEngine.Content.Load<Ubershader>("globe.Tile.hlsl");
			factory = new StateFactory(shader, typeof(TileFlags), Primitive.TriangleList, VertexInputElement.FromStructure<GIS.GeoPoint>(), BlendState.Opaque, RasterizerState.CullCW, DepthStencilState.Default);
		}


		public override void Update(GameTime gameTime)
		{
			var oldProj = CurrentMapSource.Projection;

			CurrentMapSource.Update(gameTime);

			CurrentMapSource = MapSources[(int)0];


			if (!oldProj.Equals(CurrentMapSource.Projection)) {
				updateTiles = true;
			}


			DetermineTiles();
		}


		public override void Draw(GameTime gameTime, ConstantBuffer constBuffer)
		{
			var dev = GameEngine.GraphicsDevice;

			dev.VertexShaderConstants[0]	= constBuffer;
			dev.PixelShaderSamplers[0]		= SamplerState.LinearClamp;
			dev.PixelShaderResources[1]		= frame;

			dev.PipelineState = factory[(int)(TileFlags.SHOW_FRAMES)];


			foreach (var globeTile in tilesToRender) {
				var tex = CurrentMapSource.GetTile(globeTile.Value.X, globeTile.Value.Y, globeTile.Value.Z).Tile;
				
				dev.PixelShaderResources[0] = tex;

				dev.SetupVertexInput(globeTile.Value.VertexBuf, globeTile.Value.IndexBuf);
				dev.DrawIndexed(globeTile.Value.IndexBuf.Capacity, 0, 0);
			}
		}


		public override void Dispose()
		{
			factory.Dispose();
			shader.Dispose();

			foreach (var mapSource in MapSources) {
				mapSource.Dispose();
			}

			if (BaseMapSource.EmptyTile != null) {
				BaseMapSource.EmptyTile.Dispose();
			}

			foreach (var tile in tilesToRender) {
				tile.Value.Dispose();
			}
			foreach (var tile in tilesFree) {
				tile.Value.Dispose();
			}
		}
	}
}