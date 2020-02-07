using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;

public class WebSocketUser
{
	public string Username { get; set; }
	public int ID { get; set; }
	public Socket ConnectedSocket { get; set; }
	public Server ConnectedServer { get; set; }
	private byte[] ReceiveBuffer;
	private WebSocketPacket CurrentPacket { get; set; }

	public AES EncryptionManager;

	public WebSocketUser(Socket ConnectedSocket, Server ConnectedServer)
	{
		this.ConnectedSocket = ConnectedSocket;
		this.ConnectedServer = ConnectedServer;
		ReceiveBuffer = new byte[2];
		ConnectedSocket.BeginReceive(ReceiveBuffer, 0, 2, SocketFlags.None, ReceiveMessageCallback, null);
	}

	public void InitilizeEncryption(byte[] Key)
	{
		EncryptionManager = new AES(Key);

	}

	public void InitilizeEncryption()
	{
		EncryptionManager = new AES();
	}

	public void SendFrame(string Data, WebSocketOpCode OpCode, bool LastFrame)
	{
		if (ConnectedSocket.Connected)
		{
			WebSocketFrame Frame = new WebSocketFrame(Data, OpCode);
			byte[] FrameBuffer = Frame.GetRawBuffer(LastFrame);
			ConnectedSocket.BeginSend(FrameBuffer, 0, FrameBuffer.Length, SocketFlags.None, SendMessageCallback, null);
		}
	}

	public void SendFrames(List<string> Data, WebSocketOpCode OpCode)
	{
		// Skicka initiella framen
		SendFrame(Data[0], OpCode, false);

		for (int i = 1; i < Data.Count - 1; i++)
		{
			SendFrame(Data[i], WebSocketOpCode.ContinuationFrame, false);
		}

		//Skicka sista framen
		SendFrame(Data[Data.Count - 1], WebSocketOpCode.ContinuationFrame, true);
	}

	public void SendJSON(JObject json, string type, bool encrypted)
	{

		JObject Packet = new JObject();
		Packet["Type"] = type;
		if (encrypted)
		{
			EncryptionManager.Manager.GenerateIV();
			Packet["IV"] = EncryptionManager.Manager.IV;
			Packet["Packet"] = EncryptionManager.Encrypt(json.ToString());
		}
		else
		{
			Packet["IV"] = "blank";
			Packet["Packet"] = json;
		}

		SendFrame(Packet.ToString(), WebSocketOpCode.TextFrame, true);
	}

	public void SendText(string Payload)
	{
		JObject packet = new JObject();
		packet["Sträng"] = Payload;
		SendJSON(packet, "text");
	}

	public void SendJSON(JObject Packet, string DataType)
	{
		JObject packet = new JObject();

		EncryptionManager.Manager.GenerateIV();

		var EncryptionData = EncryptionManager.Encrypt(Packet.ToString());

		packet["Type"] = DataType;

		packet["IV"] = EncryptionManager.Manager.IV;

		packet["Packet"] = EncryptionData;

		Console.WriteLine(packet);

		SendFrame(packet.ToString(), WebSocketOpCode.TextFrame, true);
	}

	public bool isEncrypted()
	{
		return EncryptionManager != null;
	}

	private void SendMessageCallback(IAsyncResult Result)
	{
		ConnectedSocket.EndSend(Result);
	}

	private void ReceiveMessageCallback(IAsyncResult Result)
	{

		ConnectedSocket.EndReceive(Result);

		WebSocketFrame CurrentFrame = new WebSocketFrame(ReceiveBuffer, ConnectedSocket);

		CurrentFrame.ReceivePayload(ConnectedSocket);

		if (CurrentFrame.PayloadOpCode == WebSocketOpCode.ConnectionCloseFrame)
		{
			ConnectedServer.DisconnectUser(this);
			return;
		}

		if (CurrentPacket == null)
		{
			CurrentPacket = new WebSocketPacket(CurrentFrame);
		}

		CurrentPacket.AddFrame(CurrentFrame);

		if (CurrentFrame.HeaderFrame.FinalPacket)
		{
			ConnectedServer.Receive(this, CurrentPacket.Payload, CurrentPacket.PayloadOpCode);
			CurrentPacket = null;
			GC.Collect();
		}

		Array.Clear(ReceiveBuffer, 0, ReceiveBuffer.Length);

		ConnectedSocket.BeginReceive(ReceiveBuffer, 0, 2, SocketFlags.None, ReceiveMessageCallback, null);
	}
}