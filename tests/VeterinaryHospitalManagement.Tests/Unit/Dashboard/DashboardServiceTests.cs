using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Dashboard;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Tests.Unit.Dashboard;

public class DashboardServiceTests
{
    private sealed class FakeVietnamTimeProvider(DateTimeOffset utcNow) : IVietnamTimeProvider
    {
        public DateTimeOffset UtcNow => utcNow;
        public DateTimeOffset LocalNow => utcNow.ToOffset(TimeSpan.FromHours(7));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Dashboard_Calculates_Counts_And_Revenue_With_Vietnam_Time_Boundary()
    {
        using var db = CreateDbContext();

        // Giả sử hiện tại là 14:00 giờ Việt Nam (07:00 UTC) ngày 28/09/2026
        // Ranh giới ngày 28/09 VN: từ 17:00 UTC ngày 27/09 đến 17:00 UTC ngày 28/09
        var nowUtc = new DateTimeOffset(2026, 9, 28, 7, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeVietnamTimeProvider(nowUtc);

        var species = new Species { Code = "CAT", Name = "Mèo" };
        var owner = new Owner { OwnerCode = "O-001", FullName = "Nguyễn Văn A", PhoneNumber = "0123456789" };
        var pet = new Pet { PetCode = "P-001", Name = "Miu", Species = species, Owner = owner };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();

        // 1. Thêm các lượt khám với các trạng thái khác nhau
        db.Visits.AddRange(
            new Visit
            {
                VisitNumber = "V-001",
                PetId = pet.Id,
                Status = VisitStatus.Waiting,
                CheckedInAt = nowUtc.AddMinutes(-30),
                PetNameSnapshot = "Miu",
                OwnerNameSnapshot = "Nguyễn Văn A"
            },
            new Visit
            {
                VisitNumber = "V-002",
                PetId = pet.Id,
                Status = VisitStatus.Waiting,
                CheckedInAt = nowUtc.AddMinutes(-20),
                PetNameSnapshot = "Ki",
                OwnerNameSnapshot = "Trần Thị B"
            },
            new Visit
            {
                VisitNumber = "V-003",
                PetId = pet.Id,
                Status = VisitStatus.InProgress,
                CheckedInAt = nowUtc.AddHours(-1),
                PetNameSnapshot = "Lu",
                OwnerNameSnapshot = "Lê Văn C"
            },
            new Visit
            {
                VisitNumber = "V-004",
                PetId = pet.Id,
                Status = VisitStatus.Completed, // Chưa invoice
                CheckedInAt = nowUtc.AddHours(-2),
                PetNameSnapshot = "Bông",
                OwnerNameSnapshot = "Phạm Văn D"
            },
            new Visit
            {
                VisitNumber = "V-005",
                PetId = pet.Id,
                Status = VisitStatus.Completed, // Đã invoice
                CheckedInAt = nowUtc.AddHours(-3),
                PetNameSnapshot = "Đốm",
                OwnerNameSnapshot = "Hoàng Thị E"
            }
        );
        await db.SaveChangesAsync();

        var visitWithInvoice = await db.Visits.FirstAsync(v => v.VisitNumber == "V-005");

        // 2. Thêm Invoices: 1 invoice hôm qua VN, 2 invoices hôm nay VN
        var yesterdayVnUtc = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero); // 17:00 ngày 27/09 VN -> hôm qua
        var todayVnUtc1 = new DateTimeOffset(2026, 9, 27, 18, 0, 0, TimeSpan.Zero); // 01:00 ngày 28/09 VN -> hôm nay
        var todayVnUtc2 = new DateTimeOffset(2026, 9, 28, 5, 0, 0, TimeSpan.Zero); // 12:00 ngày 28/09 VN -> hôm nay

        db.Invoices.AddRange(
            new Invoice
            {
                InvoiceNumber = "INV-YESTERDAY",
                VisitId = 999,
                TotalAmount = 500_000m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAt = yesterdayVnUtc,
                PetNameSnapshot = "Pet X",
                OwnerNameSnapshot = "Owner X"
            },
            new Invoice
            {
                InvoiceNumber = "INV-TODAY-1",
                VisitId = visitWithInvoice.Id,
                TotalAmount = 250_000m,
                PaymentMethod = PaymentMethod.Cash,
                PaidAt = todayVnUtc1,
                PetNameSnapshot = "Đốm",
                OwnerNameSnapshot = "Hoàng Thị E"
            },
            new Invoice
            {
                InvoiceNumber = "INV-TODAY-2",
                VisitId = 998,
                TotalAmount = 350_000m,
                PaymentMethod = PaymentMethod.BankTransfer,
                PaidAt = todayVnUtc2,
                PetNameSnapshot = "Pet Y",
                OwnerNameSnapshot = "Owner Y"
            }
        );
        await db.SaveChangesAsync();

        var service = new DashboardService(db, timeProvider);

        // Act
        var result = await service.GetDashboardMetricsAsync();

        // Assert
        Assert.Equal(2, result.WaitingCount);
        Assert.Equal(1, result.InProgressCount);
        Assert.Equal(1, result.CompletedUnpaidCount); // Chỉ V-004
        Assert.Equal(2, result.TodayPaidCount); // Chỉ 2 invoices hôm nay
        Assert.Equal(600_000m, result.TodayRevenue); // 250_000 + 350_000

        Assert.Equal(3, result.ActiveVisits.Count); // 2 Waiting + 1 InProgress
        Assert.Equal(2, result.RecentInvoices.Count);
    }
}
