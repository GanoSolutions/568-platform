using Five68.Utils;
using FluentAssertions;
namespace Five68.UnitTests;

public class TestPushKeyAttribute
{
	private readonly PushKeyAttribute p256dh_ = new(65) { FirstByte = 0x04 };
	private readonly PushKeyAttribute auth_ = new(16);

	private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	private static byte[] KeyBytes(int length, byte firstByte = 0x04)
	{
		byte[] bytes = new byte[length];
		Array.Fill(bytes, (byte)0xFB);
		bytes[0] = firstByte;
		return bytes;
	}

	// --- Chiavi valide ---

	[Fact]
	public void IsValid_P256dhWithExpectedLengthAndPrefix_ReturnsTrue()
	{
		p256dh_.IsValid(Encode(KeyBytes(65))).Should().BeTrue();
	}

	[Fact]
	public void IsValid_AuthWithExpectedLength_ReturnsTrue()
	{
		auth_.IsValid(Encode(KeyBytes(16, 0x7F))).Should().BeTrue();
	}

	// --- Valore mancante: lo segnala [Required], non questo attributo ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void IsValid_MissingValue_ReturnsTrue(string? key)
	{
		auth_.IsValid(key).Should().BeTrue();
	}

	// --- Chiavi malformate ---

	[Theory]
	[InlineData(64)]
	[InlineData(66)]
	public void IsValid_P256dhWithWrongLength_ReturnsFalse(int length)
	{
		p256dh_.IsValid(Encode(KeyBytes(length))).Should().BeFalse();
	}

	[Fact]
	public void IsValid_P256dhWithWrongPrefix_ReturnsFalse()
	{
		p256dh_.IsValid(Encode(KeyBytes(65, 0x02))).Should().BeFalse();
	}

	[Fact]
	public void IsValid_AuthWithWrongLength_ReturnsFalse()
	{
		auth_.IsValid(Encode(KeyBytes(15))).Should().BeFalse();
	}

	[Theory]
	[InlineData("non-e-base64!!")]
	[InlineData("a")]
	public void IsValid_NotBase64Url_ReturnsFalse(string key)
	{
		auth_.IsValid(key).Should().BeFalse();
	}
}
