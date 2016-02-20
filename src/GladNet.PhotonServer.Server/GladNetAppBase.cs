﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Photon.SocketServer;
using GladNet.Common;
using Logging.Services;
using GladNet.Server.Common;
using GladNet.Serializer;

namespace GladNet.PhotonServer.Server
{
	/// <summary>
	/// GladNet2 ApplicationBase for Photon applications.
	/// </summary>
	public abstract class GladNetAppBase : Photon.SocketServer.ApplicationBase
	{
		/// <summary>
		/// Application logger. Root logger for the <see cref="ApplicationBase"/>.
		/// </summary>
		protected abstract ILogger AppLogger { get; set; }

		/// <summary>
		/// Provider for <see cref="ISerializerStrategy"/>s.
		/// </summary>
		protected abstract ISerializerStrategy Serializer { get; set; }

		/// <summary>
		/// Provider for <see cref="IDeserializerStrategy"/>s.
		/// </summary>
		protected abstract IDeserializerStrategy Deserializer { get; set; }

		/// <summary>
		/// Called internally by Photon when a peer is attempting to connect.
		/// Services the connection attempt.
		/// </summary>
		/// <param name="initRequest">Request details.</param>
		/// <returns></returns>
		protected override PeerBase CreatePeer(InitRequest initRequest)
		{
			//Create the details so that the consumer of this class, who extends it, can indicate if this is a request we should service
			//AKA should a peer be made
			IConnectionDetails details = new PhotonServerIConnectionDetailsAdapter(initRequest.RemoteIP, initRequest.RemotePort, initRequest.LocalPort, initRequest.ConnectionId);

			//If we should service the peer
			if (ShouldServiceIncomingPeerConnect(details))
			{
				//Unlike in PhotonServer we have the expectation that they WILL be creating a peer since they said they would
				//Because of this we'll be creating the actual PeerBase in advance.
				NetworkMessagePublisher publisher = new NetworkMessagePublisher();
				IDisconnectionServiceHandler disconnectionHandler = new PhotonServerIDisconnectionServiceHandlerAdapter();

				//Build the peer first since it's required for the network message sender
				GladNetPeerBase peerBase = new GladNetPeerBase(initRequest.Protocol, initRequest.PhotonPeer, publisher, Deserializer, disconnectionHandler);
				//We should make the ClientPeerSession now
				ClientPeerSession session = CreateClientSession(new PhotonServerINetworkMessageSenderClientAdapter(peerBase, Serializer), details, publisher, disconnectionHandler);

				if (session == null)
				{
					peerBase.Disconnect();

					return null;
				}
				
				//This must be done to keep alive the reference of the session
				//Otherwise GC will clean it up (WARNING: This will create circular reference and cause a leak if you do not null the peer out eventually)
				peerBase.Peer = session;

				return peerBase;
			}
			else
			{
				//Disconnect the client if they're not going to have a peer serviced
				initRequest.PhotonPeer.DisconnectClient();

				return null;
			}
		}

		/// <summary>
		/// Processes incoming connection details and decides if a connection should be established.
		/// </summary>
		/// <param name="details">Details of the connection.</param>
		/// <returns>Indicates if, based on the details, a connection should be serviced.</returns>
		protected abstract bool ShouldServiceIncomingPeerConnect(IConnectionDetails details);

		/// <summary>
		/// Creates a client session for the incoming connection request.
		/// </summary>
		/// <param name="sender">Message sending service.</param>
		/// <param name="details">Connection details.</param>
		/// <param name="subService">Subscription service for networked messages.</param>
		/// <param name="disconnectHandler">Disconnection handling service.</param>
		/// <returns>A new client session.</returns>
		protected abstract ClientPeerSession CreateClientSession(INetworkMessageSender sender, IConnectionDetails details, INetworkMessageSubscriptionService subService,
			IDisconnectionServiceHandler disconnectHandler);

		/// <summary>
		/// Called internally by Photon when the application is just about to finish startup.
		/// </summary>
		protected override abstract void Setup();

		/// <summary>
		/// Called internally by Photon when the application is about to be torn down.
		/// </summary>
		protected override abstract void TearDown();
	}
}