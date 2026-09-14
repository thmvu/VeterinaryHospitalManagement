namespace VeterinaryHospitalManagement.Web.Services.Time;

public sealed class VietnamTimeProvider(TimeProvider timeProvider) : IVietnamTimeProvider
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(UtcNow, VietnamTimeZone);

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        var timeZoneId = OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh";
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}
