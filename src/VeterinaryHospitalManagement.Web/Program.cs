using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VeterinaryHospitalManagement.Web.Configuration;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Data.Seed;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;
using VeterinaryHospitalManagement.Web.Services.Time;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;
using VeterinaryHospitalManagement.Web.Services.Catalogs;
using VeterinaryHospitalManagement.Web.Services.Visits;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.EventsType = typeof(ActiveUserCookieEvents);
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
builder.Services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
    options.EventsType = typeof(ActiveUserCookieEvents));
builder.Services.AddScoped<ActiveUserCookieEvents>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddSingleton<IOwnerPhoneNormalizer, OwnerPhoneNormalizer>();
builder.Services.AddScoped<IOwnerService, OwnerService>();
builder.Services.AddScoped<IVeterinarianProfileService, VeterinarianProfileService>();
builder.Services.AddScoped<IVeterinarianShiftService, VeterinarianShiftService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
builder.Services.AddScoped<IMedicineService, MedicineService>();
builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddVeterinaryAuthorization();
builder.Services
    .AddOptions<BootstrapAdminOptions>()
    .Bind(builder.Configuration.GetSection(BootstrapAdminOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<BootstrapAdminOptions>>(services =>
    new BootstrapAdminOptionsValidator(
        services.GetRequiredService<IOptions<IdentityOptions>>().Value));
builder.Services.AddScoped<IdentitySeed>();
builder.Services.AddScoped<PermissionSeed>();
builder.Services.AddScoped<SeedRunner>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IVietnamTimeProvider, VietnamTimeProvider>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("IdentitySeed:RunOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
