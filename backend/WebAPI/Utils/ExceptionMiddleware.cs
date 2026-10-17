using Five68.Exceptions;
using Npgsql;

namespace Five68.Utils;

public class ExceptionMiddleware
{
	private readonly RequestDelegate _next;
	private readonly ILogger _logger;

	public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
	{
		_next = next;
		_logger = logger;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		try
		{
			await _next(context);
		}
		catch (Exception ex)
		{
			await HandleExceptionAsync(context, ex);
		}
	}

	private Task HandleExceptionAsync(HttpContext context, Exception ex)
	{
		context.Response.ContentType = "application/json";
		string message = ex.Message;

		switch (ex)
		{
			case EntityException e:
				_logger.LogError(e, "");
				context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
				break;
			case InternalServerErrorException e:
				_logger.LogError(e, "");
				context.Response.StatusCode = StatusCodes.Status500InternalServerError;
				break;
			case UnauthorizedException e:
				_logger.LogError(e, "");
				context.Response.StatusCode = StatusCodes.Status401Unauthorized;
				break;
			case ForbiddenException e:
				_logger.LogError(e, "");
				context.Response.StatusCode = StatusCodes.Status403Forbidden;
				break;
			case NotFoundException:
				context.Response.StatusCode = StatusCodes.Status404NotFound;
				break;
			case NpgsqlException { IsTransient: true }:
			case InvalidOperationException { InnerException: NpgsqlException { IsTransient: true } }:
				_logger.LogError(ex, "Database unavailable");
				context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
				message = "Database is unavailable. Please try again later.";
				break;
			default:
				_logger.LogCritical(ex, "");
				context.Response.StatusCode = StatusCodes.Status500InternalServerError;
				break;
		}

		return context.Response.WriteAsJsonAsync(new { message });
	}
}