using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Veterinarians;

public sealed class VeterinarianProfileService(ApplicationDbContext db, UserManager<ApplicationUser> users, TimeProvider clock) : IVeterinarianProfileService
{
    public async Task<IReadOnlyList<VeterinarianListItem>> ListAsync(CancellationToken ct = default) =>
        await db.VeterinarianProfiles.AsNoTracking().OrderBy(x => x.DoctorCode)
            .Select(x => new VeterinarianListItem(x.Id,x.DoctorCode,x.User.FullName,x.Specialty,x.IsActive)).ToListAsync(ct);

    public async Task<VeterinarianDetails?> FindAsync(int id, CancellationToken ct = default) =>
        await db.VeterinarianProfiles.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new VeterinarianDetails(x.Id,x.UserId,x.DoctorCode,x.User.FullName,x.Specialty,x.IsActive,x.RowVersion)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EligibleVeterinarianUser>> EligibleUsersAsync(CancellationToken ct = default)
    {
        var role = await db.Roles.Where(x => x.Name == SystemRoleNames.Veterinarian).Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (role is null) return [];
        return await (from user in db.Users.AsNoTracking()
                      join ur in db.UserRoles on user.Id equals ur.UserId
                      where ur.RoleId == role && user.IsActive && !db.VeterinarianProfiles.Any(x => x.UserId == user.Id)
                      orderby user.FullName
                      select new EligibleVeterinarianUser(user.Id,user.FullName,user.Email ?? string.Empty)).ToListAsync(ct);
    }

    public Task<int> CreateAsync(CreateVeterinarianRequest request, CancellationToken ct = default) => InTransactionAsync(async () =>
    {
        var user = await users.FindByIdAsync(request.UserId) ?? throw new VeterinarianManagementException("Không tìm thấy tài khoản bác sĩ.");
        if (!user.IsActive || !await users.IsInRoleAsync(user,SystemRoleNames.Veterinarian)) throw new VeterinarianManagementException("Tài khoản phải đang hoạt động và có vai trò Veterinarian.");
        if (await db.VeterinarianProfiles.AnyAsync(x => x.UserId == user.Id,ct)) throw new VeterinarianManagementException("Tài khoản đã có hồ sơ bác sĩ.");
        var profile = new VeterinarianProfile { UserId=user.Id, DoctorCode=VeterinarianProfileRules.NormalizeDoctorCode(request.DoctorCode), Specialty=VeterinarianProfileRules.NormalizeSpecialty(request.Specialty), IsActive=true };
        db.Add(profile); await db.SaveChangesAsync(ct); AddAudit(request.ActorUserId,"VeterinarianProfile.Created",profile.Id,$"Created veterinarian {profile.DoctorCode}."); await db.SaveChangesAsync(ct); return profile.Id;
    },ct);

    public Task UpdateAsync(UpdateVeterinarianRequest request, CancellationToken ct = default) => GuardAsync(() => InTransactionAsync(async () =>
    {
        var profile=await LoadAsync(request.Id,request.ExpectedRowVersion,ct); profile.Specialty=VeterinarianProfileRules.NormalizeSpecialty(request.Specialty);
        AddAudit(request.ActorUserId,"VeterinarianProfile.Updated",profile.Id,$"Updated veterinarian {profile.DoctorCode}."); await db.SaveChangesAsync(ct); return 0;
    },ct));

    public Task SetActiveAsync(VeterinarianActivationRequest request, CancellationToken ct = default) => GuardAsync(() => InTransactionAsync(async () =>
    {
        var profile=await LoadAsync(request.Id,request.ExpectedRowVersion,ct); profile.IsActive=request.IsActive;
        AddAudit(request.ActorUserId,request.IsActive?"VeterinarianProfile.Activated":"VeterinarianProfile.Deactivated",profile.Id,$"Changed veterinarian {profile.DoctorCode} active status to {request.IsActive}."); await db.SaveChangesAsync(ct); return 0;
    },ct));

    private async Task<VeterinarianProfile> LoadAsync(int id,byte[] version,CancellationToken ct) { var x=await db.VeterinarianProfiles.SingleOrDefaultAsync(p=>p.Id==id,ct) ?? throw new VeterinarianManagementException("Không tìm thấy hồ sơ bác sĩ."); if(!x.RowVersion.AsSpan().SequenceEqual(version)) throw new VeterinarianConcurrencyException(); return x; }
    private void AddAudit(string actor,string action,int id,string description)=>db.AuditLogs.Add(new AuditLog { ActorType="Internal",UserId=actor,Action=action,EntityName="VeterinarianProfile",EntityId=id.ToString(CultureInfo.InvariantCulture),Description=description,CreatedAt=clock.GetUtcNow() });
    private async Task<T> InTransactionAsync<T>(Func<Task<T>> op,CancellationToken ct) { var strategy=db.Database.CreateExecutionStrategy(); return await strategy.ExecuteAsync(async()=>{ await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct); try { var result=await op(); await tx.CommitAsync(ct); return result; } catch(DbUpdateException exception) when (exception is not DbUpdateConcurrencyException) { throw new VeterinarianManagementException("Mã bác sĩ hoặc tài khoản đã được sử dụng."); } }); }
    private static async Task GuardAsync(Func<Task> op) { try { await op(); } catch(DbUpdateConcurrencyException) { throw new VeterinarianConcurrencyException(); } }
}
