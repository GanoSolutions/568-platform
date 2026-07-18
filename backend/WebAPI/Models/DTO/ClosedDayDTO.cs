namespace Five68.Models.DTO
{
	public class ClosedDayDTO
	{
		public Guid Id { get; set; }
		public DateOnly Date { get; set; }

		public static ClosedDayDTO FromClosedDay(ClosedDay cd)
		{
			if (cd is null)
			{
				return null;
			}

			return new ClosedDayDTO
			{
				Id = cd.Id,
				Date = cd.Date
			};
		}
	}
}