using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;

public class DatabaseConnection
{
	private MongoClient Connection;
	private string ConnectionString;

	public DatabaseConnection(string ConnectString)
	{
		ConnectionString = ConnectString;
		Connect();
	}
	
	public void Connect()
	{
		Connection = new MongoClient(ConnectionString);
	}

	//Syntax candy
	public void Reconnect()
	{
		Connect();
	}

	public IMongoDatabase OpenDatabase(string name)
	{
		return Connection.GetDatabase(name);
	}
}
