using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class GroupDatabase : Database
{
	private readonly IMongoCollection<ChatGroup> GroupCollection;

	public GroupDatabase(DatabaseConnection connection) : base(connection, "Chat_App")
	{
		GroupCollection = GetCollection<ChatGroup>("Groups");
	}
}
