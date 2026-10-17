namespace Five68.Services;

public interface IShiftNotificationService
{
	Task NotifyShiftChangedAsync(DateOnly shiftDate);
}