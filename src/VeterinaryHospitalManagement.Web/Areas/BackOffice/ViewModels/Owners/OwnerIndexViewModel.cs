using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Owners;

public sealed class OwnerIndexViewModel
{
    public string? PhoneNumber { get; set; }

    public string? OwnerCode { get; set; }

    public bool Searched { get; set; }

    public IReadOnlyList<OwnerSearchItem> Results { get; set; } = [];
}
