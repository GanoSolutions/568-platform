using System.Threading.Channels;

namespace Five68.Services
{
	/// <summary>
	/// Coda in memoria (singleton) tra chi genera le notifiche e il worker che le invia.
	/// Bounded: se il worker è in difficoltà si scartano notifiche invece di gonfiare la memoria.
	/// </summary>
	public class PushDispatchQueue
	{
		private readonly Channel<PushJob> _channel = Channel.CreateBounded<PushJob>(
			new BoundedChannelOptions(1000)
			{
				FullMode = BoundedChannelFullMode.DropWrite,
				SingleReader = true,
			});

		public void Enqueue(PushJob job) => _channel.Writer.TryWrite(job);
		public IAsyncEnumerable<PushJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
	}
}