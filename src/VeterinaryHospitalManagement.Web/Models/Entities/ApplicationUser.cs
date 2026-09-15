using Microsoft.AspNetCore.Identity;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}
