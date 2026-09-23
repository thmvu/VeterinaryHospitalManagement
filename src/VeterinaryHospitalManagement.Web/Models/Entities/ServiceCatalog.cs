namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class ServiceCatalog
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; set; } = [];
}
