using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Audit;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Audit;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Tests.Unit.Audit;

public class AuditQueryServiceTests
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
    public async Task AuditQueryService_Filters_By_Action_And_Search_Correctly()
    {
        using var db = CreateDbContext();
        var nowUtc = new DateTimeOffset(2026, 9, 28, 7, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeVietnamTimeProvider(nowUtc);

        db.AuditLogs.AddRange(
            new AuditLog
            {
                ActorType = "Internal",
                UserId = "user-1",
                Action = "Visit.Completed",
                EntityName = "Visit",
                EntityId = "101",
                Description = "Hoàn tất ca khám cho thú cưng Miu",
                CreatedAt = nowUtc.AddMinutes(-30)
            },
            new AuditLog
            {
                ActorType = "Internal",
                UserId = "user-2",
                Action = "Invoice.Paid",
                EntityName = "Invoice",
                EntityId = "201",
                Description = "Thanh toán hóa đơn tiền mặt",
                CreatedAt = nowUtc.AddMinutes(-20)
            },
            new AuditLog
            {
                ActorType = "System",
                Action = "Identity.Seed",
                EntityName = "System",
                EntityId = "1",
                Description = "Khởi tạo hệ thống",
                CreatedAt = nowUtc.AddMinutes(-10)
            }
        );
        await db.SaveChangesAsync();

        var service = new AuditQueryService(db, timeProvider);

        // 1. Filter theo Action
        var filterByAction = new AuditLogIndexViewModel { Action = "Visit.Completed" };
        var result1 = await service.QueryAuditLogsAsync(filterByAction);

        Assert.Equal(1, result1.TotalItems);
        Assert.Single(result1.Items);
        Assert.Equal("101", result1.Items[0].EntityId);

        // 2. Filter theo Search
        var filterBySearch = new AuditLogIndexViewModel { Search = "tiền mặt" };
        var result2 = await service.QueryAuditLogsAsync(filterBySearch);

        Assert.Equal(1, result2.TotalItems);
        Assert.Equal("201", result2.Items[0].EntityId);

        // 3. Dropdown options
        Assert.Contains("Visit.Completed", result1.AvailableActions);
        Assert.Contains("Invoice.Paid", result1.AvailableActions);
        Assert.Contains("Visit", result1.AvailableEntities);
    }

    [Fact]
    public async Task AuditQueryService_Clamps_Page_And_PageSize()
    {
        using var db = CreateDbContext();
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "System",
            Action = "Identity.Seed",
            EntityName = "System",
            EntityId = "1",
            Description = "Khởi tạo hệ thống",
            CreatedAt = new DateTimeOffset(2026, 9, 28, 7, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var service = new AuditQueryService(db,
            new FakeVietnamTimeProvider(new DateTimeOffset(2026, 9, 28, 7, 0, 0, TimeSpan.Zero)));
        var result = await service.QueryAuditLogsAsync(new AuditLogIndexViewModel
        {
            Page = int.MaxValue,
            PageSize = int.MaxValue
        });

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Items);
    }
}
