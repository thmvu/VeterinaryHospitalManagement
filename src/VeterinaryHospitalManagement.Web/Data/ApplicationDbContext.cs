using Microsoft.EntityFrameworkCore;

namespace VeterinaryHospitalManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
}
