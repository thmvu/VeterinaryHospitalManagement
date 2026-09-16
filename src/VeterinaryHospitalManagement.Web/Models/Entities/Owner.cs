namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class Owner
{
    public int Id { get; set; }
    public string OwnerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<Pet> Pets { get; set; } = [];
}
