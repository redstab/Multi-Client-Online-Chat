using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;

using PacketManagerItems = System.Collections.Generic.Dictionary<string, System.Func<OnMessageAction, ChatServer, Newtonsoft.Json.Linq.JObject, PacketResponse>>;

public struct PacketResponse
{
	public bool ShouldSend;
	public string Type;
	public JObject Response;
}

class ChatServer
{

	private Server WebSocketServer;

	public UserDatabase UserDB { get; set; }

	public PacketManagerItems PacketManager { get; set; }

	public ChatServer(EndPoint ListenAddress, DatabaseConnection Connection, PacketManagerItems PacketManager)
	{
		WebSocketServer = new Server(ListenAddress);
		WebSocketServer.OnMessageReceive += WebSocketServer_OnMessageReceive;
		WebSocketServer.OnUserConnect += WebSocketServer_OnUserConnect;
		WebSocketServer.OnUserDisconnect += WebSocketServer_OnUserDisconnect;
		UserDB = new UserDatabase(Connection);
		this.PacketManager = PacketManager;
	}

	private void WebSocketServer_OnUserDisconnect(object sender, OnUserAction e)
	{

	}

	private void WebSocketServer_OnUserConnect(object sender, OnUserAction e)
	{
		Console.WriteLine($"{e.Sender.Socket.ConnectedSocket.RemoteEndPoint} Connected as a new user, giving user 5 sec for authentication");
		Task.Run(() => WaitForAuthentication(e.Sender));
	}

	private void WebSocketServer_OnMessageReceive(object sender, OnMessageAction e)
	{
		PacketResponse HandledPacket = HandlePacket(e, JObject.Parse(e.Payload.PayloadString));

		if (HandledPacket.ShouldSend)
		{
			e.Sender.Send(HandledPacket.Response, HandledPacket.Type);
		}
	}

	private PacketResponse HandlePacket(OnMessageAction e, JObject packet)
	{
		string Type = packet["Type"].ToString();

		PacketResponse Response = new PacketResponse
		{
			ShouldSend = false
		};

		if (PacketManager.ContainsKey(Type))
		{
			Console.WriteLine($"{e.Sender.Socket.ConnectedSocket.RemoteEndPoint} Sent packet with type: {Type}");
			Response = PacketManager[Type](e, this, packet);
		}
		else
		{
			Console.WriteLine($"{e.Sender.Socket.ConnectedSocket.RemoteEndPoint} Sent packet containing unknown type: {Type}");
		}

		return Response;
	}

	private void WaitForAuthentication(ChatUser user)
	{
		Console.WriteLine($"Before Authentication period user has username => {user.LoginUsername}");
		Thread.Sleep(10000);
		Console.WriteLine($"Authentication period over user has username => {user.LoginUsername}");
	}

	public void StartServer()
	{
		WebSocketServer.StartServer();
	}
}