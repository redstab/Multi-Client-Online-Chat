using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class OnMessageAction : EventArgs
{
	public WebSocketUser Sender { get; private set; }
	public (string PayloadString, WebSocketOpCode Opcode) Payload { get; set; }

	public OnMessageAction(WebSocketUser Sender, (string Payload, WebSocketOpCode Opcode) Packet)
	{
		this.Sender = Sender;
		this.Payload = Packet;
	}
}

public class OnUserAction : EventArgs
{
	public WebSocketUser Sender { get; private set; }

	public OnUserAction(WebSocketUser Sender)
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

	private ManualResetEvent UntilConnected = new ManualResetEvent(false);

	public bool LogEnable = false;

	public event EventHandler<OnMessageAction> OnMessageReceive;
	public event EventHandler<OnUserAction> OnUserConnect;
	public event EventHandler<OnUserAction> OnUserDisconnect;

	public List<WebSocketUser> Users = new List<WebSocketUser>();
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
		ListenerSocket.Bind(ListenAddress.ToIpEndPoint());
		ListenerSocket.Listen(100);

		while (Alive)
		{
			UntilConnected.Reset();
			Log("INFO", "Waiting for a new connection");
			ListenerSocket.BeginAccept(new AsyncCallback(AcceptUserCallback), null);
			Log("INFO-VERBOSE", "Waiting for server to get the new socket");
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
		Log("INFO-VERBOSE", "Got the new connection socket");
		Log("INFO", "Allowing new connections");
		UntilConnected.Set();

		Log("INFO", "Handling WebSocket Upgrade");
		if (HandleWebSocketUpgrade(NewSocket))
		{
			Log("INFO", "WebSocket Upgrade Successful");
			Log("INFO", "Added User to UserList");

			var NewUser = new WebSocketUser(NewSocket, this);
			var PublicKeyPemObject = new JObject();
			PublicKeyPemObject["KeyType"] = "RSAPublicKey";
			PublicKeyPemObject["Key"] = ServerEncryptionManager.ExportPublicKey();

			NewUser.SendJSON(PublicKeyPemObject, "PublicKeyExchange", false);

			Users.Add(NewUser);
			OnUserConnect(this, new OnUserAction(NewUser));
		}
		else
		{
			Log("INFO", "WebSocket Upgrade Failed");
			NewSocket.Send(Encoding.Default.GetBytes("Not a Websocket Connection"));
			NewSocket.Shutdown(SocketShutdown.Both);
			NewSocket.Close();
		}
	}

	public void Receive(WebSocketUser user, string Payload, WebSocketOpCode OpCode)
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

				user.InitilizeEncryption(AesKey);

				Deserilized["Packet"] = AesKey;

			}
			else
			{
				user.EncryptionManager.Manager.IV = Deserilized["IV"].ToObject<byte[]>();
				byte[] DecryptedString = user.EncryptionManager.Decrypt(EncryptedPayload);
				Deserilized["Packet"] = Encoding.Default.GetString(DecryptedString);

				OnMessageReceive(this, new OnMessageAction(user, (Deserilized.ToString(), OpCode)));
			}
		}
		else
		{
			OnMessageReceive(this, new OnMessageAction(user, (JsonConvert.SerializeObject(Deserilized), OpCode)));
		}
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

		string SecureKey = WebSocketHelper.GetWebSocketKeyFromResponse(UpgradeGetRequest);

		if (SecureKey == string.Empty)
		{
			return false;
		}

		string ResponseKey = WebSocketHelper.GetAcceptKey(SecureKey);

		string Response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + ResponseKey + "\r\n\r\n";

		Client.Send(Encoding.Default.GetBytes(Response));

		return true;
	}

	public void DisconnectUser(WebSocketUser user)
	{
		OnUserDisconnect(this, new OnUserAction(user));
		user.ConnectedSocket.Shutdown(SocketShutdown.Both);
		user.ConnectedSocket.Close();
		Users.Remove(user);
	}

}