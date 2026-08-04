using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO;

public class SwapRequestDTO
{
	public Guid Id { get; set; }
	public Guid ShiftId { get; set; }
	public Guid RequesterId { get; set; }
	public Guid TargetEmployeeId { get; set; }
	public SwapRequestStatus Status { get; set; }
	public DateTimeOffset CreatedAt { get; set; }
	public DateTimeOffset? RespondedAt { get; set; }

	public static SwapRequestDTO? FromSwapRequest(SwapRequest? request)
	{
		return request is null
			? null
			: new SwapRequestDTO
			{
				Id = request.Id,
				ShiftId = request.ShiftId,
				RequesterId = request.RequesterId,
				TargetEmployeeId = request.TargetEmployeeId,
				Status = request.Status,
				CreatedAt = request.CreatedAt,
				RespondedAt = request.RespondedAt,
			};
	}
}

public class SwapRequestCreate
{
	[Required]
	public Guid ShiftId { get; set; }
	[Required]
	[MinLength(1)]
	public required List<Guid> TargetEmployeeIds { get; set; }
}