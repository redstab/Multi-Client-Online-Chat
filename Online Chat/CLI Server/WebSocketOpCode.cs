﻿public enum WebSocketOpCode
{
	ContinuationFrame = 0,
	TextFrame = 1,
	BinaryFrame = 2,
	ConnectionCloseFrame = 8,
	PingFrame = 9,
	Pong = 10
}