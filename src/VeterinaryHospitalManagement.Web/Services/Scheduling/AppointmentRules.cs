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

    public static string NormalizeCancellationReason(string? reason)
    {
        var value = reason?.Trim() ?? string.Empty;
        if (value.Length == 0) throw new AppointmentManagementException("Lý do hủy lịch hẹn là bắt buộc.");
        if (value.Length > 500) throw new AppointmentManagementException("Lý do hủy không được vượt quá 500 ký tự.");
        return value;
    }

    public static void ValidateCanCancel(Models.Enums.AppointmentStatus status)
    {
        if (status != Models.Enums.AppointmentStatus.Scheduled)
            throw new AppointmentManagementException("Chỉ có thể hủy lịch hẹn đang ở trạng thái Chờ khám.");
    }

    public static void ValidateCanMarkNoShow(Models.Enums.AppointmentStatus status, DateTimeOffset endAt, DateTimeOffset now)
    {
        if (status != Models.Enums.AppointmentStatus.Scheduled)
            throw new AppointmentManagementException("Chỉ có thể đánh dấu vắng mặt cho lịch hẹn đang ở trạng thái Chờ khám.");
        if (now < endAt)
            throw new AppointmentManagementException("Chỉ có thể đánh dấu vắng mặt sau khi đã kết thúc thời gian hẹn.");
    }

    public static bool AreOverlapping(DateTimeOffset firstStart, DateTimeOffset firstEnd,
        DateTimeOffset secondStart, DateTimeOffset secondEnd)
        => firstStart < secondEnd && secondStart < firstEnd;
}
