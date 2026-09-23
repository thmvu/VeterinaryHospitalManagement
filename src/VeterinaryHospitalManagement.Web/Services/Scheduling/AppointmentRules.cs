namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

public static class AppointmentRules
{
    public static void ValidateTimeRange(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (startAt >= endAt) throw new AppointmentManagementException("Giờ kết thúc phải sau giờ bắt đầu.");
    }

    public static string NormalizeReason(string reason)
    {
        var value = reason?.Trim() ?? string.Empty;
        if (value.Length == 0) throw new AppointmentManagementException("Lý do khám là bắt buộc.");
        if (value.Length > 500) throw new AppointmentManagementException("Lý do khám không được vượt quá 500 ký tự.");
        return value;
    }

    public static bool AreOverlapping(DateTimeOffset firstStart, DateTimeOffset firstEnd,
        DateTimeOffset secondStart, DateTimeOffset secondEnd)
        => firstStart < secondEnd && secondStart < firstEnd;
}
