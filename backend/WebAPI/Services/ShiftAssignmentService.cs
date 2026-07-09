using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;

namespace Five68.Services
{
	public class ShiftAssignmentService
	{
		private readonly ShiftAssignmentFacade _shiftAssignmentFacade;
		private readonly UserFacade _userFacade;
		private readonly EmployeeFacade _employeeFacade;
		private ILogger _logger;

		public ShiftAssignmentService(
			ShiftAssignmentFacade shiftAssignmentFacade,
			UserFacade userFacade,
			EmployeeFacade employeeFacade,
			ILogger<ShiftAssignmentService> logger)
		{
			_shiftAssignmentFacade = shiftAssignmentFacade;
			_userFacade = userFacade;
			_employeeFacade = employeeFacade;
			_logger = logger;
		}

		public async Task<ShiftAssignmentDTO> GetById(Guid id)
		{
			return ShiftAssignmentDTO.FromShiftAssignment(await _shiftAssignmentFacade.FindByIdAsync(id));
		}

		public async Task<IEnumerable<ShiftAssignmentDTO>> GetByDateRange(DateOnly start, DateOnly end)
		{
			if (end < start)
			{
				throw new EntityException("End time must be after start time");
			}

			return (await _shiftAssignmentFacade.GetByDateRangeAsync(start, end)).Select(ShiftAssignmentDTO.FromShiftAssignment);
		}

		public async Task<ShiftAssignmentDTO> Create(ShiftAssignmentCreate model, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			if (model.EndTime <= model.StartTime)
			{
				throw new EntityException("End time must be after start time");
			}

			Employee employee = await _employeeFacade.FindByIdAsync(model.EmployeeId);
			if (employee is null)
			{
				throw new NotFoundException("Employee not found");
			}

			if (await _shiftAssignmentFacade.IsClosedDayAsync(model.Date))
			{
				throw new EntityException("Cannot assign an employee on a closed day");
			}

			ShiftAssignment existing = await _shiftAssignmentFacade.FindByDateAndEmployeeAsync(model.Date, model.EmployeeId);
			if (existing is not null)
			{
				throw new EntityException("Employee is already assigned on this date");
			}

			await _shiftAssignmentFacade.CreateAsync(new ShiftAssignment
			{
				Id = Guid.NewGuid(),
				Date = model.Date,
				EmployeeId = model.EmployeeId,
				StartTime = model.StartTime,
				EndTime = model.EndTime,
				CreatedBy = requesterId,
			});

			_logger.LogInformation($"User {requester.Email} created shift assignment for employee {model.EmployeeId} on {model.Date}");
		}

		public async Task Update(Guid id, ShiftAssignmentUpdate model, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			if (model.EndTime <= model.StartTime)
			{
				throw new EntityException("End time must be after start time");
			}

			ShiftAssignment sa = await _shiftAssignmentFacade.FindByIdAsync(id);
			if (sa is null)
			{
				throw new NotFoundException("Shift assignment not found");
			}

			if (await _shiftAssignmentFacade.IsClosedDayAsync(sa.Date))
			{
				throw new EntityException("Cannot update a shift assignment on a closed day");
			}

			sa.StartTime = model.StartTime;
			sa.EndTime = model.EndTime;

			await _shiftAssignmentFacade.UpdateAsync(sa);

			_logger.LogInformation($"User {requester.Email} updated shift assignment to {model.StartTime} - {model.EndTime}");
		}

		public async Task Delete(Guid id, Guid requesterId)
		{
			User requester = await RequireManagerOrAdmin(requesterId);

			ShiftAssignment sa = await _shiftAssignmentFacade.FindByIdAsync(id);
			if (sa is null)
			{
				throw new NotFoundException("Shift assignment not found");
			}

			await _shiftAssignmentFacade.DeleteAsync(sa);

			_logger.LogInformation($"User {requester.Email} deleted shift assignment on {sa.Date} at {sa.StartTime} - {sa.EndTime}");
		}

		private async Task<User> RequireManagerOrAdmin(Guid requesterId)
		{
			User requester = await _userFacade.FindByIdAsync(requesterId);
			if (requester is null)
			{
				throw new UnauthorizedException();
			}

			if (requester.Role == UserRole.Employee)
			{
				throw new ForbiddenException("You can't perform this action");
			}

			return requester;
		}
	}
}