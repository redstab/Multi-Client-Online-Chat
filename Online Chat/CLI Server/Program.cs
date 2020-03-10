using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

public class Program
{
	public static void Main()
	{

		DatabaseConnection ChatAppConnection = new DatabaseConnection("mongodb://localhost:27017");

		Database ChatApp = new Database(ChatAppConnection, "Chat_App");

		ChatApp.GetCollection<BsonDocument>("Users").InsertOne(new BsonDocument { { "name", "Jes" }, { "pwd", "temp" } });
		//Console.WriteLine(collection.ToJson());

		Console.Read();

		//db.GetCollection<BsonDocument>("Users").InsertOne(new BsonDocument { {"name", "Jes" }, { "pwd", "temp" } });

		//users.db.GetDatabase("ChatUsers").GetCollection<BsonDocument>("users").InsertOne(new BsonDocument { { "name", "jens" }, { "age", 12 } } );

		//EndPoint ListenAddress = new EndPoint("127.0.0.1", 8010);

		//Server server = new Server(ListenAddress);

		//server.OnMessageReceive += (object sender, OnMessageAction e) =>
		//{
		//	Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Sent packet containing a frame with data: {e.Payload.PayloadString}");

		//	e.Sender.SendJSON(JObject.Parse("{password: \"hello\", username:\"JENS\"}"), "text");
		//};

		//server.OnUserConnect += (object sender, OnUserAction e) =>
		//{
		//	Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Connected to the server");
		//	//server.Send(e.Sender, (aes.SerilizeKey().ToString(), WebSocketOpCode.TextFrame));
		//};

		//server.OnUserDisconnect += (object sender, OnUserAction e) =>
		//{
		//	Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Disconnected from the server");
		//};

		//server.StartServer();

	}
}
