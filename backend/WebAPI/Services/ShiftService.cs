using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;

namespace Five68.Services
{
	public class ShiftService
	{
		private readonly ShiftFacade _shiftFacade;
		private readonly UserFacade _userFacade;
		private readonly EmployeeFacade _employeeFacade;
		private ILogger _logger;

		public ShiftService(
			ShiftFacade shiftFacade,
			UserFacade userFacade,
			EmployeeFacade employeeFacade,
			ILogger<ShiftService> logger)
		{
			_shiftFacade = shiftFacade;
			_userFacade = userFacade;
			_employeeFacade = employeeFacade;
			_logger = logger;
		}

		public async Task<ShiftDTO> GetById(Guid id)
		{
			return ShiftDTO.FromShift(await _shiftFacade.FindByIdAsync(id));
		}

		public async Task<IEnumerable<ShiftDTO>> GetByDateRange(DateOnly start, DateOnly end)
		{
			if (end < start)
			{
				throw new EntityException("La data di fine deve essere successiva alla data di inizio");
			}

			return (await _shiftFacade.GetByDateRangeAsync(start, end)).Select(ShiftDTO.FromShift);
		}

		public async Task<ShiftDTO> Create(ShiftCreate model, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			Employee employee = await _employeeFacade.FindByIdAsync(model.EmployeeId) ?? throw new NotFoundException("Dipendente non trovato");
			if (await _shiftFacade.IsClosedDayAsync(model.Date))
			{
				throw new EntityException("Non è possibile assegnare un dipendente in un giorno di chiusura");
			}

			Shift existing = await _shiftFacade.FindByDateAndEmployeeAsync(model.Date, model.EmployeeId);
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

			_logger.LogInformation($"User {requester.Email} created a {created.Duration} hours shift on {created.Date} for employee {created.EmployeeId} starting at {created.StartTime}");
			return ShiftDTO.FromShift(created);
		}

		public async Task<ShiftDTO> Update(Guid id, ShiftUpdate model, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			Shift shift = await _shiftFacade.FindByIdAsync(id) ?? throw new NotFoundException("Turno non trovato");
			if (await _shiftFacade.IsClosedDayAsync(shift.Date))
			{
				throw new EntityException("Non è possibile modificare un turno in un giorno di chiusura");
			}

			shift.StartTime = model.StartTime;
			shift.Duration = model.Duration;

			Shift updated = await _shiftFacade.UpdateAsync(shift);

			_logger.LogInformation($"User {requester.Email} updated shift on {updated.Date} to a {updated.Duration} hours shift starting at {updated.StartTime}");
			return ShiftDTO.FromShift(updated);
		}

		public async Task Delete(Guid id, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			Shift shift = await _shiftFacade.FindByIdAsync(id) ?? throw new NotFoundException("Turno non trovato");
			await _shiftFacade.DeleteAsync(shift);

			_logger.LogInformation($"User {requester.Email} deleted shift on {shift.Date} - {shift.StartTime}");
		}

		private async Task<User> RequireManagerOrAdmin(Guid requesterId)
		{
			User requester = await _userFacade.FindByIdAsync(requesterId) ?? throw new UnauthorizedException();
			if (requester.Role == UserRole.Employee)
			{
				throw new ForbiddenException("Non hai i permessi per eseguire questa azione");
			}

			return requester;
		}
	}
}