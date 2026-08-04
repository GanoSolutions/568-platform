namespace Five68.Models.DTO;

public class ClosedDayDTO
{
	public Guid Id { get; set; }
	public DateOnly Date { get; set; }

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