using System;
using System.Net.Sockets;

// Base Framing Protocol for WebSockets - https://tools.ietf.org/html/rfc6455#section-5.2
//   0                   1                   2                   3
//   0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
//  +-+-+-+-+-------+-+-------------+-------------------------------+
//  |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
//  |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
//  |N|V|V|V|       |S|             |   (if payload len==126/127)   |
//  | |1|2|3|       |K|             |                               |
//  +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
//  |     Extended payload length continued, if payload len == 127  |
//  + - - - - - - - - - - - - - - - +-------------------------------+
//  |                               |Masking-key, if MASK set to 1  |
//  +-------------------------------+-------------------------------+
//  | Masking-key (continued)       |          Payload Data         |
//  +-------------------------------- - - - - - - - - - - - - - - - +
//  :                     Payload Data continued ...                :
//  + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
//  |                     Payload Data continued ...                |
//  +---------------------------------------------------------------+

public class WebSocketHeaderFrame
{
	public int FIN { get; set; } // Sista paket i ett meddelande ?
	public int RSV1 { get; set; }
	public int RSV2 { get; set; }
	public int RSV3 { get; set; }
	public int MASK { get; set; }
	public byte[] MaskKey { get; set; } // Nyckel för enkodningen av paketet
	public long PayloadLength { get; set; } // Hur stort payload som skickades
	public WebSocketOpCode OpCode { get; set; } // Vilken typ av payload som skickades
	public bool FinalPacket { get { return FIN == 1; } } // Wrapper för FIN

	public void Deserilize(byte[] InputBuffer, Socket ConnectedSocket)
	{
		ParseHeader(InputBuffer);
		GetRemaindingHeader(ConnectedSocket);
	}

	public byte[] Serilize()
	{
		int Size = (int)PayloadLength; //Basecase Storleken får plats i först byten (7bits)
		int HeaderByteSize = 2;
		if (PayloadLength > 125)
		{
			if (PayloadLength < Math.Pow(2, 16)) // Storleken får plats i 2 bytes
			{
				Size = 126;
				HeaderByteSize += 2;
			}
			else if (PayloadLength < Math.Pow(2, 64)) // Storleken får plats i 8 bytes
			{
				Size = 127;
				HeaderByteSize += 8;
			}
		}

		byte[] Header = new byte[HeaderByteSize];

		Header[0] = (byte)(0b00000000 | ((FinalPacket ? 1 : 0) << 7) | (int)OpCode); // FIN och OpCode i första byten

		Header[1] = (byte)(0b00000000 | Size); // Mask och payload size id i andra byten

		if (PayloadLength > 125)
		{
			for (int i = 2; i < Header.Length; i++)
			{
				int shiftamnt = (HeaderByteSize - 2) * 8 - ((i - 1) * 8);
				Header[i] = (byte)((PayloadLength & (0b11111111 << shiftamnt)) >> shiftamnt);
			}
		}

		return Header;
	}

	private void GetRemaindingHeader(Socket ConnectedSocket)
	{
		// Extrahera ints genom att ta emot k bits i big endian (tex ta emot 2 bytes för en Int16 eller ta emot 8 bytes för en Int64)

		/* ex bytes: 00000000 00000000 00000000 11110000
		 * för att lägga ihop flera bytes så skiftar man byten till sin binära position
		 * första byten måste skiftas k bits för att få korrekt värde
		 * Resterande skiftas k - 8n bits för att dem ska representera sina värden
		 * Jag använder bitwise OR för att appenda bits till nyckeln
		 * 
		 * Ex:
		 *  (| = Bitwise OR, << = bitvis skifte till vänster, a |= b => a = a | b)
		 *  buffer = { 00100000, 00011000, 00000110, 11110000}
		 *		=> 
		 *	Start genom att allokera resultatet till 0
		 *	00000000 00000000 00000000 00000000 = r
		 *	
		 *	i = 0
		 *	
		 *	buffer[i] << k - 8*i = 00100000 00000000 00000000 00000000 = s
		 *	
		 *  r |= s 
		 *  
		 *  => r = 00100000 00000000 00000000 00000000
		 *  
		 *  i = 1
		 *  
		 *  buffer[i] << k - 8*1 = 00011000 00000000 00000000
		 *  
		 *  r |= s
		 *  
		 *  =>  r = 00100000 00011000 00000000 00000000
		 *  
		 *  ...
		 *  
		 *  => 
		 *  (TLRD) (| = Bitwise OR)
		 *  00100000 00000000 00000000 00000000
		 *  |        00011000 00000000 00000000
		 *           |        00000110 00000000
		 *                    |        11110000
		 *  ___________________________________
		 *  00100000 00011000 00000110 11110000
		 */

		int HeaderByteSize = 0;

		if (PayloadLength == 126)
		{
			HeaderByteSize = 2; // Payload storleken är 2 bytes (int16)
		}
		else if (PayloadLength == 127)
		{
			HeaderByteSize = 8; // Payload storleken är 8 bytes (long/int64)
		}

		byte[] HeaderBuffer = new byte[HeaderByteSize + (MASK == 1 ? 4 : 0)]; // allokera buffer för payload storleken och masknyckeln (4 bytes) om MASK == 1

		ConnectedSocket.Receive(HeaderBuffer); // ta emot storleken

		if (PayloadLength == 126 || PayloadLength == 127) // om vi måste ta emot en större storlek
		{
			PayloadLength = 0; // reset

			for (int i = 0; i < HeaderByteSize; i++)
			{
				// Se topp kommentar (lägger ihop många bytes till en storlek)
				PayloadLength |= (long)(HeaderBuffer[i] << (8 * HeaderByteSize) - ((i + 1) * 8));
			}
		}

		if (MASK == 1) // Om vi ska ta emot en masknyckel
		{
			MaskKey = new byte[4];
			for (int i = 0; i < 4; i++)
			{
				// Lägger masknyckeln i sin buffer
				MaskKey[i] = HeaderBuffer[i + HeaderByteSize];
			}
		}

	}

	private void ParseHeader(byte[] Buffer)
	{

		//Extrahera värden från WebSocket paketet (Se klass kommentar för paket struktur)

		FIN = (Buffer[0] & 0b10000000) >> 7;
		// Sista paket i ett meddelande ?

		RSV1 = (Buffer[0] & 0b01000000) >> 6;
		RSV2 = (Buffer[0] & 0b00100000) >> 5;
		RSV3 = (Buffer[0] & 0b00010000) >> 4;
		// Reserverade värden för unika implementeringar

		OpCode = (WebSocketOpCode)(Buffer[0] & 0b00001111);
		// Vilken typ av payload paketet innehåller

		MASK = (Buffer[1] & 0b10000000) >> 7;
		// Om paketet är enkodat med en mask

		PayloadLength = Buffer[1] & 0b01111111;
		// Hur stort payload har paketet (om storleken är mellan 0-125 så används 7 bits för att avläsa storlek)
	}
}