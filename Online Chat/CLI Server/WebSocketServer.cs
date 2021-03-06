﻿using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;

public class OnMessageAction : EventArgs
{
	public ChatUser Sender { get; private set; }
	public (string PayloadString, WebSocketOpCode Opcode) Payload { get; set; }

	public OnMessageAction(ChatUser Sender, (string Payload, WebSocketOpCode Opcode) Packet)
	{
		this.Sender = Sender;
		this.Payload = Packet;
	}
}

public class OnUserAction : EventArgs
{
	public ChatUser Sender { get; private set; }

	public OnUserAction(ChatUser Sender)
	{
		this.Sender = Sender;
	}
}

public class Server
{
	/// <summary>
	/// Socket för att hantera att nya anslutningar kommer in på servern
	/// </summary>
	public Socket ListenerSocket { get; set; }

	public RSA ServerEncryptionManager;

	/// <summary>
	/// EndPoint för att beskriva ip och port som servern lyssnar på
	/// </summary>
	public EndPoint ListenAddress { get; set; }

	public bool Alive { get; set; }

	private readonly ManualResetEvent UntilConnected = new ManualResetEvent(false);

	public bool LogEnable = true;

	public event EventHandler<OnMessageAction> OnMessageReceive;
	public event EventHandler<OnUserAction> OnUserConnect;
	public event EventHandler<OnUserAction> OnUserDisconnect;

	public List<ChatUser> Users = new List<ChatUser>();
	/// <summary>
	/// Konstruera en server som ska lyssna på en EndPoint
	/// </summary>
	/// <param name="ListenAddress"></param>
	public Server(EndPoint ListenAddress)
	{
		this.ListenAddress = ListenAddress;
		ListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		Alive = true;
		ServerEncryptionManager = new RSA();
	}

	private void Log(string severity, string message)
	{
		if (LogEnable)
		{
			Console.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]") + " [" + severity + "] " + message);
		}
	}

	public void StartServer()
	{
		Log("INFO", "Starting Server");
		Log("INFO", "Binding to " + ListenAddress.ToIpEndPoint().ToString());
		ListenerSocket.Bind(ListenAddress.ToIpEndPoint());
		Log("INFO", "Listening for connections");
		ListenerSocket.Listen(100);

		while (Alive)
		{
			UntilConnected.Reset();
			Log("INFO", "Awaiting a new connection");
			ListenerSocket.BeginAccept(new AsyncCallback(AcceptUserCallback), null);
			UntilConnected.WaitOne();
		}

	}

	/// <summary>
	/// Async funktion för att acceptera en ny client till servern.
	/// Etablerarar också WebSocket Upgraderingen
	/// </summary>
	/// <param name="Result">State of async operation</param>
	private void AcceptUserCallback(IAsyncResult Result)
	{
		Socket NewSocket = ListenerSocket.EndAccept(Result);
		Log("INFO", "Got the new connection socket");
		Log("INFO", "Allowing new connections");
		UntilConnected.Set();

		Log("INFO", "Handling WebSocket Upgrade");
		if (HandleWebSocketUpgrade(NewSocket))
		{
			Log("INFO", "WebSocket Upgrade Successful");
			Log("INFO", "Added User to UserList");

			var NewUser = new ChatUser(new WebSocketUser(NewSocket, this));
			var PublicKeyPemObject = new JObject
			{
				["KeyType"] = "RSAPublicKey",
				["Key"] = ServerEncryptionManager.ExportPublicKey()
			};

			NewUser.Socket.SendJSON(PublicKeyPemObject, "PublicKeyExchange", false);
			Log("INFO", "Sent Public RSA Key");

			Users.Add(NewUser);
			Log("INFO", "Successfully added a new user");
			OnUserConnect(this, new OnUserAction(NewUser));
		}
		else
		{
			Log("WARN", "WebSocket Upgrade Failed");
			Log("WARN", "Disconnecting user");
			NewSocket.Send(Encoding.Default.GetBytes("Not a Websocket Connection"));
			NewSocket.Shutdown(SocketShutdown.Both);
			NewSocket.Close();
		}
	}

	public void Receive(ChatUser user, string Payload, WebSocketOpCode OpCode)
	{
		// Decrypt here

		JObject Deserilized = JsonConvert.DeserializeObject<JObject>(Payload);

		string Type = Deserilized["Type"].ToObject<string>();

		string IV = Deserilized["IV"].ToObject<string>();

		JToken Packet = Deserilized["Packet"];

		if (Packet.Type == JTokenType.String)
		{

			byte[] EncryptedPayload = Packet.ToObject<byte[]>();

			if (IV == "blank") // => decrypt with rsa private key
			{

				var AesKey = ServerEncryptionManager.Decrypt(EncryptedPayload);

				user.Socket.InitilizeEncryption(AesKey);

				Deserilized["Packet"] = AesKey;

			}
			else
			{
				user.Socket.EncryptionManager.Manager.IV = Deserilized["IV"].ToObject<byte[]>();
				byte[] DecryptedString = user.Socket.EncryptionManager.Decrypt(EncryptedPayload);
				Deserilized["Packet"] = Encoding.Default.GetString(DecryptedString);

				OnMessageReceive(this, new OnMessageAction(user, (Deserilized.ToString(), OpCode)));
			}
		}
		else
		{
			OnMessageReceive(this, new OnMessageAction(user, (JsonConvert.SerializeObject(Deserilized), OpCode)));
		}
	}
	public void Receive(WebSocketUser user, string Payload, WebSocketOpCode OpCode)
	{
		var ChatUser = Users.Where(u => u.Socket == user).First();
		Receive(ChatUser, Payload, OpCode);
	}

	/// <summary>
	/// Hanterar upgraderingen av protokollet från http till Websockets
	/// </summary>
	/// Eftersom att denna funktion körs från en async funktion så kan vi kalla sync funktioner i den utan att blockera huvudtråden
	/// <param name="Client">Klienten som ska upgraderas</param>
	/// <returns>Om upgraderingen lyckades</returns>
	private bool HandleWebSocketUpgrade(Socket Client)
	{
		string UpgradeGetRequest = WebSocketHelper.GetUpgradeRequest(Client);
		Log("INFO", "Got WebSocket Upgrade Request");

		string SecureKey = WebSocketHelper.GetWebSocketKeyFromResponse(UpgradeGetRequest);

		if (SecureKey == string.Empty)
		{
			return false;
		}

		Log("INFO", "Got WebSocket Key");

		string ResponseKey = WebSocketHelper.GetAcceptKey(SecureKey);

		string Response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + ResponseKey + "\r\n\r\n";

		Client.Send(Encoding.Default.GetBytes(Response));

		Log("INFO", "Sent WebSocket Upgrade Response");
		
		return true;
	}

	public void DisconnectUser(WebSocketUser user)
	{
		var DisconnectedUser = new ChatUser(user);
		OnUserDisconnect(this, new OnUserAction(DisconnectedUser));
		user.ConnectedSocket.Shutdown(SocketShutdown.Both);
		user.ConnectedSocket.Close();
		Users.Remove(Users.Where(u => u.Socket == user).First());
	}

}