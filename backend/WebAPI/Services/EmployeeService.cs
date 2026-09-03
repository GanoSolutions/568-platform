using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.DTO;
using Five68.Utils;

namespace Five68.Services
{
	public class EmployeeService
	{
		// Stessa palette di frontend/src/lib/colorUtils.ts — tenerle allineate a mano finché
		// non esiste un'unica fonte di verità condivisa.
		private static readonly string[] ColorPalette =
		[
			"#6366f1", "#f43f5e", "#f59e0b", "#10b981", "#3b82f6",
			"#8b5cf6", "#ec4899", "#06b6d4", "#84cc16", "#f97316",
		];

		private readonly UserFacade _userFacade;
		private readonly EmployeeFacade _employeeFacade;
		private readonly TransactionFacade _transactionFacade;
		private readonly AuthUtils _authUtils;
		private readonly ILogger _logger;

		public EmployeeService(
			UserFacade userFacade,
			EmployeeFacade employeeFacade,
			TransactionFacade transactionFacade,
			AuthUtils authUtils,
			ILogger<EmployeeService> logger)
		{
			_userFacade = userFacade;
			_employeeFacade = employeeFacade;
			_transactionFacade = transactionFacade;
			_authUtils = authUtils;
			_logger = logger;
		}

		public async Task<IEnumerable<EmployeeDTO>> GetAll()
		{
			return (await _employeeFacade.GetAllAsync()).Select(EmployeeDTO.FromEmployee);
		}

		public async Task<EmployeeDTO> Create(EmployeeCreate model, Guid requesterId)
		{
			User requester = await _authUtils.RequireManagerOrAdmin(requesterId);

			if (await _userFacade.FindByEmailAsync(model.Email) is not null)
			{
				throw new EntityException("Email già in uso");
			}
			if (await _employeeFacade.FindByFiscalCodeAsync(model.FiscalCode) is not null)
			{
				throw new EntityException("Codice fiscale già in uso");
			}

			string color = await NextAvailableColor();
			User user = null;
			Employee employee = null;

			await _transactionFacade.ExecuteAsync(async () =>
			{
				user = await _userFacade.CreateAsync(model.Email, UserRole.Employee, UserStatus.Pending);
				employee = new Employee
				{
					UserId = user.Id,
					Name = model.Name,
					Surname = model.Surname,
					FiscalCode = model.FiscalCode,
					Phone = model.Phone,
					ContractEnd = model.ContractEnd,
					Color = color,
				};
				await _employeeFacade.CreateAsync(employee);
			});

			_logger.LogInformation($"User {requester.Id} ({requester.Email}) created employee {user.Id} ({model.Email})");

			return EmployeeDTO.FromEmployee(employee);
		}

		public async Task<EmployeeDTO> Update(Guid id, EmployeeCreate model, Guid requesterId)
		{
			User requester = await _authUtils.RequireManagerOrAdmin(requesterId);

			User user = await _userFacade.FindByIdAsync(id) ?? throw new NotFoundException("Dipendente non trovato");
			if (user.Employee is null)
			{
				throw new NotFoundException("Dipendente non trovato");
			}

			User existingByEmail = await _userFacade.FindByEmailAsync(model.Email);
			if (existingByEmail is not null && existingByEmail.Id != id)
			{
				throw new EntityException("Email già in uso");
			}
			Employee existingByFiscalCode = await _employeeFacade.FindByFiscalCodeAsync(model.FiscalCode);
			if (existingByFiscalCode is not null && existingByFiscalCode.UserId != id)
			{
				throw new EntityException("Codice fiscale già in uso");
			}

			user.Email = model.Email;
			user.Employee.Name = model.Name;
			user.Employee.Surname = model.Surname;
			user.Employee.FiscalCode = model.FiscalCode;
			user.Employee.Phone = model.Phone;
			user.Employee.ContractEnd = model.ContractEnd;

			await _transactionFacade.ExecuteAsync(async () =>
			{
				await _userFacade.UpdateAsync(user);
				await _employeeFacade.UpdateAsync(user.Employee);
			});

			_logger.LogInformation($"User {requester.Id} ({requester.Email}) updated employee {id}");

			return EmployeeDTO.FromEmployee(user.Employee);
		}

		private async Task<string> NextAvailableColor()
		{
			HashSet<string> used = (await _employeeFacade.GetAllAsync())
				.Where(x => x.Color is not null)
				.Select(x => x.Color)
				.ToHashSet();

			string available = ColorPalette.FirstOrDefault(c => !used.Contains(c));
			if (available is not null)
			{
				return available;
			}

			int count = (await _employeeFacade.GetAllAsync()).Count();
			return ColorPalette[count % ColorPalette.Length];
		}
	}
}