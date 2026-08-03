using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;

namespace Five68.Services;

public class SwapRequestService
{
	private readonly INotificationService _notificationService;
	private readonly SwapRequestFacade _swapRequestFacade;
	private readonly ShiftFacade _shiftFacade;
	private readonly EmployeeFacade _employeeFacade;
	private readonly UserFacade _userFacade;
	private readonly ILogger _logger;

	private enum SwapRequestAction
	{
		Respond,
		Cancel
	}

	public SwapRequestService(
		INotificationService notificationService,
		SwapRequestFacade swapRequestFacade,
		ShiftFacade shiftFacade,
		EmployeeFacade employeeFacade,
		UserFacade userFacade,
		ILogger<SwapRequestService> logger)
	{
		_notificationService = notificationService;
		_swapRequestFacade = swapRequestFacade;
		_shiftFacade = shiftFacade;
		_employeeFacade = employeeFacade;
		_userFacade = userFacade;
		_logger = logger;
	}

	public async Task<IEnumerable<SwapRequestDTO>> Create(SwapRequestCreate model, Guid requesterId)
	{
		Shift shift = await _shiftFacade.FindByIdAsync(model.ShiftId) ?? throw new NotFoundException("Turno non trovato");

		if (shift.EmployeeId != requesterId)
		{
			throw new ForbiddenException("Non puoi richiedere il cambio di un turno non tuo");
		}

		List<Guid> targetIds = model.TargetEmployeeIds.Distinct().ToList();
		List<SwapRequest> toCreate = [];

		foreach (Guid targetId in targetIds)
		{
			if (targetId == requesterId)
			{
				throw new EntityException("Non puoi selezionare te stesso come sostituto");
			}

			Employee target = await _employeeFacade.FindByIdAsync(targetId) ?? throw new NotFoundException("Dipendente non trovato");
			if (await _shiftFacade.FindByDateAndEmployeeAsync(shift.Date, targetId) is not null)
			{
				throw new EntityException($"{target.Name} {target.Surname} ha già un turno assegnato in questa data");
			}

			if (await _swapRequestFacade.FindPendingByShiftAndTargetAsync(shift.Id, targetId) is not null)
			{
				throw new EntityException($"Esiste già una richiesta pendente per {target.Name} {target.Surname}");
			}

			toCreate.Add(new SwapRequest
			{
				Id = Guid.NewGuid(),
				ShiftId = shift.Id,
				RequesterId = requesterId,
				TargetEmployeeId = targetId
			});
		}

		await _swapRequestFacade.CreateRangeAsync(toCreate);

		// notify only after db save
		foreach (SwapRequest r in toCreate)
		{
			await _notificationService.NotifySwapRequestCreatedAsync(r);
			_logger.LogInformation($"User {requesterId} requested a shift swap on {shift.Date} for {r.TargetEmployeeId}");
		}

		return toCreate.Select(SwapRequestDTO.FromSwapRequest);
	}

	public async Task<SwapRequestDTO> Accept(Guid swapRequestId, Guid requesterId)
	{
		SwapRequest request = await RequireCanAct(swapRequestId, requesterId, SwapRequestAction.Respond);
		SwapRequestFacade.AcceptResult result = await _swapRequestFacade.TryAcceptAsync(swapRequestId, request.ShiftId, request.TargetEmployeeId);

		if (result == SwapRequestFacade.AcceptResult.AlreadyHandled)
		{
			throw new EntityException("La richiesta è già stata gestita");
		}

		if (result == SwapRequestFacade.AcceptResult.TargetBusy)
		{
			throw new EntityException("Il collega ha già un turno assegnato in questa data");
		}

		SwapRequest updated = await _swapRequestFacade.FindByIdAsync(swapRequestId);
		_logger.LogInformation($"User {requesterId} accepted swap request {swapRequestId}");
		await _notificationService.NotifySwapRequestRespondedAsync(updated);

		return SwapRequestDTO.FromSwapRequest(await _swapRequestFacade.FindByIdAsync(swapRequestId));
	}

	public async Task<SwapRequestDTO> Reject(Guid swapRequestId, Guid requesterId)
	{
		await RequireCanAct(swapRequestId, requesterId, SwapRequestAction.Respond); // check target

		if (!await _swapRequestFacade.TrySetStatusAsync(swapRequestId, SwapRequestStatus.Rejected))
		{
			throw new EntityException("La richiesta è stata già gestita");
		}

		SwapRequest updated = await _swapRequestFacade.FindByIdAsync(swapRequestId);
		_logger.LogInformation($"User {requesterId} rejected swap request {swapRequestId}");
		await _notificationService.NotifySwapRequestRespondedAsync(updated);

		return SwapRequestDTO.FromSwapRequest(updated);
	}

	public async Task<SwapRequestDTO> Cancel(Guid swapRequestId, Guid requesterId)
	{
		await RequireCanAct(swapRequestId, requesterId, SwapRequestAction.Cancel); // check sender

		if (!await _swapRequestFacade.TrySetStatusAsync(swapRequestId, SwapRequestStatus.Cancelled))
		{
			throw new EntityException("La richiesta è stata già gestita");
		}

		SwapRequest updated = await _swapRequestFacade.FindByIdAsync(swapRequestId);
		_logger.LogInformation($"User {requesterId} cancelled swap request {swapRequestId}");
		await _notificationService.NotifySwapRequestCancelledAsync(updated);

		return SwapRequestDTO.FromSwapRequest(updated);
	}

	public async Task<IEnumerable<SwapRequestDTO>> GetForUser(Guid userId, UserRole role)
	{
		bool seeAll = role is UserRole.Admin or UserRole.Manager;
		return (await _swapRequestFacade.GetForUserAsync(userId, seeAll)).Select(SwapRequestDTO.FromSwapRequest);
	}

	private async Task<SwapRequest> RequireCanAct(Guid swapRequestId, Guid requesterId, SwapRequestAction action)
	{
		SwapRequest request = await _swapRequestFacade.FindByIdAsync(swapRequestId) ?? throw new NotFoundException("Richiesta non trovata");
		User requester = await _userFacade.FindByIdAsync(requesterId) ?? throw new UnauthorizedException();

		Guid authorizedUserId = action == SwapRequestAction.Respond ? request.TargetEmployeeId : request.RequesterId;
		if (authorizedUserId != requesterId && requester.Role != UserRole.Admin)
		{
			string message = action == SwapRequestAction.Respond
				? "Non hai i permessi per rispondere a questa richiesta"
				: "Non hai i permessi per annullare questa richiesta";
			throw new ForbiddenException(message);
		}

		return request;
	}
}