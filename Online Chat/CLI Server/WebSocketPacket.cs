using System.Collections.Generic;

public class WebSocketPacket
{
	public List<WebSocketFrame> Frames { get; set; }

	public string Payload { get; private set; }

	public WebSocketOpCode PayloadOpCode { get; private set; }

	public WebSocketPacket(WebSocketFrame InitialPacket)
	{
		Frames = new List<WebSocketFrame> { };
		PayloadOpCode = InitialPacket.PayloadOpCode;
	}

	public void AddFrame(WebSocketFrame Frame)
	{
		Frames.Add(Frame);
		Payload += Frame.PayloadData;
	}

}
