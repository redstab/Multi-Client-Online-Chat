using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

public static class WebSocketHelper
{
	public static string GetWebSocketKeyFromResponse(string GetResponse)
	{
		string Key = "";

		string Pattern = @"(?<=Sec-WebSocket-Key:\s).*";

		Match KeyMatch = Regex.Match(GetResponse, Pattern);

		if (KeyMatch.Success)
		{
			Key = KeyMatch.Value.Trim('\r');
		}

		return Key;
	}

	public static string GetUpgradeRequest(Socket Client)
	{
		Client.ReceiveTimeout = 1000;
		byte[] ByteBuffer = new byte[1024];
		int BytesReceived = 0;
		try
		{
			BytesReceived = Client.Receive(ByteBuffer);
		}
		catch { }
		Client.ReceiveTimeout = -1;
		return Encoding.Default.GetString(ByteBuffer, 0, BytesReceived).TrimEnd();
	}

	public static string GetAcceptKey(string SecWebSocketKey)
	{
		// https://tools.ietf.org/html/rfc6455#section-4.2.2

		string Hash = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		string Concatinated = SecWebSocketKey + Hash;

		byte[] HashedValue = new SHA1Managed().ComputeHash(Encoding.Default.GetBytes(Concatinated));

		string Base64Value = Convert.ToBase64String(HashedValue);

		return Base64Value;
	}

}