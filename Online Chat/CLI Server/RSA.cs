using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class RSA
{

	public RSACryptoServiceProvider Manager = new RSACryptoServiceProvider(2048);

	public RSA(){}

	public RSA(string pem, bool key)
	{
		if (key)
		{
			ImportPublicKey(pem);
		}
		else
		{
			ImportPrivateKey(pem);
		}
	}

	public RSA(string publicKey, string privateKey)
	{
		ImportPublicKey(publicKey);
		ImportPrivateKey(privateKey);
	}

	public void ImportPrivateKey(string pem)
	{
		PemReader pr = new PemReader(new StringReader(pem));
		AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
		RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)KeyPair.Private);

		Manager.ImportParameters(rsaParams);
	}

	public void ImportPublicKey(string pem)
	{
		PemReader pr = new PemReader(new StringReader(pem));
		AsymmetricKeyParameter publicKey = (AsymmetricKeyParameter)pr.ReadObject();
		RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaKeyParameters)publicKey);
		Manager.ImportParameters(rsaParams);
	}

	public string ExportPrivateKey()
	{
		StringWriter outputStream = new StringWriter();
		if (Manager.PublicOnly) throw new ArgumentException("CSP does not contain a private key", "csp");
		var parameters = Manager.ExportParameters(true);
		using (var stream = new MemoryStream())
		{
			var writer = new BinaryWriter(stream);
			writer.Write((byte)0x30); // SEQUENCE
			using (var innerStream = new MemoryStream())
			{
				var innerWriter = new BinaryWriter(innerStream);
				EncodeIntegerBigEndian(innerWriter, new byte[] { 0x00 }); // Version
				EncodeIntegerBigEndian(innerWriter, parameters.Modulus);
				EncodeIntegerBigEndian(innerWriter, parameters.Exponent);
				EncodeIntegerBigEndian(innerWriter, parameters.D);
				EncodeIntegerBigEndian(innerWriter, parameters.P);
				EncodeIntegerBigEndian(innerWriter, parameters.Q);
				EncodeIntegerBigEndian(innerWriter, parameters.DP);
				EncodeIntegerBigEndian(innerWriter, parameters.DQ);
				EncodeIntegerBigEndian(innerWriter, parameters.InverseQ);
				var length = (int)innerStream.Length;
				EncodeLength(writer, length);
				writer.Write(innerStream.GetBuffer(), 0, length);
			}

			var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
			// WriteLine terminates with \r\n, we want only \n
			outputStream.Write("-----BEGIN RSA PRIVATE KEY-----\n");
			// Output as Base64 with lines chopped at 64 characters
			for (var i = 0; i < base64.Length; i += 64)
			{
				outputStream.Write(base64, i, Math.Min(64, base64.Length - i));
				outputStream.Write("\n");
			}
			outputStream.Write("-----END RSA PRIVATE KEY-----");
		}

		return outputStream.ToString();
	}

	public string ExportPublicKey()
	{
		StringWriter outputStream = new StringWriter();
		var parameters = Manager.ExportParameters(false);
		using (var stream = new MemoryStream())
		{
			var writer = new BinaryWriter(stream);
			writer.Write((byte)0x30); // SEQUENCE
			using (var innerStream = new MemoryStream())
			{
				var innerWriter = new BinaryWriter(innerStream);
				innerWriter.Write((byte)0x30); // SEQUENCE
				EncodeLength(innerWriter, 13);
				innerWriter.Write((byte)0x06); // OBJECT IDENTIFIER
				var rsaEncryptionOid = new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01 };
				EncodeLength(innerWriter, rsaEncryptionOid.Length);
				innerWriter.Write(rsaEncryptionOid);
				innerWriter.Write((byte)0x05); // NULL
				EncodeLength(innerWriter, 0);
				innerWriter.Write((byte)0x03); // BIT STRING
				using (var bitStringStream = new MemoryStream())
				{
					var bitStringWriter = new BinaryWriter(bitStringStream);
					bitStringWriter.Write((byte)0x00); // # of unused bits
					bitStringWriter.Write((byte)0x30); // SEQUENCE
					using (var paramsStream = new MemoryStream())
					{
						var paramsWriter = new BinaryWriter(paramsStream);
						EncodeIntegerBigEndian(paramsWriter, parameters.Modulus); // Modulus
						EncodeIntegerBigEndian(paramsWriter, parameters.Exponent); // Exponent
						var paramsLength = (int)paramsStream.Length;
						EncodeLength(bitStringWriter, paramsLength);
						bitStringWriter.Write(paramsStream.GetBuffer(), 0, paramsLength);
					}
					var bitStringLength = (int)bitStringStream.Length;
					EncodeLength(innerWriter, bitStringLength);
					innerWriter.Write(bitStringStream.GetBuffer(), 0, bitStringLength);
				}
				var length = (int)innerStream.Length;
				EncodeLength(writer, length);
				writer.Write(innerStream.GetBuffer(), 0, length);
			}

			var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
			// WriteLine terminates with \r\n, we want only \n
			outputStream.Write("-----BEGIN PUBLIC KEY-----\n");
			for (var i = 0; i < base64.Length; i += 64)
			{
				outputStream.Write(base64, i, Math.Min(64, base64.Length - i));
				outputStream.Write("\n");
			}
			outputStream.Write("-----END PUBLIC KEY-----");
		}

		return outputStream.ToString();
	}

	private void EncodeLength(BinaryWriter stream, int length)
	{
		if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
		if (length < 0x80)
		{
			// Short form
			stream.Write((byte)length);
		}
		else
		{
			// Long form
			var temp = length;
			var bytesRequired = 0;
			while (temp > 0)
			{
				temp >>= 8;
				bytesRequired++;
			}
			stream.Write((byte)(bytesRequired | 0x80));
			for (var i = bytesRequired - 1; i >= 0; i--)
			{
				stream.Write((byte)(length >> (8 * i) & 0xff));
			}
		}
	}

	private void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
	{
		stream.Write((byte)0x02); // INTEGER
		var prefixZeros = 0;
		for (var i = 0; i < value.Length; i++)
		{
			if (value[i] != 0) break;
			prefixZeros++;
		}
		if (value.Length - prefixZeros == 0)
		{
			EncodeLength(stream, 1);
			stream.Write((byte)0);
		}
		else
		{
			if (forceUnsigned && value[prefixZeros] > 0x7f)
			{
				// Add a prefix zero to force unsigned if the MSB is 1
				EncodeLength(stream, value.Length - prefixZeros + 1);
				stream.Write((byte)0);
			}
			else
			{
				EncodeLength(stream, value.Length - prefixZeros);
			}
			for (var i = prefixZeros; i < value.Length; i++)
			{
				stream.Write(value[i]);
			}
		}
	}

	public byte[] Encrypt(string Input)
	{
		return Manager.Encrypt(Encoding.Default.GetBytes(Input), true);
	}

	public byte[] Decrypt(byte[] Input)
	{
		return Manager.Decrypt(Input, true);
	}
}