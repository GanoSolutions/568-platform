using Five68.Exceptions;
using Five68.Facades;
using Five68.Models;

namespace Five68.Utils
{
	public class AuthUtils
	{
		private readonly UserFacade _userFacade;

		public AuthUtils(UserFacade userFacade)
		{
			_userFacade = userFacade;
		}

		public async Task<User> RequireManagerOrAdmin(Guid requesterId)
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