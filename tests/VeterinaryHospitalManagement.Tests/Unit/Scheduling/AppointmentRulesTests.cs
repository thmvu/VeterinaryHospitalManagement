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
}
