using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Dashboard;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Dashboard;

public sealed class DashboardService(
    ApplicationDbContext db,
    IVietnamTimeProvider timeProvider) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardMetricsAsync(CancellationToken cancellationToken = default)
    {
        var localNow = timeProvider.LocalNow;
        var startOfTodayUtc = new DateTimeOffset(localNow.Date, localNow.Offset).ToUniversalTime();
        var endOfTodayUtc = startOfTodayUtc.AddDays(1);

        var waitingCount = await db.Visits
            .CountAsync(v => v.Status == VisitStatus.Waiting, cancellationToken);

        var inProgressCount = await db.Visits
            .CountAsync(v => v.Status == VisitStatus.InProgress, cancellationToken);

        var completedUnpaidCount = await db.Visits
            .CountAsync(v => v.Status == VisitStatus.Completed && v.Invoice == null, cancellationToken);

        var todayInvoicesQuery = db.Invoices
            .Where(i => i.PaidAt >= startOfTodayUtc && i.PaidAt < endOfTodayUtc);

        var todayPaidCount = await todayInvoicesQuery
            .CountAsync(cancellationToken);

        var todayRevenue = await todayInvoicesQuery
            .SumAsync(i => (decimal?)i.TotalAmount, cancellationToken) ?? 0m;

        var activeVisits = await db.Visits
            .AsNoTracking()
            .Include(v => v.Pet)
                .ThenInclude(p => p.Species)
            .Where(v => v.Status == VisitStatus.Waiting || v.Status == VisitStatus.InProgress)
            .OrderBy(v => v.CheckedInAt)
            .Take(15)
            .Select(v => new DashboardActiveVisitItemViewModel(
                v.Id,
                v.PetNameSnapshot,
                v.Pet != null && v.Pet.Species != null ? v.Pet.Species.Name : string.Empty,
                v.OwnerNameSnapshot,
                v.OwnerPhoneSnapshot,
                v.VeterinarianNameSnapshot,
                v.Status.ToString(),
                v.CheckedInAt
            ))
            .ToListAsync(cancellationToken);

        var recentInvoices = await todayInvoicesQuery
            .AsNoTracking()
            .OrderByDescending(i => i.PaidAt)
            .Take(5)
            .Select(i => new DashboardRecentInvoiceItemViewModel(
                i.Id,
                i.InvoiceNumber,
                i.VisitId,
                i.PetNameSnapshot,
                i.OwnerNameSnapshot,
                i.TotalAmount,
                i.PaymentMethod.ToString(),
                i.PaidAt
            ))
            .ToListAsync(cancellationToken);

        return new DashboardViewModel
        {
            WaitingCount = waitingCount,
            InProgressCount = inProgressCount,
            CompletedUnpaidCount = completedUnpaidCount,
            TodayPaidCount = todayPaidCount,
            TodayRevenue = todayRevenue,
            ActiveVisits = activeVisits,
            RecentInvoices = recentInvoices
        };
    }
}
