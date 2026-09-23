using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class Pet
{
    public int Id { get; set; }
    public string PetCode { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SpeciesId { get; set; }
    public int? BreedId { get; set; }
    public PetSex Sex { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Color { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Owner Owner { get; set; } = null!;
    public Species Species { get; set; } = null!;
    public Breed? Breed { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
