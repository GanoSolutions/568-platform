using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;
using Five68.Models.Authentication;
using Five68.Models.DTO;
using Five68.Utils;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Five68.Services
{
	public class UserService
	{
		private readonly UserFacade _userFacade;
		private readonly EmployeeFacade _employeeFacade;
		private readonly UserUtils _userUtils;
		private readonly IInviteService _notificationService;
		private readonly ILogger _logger;

		public UserService(
			UserFacade userFacade,
			EmployeeFacade employeeFacade,
			UserUtils userUtils,
			IInviteService notificationService,
			ILogger<UserService> logger)
		{
			_userFacade = userFacade;
			_employeeFacade = employeeFacade;
			_userUtils = userUtils;
			_notificationService = notificationService;
			_logger = logger;
		}

		public async Task<UserDTO> GetUserDTO(Guid id)
		{
			return UserDTO.FromUser(await _userFacade.FindByIdAsync(id));
		}

		public async Task<User> Get(Guid id)
		{
			return await _userFacade.FindByIdAsync(id);
		}

		public async Task<(bool success, User? user)> TryGetUserAndCheckPasswordAsync(UserLogin loginCredentials)
		{
			User? user = await _userFacade.FindByEmailAsync(loginCredentials.Email);

			if (user is null)
			{
				return (false, null);
			}

			if (!_userUtils.CheckPassword(user, loginCredentials.Password))
			{
				return (false, null);
			}

			return (true, user);
		}

		public async Task<IEnumerable<UserDTO>> GetAll()
		{
			return (await _userFacade.GetAll()).Select(x => UserDTO.FromUser(x));
		}

		public async Task<string> GenerateInvite(Guid userId, Guid requesterId)
		{
			User requester = await _userFacade.FindByIdAsync(requesterId);
			if (requester is null)
			{
				throw new UnauthorizedException();
			}
			if (requester.Role >= UserRole.Employee)
			{
				throw new ForbiddenException("Non hai i permessi per eseguire questa azione");
			}

			User user = await _userFacade.FindByIdAsync(userId);
			if (user is null)
				throw new NotFoundException("Utente non trovato");

			string token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

			user.Status = UserStatus.Pending;
			user.InviteToken = token;
			user.InviteTokenExpiry = DateTime.UtcNow.AddDays(7);

			await _userFacade.UpdateAsync(user);
			await _notificationService.SendInviteAsync(user.Email, token);
			_logger.LogInformation($"User {requester.Email} invited {user.Email} to change password");
			return token;
		}

		public async Task AcceptInvite(InviteAccept model)
		{
			User user = await _userFacade.FindByInviteTokenAsync(model.Token);
			if (user is null || user.InviteTokenExpiry < DateTime.UtcNow)
				throw new UnauthorizedException("Token di invito non valido o scaduto");

			await _employeeFacade.CreateAsync(new Employee
			{
				UserId = user.Id,
				Name = model.Name,
				Surname = model.Surname,
				FiscalCode = model.FiscalCode,
				Phone = model.Phone,
			});

			user.PasswordHash = _userUtils.HashAndCheckPassword(model.Password);
			user.Status = UserStatus.Active;
			user.InviteToken = null;
			user.InviteTokenExpiry = null;
			_logger.LogInformation($"User {user.Email} accepted invite");

			await _userFacade.UpdateAsync(user);
		}

		public async Task CreateUser(UserRegister model, Guid userId)
		{
			User requester = await _userFacade.FindByIdAsync(userId);
			if (requester is null)
			{
				throw new UnauthorizedException();
			}
			_logger.LogInformation($"User {requester.Email} requested signup of user {model.Email}");

			if (model.Role <= requester.Role)
			{
				throw new ForbiddenException("Non puoi creare un utente con un ruolo uguale o superiore al tuo");
			}

			User existing = await _userFacade.FindByEmailAsync(model.Email);
			if (existing is not null)
			{
				throw new EntityException("Email già in uso");
			}

			await _userFacade.CreateAsync(new User
			{
				Id = Guid.NewGuid(),
				Email = model.Email,
				PasswordHash = _userUtils.HashAndCheckPassword(model.Password),
				Role = model.Role,
				Status = UserStatus.Disabled,
			});
		}

		public async Task ChangePassword(Guid userId, ChangePassword model)
		{
			User user = await _userFacade.FindByIdAsync(userId);
			if (user is null)
			{
				throw new UnauthorizedException();
			}

			if (!_userUtils.CheckPassword(user, model.CurrentPassword))
			{
				throw new EntityException("La password attuale non è corretta");
			}

			user.PasswordHash = _userUtils.HashAndCheckPassword(model.NewPassword);
			await _userFacade.UpdateAsync(user);
		}
	}
}