using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;

namespace Five68.Utils
{
	/// <summary>
	/// Chiave di una push subscription in base64url: lunghezza decodificata esatta ed eventuale
	/// primo byte atteso. Chiavi malformate farebbero fallire la cifratura a ogni invio.
	/// </summary>
	public class PushKeyAttribute : ValidationAttribute
	{
		private readonly int _length;

		/// <summary>-1 = nessun vincolo sul primo byte.</summary>
		public int FirstByte { get; set; } = -1;

		public PushKeyAttribute(int length) : base("Chiave della subscription non valida")
		{
			_length = length;
		}

		public override bool IsValid(object? value)
		{
			// null o vuoto lo gestisce [Required]
			if (value is not string key || string.IsNullOrWhiteSpace(key))
			{
				return true;
			}

			try
			{
				byte[] bytes = Base64Url.DecodeFromChars(key);
				return bytes.Length == _length && (FirstByte < 0 || bytes[0] == FirstByte);
			}
			catch (FormatException)
			{
				return false;
			}
		}
	}
}
