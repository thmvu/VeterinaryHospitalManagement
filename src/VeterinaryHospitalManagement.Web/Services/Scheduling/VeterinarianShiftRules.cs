namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

public class ShiftManagementException(string message) : Exception(message);

public sealed class ShiftConcurrencyException()
    : ShiftManagementException("Ca làm việc đã được thay đổi bởi người khác. Hãy tải lại trang và thử lại.");

public static class VeterinarianShiftRules
{
    /// <summary>
    /// Two shifts overlap when a.StartAt &lt; b.EndAt &amp;&amp; b.StartAt &lt; a.EndAt.
    /// Boundary-touching shifts (end of one == start of next) are NOT overlapping.
    /// </summary>
    public static bool AreOverlapping(
        DateTimeOffset s1, DateTimeOffset e1,
        DateTimeOffset s2, DateTimeOffset e2)
        => s1 < e2 && s2 < e1;

    /// <summary>
    /// Validates that startAt &lt; endAt (both UTC). Throws <see cref="ShiftManagementException"/> otherwise.
    /// </summary>
    public static void ValidateTimeRange(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        if (startAt >= endAt)
        {
            throw new ShiftManagementException("Thời gian kết thúc ca phải sau thời gian bắt đầu.");
        }
    }
}
