using System.Net;
public struct EndPoint
{
	string Address;
	int Port;

	public EndPoint(string Address, int Port)
	{
		this.Address = Address;
		this.Port = Port;
	}

	public IPEndPoint ToIpEndPoint()
	{
		return new IPEndPoint(IPAddress.Parse(Address), Port);
	}
}