using VeterinaryHospitalManagement.Web.Services.Scheduling;

namespace VeterinaryHospitalManagement.Tests.Unit.Scheduling;

public sealed class AppointmentRulesTests
{
    [Fact] public void Adjacent_periods_do_not_overlap()
    { var a=DateTimeOffset.UtcNow; Assert.False(AppointmentRules.AreOverlapping(a,a.AddHours(1),a.AddHours(1),a.AddHours(2))); }
    [Fact] public void Intersecting_periods_overlap()
    { var a=DateTimeOffset.UtcNow; Assert.True(AppointmentRules.AreOverlapping(a,a.AddHours(2),a.AddHours(1),a.AddHours(3))); }
    [Fact] public void Reason_is_trimmed_and_required()
    { Assert.Equal("Tái khám", AppointmentRules.NormalizeReason("  Tái khám  ")); Assert.Throws<AppointmentManagementException>(()=>AppointmentRules.NormalizeReason("  ")); }
    [Fact] public void End_must_be_after_start()
    { var a=DateTimeOffset.UtcNow; Assert.Throws<AppointmentManagementException>(()=>AppointmentRules.ValidateTimeRange(a,a)); }
    [Fact] public void Cancellation_reason_is_trimmed_and_required()
    {
        Assert.Equal("Bận đột xuất", AppointmentRules.NormalizeCancellationReason("  Bận đột xuất  "));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.NormalizeCancellationReason("   "));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.NormalizeCancellationReason(null));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.NormalizeCancellationReason(new string('a', 501)));
    }
    [Fact] public void Can_cancel_only_when_scheduled()
    {
        AppointmentRules.ValidateCanCancel(Web.Models.Enums.AppointmentStatus.Scheduled);
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.ValidateCanCancel(Web.Models.Enums.AppointmentStatus.CheckedIn));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.ValidateCanCancel(Web.Models.Enums.AppointmentStatus.Cancelled));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.ValidateCanCancel(Web.Models.Enums.AppointmentStatus.NoShow));
    }
    [Fact] public void Can_mark_no_show_only_after_end_time_when_scheduled()
    {
        var now = DateTimeOffset.UtcNow;
        AppointmentRules.ValidateCanMarkNoShow(Web.Models.Enums.AppointmentStatus.Scheduled, now.AddMinutes(-5), now);
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.ValidateCanMarkNoShow(Web.Models.Enums.AppointmentStatus.Scheduled, now.AddMinutes(5), now));
        Assert.Throws<AppointmentManagementException>(() => AppointmentRules.ValidateCanMarkNoShow(Web.Models.Enums.AppointmentStatus.CheckedIn, now.AddMinutes(-5), now));
    }
}
