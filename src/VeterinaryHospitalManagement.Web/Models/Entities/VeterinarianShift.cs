namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class VeterinarianShift
{
    public int Id { get; set; }
    public int VeterinarianId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];
    public VeterinarianProfile Veterinarian { get; set; } = null!;
}
