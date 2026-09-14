using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace Five68.Models
{
	public class PushSubscription
	{
		public Guid Id { get; set; }
		public Guid UserId { get; set; }
		/// <summary>URL univoco del push service del browser (Google/Apple/Mozilla).</summary>
		public string Endpoint { get; set; } = string.Empty;
		/// <summary>Chiave pubblica del client, usata per cifrare il payload.</summary>
		public string P256dh { get; set; } = string.Empty;
		/// <summary>Secret di autenticazione della subscription.</summary>
		public string Auth { get; set; } = string.Empty;
		/// <summary>Solo informativo (debug: "che dispositivo è").</summary>
		public string? UserAgent { get; set; }
		public bool Active { get; set; }
		public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
		public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
	}
}