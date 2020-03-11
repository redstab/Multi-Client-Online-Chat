using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class UserDatabase : Database
{
	private readonly IMongoCollection<ChatUser> UserCollection;

	public UserDatabase(DatabaseConnection connection) : base(connection, "Chat_App")
	{
		UserCollection = GetCollection<ChatUser>("Users");
	}

	public bool Login(string Username, string Password)
	{
		return UserCollection.Find(u => u.LoginUsername == Username && u.Password == EncryptString(Password)).FirstOrDefault() != null;
	}

	public bool UsernameTaken(string Username)
	{
		return UserCollection.Find(u => u.LoginUsername == Username).FirstOrDefault() != null;
	}

	public bool AddUser(string Username, string Password, string Alias)
	{
		if (UsernameTaken(Username) || AliasTaken(Alias))
		{
			return false;
		}
		else
		{
			UserCollection.InsertOne(new ChatUser(Username, EncryptString(Password), Alias));
			return true;
		}
	}

	public void RemoveUser(string Username)
	{
		UserCollection.DeleteOne(u => u.LoginUsername == Username);
	}

	public bool AliasTaken(string alias)
	{
		return UserCollection.Find(u => u.Alias == alias).FirstOrDefault() != null;
	}

	private string EncryptString(string text)
	{
		using (var sha256 = new SHA256Managed())
		{
			return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
		}
	}

}