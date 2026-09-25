namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineNameSnapshot { get; set; } = string.Empty;
    public string UnitSnapshot { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Instructions { get; set; }
    public Prescription Prescription { get; set; } = null!;
    public Medicine Medicine { get; set; } = null!;
}
