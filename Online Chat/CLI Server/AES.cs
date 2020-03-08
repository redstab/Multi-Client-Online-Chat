using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class AES
{
	public Aes Manager = Aes.Create();

	public AES(byte[] Key, byte[] IV)
	{
		Manager.Key = Key;
		Manager.IV = IV;
	}

	public AES(byte[] Key)
	{
		Manager.Key = Key;
	}

	public AES() { }

	/// <summary>
	/// Format for json
	///	{
	///		"KEY" : fsdfsdfsdfsdhjklfgsdjhkfgsdjhkfg,
	///		"IV"  : ljhksdafljhkjhlksdf
	///	}
	/// </summary>
	/// <param name="Key"></param>
	public AES(JObject Key) { DeserilizeKey(Key); }

	public JObject SerilizeKey()
	{
		JObject Key = new JObject
		{
			["KEY"] = new JArray(Manager.Key.Select(b => (uint)b).ToArray()),
			["IV"] = new JArray(Manager.IV.Select(b => (uint)b).ToArray())
		};
		return Key;
	}
	public void DeserilizeKey(JObject Key)
	{
		Manager.Key = Key["KEY"].ToObject<byte[]>();
		Manager.IV = Key["IV"].ToObject<byte[]>();
	}

	public byte[] Encrypt(string Input)
	{

		if (Input == "")
		{
			throw new ArgumentException("empty input");
		}

		byte[] Encrypted;

		Manager.GenerateIV();

		ICryptoTransform encryptor = Manager.CreateEncryptor(Manager.Key, Manager.IV);

		using (MemoryStream msEncrypt = new MemoryStream())
		{
			using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
			{
				using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
				{
					swEncrypt.Write(Input);
				}
				Encrypted = msEncrypt.ToArray();
			}
		}

		return Encrypted;

	}

	public byte[] Decrypt(byte[] Input)
	{
		// Create a decrytor to perform the stream transform.
		ICryptoTransform decryptor = Manager.CreateDecryptor(Manager.Key, Manager.IV);

		// Create the streams used for decryption. 
		return PerformCryptography(Input, decryptor);
	}

	private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
	{
		using (var ms = new MemoryStream())
		using (var cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
		{
			cryptoStream.Write(data, 0, data.Length);
			cryptoStream.FlushFinalBlock();

			return ms.ToArray();
		}
	}

}
