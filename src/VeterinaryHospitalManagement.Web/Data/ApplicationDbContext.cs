using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Owner> Owners => Set<Owner>();

    public DbSet<Species> Species => Set<Species>();

    public DbSet<Breed> Breeds => Set<Breed>();

    public DbSet<Pet> Pets => Set<Pet>();

    public DbSet<VeterinarianProfile> VeterinarianProfiles => Set<VeterinarianProfile>();

    public DbSet<VeterinarianShift> VeterinarianShifts => Set<VeterinarianShift>();

    public DbSet<ServiceCatalog> ServiceCatalogs => Set<ServiceCatalog>();

    public DbSet<Medicine> Medicines => Set<Medicine>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Visit> Visits => Set<Visit>();

    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    public DbSet<VisitService> VisitServices => Set<VisitService>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.HasSequence<long>("OwnerCodeSequence")
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMin(1)
            .HasMax(999999)
            .IsCyclic(false);

        builder.HasSequence<long>("PetCodeSequence")
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMin(1)
            .HasMax(999999)
            .IsCyclic(false);

        builder.HasSequence<long>("VisitNumberSequence")
            .StartsAt(1)
            .IncrementsBy(1)
            .HasMin(1)
            .HasMax(9999)
            .IsCyclic(false);

        builder.Entity<IdentityUserRole<string>>()
            .HasIndex(userRole => userRole.UserId)
            .IsUnique();
    }
}
