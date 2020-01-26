﻿using Newtonsoft.Json.Linq;
using System;
using System.Text;

public class Program
{
	public static void Main()
	{

		EndPoint ListenAddress = new EndPoint("127.0.0.1", 8010);

		Server server = new Server(ListenAddress);

		server.OnMessageReceive += (object sender, OnMessageAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint.ToString()} Sent packet containing a frame with data: {e.Payload.Payload}");
			//server.Send(e.Sender, (Encoding.UTF8.GetString(aes.Manager.Key), WebSocketOpCode.TextFrame));
		};

		server.OnMessageSend += (object sender, OnMessageAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint.ToString()} Sending {e.Payload.Payload}");
		};

		server.OnUserConnect += (object sender, OnUserAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint.ToString()} Connected to the server");
			//server.Send(e.Sender, (aes.SerilizeKey().ToString(), WebSocketOpCode.TextFrame));
		};

		server.OnUserDisconnect += (object sender, OnUserAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint.ToString()} Disconnected from the server");
		};

		server.StartServer();

	}
}
