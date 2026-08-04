using NLog;
using NLog.Targets;
using System.Text;
using System.Text.Json;

namespace Five68.Utils;

public class TelegramTarget : AsyncTaskTarget
{
	private static readonly HttpClient _httpClient = new();
	private const int MaxMessageLength = 1000;

	public required string BotToken { get; set; }
	public List<string> ChatIds { get; set; } = [];

	protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
	{
		string text = RenderLogEvent(Layout, logEvent);
		if (text.Length > MaxMessageLength)
		{
			text = text[..MaxMessageLength] + "...(troncato)";
		}

		string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
		foreach (string chatId in ChatIds)
		{
			var payload = new { chat_id = chatId, text };
			using StringContent content = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			await _httpClient.PostAsync(url, content, cancellationToken);
		}
	}
}