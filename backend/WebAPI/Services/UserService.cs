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
		private readonly UserFacade userFacade_;
		private readonly UserUtils userUtils_;
		private readonly IEmailService emailService_;
		private ILogger logger_;

		public UserService(UserFacade userFacade, UserUtils userUtils, IEmailService emailService, ILogger<UserService> logger)
		{
			userFacade_ = userFacade;
			userUtils_ = userUtils;
			emailService_ = emailService;
			logger_ = logger;
		}

		public async Task<UserDTO> GetUserDTO(Guid id)
		{
			return UserDTO.FromUser(await userFacade_.FindByIdAsync(id));
		}

		public async Task<User> Get(Guid id)
		{
			return await userFacade_.FindByIdAsync(id);
		}

		public async Task<(bool success, User? user)> TryGetUserAndCheckPasswordAsync(UserLogin loginCredentials)
		{
			User? user = await userFacade_.FindByEmailAsync(loginCredentials.Email);

			if (user is null)
			{
				return (false, null);
			}

			if (!userUtils_.CheckPassword(user, loginCredentials.Password))
			{
				return (false, null);
			}

			return (true, user);
		}

		public async Task<IEnumerable<UserDTO>> GetAll()
		{
			return (await userFacade_.GetAll()).Select(x => UserDTO.FromUser(x));
		}

		public async Task<string> GenerateInvite(Guid userId, Guid requesterId)
		{
			User requester = await userFacade_.FindByIdAsync(requesterId);
			if (requester is null)
			{
				throw new UnauthorizedException();
			}
			if (requester.Role >= UserRole.Employee)
			{
				throw new ForbiddenException("You can't perform this action");
			}

			User user = await userFacade_.FindByIdAsync(userId);
			if (user is null)
				throw new NotFoundException("User not found");

			string token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

			user.Status = UserStatus.Pending;
			user.InviteToken = token;
			user.InviteTokenExpiry = DateTime.UtcNow.AddDays(7);

			await userFacade_.UpdateAsync(user);
			await emailService_.SendInviteAsync(user.Email, token);
			logger_.LogInformation($"User {requester.Email} invited {user.Email} to change password");
			return token;
		}

		public async Task AcceptInvite(InviteAccept model)
		{
			User user = await userFacade_.FindByInviteTokenAsync(model.Token);
			if (user is null || user.InviteTokenExpiry < DateTime.UtcNow)
				throw new UnauthorizedException("Invalid or expired invite token");

			user.PasswordHash = userUtils_.HashAndCheckPassword(model.Password);
			user.Status = UserStatus.Active;
			user.InviteToken = null;
			user.InviteTokenExpiry = null;
			logger_.LogInformation($"User {user.Email} accepted invite");
			await userFacade_.UpdateAsync(user);
		}

		public async Task CreateUser(UserRegister model, Guid userId)
		{
			User requester = await userFacade_.FindByIdAsync(userId);
			if (requester is null)
			{
				throw new UnauthorizedException();
			}
			logger_.LogInformation($"User {requester.Email} requested signup of user {model.Email}");

			if (model.Role <= requester.Role)
			{
				throw new ForbiddenException("Cannot create a user with higher or equal role");
			}

			User existing = await userFacade_.FindByEmailAsync(model.Email);
			if (existing is not null)
			{
				throw new EntityException("Email already in use");
			}

			await userFacade_.CreateAsync(new User
			{
				Id = Guid.NewGuid(),
				Email = model.Email,
				PasswordHash = userUtils_.HashAndCheckPassword(model.Password),
				FullName = model.FullName,
				Role = model.Role,
				Status = UserStatus.Disabled,
			});
		}

		public async Task ChangePassword(Guid userId, ChangePassword model)
		{
			User user = await userFacade_.FindByIdAsync(userId);
			if (user is null)
			{
				throw new UnauthorizedException();
			}

			if (!userUtils_.CheckPassword(user, model.CurrentPassword))
			{
				throw new EntityException("Current password is incorrect");
			}

			user.PasswordHash = userUtils_.HashAndCheckPassword(model.NewPassword);
			await userFacade_.UpdateAsync(user);
		}
	}
}