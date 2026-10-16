using System;
using System.ComponentModel.DataAnnotations;

namespace Five68.Utils
{
	/// <summary>Ammette solo endpoint di push service noti: difesa contro SSRF via subscription.</summary>
	public class PushServiceEndpointAttribute : ValidationAttribute
	{
		private static readonly string[] ExactHost = ["fcm.googleapis.com"];
		private static readonly string[] HostSuffixes = [".push.services.mozilla.com", ".push.apple.com", ".notify.windows.com"];

		public PushServiceEndpointAttribute() : base("Endpoint non appertenente a un push service supportato")
		{ }

		public override bool IsValid(object value)
		{
			// null o vuoto lo gestisce [Required]
			if (value is not string endpoint || string.IsNullOrWhiteSpace(endpoint))
			{
				return true;
			}

			if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
				|| uri.Scheme != Uri.UriSchemeHttps
				|| !uri.IsDefaultPort
				|| !string.IsNullOrEmpty(uri.UserInfo))
			{
				return false;
			}
			string host = uri.IdnHost;
			return ExactHost.Any(h => host.Equals(h, StringComparison.OrdinalIgnoreCase))
				|| HostSuffixes.Any(s => host.EndsWith(s, StringComparison.OrdinalIgnoreCase));
		}
	}
}
