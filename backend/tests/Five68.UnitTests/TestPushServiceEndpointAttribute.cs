using Five68.Utils;
using FluentAssertions;
namespace Five68.UnitTests;

public class TestPushServiceEndpointAttribute
{
	private readonly PushServiceEndpointAttribute sut_ = new();

	// --- Push service reali ---

	[Theory]
	[InlineData("https://fcm.googleapis.com/fcm/send/abc123")]
	[InlineData("https://FCM.googleapis.com/fcm/send/abc123")]
	[InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc123")]
	[InlineData("https://web.push.apple.com/abc123")]
	[InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc123")]
	[InlineData("https://fcm.googleapis.com:443/fcm/send/abc123")]
	public void IsValid_KnownPushService_ReturnsTrue(string endpoint)
	{
		sut_.IsValid(endpoint).Should().BeTrue();
	}

	// --- Valore mancante: lo segnala [Required], non questo attributo ---

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void IsValid_MissingValue_ReturnsTrue(string? endpoint)
	{
		sut_.IsValid(endpoint).Should().BeTrue();
	}

	// --- Tentativi di SSRF e host non ammessi ---

	[Theory]
	[InlineData("http://fcm.googleapis.com/fcm/send/abc123")]        // non https
	[InlineData("https://fcm.googleapis.com:8443/fcm/send/abc123")]  // porta non di default
	[InlineData("https://user@fcm.googleapis.com/fcm/send/abc123")]  // userinfo
	[InlineData("https://fcm.googleapis.com@evil.com/")]             // host reale: evil.com
	[InlineData("https://fcm.googleapis.com.evil.com/")]             // host esatto usato come prefisso
	[InlineData("https://evilnotify.windows.com/")]                  // suffisso senza punto iniziale
	[InlineData("https://push.apple.com/")]                          // il suffisso da solo non basta
	[InlineData("https://evil.com/")]
	[InlineData("https://localhost/")]
	[InlineData("http://localhost:8080/")]
	[InlineData("https://127.0.0.1/")]
	[InlineData("https://169.254.169.254/latest/meta-data/")]
	[InlineData("https://[::1]/")]
	[InlineData("ftp://fcm.googleapis.com/")]
	[InlineData("file:///etc/passwd")]
	[InlineData("/fcm/send/abc123")]                                 // relativo
	[InlineData("non-e-un-url")]
	public void IsValid_NotAPushService_ReturnsFalse(string endpoint)
	{
		sut_.IsValid(endpoint).Should().BeFalse();
	}

	[Fact]
	public void IsValid_NotAString_ReturnsTrue()
	{
		// Applicato per errore a una proprietà non stringa non deve far fallire la validazione:
		// coerente con gli attributi built-in come [Url]
		sut_.IsValid(42).Should().BeTrue();
	}
}
