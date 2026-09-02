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
		private readonly ShiftFacade _shiftFacade;
		private readonly TransactionFacade _transactionFacade;
		private readonly AuthUtils _authUtils;
		private readonly UserUtils _userUtils;
		private readonly IInviteService _notificationService;
		private readonly ILogger _logger;

		public UserService(
			UserFacade userFacade,
			ShiftFacade shiftFacade,
			TransactionFacade transactionFacade,
			AuthUtils authUtils,
			UserUtils userUtils,
			IInviteService notificationService,
			ILogger<UserService> logger)
		{
			_userFacade = userFacade;
			_shiftFacade = shiftFacade;
			_transactionFacade = transactionFacade;
			_authUtils = authUtils;
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

		public async Task<string> GenerateInvite(Guid userId, Guid requesterId, bool sendEmail)
		{
			(User requester, User user, string token) = await CreateInviteToken(userId, requesterId);

			if (sendEmail)
			{
				await _notificationService.SendInviteAsync(user.Email, token);
				_logger.LogInformation($"User {requester.Id} ({requester.Email}) invited {user.Id} ({user.Email}) to change password");
			}
			else
			{
				_logger.LogInformation($"User {requester.Id} ({requester.Email}) generated an invite link for {user.Id} ({user.Email}) without sending an email");
			}

			return token;
		}

		private async Task<(User requester, User user, string token)> CreateInviteToken(Guid userId, Guid requesterId)
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

			return (requester, user, token);
		}

		public async Task Delete(Guid id, Guid requesterId)
		{
			User requester = await _authUtils.RequireManagerOrAdmin(requesterId);
			User user = await _userFacade.FindByIdAsync(id) ?? throw new NotFoundException("Utente non trovato");

			// Soft-delete: NON si cancella la riga User/Employee (cascaderebbe su Shift/SwapRequest
			// storici, vedi Five68DbContext.cs OnDelete=Cascade su quelle FK). Si disabilita
			// l'account e si rimuove solo dalle giornate future, come richiesto dallo spec.
			user.Status = UserStatus.Disabled;

			await _transactionFacade.ExecuteAsync(async () =>
			{
				await _userFacade.UpdateAsync(user);
				await _shiftFacade.DeleteFutureAssignmentsForEmployeeAsync(id, DateOnly.FromDateTime(DateTime.UtcNow));
			});

			_logger.LogInformation($"User {requester.Id} ({requester.Email}) disabled user {id} ({user.Email})");
		}

		public async Task AcceptInvite(InviteAccept model)
		{
			User user = await _userFacade.FindByInviteTokenAsync(model.Token);
			if (user is null || user.InviteTokenExpiry < DateTime.UtcNow)
				throw new UnauthorizedException("Token di invito non valido o scaduto");

			user.PasswordHash = _userUtils.HashAndCheckPassword(model.Password);
			user.Status = UserStatus.Active;
			user.InviteToken = null;
			user.InviteTokenExpiry = null;
			_logger.LogInformation($"User {user.Id} ({user.Email}) accepted invite");

			await _userFacade.UpdateAsync(user);
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