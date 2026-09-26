using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;

namespace VeterinaryHospitalManagement.Tests.Integration.Scheduling;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class AppointmentServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Create_normalizes_utc_writes_audit_and_lists_appointment()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory();
        var data=await Setup(f); var start=new DateTimeOffset(2026,11,1,9,0,0,TimeSpan.FromHours(7));
        var id=await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start,start.AddMinutes(30),"  Tiêm phòng  ")));
        await using var scope=f.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item=await db.Appointments.SingleAsync(x=>x.Id==id);
        Assert.StartsWith("APT-",item.AppointmentNumber); Assert.Equal(start.ToUniversalTime(),item.StartAt); Assert.Equal("Tiêm phòng",item.Reason); Assert.Equal(AppointmentStatus.Scheduled,item.Status);
        Assert.Equal(1,await db.AuditLogs.CountAsync(x=>x.Action=="Appointment.Created"&&x.EntityId==id.ToString()));
        var list=await With(f,s=>s.ListAsync(start.AddHours(-1),start.AddHours(1))); Assert.Single(list);
    }

    [IdentitySqlServerFact]
    public async Task Create_requires_active_participants_and_covering_shift()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory(); var data=await Setup(f);
        var outside=new DateTimeOffset(2026,11,1,7,0,0,TimeSpan.FromHours(7));
        await Assert.ThrowsAsync<AppointmentManagementException>(()=>With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,outside,outside.AddMinutes(30),"Khám"))));
        await using(var scope=f.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();(await db.Pets.FindAsync(data.Pet))!.IsActive=false;await db.SaveChangesAsync();}
        var inside=new DateTimeOffset(2026,11,1,9,0,0,TimeSpan.FromHours(7));
        await Assert.ThrowsAsync<AppointmentManagementException>(()=>With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,inside,inside.AddMinutes(30),"Khám"))));
    }

    [IdentitySqlServerFact]
    public async Task Create_blocks_pet_or_veterinarian_overlap_but_allows_adjacent_time()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory(); var data=await Setup(f);
        var start=new DateTimeOffset(2026,11,1,9,0,0,TimeSpan.FromHours(7));
        await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start,start.AddMinutes(30),"Khám đầu")));
        await Assert.ThrowsAsync<AppointmentManagementException>(()=>With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start.AddMinutes(15),start.AddMinutes(45),"Trùng"))));
        var adjacent=await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start.AddMinutes(30),start.AddHours(1),"Kế tiếp"))); Assert.True(adjacent>0);
    }

    [IdentitySqlServerFact]
    public async Task Concurrent_overlapping_requests_create_exactly_one_appointment()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory(); var data=await Setup(f);
        var start=new DateTimeOffset(2026,11,1,10,0,0,TimeSpan.FromHours(7));
        async Task<bool> Attempt(){try{await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start,start.AddMinutes(30),"Khám")));return true;}catch(AppointmentManagementException){return false;}}
        var results=await Task.WhenAll(Attempt(),Attempt()); Assert.Equal(1,results.Count(x=>x));
        await using var scope=f.Services.CreateAsyncScope(); Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Appointments.CountAsync());
    }

    [IdentitySqlServerFact]
    public async Task Cancel_updates_status_sets_reason_and_writes_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory(); var data=await Setup(f);
        var start=new DateTimeOffset(2026,11,1,11,0,0,TimeSpan.FromHours(7));
        var id=await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start,start.AddMinutes(30),"Khám")));
        var details=(await With(f,s=>s.GetDetailsAsync(id)))!;
        await With(f,async s=>{await s.CancelAsync(id,data.Actor,"Khách bận việc",details.RowVersion);return true;});

        await using var scope=f.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item=await db.Appointments.SingleAsync(x=>x.Id==id);
        Assert.Equal(AppointmentStatus.Cancelled,item.Status);
        Assert.Equal("Khách bận việc",item.CancellationReason);
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.Action=="Appointment.Cancelled"&&x.EntityId==id.ToString()));
    }

    [IdentitySqlServerFact]
    public async Task MarkNoShow_updates_status_and_writes_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync(); using var f=new IdentitySqlServerWebApplicationFactory(); var data=await Setup(f);
        // Tạo appointment với thời gian kết thúc trước thời gian hiện tại của clock
        var now=DateTimeOffset.UtcNow;
        var start=now.AddHours(-2);
        // Cần ca làm việc bao trọn thời gian này
        await using(var shiftScope=f.Services.CreateAsyncScope()){
            var shiftDb=shiftScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            shiftDb.VeterinarianShifts.Add(new VeterinarianShift{VeterinarianId=data.Vet,StartAt=start.AddHours(-1),EndAt=start.AddHours(4),IsActive=true});
            await shiftDb.SaveChangesAsync();
        }
        var id=await With(f,s=>s.CreateAsync(new(data.Actor,data.Pet,data.Vet,start,start.AddMinutes(30),"Khám")));
        var details=(await With(f,s=>s.GetDetailsAsync(id)))!;
        await With(f,async s=>{await s.MarkNoShowAsync(id,data.Actor,details.RowVersion);return true;});

        await using var scope=f.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item=await db.Appointments.SingleAsync(x=>x.Id==id);
        Assert.Equal(AppointmentStatus.NoShow,item.Status);
        Assert.Null(item.CancellationReason);
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.Action=="Appointment.MarkedNoShow"&&x.EntityId==id.ToString()));
    }

    private static async Task<(string Actor,int Pet,int Vet)> Setup(IdentitySqlServerWebApplicationFactory f)
    {
        using(var c=f.CreateClient()) await c.GetAsync("/"); await using var scope=f.Services.CreateAsyncScope(); var sp=scope.ServiceProvider; var db=sp.GetRequiredService<ApplicationDbContext>();
        var actor=await db.Users.Where(x=>x.NormalizedEmail==IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()).Select(x=>x.Id).SingleAsync();
        await sp.GetRequiredService<IUserManagementService>().CreateAsync(new(actor,"Bác sĩ lịch hẹn","appointment-vet@vet.test","Integration.Vet123!",SystemRoleNames.Veterinarian));
        var user=await db.Users.Where(x=>x.Email=="appointment-vet@vet.test").Select(x=>x.Id).SingleAsync();
        var vet=await sp.GetRequiredService<IVeterinarianProfileService>().CreateAsync(new(actor,user,"Tổng quát"));
        var owner=new Owner{OwnerCode="OWN-000001",FullName="Nguyễn Văn A",PhoneNumber="+84901234567",IsActive=true,CreatedAt=DateTimeOffset.UtcNow}; var species=new Species{Code="DOG",Name="Chó",IsActive=true}; db.AddRange(owner,species); await db.SaveChangesAsync();
        var pet=new Pet{PetCode="PET-000001",OwnerId=owner.Id,Name="Milo",SpeciesId=species.Id,Sex=PetSex.Male,IsActive=true,CreatedAt=DateTimeOffset.UtcNow}; db.Pets.Add(pet); await db.SaveChangesAsync();
        var shiftStart=new DateTimeOffset(2026,11,1,8,0,0,TimeSpan.FromHours(7)); db.VeterinarianShifts.Add(new VeterinarianShift{VeterinarianId=vet,StartAt=shiftStart.ToUniversalTime(),EndAt=shiftStart.AddHours(9).ToUniversalTime(),IsActive=true}); await db.SaveChangesAsync(); return(actor,pet.Id,vet);
    }
    private static async Task<T> With<T>(IdentitySqlServerWebApplicationFactory f,Func<IAppointmentService,Task<T>> call){await using var s=f.Services.CreateAsyncScope();return await call(s.ServiceProvider.GetRequiredService<IAppointmentService>());}
}
