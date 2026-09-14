# Veterinary Hospital Management — Delivery/QA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Xây dựng và nghiệm thu hệ thống ASP.NET Core MVC quản lý bệnh viện thú y ngoại trú gồm tài khoản nội bộ, chủ nuôi/thú cưng, lịch hẹn, tiếp nhận, khám, đơn thuốc, dịch vụ, hóa đơn, vận hành, báo cáo Excel và audit.

**Architecture:** Một ứng dụng MVC .NET 10 dùng Razor Views, service nghiệp vụ và một `ApplicationDbContext` EF Core 10 kết nối SQL Server; một project xUnit chứa unit test và integration test trên database riêng. Mỗi module sở hữu entity/configuration/service/UI/test của mình; các thao tác cạnh tranh mở transaction Serializable ngắn, đọc lại trạng thái rồi ghi nghiệp vụ và AuditLog trong cùng transaction.

**Tech Stack:** .NET 10, ASP.NET Core MVC + Identity, EF Core 10 SQL Server, Razor/Bootstrap/JavaScript, FullCalendar, ClosedXML, xUnit, SQL Server `localhost` với Windows Authentication.

**Spec:** `docs/VeterinaryHospitalManagement_ProjectPlan_v1.0.md`, `docs/DatabaseDesign_v1.0.md`, `docs/Codex_Foundation_Prompt.md`.

## Global Constraints

- Phạm vi là ngoại trú; không có nội trú, chuồng, tồn kho thuốc, cấp phát/bán thuốc, nhắc tiêm, xét nghiệm tích hợp, SMS, thanh toán tự động, đặt lịch public hoặc tài khoản chủ nuôi.
- Controller chỉ nhận ViewModel và gọi service; EF Core DbContext truy cập SQL Server; không thêm generic repository, microservice, CQRS, Web API hoặc SPA.
- EF Core Code First migrations là nguồn schema duy nhất. Không sửa schema thủ công; script SQL review phải sinh từ migration.
- Thời điểm lưu bằng `DateTimeOffset` UTC/`datetimeoffset(7)` và hiển thị theo giờ Việt Nam qua clock chung; tiền dùng `decimal(18,2)`, số lượng `decimal(10,2)`, làm tròn từng dòng VND bằng `MidpointRounding.AwayFromZero`.
- Enum trạng thái lưu `nvarchar(20)` kèm CHECK constraint; dữ liệu cập nhật cạnh tranh dùng `rowversion`; FK lịch sử mặc định Restrict/NoAction.
- Không hard delete dữ liệu có lịch sử. Không commit mật khẩu; Admin bootstrap đọc đúng ba key user-secrets `BootstrapAdmin:Email`, `BootstrapAdmin:Password`, `BootstrapAdmin:FullName`, không log giá trị và fail-fast khi bootstrap được bật mà thiếu/sai key.
- Integration test chỉ dùng `VeterinaryHospitalManagement_Test`, không dùng EF InMemory để chứng minh transaction, filtered unique index, isolation hoặc rollback.
- Mỗi task kết thúc bằng test riêng và một commit; migration chỉ được tạo sau khi model/configuration cùng test schema của module đã được review.

---

## 1. Các quyết định phải đóng băng

Các quyết định dưới đây là cổng vào module. Người thực thi phải ghi lựa chọn được người dùng duyệt vào biên bản milestone trước khi tạo entity hoặc migration liên quan. Dòng ghi **CHƯA CHỐT** không có phương án mặc định và cấm module phụ thuộc bắt đầu cho đến khi một lựa chọn cụ thể được duyệt.

| Cổng | Phải đóng băng trước | Giá trị thi hành |
| --- | --- | --- |
| G0 trước Identity | Một user có bao nhiêu role; quyền Admin bất biến; cách tạo Admin | Đúng một role/user; `User.Manage`, `Permission.Manage`, `Audit.View` chỉ Admin; bootstrap đọc ba key user-secrets đã định danh ở Global Constraints |
| G1 trước Owner | (DEC-01) Chuẩn hóa định dạng SĐT và có cho nhiều Owner dùng chung một số hay không | **CHƯA CHỐT:** người dùng phải duyệt tập input hợp lệ, dạng canonical lưu DB, và chọn unique hay non-unique; không mặc định `+84`/10 số hoặc unique |
| G2 trước Appointment | Số cơ sở, kênh đặt lịch, độ dài hẹn, đổi lịch, NoShow; (DEC-03) dữ liệu lịch sử Cancel/NoShow | Phần workflow dùng một cơ sở, lễ tân nhập, `EndAt` bắt buộc, đổi lịch bằng hủy rồi tạo mới, NoShow sau giờ hẹn. **DEC-03 CHƯA CHỐT:** chọn (A) chỉ AuditLog + CancellationReason, hoặc (B) thêm `StatusChangedAt`/`StatusChangedByUserId`, hoặc (C) cột riêng cho Cancel và NoShow; lựa chọn phải quy định CHECK tuple và trường report dùng |
| G3 trước Visit | Bác sĩ chính, walk-in, chuyển bác sĩ | Một bác sĩ chính; walk-in phải phân công bác sĩ; chỉ chuyển khi Waiting; một Pet chỉ có một Visit mở |
| G4 trước Clinical | Phạm vi đơn thuốc; quy tắc FollowUpDate; cơ chế đính chính; (DEC-02) xóa dữ liệu Draft | Đơn chỉ là chỉ định mang về, không phát sinh phí; hồ sơ/đơn Finalized không sửa qua form thường. **FollowUpDate CHƯA CHỐT:** người dùng phải chọn nullable-only hoặc thêm giới hạn ngày cụ thể. **DEC-02 CHƯA CHỐT:** chọn (A) cho xóa PrescriptionItem nháp và Prescription Draft rỗng trong Visit InProgress nhưng VisitService chuyển Cancelled, hoặc (B) không hard-delete mọi dòng và dùng trạng thái/audit tương ứng; lựa chọn phải chốt DeleteBehavior |
| G5 trước Billing | Phương thức và cấu trúc thanh toán | Thanh toán đủ một lần bằng Cash/BankTransfer; không cọc, công nợ, split payment, refund; Invoice tổng 0 vẫn hợp lệ |
| G6 trước Reports | Mốc thời gian và nguồn doanh thu | Doanh thu theo `PaidAt` và `InvoiceItem`; lượt khám theo `CheckedInAt` + trạng thái; không đọc giá catalog hiện tại |

Nếu một quyết định thay đổi, dừng đúng module chịu ảnh hưởng, sửa spec/schema trước, rồi lập migration mới; không âm thầm uốn code quanh quyết định mới.

## 2. Bản đồ phụ thuộc và quyền sở hữu file

```text
M0 Foundation
  └─ M1 Identity/RBAC
      ├─ M2 Owner/Species/Breed/Pet
      └─ M3 Veterinarian/Shift + Service/Medicine Catalog
          └─ M4 Appointment/Availability (cần M2 + M3)
              └─ M5 Visit/Reception (cần M4)
                  └─ M6 Clinical (cần M3 + M5)
                      └─ M7 Checkout/Invoice
                          ├─ M8 Dashboard/Admin/Audit
                          └─ M9 Reports/Excel
                              └─ M10 End-to-end hardening
```

Hai task chỉ chạy song song khi không sửa cùng file và mọi interface dùng chung đã được merge. `Program.cs`, `ApplicationDbContext.cs`, `_Layout.cshtml`, `PermissionCodes.cs`, seed orchestration và model snapshot là vùng tích hợp do một owner duy nhất sửa theo thứ tự. Mỗi module sở hữu thư mục `Services/<Module>`, `Areas/BackOffice/{Controllers,ViewModels,Views}/<Module>`, entity/configuration và test tương ứng. Không cho hai agent cùng tạo/chỉnh migration hoặc `ApplicationDbContextModelSnapshot.cs`.

Các hợp đồng dùng chung phải được merge trước khi tách nhánh song song: `IClock.UtcNow: DateTimeOffset`; `IVisitAccessGuard.RequireAssignedVeterinarianAsync(int visitId, string userId, CancellationToken)` trả về Visit đã đọc lại hoặc ném lỗi quyền/trạng thái; `IBillingCalculator.Calculate(IReadOnlyCollection<BillableVisitService>)` trả `BillingSummary` gồm các dòng đã làm tròn và `TotalAmount`; `IReportQuery.BuildAsync(ReportFilter, CancellationToken)` trả DTO dùng chung cho web và Excel. Tên/chữ ký chỉ đổi qua review tích hợp, đồng thời cập nhật mọi consumer và contract test.

Thứ tự migration bắt buộc:

1. `InitialIdentityAndPermissions`
2. `AddOwnersAndPets`
3. `AddVeterinariansAndCatalogs`
4. `AddAppointments`
5. `AddVisits`
6. `AddClinicalRecords`
7. `AddInvoices`

Mỗi migration phải được tạo từ model đã review, sinh SQL script để kiểm tra, áp lên database test sạch, xác minh FK/index/CHECK/filtered unique index, rồi chạy test module. Chỉ sau đó mới áp vào database ứng dụng đã xác nhận đúng tên và không có dữ liệu cần bảo toàn ngoài kế hoạch.

## 3. Kế hoạch task theo milestone

### M0 — Foundation

#### Task 0.1: Solution, MVC, test harness và cấu hình local

**Phụ thuộc:** Không có.  
**Files sở hữu:** `VeterinaryHospitalManagement.slnx` hoặc `.sln`; `src/VeterinaryHospitalManagement.Web/VeterinaryHospitalManagement.Web.csproj`; `tests/VeterinaryHospitalManagement.Tests/VeterinaryHospitalManagement.Tests.csproj`; `src/.../Program.cs`; `src/.../appsettings*.json`; `src/.../Data/ApplicationDbContext.cs`; `tests/.../Infrastructure/*`; `README.md`.

- [ ] Tạo MVC .NET 10 và xUnit, thêm project reference và package EF Core SQL Server/Design/TestHost cùng major version 10; tạo cấu trúc thư mục đúng spec.
- [ ] Cấu hình `DefaultConnection` dùng `Server=localhost;Database=VeterinaryHospitalManagementDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=False`, nhưng cho phép test override sang `VeterinaryHospitalManagement_Test`.
- [ ] Khai báo contract bootstrap Admin bằng `BootstrapAdmin:Email`, `BootstrapAdmin:Password`, `BootstrapAdmin:FullName`; khi bật bootstrap, validate cả ba giá trị lúc startup và dừng ứng dụng với thông báo chỉ nêu tên key thiếu/sai, tuyệt đối không đưa password/value vào log. Development dùng `dotnet user-secrets`; production phải cung cấp configuration provider an toàn hoặc tắt bootstrap sau khi có Admin.
- [ ] Viết configuration tests: đủ ba key hợp lệ thì binding thành công; thiếu từng key, email sai định dạng hoặc password không đạt Identity policy thì startup fail-fast; captured logs không chứa bất kỳ secret value nào.
- [ ] Tạo clock abstraction trả UTC và converter hiển thị Asia/Bangkok; tạo trang chủ tiếng Việt, AccessDenied và error handler không lộ stack trace production.
- [ ] Tạo SQL Server integration fixture kiểm tra tên database có hậu tố `_Test`, tạo schema bằng migrations và reset dữ liệu giữa test; fixture phải từ chối chạy nếu connection trỏ vào database ứng dụng.
- [ ] Chạy `dotnet restore`, `dotnet build`, `dotnet test`; khởi động app và xác minh HTTP 200 trang chủ. Chưa tạo migration ở task này.

**Acceptance:** Solution build sạch; unit test clock/timezone pass; integration guard chứng minh database ứng dụng bị từ chối; app chạy và render tên Veterinary Hospital Management; README có lệnh cấu hình/chạy/test và cảnh báo hai database.

### M1 — Identity, RBAC và Audit

#### Task 1.1: Schema Identity/RBAC/Audit

**Phụ thuộc:** Task 0.1, cổng G0.  
**Files sở hữu:** `Models/Entities/ApplicationUser.cs`, `Permission.cs`, `RolePermission.cs`, `AuditLog.cs`; `Data/Configurations/{ApplicationUser,Permission,RolePermission,AuditLog}Configuration.cs`; `Data/ApplicationDbContext.cs`; `Data/Migrations/*InitialIdentityAndPermissions*`.

- [ ] Viết model/configuration cho FullName, IsActive, CreatedAt, Permission.Code unique, PK ghép RolePermission và AuditLog bigint; cấu hình unique `AspNetUserRoles.UserId` để giữ đúng một role.
- [ ] Viết integration schema tests cho unique Permission.Code, một role/user và FK RolePermission; tạo migration `InitialIdentityAndPermissions` sau khi test kỳ vọng đã mô tả schema.
- [ ] Sinh script SQL, kiểm tra không có bảng role/password song song, áp database test sạch và chạy schema tests.

**Acceptance:** Migration tạo Identity + Permissions + RolePermissions + AuditLogs đúng constraint; rollback migration về 0 và migrate lại thành công; schema test pass trên SQL Server.

#### Task 1.2: Login, policy động, seed và quản trị tài khoản lõi

**Phụ thuộc:** Task 1.1.  
**Files sở hữu:** `Authorization/*`; `Services/Identity/*`; `Services/Audit/*`; `Data/Seed/{IdentitySeed,PermissionSeed,SeedRunner}.cs`; `Areas/BackOffice/Controllers/UsersController.cs`; `Areas/BackOffice/ViewModels/Users/*`; `Areas/BackOffice/Views/Users/*`; `Controllers/AccountController.cs`; `Views/Account/*`; `tests/.../{Unit,Integration}/Identity/*`.

- [ ] Seed idempotent bốn role và toàn bộ permission code theo matrix; Admin chỉ được tạo sau khi contract ba key bootstrap đã validate; nếu tạo user hoặc gán Admin role lỗi thì rollback/xóa user bootstrap chưa hoàn chỉnh; lần chạy lại không ghi đè RolePermission đã chỉnh.
- [ ] Cài policy provider/handler đọc quyền ở mỗi request, chặn user inactive, enforce quyền bất biến Admin và quyền phụ thuộc (`Report.Export` cần `Report.View`, `Invoice.Print` cần `Invoice.View`, `Prescription.Print` cần `Prescription.View`).
- [ ] Cài login/logout/change password, quản lý user, một role/user, reset mật khẩu và revoke security stamp sau khóa/đổi role/reset. Đổi role mở Serializable transaction, đọc lại user + role hiện hành, kiểm tra RowVersion/ConcurrencyStamp và bảo vệ Admin cuối cùng, xóa role cũ, thêm đúng role mới, ghi AuditLog, cập nhật security stamp rồi commit; mọi lỗi ở add/audit/stamp rollback cả xóa role cũ.
- [ ] Bảo vệ Admin cuối cùng trong Serializable transaction cho khóa hoặc hạ quyền; ghi AuditLog cùng transaction.
- [ ] Test endpoint trực tiếp trả 401/403 đúng; đổi permission có hiệu lực ở request kế; hai request đổi role đồng thời chỉ một kết quả hợp lệ; lỗi sau bước xóa role rollback về role cũ; không request thành công nào để user zero-role; create-user + add-role lỗi không để user mồ côi; hai request đồng thời không thể khóa/hạ quyền Admin cuối cùng; audit rollback khi thao tác thất bại.

**Acceptance:** Login không có public registration; user khóa bị chặn ở request kế; role/permission seed chạy hai lần không trùng; toàn bộ permission matrix được test ít nhất một allow và một deny theo role; Admin cuối cùng còn tồn tại sau concurrency test.

### M2 — Owner, Species, Breed và Pet

#### Task 2.1: Owner và chuẩn hóa SĐT

**Phụ thuộc:** Task 1.2, cổng G1.  
**Files sở hữu:** `Models/Entities/Owner.cs`; `Services/Owners/*`; `Areas/BackOffice/{Controllers,ViewModels,Views}/Owners/*`; `tests/.../Owners/*`. `ApplicationDbContext` chỉ integration owner sửa.

- [ ] Chép nguyên quyết định G1 đã được duyệt vào contract test: liệt kê input hợp lệ/không hợp lệ và giá trị canonical tương ứng; service luôn lưu/search bằng canonical, không tự thêm quy tắc `+84` hoặc độ dài ngoài quyết định.
- [ ] Cài Owner code unique, optimistic concurrency bằng RowVersion, soft disable và quyền Owner.View/Owner.Manage; chỉ tạo unique index PhoneNumber nếu G1 đã duyệt unique.
- [ ] Nếu G1 chọn unique, test hai request tạo cùng canonical phone chỉ một thành công; nếu chọn non-unique, test tìm kiếm trả đủ nhiều Owner và UI buộc người dùng chọn bằng OwnerCode/Pet. Luôn test user chỉ View không gọi được POST bằng URL trực tiếp.

**Acceptance:** Tìm đúng Owner theo tập input và chính sách trùng số đã duyệt ở G1; migration/index khớp lựa chọn đó; sửa từ form cũ báo conflict; Owner có lịch sử không bị hard delete.

#### Task 2.2: Species/Breed/Pet và migration hồ sơ

**Phụ thuộc:** Task 2.1.  
**Files sở hữu:** `Models/Entities/{Species,Breed,Pet}.cs`; `Models/Enums/PetSex.cs`; configurations tương ứng; `Services/Pets/*`; UI `Pets/*`, `Species/*`, `Breeds/*`; `Data/Migrations/*AddOwnersAndPets*`; `tests/.../{Pets,Owners}/**`.

- [ ] Cấu hình Species, Breed, Pet, unique code, Breed(SpeciesId,Name), alternate key và FK ghép để Breed luôn thuộc Species; BirthDate nullable và Sex chỉ Unknown/Male/Female.
- [ ] Tạo service/UI một Owner có nhiều Pet, lọc breed theo species, lịch sử visit để trống có chủ đích ở mốc này; khóa catalog thay vì xóa khi đã tham chiếu.
- [ ] Tạo `AddOwnersAndPets`, sinh/review SQL, migrate test DB và test FK ghép bằng dữ liệu cố tình chọn breed sai species.

**Acceptance:** CRUD Owner/Pet theo quyền; không lưu được breed sai loài kể cả bỏ qua UI; một Owner hiển thị nhiều Pet; migration lên/xuống/lên thành công và giữ đúng unique/index/FK.

### M3 — Bác sĩ, ca làm việc và danh mục

Sau migration M2, Task 3.1 và 3.2 có thể triển khai song song nếu mỗi task chỉ sửa vùng sở hữu; một integration owner ghép configuration rồi tạo duy nhất migration M3.

#### Task 3.1: VeterinarianProfile và ca cụ thể theo ngày

**Phụ thuộc:** Task 1.2; cổng G2 phần ca/bác sĩ.  
**Files sở hữu:** entity/configuration `VeterinarianProfile`, `VeterinarianShift`; `Services/Scheduling/*`; UI `Veterinarians/*`, `Schedules/*`; `tests/.../Scheduling/*`.

- [ ] Liên kết profile 1–1 ApplicationUser, DoctorCode unique, RowVersion; chỉ user active + profile active được nhận lịch/khám mới.
- [ ] Cài ca `[StartAt,EndAt)`, StartAt < EndAt, không overlap cùng bác sĩ trong Serializable transaction.
- [ ] Test hai request tạo ca chồng nhau chỉ một thành công; khóa/sửa ca bị từ chối nếu làm vô hiệu Appointment Scheduled tương lai khi module Appointment đã tồn tại, với test regression được bật ở M4.

**Acceptance:** Manager/Admin quản lý profile và ca theo permission; ca sát biên không bị coi overlap; ca chồng bị chặn ở service và concurrency test.

#### Task 3.2: ServiceCatalog và Medicine

**Phụ thuộc:** Task 1.2; cổng G4 xác nhận thuốc chỉ là danh mục kê đơn.  
**Files sở hữu:** entity/configuration `ServiceCatalog`, `Medicine`; `Services/Catalogs/*`; UI `ServiceCatalogs/*`, `Medicines/*`; `tests/.../Catalogs/*`.

- [ ] Cấu hình code unique, giá dịch vụ không âm/nguyên VND, medicine không có giá bán/tồn kho, soft disable và RowVersion.
- [ ] Test dịch vụ giá 0 hợp lệ, giá âm hoặc có phần lẻ VND bị từ chối; catalog đã tham chiếu không hard delete.

**Acceptance:** Catalog.Manage bảo vệ toàn bộ write endpoints; catalog inactive không xuất hiện khi tạo dữ liệu mới nhưng lịch sử vẫn đọc được.

#### Task 3.3: Migration bác sĩ và danh mục

**Phụ thuộc:** Task 3.1 và 3.2 hoàn tất review.  
**Files sở hữu:** `Data/ApplicationDbContext.cs`, `Data/Migrations/*AddVeterinariansAndCatalogs*`, model snapshot.

- [ ] Ghép configuration không đổi interface hai task; tạo `AddVeterinariansAndCatalogs`, sinh/review SQL và áp test DB sạch.
- [ ] Chạy toàn bộ Identity, Owner/Pet, Scheduling và Catalog tests.

**Acceptance:** Migration thứ ba chứa đúng bốn bảng, FK/index/CHECK/rowversion; không có schema Appointment/Visit/Clinical/Invoice xuất hiện sớm.

### M4 — Appointment, Availability và Calendar

#### Task 4.1: Appointment schema và availability service

**Phụ thuộc:** M2, M3, cổng G2.  
**Files sở hữu:** `Models/Entities/Appointment.cs`; `Models/Enums/AppointmentStatus.cs`; configuration; `Services/Appointments/*`; `Services/Scheduling/AvailabilityService.cs`; `Data/Migrations/*AddAppointments*`; `tests/.../Appointments/*`.

- [ ] **[DB-01]** Cài trạng thái Scheduled/CheckedIn/Cancelled/NoShow, thời gian hợp lệ, reason bắt buộc, RowVersion và các cột lịch sử đúng lựa chọn DEC-03. CHECK phải khóa tuple hai chiều: Scheduled/CheckedIn không có CancellationReason hoặc dữ liệu đổi trạng thái dành riêng cho Cancel/NoShow; Cancelled có reason và actor/time theo lựa chọn; NoShow có actor/time theo lựa chọn và không mang cancellation fields không liên quan.
- [ ] Trong Serializable transaction, đọc lại shift/bác sĩ/pet, yêu cầu appointment nằm trọn một ca active và chặn overlap `[start,end)` với Scheduled cùng bác sĩ hoặc Pet; quy tắc CheckedIn giữ khoảng đến EndAt trừ Visit Completed/Cancelled được bổ sung khi M5 tồn tại.
- [ ] Tạo/hủy/NoShow; NoShow chỉ sau EndAt và chưa check-in; đổi lịch bằng transaction tạo lịch mới chỉ sau khi hủy lịch cũ thành công hoặc rollback cả hai.
- [ ] Tạo `AddAppointments`, sinh SQL và kiểm tra trực tiếp CHECK tuple hai chiều. Schema negative tests chèn SQL cho từng trạng thái lai (Scheduled có cancellation data, Cancelled thiếu reason/actor/time đã chọn, NoShow mang cancellation data hoặc thiếu event data đã chọn) và phải nhận CHECK violation; đồng thời test hai request cùng slot bác sĩ, cùng Pet ở hai bác sĩ, lịch sát biên, ngoài ca, bác sĩ inactive và NoShow trước giờ.

**Acceptance:** Mỗi race chỉ một appointment thành công; lỗi không để AuditLog hoặc trạng thái hủy nửa chừng; migration có hai index availability và CHECK thời gian/status.

#### Task 4.2: Appointment screens và lịch ngày/tuần

**Phụ thuộc:** Task 4.1.  
**Files sở hữu:** UI `Appointments/*`, `Calendar/*`; `wwwroot/js/calendar.js`; tests authorization/UI Appointment.

- [ ] Tạo màn hình create/detail/cancel/no-show, ViewModel không bind entity, hiển thị RowVersion và validation server.
- [ ] Cấp JSON feed tối thiểu cho FullCalendar timeGridDay/timeGridWeek, lọc bác sĩ và kiểm tra Calendar.View/Appointment.View ở server.
- [ ] Test script/endpoint không lộ lịch cho user thiếu quyền; kiểm tra ngày giờ hiển thị Asia/Bangkok nhưng round-trip lưu UTC.

**Acceptance:** Receptionist tạo/hủy/vắng, Veterinarian chỉ xem, Manager theo matrix; lịch ngày/tuần phản ánh đúng trạng thái và bộ lọc.

### M5 — Visit, check-in, walk-in và hàng đợi

#### Task 5.1: Visit schema và tiếp nhận idempotent

**Phụ thuộc:** M4, cổng G3.  
**Files sở hữu:** `Models/Entities/Visit.cs`; `Models/Enums/VisitStatus.cs`; configuration; `Services/Visits/*`; `Data/Migrations/*AddVisits*`; `tests/.../Visits/*`.

- [ ] **[DB-01]** Cấu hình snapshot owner/pet/veterinarian, filtered unique AppointmentId, Pet mở và bác sĩ InProgress; CHECK Visit theo tuple hai chiều: Waiting chỉ có CheckedInAt; InProgress có StartedAt và chưa có CompletedAt/reason; Completed có StartedAt + CompletedAt theo thứ tự và không có cancellation reason; Cancelled có reason, chưa StartedAt/CompletedAt.
- [ ] Check-in trong Serializable transaction: đọc Appointment Scheduled, tạo Visit Waiting cùng Pet/bác sĩ + snapshot, chuyển CheckedIn và audit; request lặp trả Visit đã có.
- [ ] Walk-in tạo Visit Waiting không Appointment; phân công lại chỉ Waiting và cập nhật veterinarian snapshot + audit; hủy Waiting cần lý do và không có clinical/performed service.
- [ ] Start chỉ bác sĩ phụ trách có profile/user active; hai request hoặc hai Visit cùng bác sĩ chỉ một chuyển InProgress.
- [ ] Bổ sung regression availability: Appointment CheckedIn chỉ giữ khoảng lịch gốc đến EndAt; Visit Completed/Cancelled không tiếp tục chặn slot và Visit kéo dài không biến thành khoảng đặt hẹn vô hạn.
- [ ] Tạo `AddVisits`, review SQL CHECK hai chiều và chạy schema negative tests chèn từng tổ hợp lai (Waiting có StartedAt, InProgress có CompletedAt, Completed có reason, Cancelled có StartedAt/thiếu reason); đồng thời test check-in lặp, check-in đồng thời, walk-in đồng thời cùng Pet, start đồng thời cùng bác sĩ, assign/lock concurrency và rollback audit.

**Acceptance:** Một Appointment sinh tối đa một Visit; một Pet tối đa một Waiting/InProgress; một bác sĩ tối đa một InProgress; snapshot không đổi khi Owner/Pet sửa; tất cả permission và bác sĩ phụ trách được kiểm tra ở endpoint lẫn service.

#### Task 5.2: Queue và reception UI

**Phụ thuộc:** Task 5.1.  
**Files sở hữu:** UI `Visits/*`, `Queue/*`; tests MVC Visit.

- [ ] Tạo check-in từ appointment, walk-in, queue, assign, cancel và start forms với anti-forgery + RowVersion.
- [ ] Phân biệt Waiting, InProgress, Completed chưa thu và đã thanh toán bằng nhãn rõ ràng; phần invoice chỉ bật sau M7.

**Acceptance:** Demo được appointment check-in và walk-in; gọi thẳng action trái trạng thái/quyền trả 403 hoặc lỗi nghiệp vụ, không ghi dữ liệu.

### M6 — Bệnh án, đơn thuốc, dịch vụ và hoàn tất khám

Task 6.1–6.3 có thể viết song song sau khi hợp đồng `VisitAccessGuard` và enum trạng thái được merge. Chúng không tạo migration riêng; Task 6.4 là owner duy nhất của migration và orchestration hoàn tất khám.

#### Task 6.1: MedicalRecord Draft

**Phụ thuộc:** M5, cổng G4.  
**Files sở hữu:** MedicalRecord entity/config/service/ViewModel/View/tests.

- [ ] Tạo tối đa một record/Visit; chỉ bác sĩ phụ trách sửa khi Visit InProgress; validate WeightKg > 0, giới hạn chuỗi và FollowUpDate đúng chính xác lựa chọn đã duyệt tại G4; nếu G4 chọn nullable-only thì không tự thêm giới hạn ngày.
- [ ] Test người không phụ trách không đọc nội dung qua MedicalRecord endpoint; Receptionist có Visit.View vẫn không có MedicalRecord.View; Finalized không sửa được.

**Acceptance:** Draft lưu được qua optimistic concurrency; quyền đọc và sửa lâm sàng tách biệt; diagnosis bắt buộc tại bước Complete, chưa bắt buộc khi lưu draft.

#### Task 6.2: Prescription Draft và items

**Phụ thuộc:** M5, M3, cổng G4.  
**Files sở hữu:** Prescription/PrescriptionItem entity/config/service/UI/tests.

- [ ] Tạo tối đa một prescription/Visit; cho phép Draft rỗng, nhưng Finalized cần ít nhất một item có dosage/route/frequency/duration và Quantity > 0; thao tác xóa/chuyển trạng thái item tuân thủ đúng DEC-02 và DeleteBehavior đã duyệt.
- [ ] Snapshot Medicine name/unit khi thêm item; cho phép lặp MedicineId với chỉ định khác; medicine inactive không thêm mới nhưng dòng cũ vẫn hiển thị/in.
- [ ] Test prescription không tạo InvoiceItem, không có trường giá, người không phụ trách không sửa, Finalized không đổi.

**Acceptance:** Prescription optional; nếu tồn tại khi Complete thì toàn bộ dòng hợp lệ và được khóa trong cùng transaction hoàn tất.

#### Task 6.3: VisitService lifecycle và snapshot giá

**Phụ thuộc:** M5, M3.  
**Files sở hữu:** VisitService entity/config/service/UI/tests.

- [ ] Tạo mỗi lần dùng là một dòng Pending với snapshot tên/giá; Quantity > 0; chỉ bác sĩ phụ trách của Visit InProgress chuyển Performed/Cancelled; có xóa Pending hay bắt buộc Cancelled theo đúng DEC-02.
- [ ] Performed ghi time/doctor; Cancelled bắt buộc reason; không unique theo VisitId+ServiceCatalogId.
- [ ] Test thay giá catalog không đổi dòng cũ; hai dòng cùng service hợp lệ; trạng thái trái phép hoặc request cũ bị RowVersion từ chối.

**Acceptance:** Chỉ Performed là ứng viên tính tiền; không còn cách sửa snapshot/quantity/price sau Performed.

#### Task 6.4: Clinical migration và Complete Visit transaction

**Phụ thuộc:** Task 6.1–6.3 review xong.  
**Files sở hữu:** `Data/ApplicationDbContext.cs`, `Data/Migrations/*AddClinicalRecords*`, model snapshot, `Services/Visits/CompleteVisitService.cs`, completion tests.

- [ ] **[DB-01]** Ghép schema và tạo `AddClinicalRecords`; CHECK hai chiều bắt buộc: MedicalRecord Draft có `FinalizedAt`/`FinalizedByVeterinarianId` null và Finalized có cả hai; Prescription Draft có `FinalizedAt` null và Finalized có timestamp; VisitService Pending có performed/cancel fields null, Performed có performed time/doctor và reason null, Cancelled có reason và performed fields null. Cấu hình index `VisitServices(VisitId, Status)` INCLUDE `ServiceNameSnapshot, Quantity, UnitPrice`, unique VisitId, price/quantity constraint và DeleteBehavior theo DEC-02.
- [ ] Sinh migration SQL và xác minh tên/cột/filter/include của index `VisitServices(VisitId, Status)`; dùng execution plan hoặc catalog query chứng minh checkout query có thể dùng index. Schema negative tests chèn Draft có finalized data, Finalized thiếu finalized data, Pending có performed data, Performed thiếu actor/time hoặc có reason, Cancelled thiếu reason/có performed data và đều phải nhận CHECK violation.
- [ ] Complete trong Serializable transaction đọc lại Visit/record/prescription/services; yêu cầu đúng bác sĩ, Visit InProgress, diagnosis có nội dung, record tồn tại, prescription nếu có có item hợp lệ, không Pending service.
- [ ] Cùng transaction Finalize record, Finalize prescription nếu có, đặt timestamps, chuyển Visit Completed và ghi AuditLog.
- [ ] Race test Complete vs sửa record, Complete vs thêm/xóa prescription item, Complete vs perform service; một nhánh thắng, nhánh kia không để partial update/audit.

**Acceptance:** Completed luôn suy ra record Finalized, prescription nếu có Finalized, không Pending service; rollback giữ toàn bộ Draft/InProgress khi bất kỳ validation hoặc DB write thất bại.

### M7 — Checkout, Invoice và in

#### Task 7.1: Billing calculation và invoice schema

**Phụ thuộc:** M6, cổng G5.  
**Files sở hữu:** Invoice/InvoiceItem entity/config; `Services/Billing/*`; `Services/Checkout/*`; `Data/Migrations/*AddInvoices*`; `tests/.../{Billing,Checkout}/*`.

- [ ] Billing dùng duy nhất VisitService Performed, làm tròn từng `Quantity * UnitPrice` về nguyên VND AwayFromZero rồi cộng; Pending/Cancelled và prescription bị loại.
- [ ] **[DB-02]** Cấu hình unique Invoice.VisitId, InvoiceItem.VisitServiceId, snapshot đầy đủ, CHECK tiền/method và CHECK nội dòng `LineTotal = ROUND(Quantity * UnitPrice, 0)` theo SQL Server cho dữ liệu không âm; `TotalAmount = SUM(LineTotal)` tiếp tục được service bảo vệ vì là invariant nhiều bảng.
- [ ] Confirm chỉ nhận `VisitId`, `PaymentMethod` và token chống request lặp/RowVersion; không nhận VisitServiceId, description, quantity, unit price, line total hoặc invoice total từ form. Trong Serializable transaction, truy vấn lại đúng Visit Completed và `VisitServices WHERE VisitId = @visitId AND Status = 'Performed'` qua index M6, dựng toàn bộ InvoiceItems từ tập server-side này, tính tổng, tạo invoice/items/audit rồi commit; request lặp trả invoice cũ và không đổi payment method.
- [ ] Tạo `AddInvoices`, sinh SQL và xác minh CHECK công thức LineTotal. Schema negative tests dùng SQL chèn `Quantity=2`, `UnitPrice=100000`, `LineTotal=1`, tiền âm và tiền có phần lẻ VND để nhận CHECK violation; test biên nhân/làm tròn chứng minh SQL `ROUND` khớp `MidpointRounding.AwayFromZero` với miền không âm.
- [ ] Test hóa đơn 0, nhiều quantity/làm tròn, thay catalog sau perform, checkout đồng thời, payload giả có VisitServiceId của Visit khác/Cancelled/Pending và total giả đều bị bỏ qua hoặc model binding từ chối, Visit chưa completed và rollback audit. Race checkout với perform/cancel service phải cho một snapshot nhất quán: item chỉ từ Performed services cùng Visit đã đọc trong transaction.

**Acceptance:** Preview và Confirm dùng chung calculator và cho cùng tổng; mỗi Visit tối đa một Invoice; CHECK DB từ chối LineTotal sai công thức; tổng Invoice bằng tổng LineTotal; mọi item được tạo hoàn toàn server-side, thuộc cùng Visit và từ Performed service trong snapshot transaction.

#### Task 7.2: Checkout, invoice detail và browser print UI

**Phụ thuộc:** Task 7.1.  
**Files sở hữu:** UI `Checkout/*`, `Invoices/*`, print stylesheet, MVC tests.

- [ ] Tạo danh sách Completed chưa invoice, preview, Confirm Cash/BankTransfer, detail và print từ snapshot invoice.
- [ ] Enforce Invoice.View/Checkout/Print cùng quyền phụ thuộc; anti-forgery; double-click Confirm hiển thị invoice đã có.

**Acceptance:** Receptionist/Manager/Admin theo matrix thu tiền và in; Veterinarian bị chặn; in lại sau sửa Owner/catalog vẫn giữ snapshot cũ.

### M8 — Dashboard, quản trị quyền và Audit UI

#### Task 8.1: Dashboard vận hành

**Phụ thuộc:** M7.  
**Files sở hữu:** `Services/Dashboard/*`; UI `Dashboard/*`; tests Dashboard.

- [ ] Truy vấn riêng Waiting, InProgress, Completed chưa Invoice và đã thanh toán hôm nay; không tạo bảng Dashboard.
- [ ] Test ranh giới ngày theo Asia/Bangkok chuyển đúng UTC và Dashboard.View bảo vệ endpoint.

**Acceptance:** Số liệu đối chiếu được bằng truy vấn DB và trạng thái không bị gộp sai giữa hoàn tất khám/thanh toán.

#### Task 8.2: Permission matrix và Audit viewer

**Phụ thuộc:** M1 và toàn bộ permission codes đã ổn định qua M7.  
**Files sở hữu:** UI `Permissions/*`, `Audit/*`; `Services/Identity/RolePermissionAdminService.cs`; `Services/Audit/AuditQueryService.cs`; tests quản trị.

- [ ] Cho Admin xem/sửa RolePermission trừ quyền bất biến; validate dependency trước save; thay đổi có hiệu lực request sau.
- [ ] Audit viewer lọc thời gian/user/action/entity, không hiển thị password hay toàn bộ bệnh án.
- [ ] Test POST trực tiếp từ Manager bị 403; seed chạy lại không ghi đè matrix; audit của nghiệp vụ thất bại không tồn tại.

**Acceptance:** Admin quản trị được user/quyền/audit; role khác không truy cập; matrix hiển thị đúng seed và dependency.

### M9 — Reports và Excel

#### Task 9.1: Shared report queries

**Phụ thuộc:** M7, cổng G6.  
**Files sở hữu:** `Services/Reports/*`; ViewModels/Views `Reports/*`; tests Reports.

- [ ] Định nghĩa một `ReportFilter` chuẩn hóa range `[from,to)` UTC từ ngày Việt Nam và dùng chung cho web/export.
- [ ] Revenue đọc Invoice/InvoiceItem theo PaidAt; visit count đọc CheckedInAt + status; service report đọc InvoiceItem snapshot, không ServiceCatalog hiện tại.
- [ ] Test biên 00:00 Asia/Bangkok, invoice tổng 0, catalog đổi tên/giá, cancelled/no-show, và Report.View.

**Acceptance:** Tổng web đối chiếu đúng fixture SQL; cùng filter cho kết quả ổn định và không lộ dữ liệu khi thiếu quyền.

#### Task 9.2: ClosedXML export

**Phụ thuộc:** Task 9.1.  
**Files sở hữu:** `Services/Reports/ExcelReportExporter.cs`; export actions/tests.

- [ ] Export chính DTO từ shared query, có tiêu đề, filter, header, định dạng ngày/tiền và tổng; không chạy truy vấn nghiệp vụ thứ hai.
- [ ] Test đọc workbook bằng ClosedXML và so từng row/tổng với DTO web; test Report.Export thiếu Report.View bị chặn.

**Acceptance:** Excel và web cùng số dòng/tổng/filter; file mở được, tên sheet hợp lệ, thời gian/tiền không biến thành text sai định dạng.

### M10 — Seed demo, E2E hardening và tài liệu bàn giao

#### Task 10.1: Development seed và migration rehearsal

**Phụ thuộc:** M1–M9.  
**Files sở hữu:** `Data/Seed/DevelopmentSeed.cs`; seed tests; deployment notes.

- [ ] Seed idempotent receptionist/veterinarian/manager, species/breed, owner/pet giả, profile/ca theo ngày tương đối, service catalog, medicine và appointment minh họa; không ghi chỉ định thuốc như lời khuyên y tế.
- [ ] Tạo database test từ 0 qua đủ bảy migration, downgrade từng migration theo thứ tự ngược và migrate lại; kiểm tra script không drop database hoặc dữ liệu ngoài schema.
- [ ] Chạy seed hai lần và xác minh count không đổi; quyền đã chỉnh không bị reset.

**Acceptance:** Máy mới tạo được DB bằng migration/seed theo README; database ứng dụng không bị test reset; toàn bộ index/FK/CHECK được kiểm kê khớp DatabaseDesign.

#### Task 10.2: E2E, security và concurrency suite

**Phụ thuộc:** Task 10.1.  
**Files sở hữu:** `tests/.../EndToEnd/*`, `tests/.../Security/*`, `tests/.../Concurrency/*`; chỉ sửa production khi test phát hiện lỗi và phải trả file về đúng owner review.

- [ ] E2E happy path: Owner → Pet → Appointment → Check-in → Start → MedicalRecord + Prescription + Performed Service → Complete → Checkout → Print model → Reports/Excel.
- [ ] E2E alternatives: walk-in; cancel appointment có reason; mark no-show sau giờ; reassign Waiting; Visit cancel; service cancelled; invoice 0.
- [ ] Chạy endpoint authorization matrix cho từng role; kiểm tra clinical ownership kể cả Admin không có profile; CSRF cho mọi POST; validation chống overposting.
- [ ] Chạy race suite: appointment overlap, check-in lặp, Pet mở trùng, doctor InProgress trùng, assign/lock, Complete vs clinical edit, checkout lặp, last Admin; xác minh audit cùng commit/rollback.
- [ ] Chạy `dotnet build` và `dotnet test` trên cấu hình sạch; ghi command, số test pass/fail/skipped và thời lượng vào biên bản release.

**Acceptance:** Toàn bộ happy/alternative/race/security test pass; không skipped test cho rule nghiệp vụ cốt lõi; demo hoàn chỉnh chạy từ UI; không có secret trong repository; README, ERD thực tế và permission matrix khớp code/migrations.

## 4. Quy tắc review và giao việc song song

- Mỗi task có ba cổng: **spec gate** (giả định đã đóng băng), **code gate** (unit/module integration pass), **merge gate** (full regression pass sau ghép).
- Có thể chạy song song Task 3.1/3.2, Task 6.1/6.2/6.3, Task 8.1/8.2 và Task 9.1 với phần chuẩn bị seed 10.1 không đụng schema. Mọi cặp khác mặc định chạy theo dependency graph.
- Reviewer 1 tập trung nghiệp vụ/trạng thái/quyền/snapshot; Reviewer 2 tập trung schema/concurrency/idempotency/migration/test isolation. Một lỗi phải ghi: `ID`, task gây lỗi, người phát hiện, bằng chứng test/line, mức độ, owner sửa, commit sửa và kết quả retest.
- Không merge khi reviewer chỉ nhận xét bằng suy đoán; mỗi lỗi mức blocker/high cần test tái hiện trước sửa và test pass sau sửa.
- Nếu sửa file vùng tích hợp, owner vùng tích hợp rebase/ghép sau khi module owners hoàn tất; không để nhiều agent giải conflict bằng cách chọn toàn bộ một phía.

## 5. Definition of Done toàn hệ thống

- Bảy migration chạy đúng thứ tự trên SQL Server sạch; schema khớp entity, FK, unique/filtered index, CHECK, rowversion và snapshot đã duyệt.
- Permission được kiểm tra ở server; user inactive và người thiếu quyền/không phải bác sĩ phụ trách bị chặn khi gọi thẳng endpoint.
- Các workflow chỉ cho phép transition đã định nghĩa; mọi transaction lỗi rollback cả dữ liệu nghiệp vụ và AuditLog.
- Snapshot giữ nguyên khi Owner/Pet/Veterinarian/Medicine/ServiceCatalog thay đổi; finalized clinical data và invoice không sửa/xóa qua UI thường.
- Preview/checkout/report web/Excel nhất quán; prescription không phát sinh tiền; chỉ Performed VisitService tạo InvoiceItem.
- Integration test dùng riêng `VeterinaryHospitalManagement_Test`; test guard không thể reset database ứng dụng.
- Demo được toàn luồng ngoại trú và các nhánh walk-in, hủy, vắng, request lặp; build/test sạch và README đủ để chạy trên máy mới.
