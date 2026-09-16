namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class Breed
{
    public int Id { get; set; }
    public int SpeciesId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Species Species { get; set; } = null!;
    public ICollection<Pet> Pets { get; set; } = [];
}
