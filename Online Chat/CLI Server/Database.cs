using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

public class Database
{
	private MongoClient Connection;
	private string ConnectionsString;
	private IMongoDatabase DatabaseReference;

	public Database(string ConnectString, string database)
	{
		ConnectionsString = ConnectString;
	}

	public void Connect()
	{
		Connection = new MongoClient(ConnectionsString);
	}

	//Syntax candy
	public void Reconnect()
	{
		Connect();
	}

	private IMongoDatabase OpenDatabase(string name)
	{
		DatabaseReference = Connection.GetDatabase(name);
	}

	private  OpenCollection()
	{

	}
}
