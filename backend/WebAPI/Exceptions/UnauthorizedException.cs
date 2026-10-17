namespace Five68.Exceptions;

public class UnauthorizedException : Exception
{
	public UnauthorizedException(string message) : base(message)
	{
	}

	public UnauthorizedException() : base("Non sei autorizzato a eseguire questa azione") { }
}