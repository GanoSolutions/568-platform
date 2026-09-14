using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO
{
	public class PushSubscriptionCreate
	{
		[Required]
		[MaxLength(2048)]
		[Url]
		public string Endpoint { get; set; } = string.Empty;
		[Required]
		[MaxLength(256)]
		public string P256dh { get; set; } = string.Empty;
		[Required]
		[MaxLength(256)]
		public string Auth { get; set; } = string.Empty;
		[MaxLength(512)]
		public string? UserAgent { get; set; }
	}

	public class PushUnsubscribe
	{
		[Required]
		[MaxLength(2048)]
		public string Endpoint { get; set; } = string.Empty;
	}
}