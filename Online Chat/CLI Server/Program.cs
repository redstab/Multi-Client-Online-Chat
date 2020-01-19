using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;

public abstract class Chat { }

public class Group : Chat { }

public class Personal : Chat { }

public class User
{

}

public abstract class OnUserEvent : EventArgs
{
	public User AffectedUser { get; set; }

	public OnUserEvent(User user)
	{
		AffectedUser = user;
	}
}



public struct EndPoint
{
	string Address;
	int Port;

	public EndPoint(string Address, int Port)
	{
		this.Address = Address;
		this.Port = Port;
	}

	public IPEndPoint ToIpEndPoint()
	{
		return new IPEndPoint(IPAddress.Parse(Address), Port);
	}
}


public static class WebSocketHelper
{
	public static string GetWebSocketKeyFromResponse(string GetResponse)
	{
		string Key = "";

		string Pattern = @"(?<=Sec-WebSocket-Key:\s).*";

		Match KeyMatch = Regex.Match(GetResponse,Pattern);

		if (KeyMatch.Success)
		{
			Key = KeyMatch.Value.Trim('\r');
		}

		return Key;
	}

	public static string GetUpgradeRequest(Socket Client)
	{
		Client.ReceiveTimeout = 1000;
		byte[] ByteBuffer = new byte[1024];
		int BytesReceived = 0;
		try
		{
			BytesReceived = Client.Receive(ByteBuffer);
		}
		catch { }
		return Encoding.Default.GetString(ByteBuffer, 0, BytesReceived);
	}

	public static string GetAcceptKey(string SecWebSocketKey)
	{
		// https://tools.ietf.org/html/rfc6455#section-4.2.2

		string Hash = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		string Concatinated = SecWebSocketKey + Hash;

		byte[] HashedValue = new SHA1Managed().ComputeHash(Encoding.Default.GetBytes(Concatinated));

		string Base64Value = Convert.ToBase64String(HashedValue);

		return Base64Value;
	}

}

public class Server
{
	/// <summary>
	/// Socket för att hantera att nya anslutningar kommer in på servern
	/// </summary>
	public Socket ListenerSocket { get; set; }

	/// <summary>
	/// EndPoint för att beskriva ip och port som servern lyssnar på
	/// </summary>
	public EndPoint ListenAddress { get; set; }

	public bool Alive { get; set; }
	private ManualResetEvent UntilConnected = new ManualResetEvent(false);

	/// <summary>
	/// Konstruera en server som ska lyssna på en EndPoint
	/// </summary>
	/// <param name="ListenAddress"></param>
	public Server(EndPoint ListenAddress)
	{

		this.ListenAddress = ListenAddress;
		ListenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		Alive = true;
	}

	private void Log(string severity, string message)
	{
		Console.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]") + " [" + severity + "] " + message);
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
		Socket NewUser = ListenerSocket.EndAccept(Result);
		Log("INFO-VERBOSE", "Got the new connection socket");
		Log("INFO", "Allowing new connections");
		UntilConnected.Set();

		Log("INFO", "Handling WebSocket Upgrade");
		if (HandleWebSocketUpgrade(NewUser))
		{
			Log("INFO", "WebSocket Upgrade Successful");

		}
		else
		{
			Log("INFO", "WebSocket Upgrade Failed");
			NewUser.Send(Encoding.Default.GetBytes("Not a Websocket Connection"));
			NewUser.Shutdown(SocketShutdown.Both);
			NewUser.Close();
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

		if(SecureKey == string.Empty)
		{
			return false;
		}

		string ResponseKey = WebSocketHelper.GetAcceptKey(SecureKey);

		string Response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + ResponseKey + "\r\n\r\n";

		Client.Send(Encoding.Default.GetBytes(Response));

		return true;
	}

}

public class Program
{
	public static void Main()
	{

		EndPoint ListenAddress = new EndPoint("127.0.0.1", 8010);

		Server server = new Server(ListenAddress);

		server.StartServer();

	}
}