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
		return UserCollection.Find(u => u.LoginUsername == Username && u.LoginPassword == EncryptString(Password)).FirstOrDefault() != null;
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

	public bool AddUser(ChatUser user)
	{
		if (UserTaken(user))
		{
			return false;
		}
		else
		{
			user.LoginPassword = EncryptString(user.LoginPassword);
			UserCollection.InsertOne(user);
			return true;
		}
	}

	public void ReplaceUser(string Username, ChatUser user)
	{
		UserCollection.ReplaceOne(u => u.LoginUsername == Username, user);
	}

	public bool UpdateUser<T>(string username, string property, T new_property_value)
	{
		if(typeof(ChatUser).GetProperties().Select(prop => prop.Name).Contains(property))
		{
			UserCollection.UpdateOne(
					Builders<ChatUser>.Filter.Eq("LoginUsername", username),
					Builders<ChatUser>.Update.Set(property, new_property_value)
				);
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool UserTaken(ChatUser user)
	{
		return UsernameTaken(user.LoginUsername) || AliasTaken(user.Alias);
	}

	public bool RemoveUser(string Username)
	{
		if (UsernameTaken(Username))
		{
			UserCollection.DeleteOne(u => u.LoginUsername == Username);
			return true;
		}
		else
		{
			return false;
		}
	}

	public bool AliasTaken(string alias)
	{
		return UserCollection.Find(u => u.Alias == alias).FirstOrDefault() != null;
	}

	public ChatUser QueryUser(string username)
	{
		return UserCollection.Find(u => u.LoginUsername == username).FirstOrDefault();
	}

	public string EncryptString(string text)
	{
		using (var sha256 = new SHA256Managed())
		{
			return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
		}
	}

}