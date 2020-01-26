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

	public void InitilizeEncryption(byte[] Key, byte[] IV)
	{
		EncryptionManager = new AES(Key, IV);
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

	public void SendText(string Text)
	{
		SendFrame(Text, WebSocketOpCode.TextFrame, true);
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