﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using Fusion.Core.Shell;
using System.IO;
using Fusion.Engine.Common;
using Fusion.Core.Content;


namespace Fusion.Engine.Server {

	public abstract partial class GameServer : GameModule {

		/// <summary>
		/// 
		/// </summary>
		/// <param name="Game"></param>
		public GameServer ( Game game ) : base(game)
		{
			content = new ContentManager(game);
		}


		/// <summary>
		/// Gets server's content manager.
		/// </summary>
		public ContentManager Content {
			get {
				return content;
			}
		}

		ContentManager content;


		/// <summary>
		/// Releases all resources used by the GameServer class.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose ( bool disposing )
		{
			if (disposing) {
				SafeDispose( ref content );
			}
			base.Dispose( disposing );
		}


		/// <summary>
		/// Method is invoked when server started.
		/// </summary>
		/// <param name="map"></param>
		public abstract void LoadContent ( string map );

		/// <summary>
		/// Method is invoked when server shuts down.
		/// This method will be also called when server crashes.
		/// </summary>
		/// <param name="map"></param>
		public abstract void UnloadContent ();

		/// <summary>
		/// Runs one step of server-side world simulation.
		/// </summary>
		/// <param name="gameTime"></param>
		/// <returns>Snapshot bytes</returns>
		public abstract byte[] Update ( GameTime gameTime );

		/// <summary>
		/// Feed server with commands from particular client.
		/// </summary>
		/// <param name="id">Client's ID</param>
		/// <param name="userCommand">Client's user command stream</param>
		/// <param name="lag">Lag in seconds</param>
		public abstract void FeedCommand ( Guid id, byte[] userCommand, uint commandID, float lag );

		/// <summary>
		/// Feed server with commands from particular client.
		/// </summary>
		/// <param name="guid">Client's GUID</param>
		/// <param name="command">Client's user command stream</param>
		public abstract void FeedNotification ( Guid guid, string message );

		/// <summary>
		/// Gets server information that required for client to load the game.
		/// This information usually contains map name and game type.
		/// This information is also used for discovery response.
		/// </summary>
		/// <returns></returns>
		public abstract string ServerInfo ();

		/// <summary>
		/// Called when client connected.
		/// </summary>
		/// <param name="guid">Client GUID.</param>
		/// <param name="userInfo">User information. Cann't be used as client identifier.</param>
		public abstract void ClientConnected ( Guid guid, string userInfo );

		/// <summary>
		/// Called when client received snapshot and ready to play.
		/// </summary>
		/// <param name="guid">Client GUID.</param>
		/// <param name="userInfo">User information. Cann't be used as client identifier.</param>
		public abstract void ClientActivated ( Guid guid );

		/// <summary>
		/// Called when before disconnect.
		/// </summary>
		/// <param name="guid">Client GUID.</param>
		/// <param name="userInfo">User information. Cann't be used as client identifier.</param>
		public abstract void ClientDeactivated ( Guid guid );

		/// <summary>
		/// Called when client disconnected.
		/// </summary>
		/// <param name="clientIP">Client IP in format 123.45.67.89:PORT. Could be used as client identifier.</param>
		public abstract void ClientDisconnected ( Guid guid );

		/// <summary>
		/// Approves client by id and user information.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public abstract bool ApproveClient ( Guid guid, string userInfo, out string reason );

		/// <summary>
		/// Sends text message to all clients.
		/// </summary>
		/// <param name="message"></param>
		public void NotifyClients ( string format, params object[] args )
		{
			NotifyClientsInternal( string.Format(format, args) );
		}
	}
}
