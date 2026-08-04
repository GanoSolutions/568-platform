namespace Five68.Models.DTO;

/// <summary>A day on which no shifts can be assigned.</summary>
public class ClosedDayDTO
{
	/// <summary>Unique identifier.</summary>
	public Guid Id { get; set; }
	/// <summary>The closed date.</summary>
	public DateOnly Date { get; set; }

	/// <summary>Maps a <see cref="ClosedDay"/> entity to its DTO.</summary>
	public static ClosedDayDTO? FromClosedDay(ClosedDay? cd)
	{
		return cd is null
			? null
			: new ClosedDayDTO
			{
				Id = cd.Id,
				Date = cd.Date
			};
	}
}