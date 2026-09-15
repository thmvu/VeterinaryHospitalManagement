using System.Security.Claims;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public sealed class PermissionServiceTests
{
    [Fact]
    public void Catalog_matches_the_approved_permission_matrix()
    {
        string[] expectedCodes =
        [
            "Appointment.Cancel",
            "Appointment.Create",
            "Appointment.MarkNoShow",
            "Appointment.View",
            "Audit.View",
            "Calendar.View",
            "Catalog.Manage",
            "Dashboard.View",
            "Invoice.Checkout",
            "Invoice.Print",
            "Invoice.View",
            "MedicalRecord.Edit",
            "MedicalRecord.View",
            "Owner.Manage",
            "Owner.View",
            "Permission.Manage",
            "Pet.Manage",
            "Pet.View",
            "Prescription.Manage",
            "Prescription.Print",
            "Prescription.View",
            "Report.Export",
            "Report.View",
            "Schedule.Manage",
            "User.Manage",
            "Visit.Assign",
            "Visit.Cancel",
            "Visit.CheckIn",
            "Visit.Complete",
            "Visit.Start",
            "Visit.View",
            "Visit.WalkIn",
            "VisitService.Manage"
        ];

        Assert.Equal(expectedCodes, PermissionCatalog.All.Select(permission => permission.Code).Order());
    }

    [Theory]
    [MemberData(nameof(DefaultRoleMatrix))]
    public void Default_role_grants_match_the_approved_permission_matrix(
        string roleName,
        string[] expectedPermissions)
    {
        Assert.Equal(
            expectedPermissions.Order(StringComparer.Ordinal),
            PermissionCatalog.DefaultPermissionsFor(roleName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(SystemRoleNames.Receptionist, PermissionCodes.OwnerManage, true)]
    [InlineData(SystemRoleNames.Receptionist, PermissionCodes.MedicalRecordView, false)]
    [InlineData(SystemRoleNames.Veterinarian, PermissionCodes.PrescriptionManage, true)]
    [InlineData(SystemRoleNames.Veterinarian, PermissionCodes.InvoiceView, false)]
    [InlineData(SystemRoleNames.Manager, PermissionCodes.ReportExport, true)]
    [InlineData(SystemRoleNames.Manager, PermissionCodes.VisitStart, false)]
    public async Task HasPermissionAsync_applies_a_matrix_grant_or_denial(
        string roleName,
        string permissionCode,
        bool expected)
    {
        var state = State(roleName, PermissionCatalog.DefaultPermissionsFor(roleName));
        var service = new PermissionService(new StubPermissionGrantStore(state));

        bool actual = await service.HasPermissionAsync(AuthenticatedUser(), permissionCode);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task HasPermissionAsync_grants_active_Admin_every_known_permission_without_stored_grants()
    {
        var service = new PermissionService(new StubPermissionGrantStore(State(SystemRoleNames.Admin, [])));

        foreach (var permission in PermissionCatalog.All)
        {
            Assert.True(await service.HasPermissionAsync(AuthenticatedUser(), permission.Code));
        }
    }

    [Fact]
    public async Task HasPermissionAsync_denies_inactive_Admin()
    {
        var service = new PermissionService(
            new StubPermissionGrantStore(State(SystemRoleNames.Admin, [], isActive: false)));

        bool allowed = await service.HasPermissionAsync(AuthenticatedUser(), PermissionCodes.UserManage);

        Assert.False(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_denies_an_unknown_permission_even_for_Admin()
    {
        var service = new PermissionService(new StubPermissionGrantStore(State(SystemRoleNames.Admin, [])));

        bool allowed = await service.HasPermissionAsync(AuthenticatedUser(), "Typo.DoesNotExist");

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(PermissionCodes.UserManage)]
    [InlineData(PermissionCodes.PermissionManage)]
    [InlineData(PermissionCodes.AuditView)]
    public async Task HasPermissionAsync_denies_Admin_only_permissions_to_other_roles_even_when_stored(
        string permissionCode)
    {
        var service = new PermissionService(
            new StubPermissionGrantStore(State(SystemRoleNames.Manager, [permissionCode])));

        bool allowed = await service.HasPermissionAsync(AuthenticatedUser(), permissionCode);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(PermissionCodes.ReportExport, PermissionCodes.ReportView)]
    [InlineData(PermissionCodes.InvoicePrint, PermissionCodes.InvoiceView)]
    [InlineData(PermissionCodes.PrescriptionPrint, PermissionCodes.PrescriptionView)]
    public async Task HasPermissionAsync_denies_a_dependent_permission_without_its_prerequisite(
        string dependentPermission,
        string prerequisitePermission)
    {
        var service = new PermissionService(
            new StubPermissionGrantStore(State(SystemRoleNames.Manager, [dependentPermission])));

        bool withoutPrerequisite = await service.HasPermissionAsync(AuthenticatedUser(), dependentPermission);

        Assert.False(withoutPrerequisite);

        var serviceWithPrerequisite = new PermissionService(
            new StubPermissionGrantStore(
                State(SystemRoleNames.Manager, [dependentPermission, prerequisitePermission])));

        Assert.True(await serviceWithPrerequisite.HasPermissionAsync(AuthenticatedUser(), dependentPermission));
    }

    [Fact]
    public async Task HasPermissionAsync_denies_an_unauthenticated_user()
    {
        var service = new PermissionService(
            new StubPermissionGrantStore(State(SystemRoleNames.Receptionist, [PermissionCodes.OwnerView])));

        bool allowed = await service.HasPermissionAsync(new ClaimsPrincipal(), PermissionCodes.OwnerView);

        Assert.False(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_observes_permission_changes_on_the_next_service_scope()
    {
        var store = new MutablePermissionGrantStore(
            State(SystemRoleNames.Manager, [PermissionCodes.ReportView]));

        var firstRequest = new PermissionService(store);
        Assert.False(await firstRequest.HasPermissionAsync(AuthenticatedUser(), PermissionCodes.ReportExport));

        store.Current = State(
            SystemRoleNames.Manager,
            [PermissionCodes.ReportView, PermissionCodes.ReportExport]);

        var nextRequest = new PermissionService(store);
        Assert.True(await nextRequest.HasPermissionAsync(AuthenticatedUser(), PermissionCodes.ReportExport));
    }

    private static ClaimsPrincipal AuthenticatedUser() =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")],
            authenticationType: "Test"));

    private static UserPermissionState State(
        string roleName,
        IEnumerable<string> permissionCodes,
        bool isActive = true) =>
        new(isActive, roleName, permissionCodes.ToHashSet(StringComparer.Ordinal));

    public static TheoryData<string, string[]> DefaultRoleMatrix => new()
    {
        {
            SystemRoleNames.Receptionist,
            [
                PermissionCodes.DashboardView,
                PermissionCodes.CalendarView,
                PermissionCodes.OwnerView,
                PermissionCodes.PetView,
                PermissionCodes.OwnerManage,
                PermissionCodes.PetManage,
                PermissionCodes.AppointmentView,
                PermissionCodes.AppointmentCreate,
                PermissionCodes.AppointmentCancel,
                PermissionCodes.AppointmentMarkNoShow,
                PermissionCodes.VisitView,
                PermissionCodes.VisitCheckIn,
                PermissionCodes.VisitWalkIn,
                PermissionCodes.VisitAssign,
                PermissionCodes.VisitCancel,
                PermissionCodes.InvoiceView,
                PermissionCodes.InvoiceCheckout,
                PermissionCodes.InvoicePrint
            ]
        },
        {
            SystemRoleNames.Veterinarian,
            [
                PermissionCodes.DashboardView,
                PermissionCodes.CalendarView,
                PermissionCodes.OwnerView,
                PermissionCodes.PetView,
                PermissionCodes.AppointmentView,
                PermissionCodes.VisitView,
                PermissionCodes.VisitStart,
                PermissionCodes.VisitComplete,
                PermissionCodes.MedicalRecordView,
                PermissionCodes.MedicalRecordEdit,
                PermissionCodes.PrescriptionView,
                PermissionCodes.PrescriptionPrint,
                PermissionCodes.PrescriptionManage,
                PermissionCodes.VisitServiceManage
            ]
        },
        {
            SystemRoleNames.Manager,
            [
                PermissionCodes.DashboardView,
                PermissionCodes.CalendarView,
                PermissionCodes.OwnerView,
                PermissionCodes.PetView,
                PermissionCodes.OwnerManage,
                PermissionCodes.PetManage,
                PermissionCodes.AppointmentView,
                PermissionCodes.AppointmentCreate,
                PermissionCodes.AppointmentCancel,
                PermissionCodes.AppointmentMarkNoShow,
                PermissionCodes.VisitView,
                PermissionCodes.VisitCheckIn,
                PermissionCodes.VisitWalkIn,
                PermissionCodes.VisitAssign,
                PermissionCodes.VisitCancel,
                PermissionCodes.InvoiceView,
                PermissionCodes.InvoiceCheckout,
                PermissionCodes.InvoicePrint,
                PermissionCodes.CatalogManage,
                PermissionCodes.ScheduleManage,
                PermissionCodes.ReportView,
                PermissionCodes.ReportExport
            ]
        },
        {
            SystemRoleNames.Admin,
            [
                PermissionCodes.AppointmentCancel,
                PermissionCodes.AppointmentCreate,
                PermissionCodes.AppointmentMarkNoShow,
                PermissionCodes.AppointmentView,
                PermissionCodes.AuditView,
                PermissionCodes.CalendarView,
                PermissionCodes.CatalogManage,
                PermissionCodes.DashboardView,
                PermissionCodes.InvoiceCheckout,
                PermissionCodes.InvoicePrint,
                PermissionCodes.InvoiceView,
                PermissionCodes.MedicalRecordEdit,
                PermissionCodes.MedicalRecordView,
                PermissionCodes.OwnerManage,
                PermissionCodes.OwnerView,
                PermissionCodes.PermissionManage,
                PermissionCodes.PetManage,
                PermissionCodes.PetView,
                PermissionCodes.PrescriptionManage,
                PermissionCodes.PrescriptionPrint,
                PermissionCodes.PrescriptionView,
                PermissionCodes.ReportExport,
                PermissionCodes.ReportView,
                PermissionCodes.ScheduleManage,
                PermissionCodes.UserManage,
                PermissionCodes.VisitAssign,
                PermissionCodes.VisitCancel,
                PermissionCodes.VisitCheckIn,
                PermissionCodes.VisitComplete,
                PermissionCodes.VisitStart,
                PermissionCodes.VisitView,
                PermissionCodes.VisitWalkIn,
                PermissionCodes.VisitServiceManage
            ]
        }
    };

    private sealed class StubPermissionGrantStore(UserPermissionState? state) : IUserPermissionStore
    {
        public Task<UserPermissionState?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(state);
    }

    private sealed class MutablePermissionGrantStore(UserPermissionState? state) : IUserPermissionStore
    {
        public UserPermissionState? Current { get; set; } = state;

        public Task<UserPermissionState?> FindByUserIdAsync(
            string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(Current);
    }
}
