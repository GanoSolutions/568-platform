using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;
using Five68.Utils;

namespace Five68.Services;

public class ShiftService
{
	private readonly ShiftFacade _shiftFacade;
	private readonly EmployeeFacade _employeeFacade;
	private readonly IShiftNotificationService _notificationService;
	private readonly AuthUtils _authUtils;
	private readonly ILogger _logger;

	public ShiftService(
		ShiftFacade shiftFacade,
		EmployeeFacade employeeFacade,
		IShiftNotificationService notificationService,
		AuthUtils authUtils,
		ILogger<ShiftService> logger)
	{
		_shiftFacade = shiftFacade;
		_employeeFacade = employeeFacade;
		_notificationService = notificationService;
		_authUtils = authUtils;
		_logger = logger;
	}

	public async Task<ShiftDTO?> GetById(Guid id)
	{
		return ShiftDTO.FromShift(await _shiftFacade.FindByIdAsync(id));
	}

	public async Task<IEnumerable<ShiftDTO>> GetByDateRange(DateOnly start, DateOnly end)
	{
		return end < start
			? throw new EntityException("La data di fine deve essere successiva alla data di inizio")
			: (await _shiftFacade.GetByDateRangeAsync(start, end)).Select(s => ShiftDTO.FromShift(s)!);
	}

	public async Task<ShiftDTO> Create(ShiftCreate model, Guid requesterId)
	{
		User requester = await _authUtils.RequireManagerOrAdmin(requesterId);
		_ = await _employeeFacade.FindByIdAsync(model.EmployeeId) ?? throw new NotFoundException("Dipendente non trovato");
		if (await _shiftFacade.IsClosedDayAsync(model.Date))
		{
			throw new EntityException("Non è possibile assegnare un dipendente in un giorno di chiusura");
		}

		Shift? existing = await _shiftFacade.FindByDateAndEmployeeAsync(model.Date, model.EmployeeId);
		if (existing is not null)
		{
			throw new EntityException("Il dipendente è già assegnato in questa data");
		}

		Shift created = await _shiftFacade.CreateAsync(new Shift
		{
			Id = Guid.NewGuid(),
			Date = model.Date,
			EmployeeId = model.EmployeeId,
			StartTime = model.StartTime,
			Duration = model.Duration,
			CreatedBy = requesterId,
		});

		await _notificationService.NotifyShiftChangedAsync(created.Date);
		_logger.LogInformation("User {RequesterId} ({RequesterEmail}) created a {Duration} hours shift on {Date} for employee {EmployeeId} starting at {StartTime}", requester.Id, requester.Email, created.Duration, created.Date, created.EmployeeId, created.StartTime);

		return ShiftDTO.FromShift(created)!;
	}

	public async Task<ShiftDTO> Update(Guid id, ShiftUpdate model, Guid requesterId)
	{
		User requester = await _authUtils.RequireManagerOrAdmin(requesterId);

		Shift shift = await _shiftFacade.FindByIdAsync(id) ?? throw new NotFoundException("Turno non trovato");
		if (await _shiftFacade.IsClosedDayAsync(shift.Date))
		{
			throw new EntityException("Non è possibile modificare un turno in un giorno di chiusura");
		}

		shift.StartTime = model.StartTime;
		shift.Duration = model.Duration;

		Shift updated = await _shiftFacade.UpdateAsync(shift);

		await _notificationService.NotifyShiftChangedAsync(updated.Date);
		_logger.LogInformation("User {RequesterId} ({RequesterEmail}) updated shift on {Date} to a {Duration} hours shift starting at {StartTime}", requester.Id, requester.Email, updated.Date, updated.Duration, updated.StartTime);

		return ShiftDTO.FromShift(updated)!;
	}

	public async Task Delete(Guid id, Guid requesterId)
	{
		User requester = await _authUtils.RequireManagerOrAdmin(requesterId);

		Shift shift = await _shiftFacade.FindByIdAsync(id) ?? throw new NotFoundException("Turno non trovato");

		DateTime shiftEnd = shift.Date.ToDateTime(shift.StartTime).Add(shift.Duration);
		if (shiftEnd < DateTime.Now)
		{
			throw new EntityException("Non è possibile eliminare un turno passato");
		}
		await _shiftFacade.DeleteAsync(shift);

		await _notificationService.NotifyShiftChangedAsync(shift.Date);
		_logger.LogInformation("User {RequesterId} ({RequesterEmail}) deleted shift on {Date} - {StartTime}", requester.Id, requester.Email, shift.Date, shift.StartTime);
	}

	public async Task<ShiftCopyWeekResult> CopyWeek(ShiftCopyWeek model, Guid requesterId)
	{
		User requester = await _authUtils.RequireManagerOrAdmin(requesterId);

		if (model.SourceWeekMonday.DayOfWeek != DayOfWeek.Monday)
		{
			throw new EntityException("La settimana sorgente deve iniziare di lunedì");
		}

		if (model.TargetStartDate.DayOfWeek != DayOfWeek.Monday)
		{
			throw new EntityException("La data di inizio del periodo deve essere un lunedì");
		}

		if (model.TargetEndDate.DayOfWeek != DayOfWeek.Sunday)
		{
			throw new EntityException("La data di fine del periodo deve essere una domenica");
		}

		if (model.TargetEndDate < model.TargetStartDate)
		{
			throw new EntityException("La data di fine deve essere successiva alla data di inizio");
		}

		ShiftCopyWeekResult result = await _shiftFacade.CopyWeekAsync(
			model.SourceWeekMonday, model.TargetStartDate, model.TargetEndDate, requesterId) ??
			throw new EntityException("La settimana selezionata non contiene turni da copiare");

		_logger.LogInformation("User {RequesterEmail} copied week of {SourceWeekMonday} onto {TargetStartDate}-{TargetEndDate}: {Created} created, {Overwritten} overwritten, {SkippedClosedDays} skipped", requester.Email, model.SourceWeekMonday, model.TargetStartDate, model.TargetEndDate, result.Created, result.Overwritten, result.SkippedClosedDays);

		return result;
	}
}