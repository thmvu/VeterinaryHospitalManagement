using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

// Explicit, repeatable development-only sample data. Existing business records are never updated.
public sealed class DemoDataSeed(
    ApplicationDbContext db,
    IOwnerService owners,
    IPetService pets,
    IUserManagementService userManager,
    IVeterinarianShiftService shifts,
    IAppointmentService appointments,
    IVietnamTimeProvider timeProvider)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var actor = await db.Users.Where(user => db.UserRoles.Any(link => link.UserId == user.Id &&
            db.Roles.Any(role => role.Id == link.RoleId && role.Name == SystemRoleNames.Admin)))
            .Select(user => user.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Demo seed cần một tài khoản Admin. Hãy chạy Identity seed trước.");

        // 1. Staff users for 3 core operational roles
        var sampleUsers = new[]
        {
            (Email: "receptionist@hospital.local", FullName: "Lê Thị Thu (Lễ tân mẫu)", Password: "Receptionist123!", Role: SystemRoleNames.Receptionist),
            (Email: "doctor.tam@hospital.local", FullName: "BS. Trần Minh Tâm (Bác sĩ mẫu)", Password: "Doctor123!", Role: SystemRoleNames.Veterinarian),
            (Email: "manager@hospital.local", FullName: "Hoàng Văn Quản (Quản lý mẫu)", Password: "Manager123!", Role: SystemRoleNames.Manager)
        };

        foreach (var u in sampleUsers)
        {
            if (!await db.Users.AnyAsync(x => x.Email == u.Email, ct))
            {
                await userManager.CreateAsync(new CreateManagedUserRequest(actor, u.FullName, u.Email, u.Password, u.Role), ct);
            }
        }

        // 2. Species & Breeds
        foreach (var (code, name) in new[] { ("DOG", "Chó"), ("CAT", "Mèo") })
        {
            if (!await db.Species.AnyAsync(x => x.Code == code, ct))
                db.Species.Add(new Species { Code = code, Name = name });
        }
        await db.SaveChangesAsync(ct);

        foreach (var (code, name) in new[] { ("DOG", "Poodle"), ("CAT", "Mèo ta") })
        {
            var speciesId = await db.Species.Where(x => x.Code == code).Select(x => x.Id).SingleAsync(ct);
            if (!await db.Breeds.AnyAsync(x => x.SpeciesId == speciesId && x.Name == name, ct))
                db.Breeds.Add(new Breed { SpeciesId = speciesId, Name = name });
        }

        // 3. Service & Medicine Catalogs
        if (!await db.ServiceCatalogs.AnyAsync(x => x.Code == "DEMO-EXAM", ct))
            db.ServiceCatalogs.Add(new ServiceCatalog { Code = "DEMO-EXAM", Name = "Khám tổng quát (mẫu)", Category = "Khám bệnh", Price = 150000 });
        if (!await db.ServiceCatalogs.AnyAsync(x => x.Code == "DEMO-US", ct))
            db.ServiceCatalogs.Add(new ServiceCatalog { Code = "DEMO-US", Name = "Siêu âm ổ bụng (mẫu)", Category = "Chẩn đoán hình ảnh", Price = 250000 });
        if (!await db.Medicines.AnyAsync(x => x.Code == "DEMO-MED", ct))
            db.Medicines.Add(new Medicine { Code = "DEMO-MED", Name = "Thuốc mẫu - không kê đơn thực tế", Unit = "viên" });
        await db.SaveChangesAsync(ct);

        // 4. Owners & Pets
        foreach (var sample in new[]
        {
            (Phone: "0900000101", Name: "Nguyễn An (dữ liệu mẫu)", Pet: "Milu", Species: "DOG", Breed: "Poodle", Sex: PetSex.Male),
            (Phone: "0900000102", Name: "Trần Bình (dữ liệu mẫu)", Pet: "Miu", Species: "CAT", Breed: "Mèo ta", Sex: PetSex.Female)
        })
        {
            var phone = "+84" + sample.Phone[1..];
            var existingOwner = await db.Owners.Where(x => x.PhoneNumber == phone)
                .Select(x => new { x.Id, x.FullName }).FirstOrDefaultAsync(ct);
            if (existingOwner is not null && existingOwner.FullName != sample.Name)
                continue;
            var ownerId = existingOwner?.Id ?? 0;
            if (ownerId == 0)
            {
                await owners.CreateAsync(new CreateOwnerRequest(actor, sample.Name, sample.Phone, null, null), ct);
                ownerId = await db.Owners.Where(x => x.PhoneNumber == phone).Select(x => x.Id).SingleAsync(ct);
            }

            var speciesId = await db.Species.Where(x => x.Code == sample.Species).Select(x => x.Id).SingleAsync(ct);
            var breedId = await db.Breeds.Where(x => x.SpeciesId == speciesId && x.Name == sample.Breed).Select(x => x.Id).SingleAsync(ct);
            if (!await db.Pets.AnyAsync(x => x.OwnerId == ownerId && x.Name == sample.Pet, ct))
                await pets.CreateAsync(new CreatePetRequest(actor, ownerId, sample.Pet, speciesId, breedId, sample.Sex, null, null, "Dữ liệu mẫu"), ct);
        }

        // 5. Shift for sample doctor
        var doctorUser = await db.Users.SingleOrDefaultAsync(u => u.Email == "doctor.tam@hospital.local", ct);
        if (doctorUser != null)
        {
            var doctorProfile = await db.VeterinarianProfiles.SingleOrDefaultAsync(p => p.UserId == doctorUser.Id, ct);
            if (doctorProfile != null)
            {
                var localNow = timeProvider.LocalNow;
                var todayDate = DateOnly.FromDateTime(localNow.DateTime);
                var shiftStartLocal = new DateTimeOffset(todayDate.Year, todayDate.Month, todayDate.Day, 8, 0, 0, localNow.Offset);
                var shiftEndLocal = new DateTimeOffset(todayDate.Year, todayDate.Month, todayDate.Day, 17, 0, 0, localNow.Offset);
                var shiftStartUtc = shiftStartLocal.ToUniversalTime();
                var shiftEndUtc = shiftEndLocal.ToUniversalTime();

                if (!await db.VeterinarianShifts.AnyAsync(s => s.VeterinarianId == doctorProfile.Id && s.StartAt == shiftStartUtc, ct))
                {
                    await shifts.CreateAsync(new CreateShiftRequest(actor, doctorProfile.Id, shiftStartUtc, shiftEndUtc), ct);
                }

                // 6. Sample Appointment
                var miluPet = await db.Pets.FirstOrDefaultAsync(p => p.Name == "Milu", ct);
                if (miluPet != null)
                {
                    var apptStartLocal = new DateTimeOffset(todayDate.Year, todayDate.Month, todayDate.Day, 9, 0, 0, localNow.Offset);
                    var apptEndLocal = new DateTimeOffset(todayDate.Year, todayDate.Month, todayDate.Day, 9, 30, 0, localNow.Offset);
                    var apptStartUtc = apptStartLocal.ToUniversalTime();
                    var apptEndUtc = apptEndLocal.ToUniversalTime();

                    if (!await db.Appointments.AnyAsync(a => a.PetId == miluPet.Id && a.StartAt == apptStartUtc, ct))
                    {
                        await appointments.CreateAsync(new CreateAppointmentRequest(
                            actor,
                            miluPet.Id,
                            doctorProfile.Id,
                            apptStartUtc,
                            apptEndUtc,
                            "Khám định kỳ và tư vấn dinh dưỡng (mẫu)"), ct);
                    }
                }
            }
        }
    }
}
