namespace VeterinaryHospitalManagement.Web.Authorization;

public sealed record PermissionDefinition(string Code, string Name);

public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(PermissionCodes.AppointmentCancel, "Hủy lịch hẹn"),
        new(PermissionCodes.AppointmentCreate, "Tạo lịch hẹn"),
        new(PermissionCodes.AppointmentMarkNoShow, "Đánh dấu khách không đến"),
        new(PermissionCodes.AppointmentView, "Xem lịch hẹn"),
        new(PermissionCodes.AuditView, "Xem nhật ký hệ thống"),
        new(PermissionCodes.CalendarView, "Xem lịch làm việc"),
        new(PermissionCodes.CatalogManage, "Quản lý danh mục"),
        new(PermissionCodes.DashboardView, "Xem bảng điều khiển"),
        new(PermissionCodes.InvoiceCheckout, "Xác nhận thanh toán"),
        new(PermissionCodes.InvoicePrint, "In hóa đơn"),
        new(PermissionCodes.InvoiceView, "Xem hóa đơn"),
        new(PermissionCodes.MedicalRecordEdit, "Sửa bệnh án"),
        new(PermissionCodes.MedicalRecordView, "Xem bệnh án"),
        new(PermissionCodes.OwnerManage, "Quản lý chủ nuôi"),
        new(PermissionCodes.OwnerView, "Xem chủ nuôi"),
        new(PermissionCodes.PermissionManage, "Quản lý phân quyền"),
        new(PermissionCodes.PetManage, "Quản lý thú cưng"),
        new(PermissionCodes.PetView, "Xem thú cưng"),
        new(PermissionCodes.PrescriptionManage, "Quản lý đơn thuốc"),
        new(PermissionCodes.PrescriptionPrint, "In đơn thuốc"),
        new(PermissionCodes.PrescriptionView, "Xem đơn thuốc"),
        new(PermissionCodes.ReportExport, "Xuất báo cáo"),
        new(PermissionCodes.ReportView, "Xem báo cáo"),
        new(PermissionCodes.ScheduleManage, "Quản lý ca làm việc"),
        new(PermissionCodes.UserManage, "Quản lý tài khoản"),
        new(PermissionCodes.VisitAssign, "Phân công bác sĩ"),
        new(PermissionCodes.VisitCancel, "Hủy lượt khám"),
        new(PermissionCodes.VisitCheckIn, "Tiếp nhận lịch hẹn"),
        new(PermissionCodes.VisitComplete, "Hoàn tất lượt khám"),
        new(PermissionCodes.VisitStart, "Bắt đầu khám"),
        new(PermissionCodes.VisitView, "Xem lượt khám"),
        new(PermissionCodes.VisitWalkIn, "Tiếp nhận khách trực tiếp"),
        new(PermissionCodes.VisitServiceManage, "Quản lý dịch vụ lượt khám")
    ];

    public static IReadOnlySet<string> AdminOnly { get; } = new HashSet<string>(
        [PermissionCodes.UserManage, PermissionCodes.PermissionManage, PermissionCodes.AuditView],
        StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> Dependencies { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PermissionCodes.ReportExport] = PermissionCodes.ReportView,
            [PermissionCodes.InvoicePrint] = PermissionCodes.InvoiceView,
            [PermissionCodes.PrescriptionPrint] = PermissionCodes.PrescriptionView
        };

    private static readonly IReadOnlySet<string> KnownCodes = All
        .Select(permission => permission.Code)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> DefaultMatrix =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [SystemRoleNames.Receptionist] = PermissionsForReceptionist(),
            [SystemRoleNames.Veterinarian] = PermissionsForVeterinarian(),
            [SystemRoleNames.Manager] = PermissionsForManager(),
            [SystemRoleNames.Admin] = KnownCodes
        };

    public static bool Contains(string permissionCode) => KnownCodes.Contains(permissionCode);

    public static IReadOnlySet<string> DefaultPermissionsFor(string roleName) =>
        DefaultMatrix.TryGetValue(roleName, out var permissions)
            ? permissions
            : EmptyPermissions;

    private static IReadOnlySet<string> EmptyPermissions { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    private static HashSet<string> CommonViewPermissions() =>
    [
        PermissionCodes.DashboardView,
        PermissionCodes.CalendarView,
        PermissionCodes.OwnerView,
        PermissionCodes.PetView,
        PermissionCodes.AppointmentView,
        PermissionCodes.VisitView
    ];

    private static IReadOnlySet<string> PermissionsForReceptionist()
    {
        var permissions = CommonViewPermissions();
        permissions.UnionWith(
        [
            PermissionCodes.OwnerManage,
            PermissionCodes.PetManage,
            PermissionCodes.AppointmentCreate,
            PermissionCodes.AppointmentCancel,
            PermissionCodes.AppointmentMarkNoShow,
            PermissionCodes.VisitCheckIn,
            PermissionCodes.VisitWalkIn,
            PermissionCodes.VisitAssign,
            PermissionCodes.VisitCancel,
            PermissionCodes.InvoiceView,
            PermissionCodes.InvoiceCheckout,
            PermissionCodes.InvoicePrint
        ]);
        return permissions;
    }

    private static IReadOnlySet<string> PermissionsForVeterinarian()
    {
        var permissions = CommonViewPermissions();
        permissions.UnionWith(
        [
            PermissionCodes.VisitStart,
            PermissionCodes.VisitComplete,
            PermissionCodes.MedicalRecordView,
            PermissionCodes.MedicalRecordEdit,
            PermissionCodes.PrescriptionView,
            PermissionCodes.PrescriptionPrint,
            PermissionCodes.PrescriptionManage,
            PermissionCodes.VisitServiceManage
        ]);
        return permissions;
    }

    private static IReadOnlySet<string> PermissionsForManager()
    {
        var permissions = PermissionsForReceptionist().ToHashSet(StringComparer.Ordinal);
        permissions.UnionWith(
        [
            PermissionCodes.CatalogManage,
            PermissionCodes.ScheduleManage,
            PermissionCodes.ReportView,
            PermissionCodes.ReportExport
        ]);
        return permissions;
    }
}
