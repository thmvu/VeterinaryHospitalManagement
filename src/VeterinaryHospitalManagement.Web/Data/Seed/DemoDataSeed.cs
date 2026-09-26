using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

// Explicit, repeatable development-only sample data. Existing business records are never updated.
public sealed class DemoDataSeed(ApplicationDbContext db, IOwnerService owners, IPetService pets)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var actor = await db.Users.Where(user => db.UserRoles.Any(link => link.UserId == user.Id &&
            db.Roles.Any(role => role.Id == link.RoleId && role.Name == "Admin")))
            .Select(user => user.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Demo seed cần một tài khoản Admin. Hãy chạy Identity seed trước.");

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
        if (!await db.ServiceCatalogs.AnyAsync(x => x.Code == "DEMO-EXAM", ct))
            db.ServiceCatalogs.Add(new ServiceCatalog { Code = "DEMO-EXAM", Name = "Khám tổng quát (mẫu)", Category = "Khám bệnh", Price = 150000 });
        if (!await db.Medicines.AnyAsync(x => x.Code == "DEMO-MED", ct))
            db.Medicines.Add(new Medicine { Code = "DEMO-MED", Name = "Thuốc mẫu - không kê đơn thực tế", Unit = "viên" });
        await db.SaveChangesAsync(ct);

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
    }
}
