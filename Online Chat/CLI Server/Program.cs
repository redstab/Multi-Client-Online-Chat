using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


public abstract class Chat { }

public class Group : Chat { }

public class Personal : Chat { }

// State object for reading client data asynchronously  
public class StateObject
{
	// Client  socket.  
	public Socket workSocket = null;

	public string IPBUFFER;
	// Size of receive buffer.  
	public const int BufferSize = 1024;
	// Receive buffer.  
	public byte[] buffer = new byte[BufferSize];
	// Received data string.  
	public StringBuilder sb = new StringBuilder();
}

public class Server
{
	public static void SuppressException(Action a)
	{
		try{a();}
		catch (Exception) { }
	}
	public static List<StateObject> Clients = new List<StateObject>();
	public static Socket listener_socket;
	public static ManualResetEvent accepted = new ManualResetEvent(false);
	public static void AcceptCallback(IAsyncResult result)
	{
		accepted.Set();

		Socket listener = (Socket)result.AsyncState;
		Socket handler = listener.EndAccept(result);
		Console.WriteLine($"Connected {handler.RemoteEndPoint} successfully");
		StateObject state = new StateObject();
		state.workSocket = handler;
		state.IPBUFFER = handler.RemoteEndPoint.ToString();

		Clients.Add(state);

	}

	public static void ListenForUsers()
	{
		while (true)
		{
			accepted.Reset();
			listener_socket.BeginAccept(new AsyncCallback(AcceptCallback), listener_socket);
			accepted.WaitOne();
		}
	}

	public static void RecvPackets()
	{
		while (true)
		{
			for (int i = 0; i < Clients.Count; i++)
			{
				var client = Clients[i];
				if (client.workSocket.Connected)
				{
					SuppressException(() => {
						client.workSocket.BeginReceive(client.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), client);
					});
				}
				else
				{
					Console.WriteLine("Client {0} Disconnected", client.IPBUFFER);
					Clients.Remove(client);
				}
			}
		}
	}

	public static void Main()
	{
		string ip = "127.0.0.1";
		int port = 11000;

		IPEndPoint local_endpoint = new IPEndPoint(IPAddress.Parse(ip), port);

		listener_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		listener_socket.Bind(local_endpoint);

		listener_socket.Listen(100);

		Console.WriteLine("Server Listening");

		Thread users = new Thread(new ThreadStart(ListenForUsers));
		Thread packets = new Thread(new ThreadStart(RecvPackets));
		users.Start();
		packets.Start();
		int a = 0;

		while(a == 0)
		{
			string input =Console.ReadLine();
			if(input == "list")
			{
				foreach (var client in Clients)
				{
					Console.WriteLine("\tclient {0}", client.workSocket.RemoteEndPoint);
				}
			}
			else
			{
				a++;
			}
		}

		packets.Join();
		users.Join();
	}
	public static void ReadCallback(IAsyncResult ar)
	{
		String content = String.Empty;

		// Retrieve the state object and the handler socket  
		// from the asynchronous state object.  
		StateObject state = (StateObject)ar.AsyncState;
		Socket handler = state.workSocket;

		// Read data from the client socket.
		int bytesRead = 0;
		if (handler.Connected)
		{
			bytesRead = handler.EndReceive(ar);
		}

		if (bytesRead > 0)
		{
			// There  might be more data, so store the data received so far.  
			state.sb.Append(Encoding.ASCII.GetString(
				state.buffer, 0, bytesRead));

			// Check for end-of-file tag. If it is not there, read   
			// more data.  
			content = state.sb.ToString();
			if (content.IndexOf("<EOF>") > -1)
			{
				// All the data has been read from the   
				// client. Display it on the console.  
				Console.WriteLine("Read {0} bytes from socket. \n Data : {1}",
					content.Length, content);
				// Echo the data back to the client.  
				Send(handler, content);
			}
			else
			{
				// Not all data received. Get more.  
				handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
				new AsyncCallback(ReadCallback), state);
			}
		}
	}

	private static void Send(Socket handler, String data)
	{
		// Convert the string data to byte data using ASCII encoding.  
		byte[] byteData = Encoding.ASCII.GetBytes(data);

		// Begin sending the data to the remote device.  
		handler.BeginSend(byteData, 0, byteData.Length, 0,
			new AsyncCallback(SendCallback), handler);
	}

	private static void SendCallback(IAsyncResult ar)
	{
		try
		{
			// Retrieve the socket from the state object.  
			Socket handler = (Socket)ar.AsyncState;

			// Complete sending the data to the remote device.  
			int bytesSent = handler.EndSend(ar);
			Console.WriteLine("Sent {0} bytes to client.", bytesSent);

			handler.Shutdown(SocketShutdown.Both);
			handler.Close();

		}
		catch (Exception e)
		{
			Console.WriteLine(e.ToString());
		}
	}
}