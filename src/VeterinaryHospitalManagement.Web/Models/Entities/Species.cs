namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class Species
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Breed> Breeds { get; set; } = [];
    public ICollection<Pet> Pets { get; set; } = [];
}
