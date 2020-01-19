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

public enum WebSocketOpCode
{
	ContinuationFrame = 0,
	TextFrame = 1,
	BinaryFrame = 2,
	ConnectionCloseFrame = 8,
	PingFrame = 9,
	Pong = 10
}

// Base Framing Protocol for WebSockets - https://tools.ietf.org/html/rfc6455#section-5.2
//   0                   1                   2                   3
//   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//  +-+-+-+-+-------+-+-------------+-------------------------------+
//  |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
//  |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
//  |N|V|V|V|       |S|             |   (if payload len==126/127)   |
//  | |1|2|3|       |K|             |                               |
//  +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
//  |     Extended payload length continued, if payload len == 127  |
//  + - - - - - - - - - - - - - - - +-------------------------------+
//  |                               |Masking-key, if MASK set to 1  |
//  +-------------------------------+-------------------------------+
//  | Masking-key (continued)       |          Payload Data         |
//  +-------------------------------- - - - - - - - - - - - - - - - +
//  :                     Payload Data continued ...                :
//  + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
//  |                     Payload Data continued ...                |
//  +---------------------------------------------------------------+

public class WebSocketHeaderFrame
{
	public int FIN { get; private set; } // Sista paket i ett meddelande ?
	public int RSV1 { get; private set; }
	public int RSV2 { get; private set; }
	public int RSV3 { get; private set; }
	public int MASK { get; private set; }
	public byte[] MaskKey { get; private set; } // Nyckel för enkodningen av paketet
	public long PayloadLength { get; private set; } // Hur stort payload som skickades
	public WebSocketOpCode OpCode { get; private set; } // Vilken typ av payload som skickades
	public bool FinalPacket { get { return FIN == 1; } } // Wrapper för FIN
	public WebSocketHeaderFrame(byte[] InputBuffer, Socket ConnectedSocket)
	{
		ParseHeader(InputBuffer);
		GetRemaindingHeader(ConnectedSocket);
	}

	private void GetRemaindingHeader(Socket ConnectedSocket)
	{
		// Extrahera ints genom att ta emot k bits i big endian (tex ta emot 2 bytes för en Int16 eller ta emot 8 bytes för en Int64)

		/* ex bytes: 00000000 00000000 00000000 11110000
		 * för att lägga ihop flera bytes så skiftar man byten till sin binära position
		 * första byten måste skiftas k bits för att få korrekt värde
		 * Resterande skiftas k - 8n bits för att dem ska representera sina värden
		 * Jag använder bitwise OR för att appenda bits till nyckeln
		 * 
		 * Ex:
		 *  (| = Bitwise OR, << = bitvis skifte till vänster, a |= b => a = a | b)
		 *  buffer = { 00100000, 00011000, 00000110, 11110000}
		 *		=> 
		 *	Start genom att allokera resultatet till 0
		 *	00000000 00000000 00000000 00000000 = r
		 *	
		 *	i = 0
		 *	
		 *	buffer[i] << k - 8*i = 00100000 00000000 00000000 00000000 = s
		 *	
		 *  r |= s 
		 *  
		 *  => r = 00100000 00000000 00000000 00000000
		 *  
		 *  i = 1
		 *  
		 *  buffer[i] << k - 8*1 = 00011000 00000000 00000000
		 *  
		 *  r |= s
		 *  
		 *  =>  r = 00100000 00011000 00000000 00000000
		 *  
		 *  ...
		 *  
		 *  => 
		 *  (TLRD) (| = Bitwise OR)
		 *  00100000 00000000 00000000 00000000
		 *  |        00011000 00000000 00000000
		 *           |        00000110 00000000
		 *                    |        11110000
		 *  ___________________________________
		 *  00100000 00011000 00000110 11110000
		 */

		int HeaderByteSize = 0;

		if (PayloadLength == 126) 
		{
			HeaderByteSize = 2; // Payload storleken är 2 bytes (int16)
		}else if(PayloadLength == 127)
		{
			HeaderByteSize = 8; // Payload storleken är 8 bytes (long/int64)
		}

		byte[] HeaderBuffer = new byte[HeaderByteSize + (MASK == 1 ? 4 : 0)]; // allokera buffer för payload storleken och masknyckeln (4 bytes) om MASK == 1

		ConnectedSocket.Receive(HeaderBuffer); // ta emot storleken
		
		if(PayloadLength == 126 || PayloadLength == 127) // om vi måste ta emot en större storlek
		{
			PayloadLength = 0; // reset

			for(int i = 0; i < HeaderByteSize; i++)
			{
				// Se topp kommentar (lägger ihop många bytes till en storlek)
				PayloadLength |= (long)(HeaderBuffer[i] << (8 * HeaderByteSize) - ((i + 1) * 8));
			}
		}

		if(MASK == 1) // Om vi ska ta emot en masknyckel
		{
			MaskKey = new byte[4];
			for(int i = 0; i < 4; i++)
			{
				// Lägger masknyckeln i sin buffer
				MaskKey[i] = HeaderBuffer[i + HeaderByteSize];
			}
		}

	}

	private void ParseHeader(byte[] Buffer)
	{

		//Extrahera värden från WebSocket paketet (Se klass kommentar för paket struktur)

		FIN = (Buffer[0] & 0b10000000) >> 7; 
		// Sista paket i ett meddelande ?

		RSV1 = (Buffer[0] & 0b01000000) >> 6; 
		RSV2 = (Buffer[0] & 0b00100000) >> 5;
		RSV3 = (Buffer[0] & 0b00010000) >> 4;
		// Reserverade värden för unika implementeringar

		OpCode = (WebSocketOpCode)(Buffer[0] & 0b00001111);
		// Vilken typ av payload paketet innehåller

		MASK = (Buffer[1] & 0b10000000) >> 7;
		// Om paketet är enkodat med en mask

		PayloadLength = Buffer[1] & 0b01111111;
		// Hur stort payload har paketet (om storleken är mellan 0-125 så används 7 bits för att avläsa storlek)
	}
}

public class WebSocketFrame
{
	public WebSocketHeaderFrame HeaderFrame { get; private set; }
	public string PayloadData { get; private set; }
	public WebSocketOpCode PayloadOpCode { get; private set; }

	public WebSocketFrame(byte[] Header, Socket ClientSocket)
	{
		HeaderFrame = new WebSocketHeaderFrame(Header, ClientSocket);
	}

	public void ReceivePayload(Socket ClientSocket)
	{
		byte[] PayloadBuffer = new byte[HeaderFrame.PayloadLength];
		ClientSocket.Receive(PayloadBuffer);
		for(int i = 0; i < PayloadBuffer.Length; i++)
		{
			PayloadBuffer[i] = (byte)(PayloadBuffer[i] ^ HeaderFrame.MaskKey[i % 4]);
		}
		PayloadData = Encoding.Default.GetString(PayloadBuffer);
		PayloadOpCode = HeaderFrame.OpCode;
	}
}

public class WebSocketPacket
{
	public List<WebSocketFrame> Frames { get; set; }

	public string Payload { get; private set; }

	public WebSocketOpCode PayloadOpCode { get; private set;}

	public WebSocketPacket(WebSocketFrame InitialPacket)
	{
		Frames = new List<WebSocketFrame> { };
		PayloadOpCode = InitialPacket.PayloadOpCode;
	}

	public void AddFrame(WebSocketFrame Frame)
	{
		Frames.Add(Frame);
		Payload += Frame.PayloadData;
	}

}

public class User
{
	public string Username { get; set; }
	public int ID { get; set; }
	public Socket ConnectedSocket { get; set; }
	public Server ConnectedServer { get; set; }
	private byte[] ReceiveBuffer;
	private WebSocketPacket CurrentPacket { get; set; }
	public User(Socket ConnectedSocket, Server ConnectedServer)
	{
		this.ConnectedSocket = ConnectedSocket;
		this.ConnectedServer = ConnectedServer;
		ReceiveBuffer = new byte[2];
		ConnectedSocket.BeginReceive(ReceiveBuffer, 0, 2, SocketFlags.None, ReceiveMessageCallback, null);
	}

	private void ReceiveMessageCallback(IAsyncResult Result)
	{

		ConnectedSocket.EndReceive(Result);

		//TODO Implement RSA Encryption HERE

		WebSocketFrame CurrentFrame = new WebSocketFrame(ReceiveBuffer, ConnectedSocket);

		CurrentFrame.ReceivePayload(ConnectedSocket);

		if(CurrentFrame.PayloadOpCode == WebSocketOpCode.ConnectionCloseFrame)
		{
			ConnectedServer.DisconnectUser(this);
			return;
		}

		if(CurrentPacket == null)
		{
			CurrentPacket = new WebSocketPacket(CurrentFrame);
		}

		CurrentPacket.AddFrame(CurrentFrame);

		if (CurrentFrame.HeaderFrame.FinalPacket)
		{
			Console.WriteLine(
				$"Packet {CurrentPacket.Frames.Count} frames\nPayload: {CurrentPacket.Payload.Length}\nOpcode: {CurrentPacket.PayloadOpCode}\n"
			);
			CurrentPacket = null;
			GC.Collect();
		}

		Array.Clear(ReceiveBuffer, 0, ReceiveBuffer.Length);

		GC.Collect();
		ConnectedSocket.BeginReceive(ReceiveBuffer, 0, 2, SocketFlags.None, ReceiveMessageCallback, null);
	}
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

		Match KeyMatch = Regex.Match(GetResponse, Pattern);

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
		Client.ReceiveTimeout = -1;
		return Encoding.Default.GetString(ByteBuffer, 0, BytesReceived).TrimEnd();
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
	public List<User> Users = new List<User>();
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
		Socket NewSocket = ListenerSocket.EndAccept(Result);
		Log("INFO-VERBOSE", "Got the new connection socket");
		Log("INFO", "Allowing new connections");
		UntilConnected.Set();

		Log("INFO", "Handling WebSocket Upgrade");
		if (HandleWebSocketUpgrade(NewSocket))
		{
			Log("INFO", "WebSocket Upgrade Successful");
			Log("INFO", "Added User to UserList");
			Users.Add(new User(NewSocket, this));
		}
		else
		{
			Log("INFO", "WebSocket Upgrade Failed");
			NewSocket.Send(Encoding.Default.GetBytes("Not a Websocket Connection"));
			NewSocket.Shutdown(SocketShutdown.Both);
			NewSocket.Close();
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

	public void DisconnectUser(User user)
	{
		user.ConnectedSocket.Shutdown(SocketShutdown.Both);
		user.ConnectedSocket.Close();
		Users.Remove(user);
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
