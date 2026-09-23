namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class VeterinarianProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DoctorCode { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public ApplicationUser User { get; set; } = null!;
    public ICollection<VeterinarianShift> Shifts { get; set; } = new List<VeterinarianShift>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
