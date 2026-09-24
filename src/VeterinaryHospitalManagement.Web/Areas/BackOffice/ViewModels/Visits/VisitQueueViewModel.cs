using Microsoft.AspNetCore.Mvc.Rendering;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class VisitQueueViewModel
{
    public int? SelectedVeterinarianId { get; set; }
    public IReadOnlyList<SelectListItem> VeterinarianOptions { get; set; } = [];
    public IReadOnlyList<VisitQueueItem> Queue { get; set; } = [];

    public int WaitingCount => Queue.Count(x => x.Status == "Waiting");
    public int InProgressCount => Queue.Count(x => x.Status == "InProgress");
    public int TotalActiveCount => Queue.Count;
}
