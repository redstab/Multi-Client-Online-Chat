using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Program
{
	public static void Main()
	{

		//DatabaseConnection ChatAppConnection = new DatabaseConnection("mongodb://localhost:27017");

		//UserDatabase Users = new UserDatabase(ChatAppConnection);

		//Console.WriteLine($"User Add => {Users.AddUser(new ChatUser("jens@corp.com", "hunter2", "472"))}");
		//Console.WriteLine($"User Login => {Users.Login("jens@corp.com", "hunter2")}");
		//Console.WriteLine($"User Update => {Users.UpdateUser("jens@corp.com", "Alias", "472.1")}");
		////Console.WriteLine($"User Remove => {Users.RemoveUser("jens@corp.com")}");


		//////Console.WriteLine(ChatUsers.Login("4712", "hunter2"));

		//Console.WriteLine(ChatUsers.AddUser("4712", "hunter2", "4722222"));
		//Console.WriteLine(ChatUsers.AddUser("47123", "hunter3", "4722223"));
		//Console.WriteLine(ChatUsers.AddUser("47124", "hunter4", "4722224"));

		//Console.WriteLine(ChatUsers.Login("4712", "hunter2"));

		//Console.WriteLine(ChatUsers.UsernameTaken("4712"));
		//Console.WriteLine(ChatUsers.AliasTaken("4722222"));

		//ChatUsers.RemoveUser("4712");

		//UserDatabase ChatApp = new Database(ChatAppConnection, "Chat_App");

		//Console.WriteLine(collection.ToJson());

		//Console.Read();

		//db.GetCollection<BsonDocument>("Users").InsertOne(new BsonDocument { {"name", "Jes" }, { "pwd", "temp" } });

		//users.db.GetDatabase("ChatUsers").GetCollection<BsonDocument>("users").InsertOne(new BsonDocument { { "name", "jens" }, { "age", 12 } } );

		EndPoint ListenAddress = new EndPoint("127.0.0.1", 8010);

		Server server = new Server(ListenAddress);

		server.OnMessageReceive += (object sender, OnMessageAction e) =>
		{
			JObject Packet = JObject.Parse(e.Payload.PayloadString);
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Sent packet containing a frame with data: {Packet}");
		};

		server.OnUserConnect += (object sender, OnUserAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Connected to the server");
			//server.Send(e.Sender, (aes.SerilizeKey().ToString(), WebSocketOpCode.TextFrame));
		};

		server.OnUserDisconnect += (object sender, OnUserAction e) =>
		{
			Console.WriteLine($"{e.Sender.ConnectedSocket.RemoteEndPoint} Disconnected from the server");
		};

		server.StartServer();

	}
}
