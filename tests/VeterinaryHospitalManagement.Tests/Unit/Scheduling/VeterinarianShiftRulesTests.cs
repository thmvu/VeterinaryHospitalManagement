using VeterinaryHospitalManagement.Web.Services.Scheduling;

namespace VeterinaryHospitalManagement.Tests.Unit.Scheduling;

public sealed class VeterinarianShiftRulesTests
{
    // ── ValidateTimeRange ────────────────────────────────────────────────────

    [Fact]
    public void ValidateTimeRange_accepts_start_before_end()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(4);
        // Should not throw
        VeterinarianShiftRules.ValidateTimeRange(start, end);
    }

    [Fact]
    public void ValidateTimeRange_rejects_start_equal_to_end()
    {
        var at = DateTimeOffset.UtcNow;
        var ex = Assert.Throws<ShiftManagementException>(
            () => VeterinarianShiftRules.ValidateTimeRange(at, at));
        Assert.Contains("sau", ex.Message);
    }

    [Fact]
    public void ValidateTimeRange_rejects_start_after_end()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddMinutes(-1);
        Assert.Throws<ShiftManagementException>(
            () => VeterinarianShiftRules.ValidateTimeRange(start, end));
    }

    // ── AreOverlapping ───────────────────────────────────────────────────────

    [Fact]
    public void AreOverlapping_returns_true_for_full_containment()
    {
        // existing: 08:00 – 17:00; new: 10:00 – 14:00 (fully inside)
        var s1 = Dt(10, 0); var e1 = Dt(14, 0);
        var s2 = Dt(8, 0);  var e2 = Dt(17, 0);
        Assert.True(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    [Fact]
    public void AreOverlapping_returns_true_for_partial_overlap()
    {
        // new: 10:00 – 14:00; existing: 12:00 – 16:00
        var s1 = Dt(10, 0); var e1 = Dt(14, 0);
        var s2 = Dt(12, 0); var e2 = Dt(16, 0);
        Assert.True(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    [Fact]
    public void AreOverlapping_returns_true_for_reverse_partial_overlap()
    {
        // new: 12:00 – 18:00; existing: 10:00 – 14:00
        var s1 = Dt(12, 0); var e1 = Dt(18, 0);
        var s2 = Dt(10, 0); var e2 = Dt(14, 0);
        Assert.True(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    [Fact]
    public void AreOverlapping_returns_false_for_boundary_touching_end_to_start()
    {
        // morning: 08:00 – 12:00; afternoon: 12:00 – 17:00 — touching boundary, not overlapping
        var s1 = Dt(8, 0);  var e1 = Dt(12, 0);
        var s2 = Dt(12, 0); var e2 = Dt(17, 0);
        Assert.False(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    [Fact]
    public void AreOverlapping_returns_false_for_completely_before()
    {
        // new: 06:00 – 07:00; existing: 08:00 – 17:00
        var s1 = Dt(6, 0); var e1 = Dt(7, 0);
        var s2 = Dt(8, 0); var e2 = Dt(17, 0);
        Assert.False(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    [Fact]
    public void AreOverlapping_returns_false_for_completely_after()
    {
        // new: 18:00 – 22:00; existing: 08:00 – 17:00
        var s1 = Dt(18, 0); var e1 = Dt(22, 0);
        var s2 = Dt(8, 0);  var e2 = Dt(17, 0);
        Assert.False(VeterinarianShiftRules.AreOverlapping(s1, e1, s2, e2));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateTimeOffset Dt(int hour, int minute)
        => new DateTimeOffset(2026, 1, 1, hour, minute, 0, TimeSpan.Zero);
}
