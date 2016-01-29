﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Core.Mathematics;
using Fusion.Drivers.Graphics;
using Fusion.Engine.Input;
using Fusion.Engine.Graphics;
using Fusion.Core;
using Fusion.Core.Configuration;
using Fusion.Framework;
using Fusion;
using Fusion.Engine.Client;
using Fusion.Engine.Common;
using Fusion.Engine.Server;

namespace ShooterDemo {


	class ShooterDemoUserInterface : UserInterface {

		[GameModule( "Console", "con", InitOrder.Before )]
		public GameConsole Console { get { return console; } }
		public GameConsole console;

		DiscTexture	background;
		SpriteLayer uiLayer;
		SpriteFont	headerFont;
		SpriteFont	textFont;
		SpriteFont	titleFont;


		/// <summary>
		/// Creates instance of ShooterDemoUserInterface
		/// </summary>
		/// <param name="engine"></param>
		public ShooterDemoUserInterface ( Game game )
			: base( game )
		{
			console = new GameConsole( game, "conchars" );
		}



		/// <summary>
		/// Called after the ShooterDemoUserInterface is created,
		/// </summary>
		public override void Initialize ()
		{
			uiLayer	=	new SpriteLayer(Game.RenderSystem, 1024);

			//	add console sprite layer to master view layer :
			Game.RenderSystem.RenderWorld.SpriteLayers.Add( uiLayer );
			Game.RenderSystem.RenderWorld.SpriteLayers.Add( console.ConsoleSpriteLayer );


			LoadContent();
			Game.Reloading += (s,e) => LoadContent();
		}



		void LoadContent ()
		{
			background	=	Game.Content.Load<DiscTexture>(@"UserInterface\background");
			headerFont	=	Game.Content.Load<SpriteFont>(@"Fonts\headerFont");
			titleFont	=	Game.Content.Load<SpriteFont>(@"Fonts\titleFont");
			textFont	=	Game.Content.Load<SpriteFont>(@"Fonts\textFont");
		}



		/// <summary>
		/// Overloaded. Immediately releases the unmanaged resources used by this object. 
		/// </summary>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {
				SafeDispose( ref uiLayer );
			}
			base.Dispose( disposing );
		}



		/// <summary>
		/// Called when the game has determined that UI logic needs to be processed.
		/// </summary>
		/// <param name="gameTime"></param>
		public override void Update ( GameTime gameTime )
		{
			//	update console :
			console.Update( gameTime );

			var vp = Game.RenderSystem.DisplayBounds;

			uiLayer.Clear();
			uiLayer.Draw( background, 0,0, vp.Width, vp.Height, Color.White );

			uiLayer.Draw( null, 0,vp.Height/4, vp.Width, vp.Height/2, new Color(0,0,0,192) );

			var h = textFont.LineHeight;
			titleFont.DrawString( uiLayer, "SHOOTER DEMO", 100,vp.Height/2 - h, new Color(242,242,242) );

			textFont.DrawString( uiLayer, "Press [~] to open console:", 100,vp.Height/2 + h, new Color(242,242,242) );
			textFont.DrawString( uiLayer, "   - Enter \"map base1\" to start the game.", 100,vp.Height/2 + h*2, new Color(242,242,242) );
			textFont.DrawString( uiLayer, "   - Enter \"killserver\" to stop the game.", 100,vp.Height/2 + h*3, new Color(242,242,242) );
			textFont.DrawString( uiLayer, "   - Enter \"connect <IP:port>\" to connect to the remote game.", 100,vp.Height/2 + h*4, new Color(242,242,242) );
		}



		/// <summary>
		/// Called when user closes game window using Close button or Alt+F4.
		/// </summary>
		public override void RequestToExit ()
		{
			Game.Exit();
		}



		/// <summary>
		/// Called when discovery respone arrives.
		/// </summary>
		/// <param name="endPoint"></param>
		/// <param name="serverInfo"></param>
		public override void DiscoveryResponse ( System.Net.IPEndPoint endPoint, string serverInfo )
		{
			Log.Message( "DISCOVERY : {0} - {1}", endPoint.ToString(), serverInfo );
		}
	}
}