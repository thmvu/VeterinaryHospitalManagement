using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Reports;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Tests.Unit.Reports;

public sealed class RevenueReportServiceTests
{
    private sealed class VietnamClock : IVietnamTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 9, 28, 7, 0, 0, TimeSpan.Zero);
        public DateTimeOffset LocalNow => UtcNow.ToOffset(TimeSpan.FromHours(7));
    }

    [Fact]
    public async Task Uses_paid_at_with_vietnam_day_boundaries_and_matches_row_total()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var instants = new[]
        {
            new DateTimeOffset(2026, 9, 27, 16, 59, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 27, 17, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 16, 59, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 28, 17, 0, 0, TimeSpan.Zero)
        };
        for (var i = 0; i < instants.Length; i++)
        {
            db.Invoices.Add(new Invoice
            {
                InvoiceNumber = $"INV-{i}", VisitId = i + 1,
                OwnerNameSnapshot = "Chủ nuôi", PetNameSnapshot = "Miu",
                TotalAmount = (i + 1) * 100m, PaymentMethod = PaymentMethod.Cash,
                PaidAt = instants[i], ProcessedByUserId = "cashier"
            });
        }
        await db.SaveChangesAsync();

        var result = await new RevenueReportService(db, new VietnamClock())
            .GetAsync(new DateOnly(2026, 9, 28), new DateOnly(2026, 9, 28));

        Assert.Equal(2, result.InvoiceCount);
        Assert.Equal(500m, result.TotalAmount);
        Assert.Equal(result.TotalAmount, result.Rows.Sum(row => row.TotalAmount));
        Assert.Equal(new[] { "INV-2", "INV-1" }, result.Rows.Select(row => row.InvoiceNumber));
    }

    [Fact]
    public async Task Rejects_reversed_range()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var service = new RevenueReportService(db, new VietnamClock());

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(
            new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 28)));
    }
}
