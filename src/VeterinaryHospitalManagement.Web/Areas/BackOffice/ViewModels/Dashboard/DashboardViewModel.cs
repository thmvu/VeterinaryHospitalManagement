namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public int WaitingCount { get; init; }
    public int InProgressCount { get; init; }
    public int CompletedUnpaidCount { get; init; }
    public int TodayPaidCount { get; init; }
    public decimal TodayRevenue { get; init; }

    public IReadOnlyList<DashboardActiveVisitItemViewModel> ActiveVisits { get; init; } = [];
    public IReadOnlyList<DashboardRecentInvoiceItemViewModel> RecentInvoices { get; init; } = [];
}

public sealed record DashboardActiveVisitItemViewModel(
    int VisitId,
    string PetName,
    string PetSpecies,
    string OwnerName,
    string OwnerPhone,
    string VeterinarianName,
    string Status,
    DateTimeOffset CheckedInAt
);

public sealed record DashboardRecentInvoiceItemViewModel(
    int InvoiceId,
    string InvoiceNumber,
    int VisitId,
    string PetName,
    string OwnerName,
    decimal TotalAmount,
    string PaymentMethod,
    DateTimeOffset PaidAt
);
