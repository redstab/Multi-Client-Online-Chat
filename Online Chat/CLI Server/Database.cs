using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class Database
{
	private DatabaseConnection dbConnection;

	private IMongoDatabase DatabaseReference;

	public Database(DatabaseConnection connection, string dbName)
	{
		dbConnection = connection;
		DatabaseReference = dbConnection.OpenDatabase(dbName);
	}

	public IMongoCollection<T> GetCollection<T>(string name)
	{
		return DatabaseReference.GetCollection<T>(name);
	}
}
