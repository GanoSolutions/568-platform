using System.ComponentModel.DataAnnotations;

namespace Five68.Models.DTO;

/// <summary>A request to swap a shift with a colleague.</summary>
public class SwapRequestDTO
{
	/// <summary>Unique identifier.</summary>
	public Guid Id { get; set; }
	/// <summary>The shift being offered.</summary>
	public Guid ShiftId { get; set; }
	/// <summary>The employee who owns the shift and is requesting the swap.</summary>
	public Guid RequesterId { get; set; }
	/// <summary>The colleague being asked to take the shift.</summary>
	public Guid TargetEmployeeId { get; set; }
	/// <summary>Current status of the request.</summary>
	public SwapRequestStatus Status { get; set; }
	/// <summary>When the request was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }
	/// <summary>When the target responded, if they have.</summary>
	public DateTimeOffset? RespondedAt { get; set; }

	/// <summary>Maps a <see cref="SwapRequest"/> entity to its DTO.</summary>
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

/// <summary>Request body to create one or more swap requests for a shift.</summary>
public class SwapRequestCreate
{
	/// <summary>The shift to offer for swap.</summary>
	[Required]
	public Guid ShiftId { get; set; }
	/// <summary>The colleagues to ask, one swap request is created per entry.</summary>
	[Required]
	[MinLength(1)]
	public required List<Guid> TargetEmployeeIds { get; set; }
}