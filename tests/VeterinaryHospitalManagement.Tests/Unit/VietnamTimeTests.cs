using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Tests.Unit;

public class VietnamTimeTests
{
    [Fact]
    public void ConvertsUtcInstantToVietnamLocalTime()
    {
        var utc = new DateTimeOffset(2026, 9, 15, 1, 30, 0, TimeSpan.Zero);
        var provider = new VietnamTimeProvider(new FixedTimeProvider(utc));

        var local = provider.LocalNow;

        Assert.Equal(new DateTimeOffset(2026, 9, 15, 8, 30, 0, TimeSpan.FromHours(7)), local);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
