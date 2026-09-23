namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class AppointmentDetailViewModel
{
    public int Id { get; set; }
    public string AppointmentNumber { get; set; } = string.Empty;
    public int PetId { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerPhone { get; set; } = string.Empty;
    public int VeterinarianId { get; set; }
    public string VeterinarianName { get; set; } = string.Empty;
    public DateTime StartAtLocal { get; set; }
    public DateTime EndAtLocal { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAtLocal { get; set; }
    public string RowVersionBase64 { get; set; } = string.Empty;

    public bool CanCancel => Status == "Scheduled";
    public bool CanMarkNoShow(DateTime nowLocal) => Status == "Scheduled" && nowLocal >= EndAtLocal;
}
