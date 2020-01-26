using System.Net.Sockets;
using System.Text;
public class WebSocketFrame
{
	public WebSocketHeaderFrame HeaderFrame { get; set; }
	public string PayloadData { get; set; }
	public WebSocketOpCode PayloadOpCode { get; set; }

	public WebSocketFrame(byte[] Header, Socket ClientSocket)
	{
		HeaderFrame = new WebSocketHeaderFrame();
		HeaderFrame.Deserilize(Header, ClientSocket);
	}

	public WebSocketFrame(string Data, WebSocketOpCode OpCode)
	{
		PayloadData = Data;
		PayloadOpCode = OpCode;
	}

	public byte[] GetRawBuffer(bool FinalPacket)
	{
		HeaderFrame = new WebSocketHeaderFrame
		{
			FIN = FinalPacket ? 1 : 0,
			OpCode = PayloadOpCode,
			MASK = 0,
			PayloadLength = PayloadData.Length,
			RSV1 = 0,
			RSV2 = 0,
			RSV3 = 0
		};
		PayloadData = Encoding.Default.GetString(HeaderFrame.Serilize()) + PayloadData;

		return Encoding.Default.GetBytes(PayloadData);
	}

	public void ReceivePayload(Socket ClientSocket)
	{
		byte[] PayloadBuffer = new byte[HeaderFrame.PayloadLength];
		ClientSocket.Receive(PayloadBuffer);
		for (int i = 0; i < PayloadBuffer.Length; i++)
		{
			PayloadBuffer[i] = (byte)(PayloadBuffer[i] ^ HeaderFrame.MaskKey[i % 4]);
		}
		PayloadData = Encoding.Default.GetString(PayloadBuffer);
		PayloadOpCode = HeaderFrame.OpCode;
	}
}