﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using System.Threading;
using System.Net;
using Fusion.Core.Shell;
using Fusion.Engine.Network;
using Fusion.Engine.Common;
using Fusion.Engine.Common.Commands;
using System.Diagnostics;


namespace Fusion.Engine.Server {

	public abstract partial class GameServer : GameModule {

		Task serverTask;
		CancellationTokenSource killToken;

		object lockObj = new object();

		Queue<string> notifications = null;


		/// <summary>
		/// Gets whether server is still alive.
		/// </summary>
		internal bool IsAlive {
			get {
				return serverTask != null; 
			}
		}



		/// <summary>
		/// Initiate server thread.
		/// </summary>
		/// <param name="map"></param>
		/// <param name="postCommand"></param>
		internal void StartInternal ( string map, string postCommand )
		{
			lock (lockObj) {
				if (IsAlive) {
					Log.Warning("Can not start server, it is already running");
					return;
				}

				killToken	=	new CancellationTokenSource();
				serverTask	=	new Task( () => ServerTaskFunc(map, postCommand), killToken.Token );
				serverTask.Start();
			}
		}


		
		/// <summary>
		/// Kills server thread.
		/// </summary>
		/// <param name="wait"></param>
		internal void KillInternal ()
		{
			lock (lockObj) {
				if (!IsAlive) {
					Log.Warning("Server is not running");
				}

				if (killToken!=null) {
					killToken.Cancel();
				}
			}
		}



		/// <summary>
		/// Waits for server thread.
		/// </summary>
		internal void Wait ()
		{
			lock (lockObj) {
				if (killToken!=null) {
					killToken.Cancel();
				}

				if (serverTask!=null) {
					Log.Message("Waiting for server task...");
					serverTask.Wait();
				}
			}
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="map"></param>
		void ServerTaskFunc ( string map, string postCommand )
		{
			var netConfig		=	new NetPeerConfiguration(GameEngine.GameID);
			netConfig.Port		=	GameEngine.Network.Config.Port;
			netConfig.MaximumConnections	=	32;
			netConfig.UnreliableSizeBehaviour = NetUnreliableSizeBehaviour.NormalFragmentation;
			netConfig.EnableMessageType( NetIncomingMessageType.ConnectionApproval );

			var server		=	new NetServer( netConfig );
			notifications	=	new Queue<string>();

			Log.Message("SV: Start: {0} {1}", map, postCommand);

			var snapshotQueue	=	new SnapshotQueue(32);


			//
			//	configure & start server :
			//
			try {

				server.Start();

				//
				//	start game specific stuff :
				//
				LoadLevel( map );

				//
				//	invoke post-start command :
				//
				if (postCommand!=null) {
					GameEngine.Invoker.Push( postCommand );
				}


				var svTime = new GameTime();

				//
				//	server loop :
				//	
				while ( !killToken.IsCancellationRequested ) {

					svTime.Update();

					#if DEBUG
					server.Configuration.SimulatedLoss	=	GameEngine.Network.Config.SimulatePacketsLoss;
					#endif

					//	read input messages :
					DispatchIM( server );

					//	update frame and get snapshot :
					var snapshot = Update( svTime );

					//	push snapshot to queue :
					snapshotQueue.Push( snapshot );

					//	send snapshot to clients :
					SendSnapshot( server, snapshotQueue );

					//	send notifications to clients :
					SendNotifications( server );

					//	execute server's command queue :
					GameEngine.Invoker.ExecuteQueue( svTime, CommandAffinity.Server );

					//	crash test for server :
					CrashServer.CrashTest();
				}

				foreach ( var conn in server.Connections ) {
					conn.Disconnect("Server is killed");
				}

			} catch ( Exception e ) {
				Log.PrintException( e, "Server error: {0}", e.Message );

				foreach ( var conn in server.Connections ) {
					conn.Disconnect(string.Format("Server error: {0}", e.Message));
				}

			} finally {

				//
				//	kill game specific stuff :
				//	try...catch???
				//
				UnloadLevel();

				//
				//	shutdown connection :
				//
				server.Shutdown("Server shutdown");
				Log.Message("SV: Shutdown");

				notifications	=	null;

				killToken	=	null;
				serverTask	=	null;
				server		=	null;
			}
		}



		/*-----------------------------------------------------------------------------------------
		 * 
		 *	Client-server stuff :
		 * 
		-----------------------------------------------------------------------------------------*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="message"></param>
		void DispatchIM ( NetServer server )
		{
			NetIncomingMessage msg;
			while ((msg = server.ReadMessage()) != null)
			{
				switch (msg.MessageType)
				{
					case NetIncomingMessageType.VerboseDebugMessage:Log.Debug	("SV Net: " + msg.ReadString()); break;
					case NetIncomingMessageType.DebugMessage:		Log.Verbose	("SV Net: " + msg.ReadString()); break;
					case NetIncomingMessageType.WarningMessage:		Log.Warning	("SV Net: " + msg.ReadString()); break;
					case NetIncomingMessageType.ErrorMessage:		Log.Error	("SV Net: " + msg.ReadString()); break;

					case NetIncomingMessageType.ConnectionApproval:

						var clientID	=	msg.SenderEndPoint.ToString();
						var userInfo	=	msg.SenderConnection.RemoteHailMessage.PeekString();
						var reason		=	"";
						var approve		=	ApproveClient( clientID, userInfo, out reason );

						if (approve) {	
							msg.SenderConnection.Approve( server.CreateMessage( ServerInfo() ) );
						} else {
							msg.SenderConnection.Deny( reason );
						}

						break;

					case NetIncomingMessageType.StatusChanged:		
						DispatchStatusChange( msg );
						break;
					
					case NetIncomingMessageType.Data:
						DispatchDataIM( msg );
						break;
					
					default:
						Log.Warning("SV: Unhandled type: " + msg.MessageType);
						break;
				}
				server.Recycle(msg);
			}		
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg"></param>
		void DispatchStatusChange ( NetIncomingMessage msg )
		{
			var connStatus	=	(NetConnectionStatus)msg.ReadByte();
			var senderEP	=	msg.SenderEndPoint;
			var text		=	msg.ReadString();

			Log.Message	("SV: {0}: {1}: {2}", connStatus, senderEP.ToString(), text);
			
			switch (connStatus) {
				case NetConnectionStatus.Connected :
					ClientConnected( senderEP.ToString(), msg.SenderConnection.RemoteHailMessage.PeekString() );
					break;

				case NetConnectionStatus.Disconnected :
					ClientDisconnected( senderEP.ToString(), msg.SenderConnection.RemoteHailMessage.PeekString() );
					break;

				default:
					break;
			}
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="server"></param>
		void SendSnapshot ( NetServer server, SnapshotQueue queue )
		{
			//	snapshot request is stored in connection's tag.s
			var debug	=	GameEngine.Network.Config.ShowSnapshots;
			var conns	=	server.Connections.Where ( c => c.Tag is uint );

			var sw		=	new Stopwatch();

			foreach ( var conn in conns ) {

				sw.Reset();
				sw.Start();
					
				var frame		=	queue.LastFrame;
				var prevFrame	=	(uint)conn.Tag;
				int size		=	0;
				var snapshot	=	queue.Compress( ref prevFrame, out size);

				//	reset snapshot request :
				conn.Tag = null;

				var msg = server.CreateMessage( snapshot.Length + 4 * 3 + 1 );
			
				msg.Write( (byte)NetCommand.Snapshot );
				msg.Write( frame );
				msg.Write( prevFrame );
				msg.Write( snapshot.Length );
				msg.Write( snapshot ); 

				//	Zero snapshot frame index means that we are waiting for first snapshot.
				//	and command shoud reach the server.
				var delivery	=	prevFrame == 0 ? NetDeliveryMethod.ReliableOrdered : NetDeliveryMethod.UnreliableSequenced;

				sw.Stop();

				server.SendMessage( msg, conn, delivery, 0 );

				if (debug) {
					Log.Message("Snapshot: #{0} - #{1} : {2}:{6} / {3} to {4} at {5} msec", frame, prevFrame, snapshot.Length, size, conn.RemoteEndPoint.ToString(), sw.Elapsed.TotalMilliseconds, msg.Data.Length );
				}
			}
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="server"></param>
		void SendNotifications ( NetServer server )
		{
			List<string> messages;
			lock (notifications) {
				messages = notifications.ToList();
				notifications.Clear();
			}

			var conns = server.Connections;

			if (!conns.Any()) {
				return;
			}

			foreach ( var message in messages ) {
				var msg = server.CreateMessage( message.Length + 1 );
				msg.Write( (byte)NetCommand.Notification );
				msg.Write( message );
				server.SendMessage( msg, conns, NetDeliveryMethod.ReliableSequenced, 0 );
			}
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg"></param>
		void DispatchDataIM ( NetIncomingMessage msg )
		{
			var netCmd = (NetCommand)msg.ReadByte();

			switch (netCmd) {
				case NetCommand.UserCommand : 
					DispatchUserCommand( msg );
					break;

				case NetCommand.Notification :
					FeedNotification( msg.SenderEndPoint.ToString(), msg.ReadString() );
					break;
			}
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="msg"></param>
		void DispatchUserCommand ( NetIncomingMessage msg )
		{	
			var snapshotID	=	msg.ReadUInt32();
			var size		=	msg.ReadInt32();

			var data		=	msg.ReadBytes( size );

			FeedCommand( msg.SenderEndPoint.ToString(), data );

			msg.SenderConnection.Tag = snapshotID;
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="message"></param>
		void NotifyClientsInternal ( string message )
		{
			if (notifications!=null) {
				lock (notifications) {
					notifications.Enqueue(message);
				}
			}
		}
	}
}
