using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

public class ChatUser
{
	public ObjectId Id { get; set; }

	public ChatUser(string username, string password)
	{
		LoginUsername = username;
		LoginPassword = password;
	}

	public ChatUser(string username, string password, string alias)
	{
		LoginUsername = username;
		LoginPassword = password;
		Alias = alias;
	}

	[BsonIgnore] public WebSocketUser Socket { get; set; }

	public string LoginUsername { get; set; } // Email or username

	public string LoginPassword { get; set; }

	public string Alias { get; set; }

	public List<List<string>> DirectMessageGroups { get; set; } // alias of users in particular dm group (small messsagegroup)
}