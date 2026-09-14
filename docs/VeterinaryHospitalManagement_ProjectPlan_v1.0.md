# Veterinary Hospital Management — Project Plan v1.0

Ngày: 14/09/2026. Trạng thái: bản thiết kế đề xuất để rà soát trước khi viết ứng dụng.

## 1. Mục tiêu và nguồn quy tắc

Xây dựng đồ án C# ASP.NET Core MVC quản lý bệnh viện thú y **ngoại trú**. Người dùng đã chọn phạm vi: lịch hẹn, khám, đơn thuốc, dịch vụ và hóa đơn.

Tham khảo `../../MusicBoxManagement_ProjectPlan_v1.2.md` về cách tổ chức kiến trúc, RBAC, workflow, dữ liệu lịch sử, migration, test và triển khai từng module. Nghiệp vụ dưới đây thuộc dự án thú y mới; không áp dụng giờ mở cửa, tính tiền theo giờ, RoomSession, đặt phòng, grace period hoặc quyền theo tên role của Music Box.

Thứ tự thiết kế: **Actor → Use Case → Workflow → ERD → Database Schema → Screen List → Permission Matrix**.

Thứ tự thực hiện mỗi module: **Model → DbContext/Configuration → Migration → Service → Controller/ViewModel → View → Test → Commit**. Xác định tình huống kiểm thử trước khi viết nghiệp vụ, chạy kiểm thử trong quá trình triển khai.

## 2. Phạm vi và các giả định cần rà soát

Phạm vi đã chọn:

- Tài khoản nội bộ, đăng nhập, phân quyền theo Role → Permission.
- Chủ nuôi, thú cưng và lịch sử khám.
- Lịch hẹn với bác sĩ, tiếp nhận khách có hẹn hoặc đến trực tiếp.
- Hàng đợi, phân công bác sĩ, bệnh án, đơn thuốc và dịch vụ thực hiện.
- Tiền tạm tính, xác nhận thanh toán, hóa đơn và in bằng trình duyệt.
- Dashboard, lịch ngày/tuần, báo cáo web/Excel và nhật ký thao tác theo phương pháp của plan mẫu.

Các quyết định nghiệp vụ **đề xuất**, chưa phải yêu cầu đã được người dùng xác nhận:

1. Một cơ sở; chủ nuôi chưa đăng nhập/đặt lịch online. Lễ tân nhập lịch qua điện thoại hoặc tại quầy. Không dùng SĐT làm khóa truy cập bệnh án public.
2. Một bác sĩ phụ trách chính một lượt khám; có lịch làm việc theo ngày. Thời gian hẹn do lễ tân chọn trong ca làm việc, không sao chép slot/giờ nghỉ của Music Box.
3. Đơn thuốc là chỉ định mang về; chưa bán/cấp phát thuốc tại quầy. Hóa đơn tính **dịch vụ đã thực hiện**, không tự tính tiền thuốc từ đơn kê. Nếu cần bán thuốc, bổ sung workflow cấp phát, giá và các bảng tương ứng trước khi code module đó.
4. Thanh toán một lần toàn bộ bằng Cash hoặc BankTransfer ghi nhận thủ công. Chưa có thu cọc, công nợ, refund hoặc thanh toán nhiều phần.
5. Không tự chuyển NoShow theo một mốc tùy ý; lễ tân đánh dấu vắng sau giờ hẹn nếu chưa tiếp nhận.
6. Nội trú, chuồng, tồn kho/lô/hạn dùng thuốc, nhập hàng, lịch nhắc tiêm, tích hợp xét nghiệm, SMS và thanh toán tự động chưa thuộc bản này. Dịch vụ tiêm có thể ghi như một dịch vụ, nhưng không có module lịch tiêm riêng.

Các giả định trên giúp bản plan có thể rà soát cụ thể. Chưa phát sinh code nghiệp vụ hoặc SQL migration từ bản dự thảo này.

## 3. Môi trường và kiến trúc C#

Đã kiểm tra trên máy ngày 14/09/2026:

- Có .NET SDK `10.0.400` và `10.0.401`.
- SQL Server default instance `MSSQLSERVER` đang chạy.
- Kết nối Windows Authentication thành công tới `localhost`; ServerName trả về `LAPTOP-31465OCJ`.
- ProductVersion `17.0.1135.8`, Enterprise Developer Edition (64-bit).
- Tài khoản Windows hiện tại có quyền CREATE ANY DATABASE. Chưa tạo database Veterinary trong bước lập plan.

Kiến trúc đề xuất: một ứng dụng MVC và một project test. Controller nhận input qua ViewModel, service xử lý nghiệp vụ, DbContext truy cập SQL Server. Không thêm microservice, CQRS hoặc generic repository bọc EF Core.

| Thành phần | Lựa chọn |
| --- | --- |
| Solution | VeterinaryHospitalManagement |
| Web project | VeterinaryHospitalManagement.Web, ASP.NET Core MVC, .NET 10 |
| ORM | EF Core 10, provider SQL Server; các package EF thống nhất phiên bản tương thích |
| Xác thực | ASP.NET Core Identity, cookie, không public registration |
| Phân quyền | IdentityRole + Permission + RolePermission, policy ở server |
| Giao diện | Razor Views, Bootstrap, JavaScript; tiếng Việt |
| Lịch | FullCalendar timeGridDay/timeGridWeek, lọc bác sĩ |
| Excel | ClosedXML, dùng cùng truy vấn/bộ lọc với báo cáo web |
| Test | xUnit; integration test trên SQL Server database riêng |
| Schema | EF Core Code First Migrations là nguồn quản lý schema |

Hai lựa chọn khác đã cân nhắc: tách Domain/Application/Infrastructure/Web phù hợp khi quy mô tăng nhưng thêm chi phí tổ chức; Web API + SPA phù hợp client độc lập nhưng phát sinh hai ứng dụng và luồng xác thực phức tạp hơn. Chọn MVC + service cho đồ án hiện tại.

```text
VeterinaryHospitalManagement/
  docs/
    VeterinaryHospitalManagement_ProjectPlan_v1.0.md
    DatabaseDesign_v1.0.md
    Codex_Foundation_Prompt.md
  src/
    VeterinaryHospitalManagement.Web/
      Areas/BackOffice/{Controllers,Views,ViewModels}/
      Controllers/                       # đăng nhập/trang lỗi khi cần
      Models/{Entities,Enums}/
      Data/{Configurations,Migrations,Seed}/
      Services/{Identity,Owners,Pets,Scheduling,Clinical,Billing,Reports}/
      Authorization/
      ViewModels/
      Views/Shared/
      wwwroot/
      Program.cs
  tests/
    VeterinaryHospitalManagement.Tests/{Unit,Integration}/
```

Đây là cấu trúc dự kiến; hiện chỉ tạo các tài liệu trong `docs`.

## 4. Actor → Use Case

| Actor | Use case chính |
| --- | --- |
| Chủ nuôi | Cung cấp thông tin, đặt hẹn qua lễ tân, đưa thú cưng đến khám, nhận đơn/hóa đơn; chưa có tài khoản web |
| Receptionist | Quản lý chủ nuôi/thú cưng, lịch hẹn, tiếp nhận, phân công bác sĩ, thu tiền và in hóa đơn |
| Veterinarian | Xem hàng đợi được phân công, bắt đầu khám, nhập bệnh án, kê đơn, ghi dịch vụ, kết thúc khám |
| Manager | Xem vận hành, quản lý danh mục, lịch làm việc bác sĩ, báo cáo và xuất Excel |
| Admin | Quản lý tài khoản/quyền, xem audit; toàn quyền truy cập quản trị |

Một tài khoản có đúng một IdentityRole. Một bác sĩ có thêm VeterinarianProfile liên kết 1–1 ApplicationUser. Quyền kỹ thuật không thay cho điều kiện chuyên môn: thao tác ký/kết thúc bệnh án cần bác sĩ phụ trách có profile đang hoạt động, kể cả người dùng có quyền Admin.

## 5. Workflow và trạng thái

### 5.1 Chủ nuôi và thú cưng

Lễ tân tìm chủ nuôi bằng SĐT đã chuẩn hóa → tạo nếu chưa có → chọn/tạo thú cưng → xem lịch sử. Một chủ nuôi có nhiều thú cưng, mỗi thú cưng thuộc một chủ nuôi trong phạm vi bản đầu. Chưa có chuyển quyền sở hữu; lưu snapshot chủ nuôi/thú cưng khi mở lượt khám để giữ lịch sử.

### 5.2 Đặt hẹn và tiếp nhận

Chọn thú cưng → bác sĩ → ngày/giờ trong ca làm việc → kiểm tra trùng lịch → tạo `Scheduled`.

```text
Appointment: Scheduled → CheckedIn
                        → Cancelled
                        → NoShow
```

Hủy phải có lý do. Đổi bác sĩ/ngày/giờ bằng hủy lịch cũ và tạo lịch mới; thông báo rõ lịch mới chỉ được bảo đảm khi lưu thành công. CheckedIn là trạng thái đã tiếp nhận; kết quả điều trị xem ở Visit.

Check-in tạo đúng một Visit `Waiting`, cùng Pet và bác sĩ nguồn, đồng thời cập nhật Appointment. Bấm lặp trả lượt khám đã có. Khách đến trực tiếp tạo Visit với AppointmentId = null, bác sĩ được phân công và trạng thái Waiting.

Một thú cưng có tối đa một Visit chưa hoàn tất hoặc hủy. Một bác sĩ có nhiều lượt Waiting nhưng tối đa một lượt InProgress; lượt Waiting không tạo một khoảng đặt hẹn vô hạn.

### 5.3 Khám và bệnh án

```text
Visit: Waiting → InProgress → Completed
       Waiting → Cancelled
```

Bác sĩ phụ trách bắt đầu khám → ghi lý do khám, triệu chứng, chỉ số, chẩn đoán, hướng xử trí → kê đơn nếu có → ghi dịch vụ → xác nhận hoàn tất.

MedicalRecord có Draft/Finalized. Kết thúc khám phải có bệnh án và chẩn đoán; không còn dịch vụ Pending; đơn đã tạo phải có dòng thuốc hợp lệ. Trong cùng transaction: khóa bệnh án, khóa đơn và chuyển Visit Completed. Không sửa bệnh án/đơn đã chốt bằng form cập nhật thông thường. Quy trình đính chính sau chốt là phần cần thiết kế riêng nếu được yêu cầu.

Visit Completed nghĩa là hoàn tất khám, **không đồng nghĩa đã thanh toán**. Dashboard hiển thị các Visit Completed chưa có Invoice để thu tiền.

### 5.4 Đơn thuốc và dịch vụ

Đơn thuốc: bác sĩ chọn thuốc từ danh mục, nhập liều/chỉ dẫn dạng văn bản, đường dùng, tần suất, thời gian dùng và số lượng. Phần mềm lưu chỉ định do bác sĩ nhập; không tự đề xuất thuốc hoặc liều. Lưu tên/đơn vị thuốc snapshot; đơn không tự tạo khoản phải thu.

Dịch vụ: chọn danh mục → tạo dòng Pending và snapshot tên/đơn giá → xác nhận Performed hoặc Cancelled. Chỉ Performed được tính tiền. Bác sĩ chỉ thay đổi dịch vụ của Visit mình phụ trách đang InProgress. Mỗi lần thực hiện là một dòng riêng, không áp dụng unique (VisitId, ServiceId).

### 5.5 Thanh toán và hóa đơn

Lễ tân mở Visit Completed chưa thu tiền → server tính tổng các dịch vụ Performed → chọn Cash/BankTransfer → Confirm.

Confirm đọc lại dữ liệu và tính lại tổng trên server; tạo Invoice + InvoiceItems + AuditLog trong một transaction. Tổng trên trình duyệt chỉ dùng hiển thị. Invoice là chứng từ đã thanh toán; không có draft Invoice trong database. Mỗi Visit tối đa một Invoice, bấm lặp trả Invoice đã có và không sửa phương thức thanh toán.

Đơn giá >= 0 cho phép dịch vụ miễn phí; hóa đơn tổng 0 vẫn được ghi nhận một lần. Không tính tiền theo thời gian khám. Không sửa/xóa hóa đơn đã xác nhận. In hóa đơn không phải hóa đơn điện tử tích hợp thuế.

## 6. Permission Matrix đề xuất

`✓` là quyền seed mặc định. Admin có toàn quyền policy cố định; các điều kiện trạng thái và bác sĩ phụ trách vẫn bắt buộc. RolePermission các role còn lại lưu độc lập, thay đổi có hiệu lực ở request tiếp theo.

| Permission | Receptionist | Veterinarian | Manager | Admin |
| --- | --- | --- | --- | --- |
| Dashboard.View, Calendar.View | ✓ | ✓ | ✓ | ✓ |
| Owner.View, Pet.View | ✓ | ✓ | ✓ | ✓ |
| Owner.Manage, Pet.Manage | ✓ | — | ✓ | ✓ |
| Appointment.View | ✓ | ✓ | ✓ | ✓ |
| Appointment.Create, Appointment.Cancel, Appointment.MarkNoShow | ✓ | — | ✓ | ✓ |
| Visit.View | ✓ | ✓ | ✓ | ✓ |
| Visit.CheckIn, Visit.WalkIn, Visit.Assign, Visit.Cancel | ✓ | — | ✓ | ✓ |
| Visit.Start, Visit.Complete | — | ✓ | — | ✓* |
| MedicalRecord.View, Prescription.View, Prescription.Print | — | ✓ | — | ✓ |
| MedicalRecord.Edit, Prescription.Manage, VisitService.Manage | — | ✓ | — | ✓* |
| Invoice.View, Invoice.Checkout, Invoice.Print | ✓ | — | ✓ | ✓ |
| Catalog.Manage, Schedule.Manage | — | — | ✓ | ✓ |
| Report.View, Report.Export | — | — | ✓ | ✓ |
| User.Manage, Permission.Manage, Audit.View | — | — | — | ✓ |

`✓*`: chỉ thực hiện nếu đồng thời là bác sĩ được phân công, có profile hợp lệ. Trong seed Admin không có profile khám bệnh; vì vậy không ký thay bác sĩ.

- User.Manage, Permission.Manage, Audit.View chỉ Admin, không cấp qua ma trận cho role khác.
- Các quyền sửa lâm sàng cần profile bác sĩ và quyền trên Visit cụ thể; ẩn nút không thay cho kiểm tra server.
- Report.Export cần Report.View; Invoice.Print cần Invoice.View; Prescription.Print cần Prescription.View.
- Visit.View cung cấp thông tin tiếp nhận/trạng thái; không tự cấp nội dung bệnh án/đơn thuốc.
- Manager không tự kế thừa quyền lâm sàng của Veterinarian.
- Nhân viên bị khóa bị chặn request kế tiếp; đổi role/reset mật khẩu làm mất hiệu lực đăng nhập cũ.
- Không tự khóa/hạ quyền Admin cuối cùng; kiểm tra cả thao tác quản trị đồng thời.

## 7. ERD và Database Schema

Chi tiết kiểu dữ liệu, quan hệ, index, constraints và chiến lược migration nằm trong [DatabaseDesign_v1.0.md](DatabaseDesign_v1.0.md).

Chuỗi quan hệ chính: `Owner → Pet → Appointment/Visit → MedicalRecord + Prescription + VisitService → Invoice`.

Giữ IdentityUser/IdentityRole; không tạo hệ Role hoặc bảng mật khẩu song song. Không tạo bảng Dashboard/Report; tổng hợp từ dữ liệu nghiệp vụ.

## 8. Screen List

| Nhóm | Màn hình |
| --- | --- |
| Đăng nhập | Login, đổi mật khẩu, AccessDenied |
| Vận hành | Dashboard, lịch Ngày/Tuần theo bác sĩ, hàng đợi |
| Chủ nuôi | Danh sách/tìm SĐT, tạo/sửa, chi tiết và thú cưng |
| Thú cưng | Tạo/sửa, hồ sơ, lịch sử khám theo quyền |
| Lịch hẹn | Tạo, chi tiết, hủy, đánh dấu vắng, check-in |
| Tiếp nhận | Walk-in, phân công bác sĩ khi Waiting, hủy Waiting có lý do |
| Khám | Danh sách được phân công, chi tiết Visit với tab bệnh án/đơn thuốc/dịch vụ |
| Thanh toán | Danh sách chờ thu, tạm tính/Confirm, danh sách/chi tiết/in hóa đơn |
| Danh mục | Loài, giống, dịch vụ, thuốc; khóa/mở, hạn chế xóa khi có lịch sử |
| Nhân sự | Hồ sơ bác sĩ và ca làm việc theo ngày |
| Báo cáo | Doanh thu, lượt khám, dịch vụ; bộ lọc và xuất Excel |
| Quản trị | User, gán role, reset mật khẩu, ma trận Permission, AuditLog |

## 9. Trách nhiệm service

| Service | Trách nhiệm |
| --- | --- |
| PermissionService/Handler | Tính quyền hiện hành, quyền bất biến và kiểm tra tài khoản |
| OwnerService, PetService | Dữ liệu chủ nuôi/thú cưng, chuẩn hóa SĐT, quy tắc loài/giống |
| SchedulingService | Ca bác sĩ và overlap theo bác sĩ/thú cưng; cấp dữ liệu lịch |
| AppointmentService | Đặt/hủy/vắng và kiểm tra khả dụng trong transaction |
| VisitService | Tiếp nhận, phân công khi Waiting, bắt đầu và hoàn tất lượt khám |
| MedicalRecordService | Bệnh án Draft và kiểm tra bác sĩ phụ trách |
| PrescriptionService | Đơn và các dòng thuốc trước khi chốt |
| ClinicalServiceService | Dịch vụ Pending/Performed/Cancelled và snapshot giá |
| BillingService | Tính tiền dịch vụ bằng decimal, dùng chung cho preview và Confirm |
| CheckoutService | Một hóa đơn mỗi Visit, transaction và xử lý request lặp |
| ReportService | Truy vấn cùng bộ lọc cho web và Excel |
| AuditService | Ghi hành động quan trọng cùng transaction nghiệp vụ |

Service điều phối ngoài cùng mở transaction; các service con dùng chung scoped DbContext. Không giữ transaction lúc người dùng đang xem form.

## 10. Lộ trình triển khai

| Mốc | Phạm vi một lần làm | Kết quả nghiệm thu |
| --- | --- | --- |
| 0 | Rà soát bản plan và các giả định mục 2 | Actor, workflow, schema, quyền thống nhất |
| 1 — Foundation | Solution MVC + test, cấu hình SQL Server, cấu trúc thư mục, trang lỗi | Build được, kết nối database riêng; chưa có CRUD nghiệp vụ |
| 2 — Identity/RBAC | ApplicationUser, IdentityRole, Permission/RolePermission, AuditLog, login/seed | Đăng nhập, chặn endpoint, quyền đổi có hiệu lực, seed chạy lại an toàn |
| 3 — Hồ sơ | Owner → Species/Breed → Pet; làm từng phần | Một chủ có nhiều thú cưng, tìm SĐT, không chọn giống sai loài |
| 4 — Danh mục/bác sĩ | VeterinarianProfile/ca làm, ServiceCatalog, Medicine | Có danh mục và bác sĩ hoạt động để đặt lịch/khám |
| 5 — Lịch hẹn | Appointment + availability + lịch ngày/tuần + hủy/vắng | Hai request trùng bác sĩ/thú cưng chỉ một thành công |
| 6 — Tiếp nhận | Check-in, walk-in, hàng đợi, phân công, bắt đầu khám | Không sinh hai Visit từ một Appointment; một Pet không có hai lượt mở |
| 7 — Lâm sàng | MedicalRecord → Prescription → VisitService → hoàn tất khám | Bác sĩ đúng quyền ghi/chốt hồ sơ; giữ snapshot |
| 8 — Thanh toán | Preview, Confirm, Invoice/InvoiceItem, in | Tổng đúng, chỉ tính Performed, bấm lặp không thu hai lần |
| 9 — Quản trị/vận hành | Dashboard, UI User/ma trận quyền/Audit | Phân biệt đang khám, hoàn tất chưa thu, đã thanh toán |
| 10 — Reports/Excel | Doanh thu, lượt khám, dịch vụ | Web/Excel cùng bộ lọc/tổng, chặn thiếu quyền |
| 11 — Hoàn thiện | Test tích hợp, seed demo, README, ERD thực tế | Demo toàn luồng và hướng dẫn chạy trên máy mới |

Audit, validation, quyền và test làm từ module tương ứng; mốc cuối chỉ tổng kiểm tra. Không dồn xử lý tính toàn vẹn dữ liệu đến cuối. Chưa có hạn nộp/số thành viên nên không tự gán số tuần.

## 11. Kiểm thử và Definition of Done

- Unit: chuẩn hóa SĐT, overlap `[start,end)`, trong ca làm việc, trạng thái hợp lệ, giá snapshot, tổng tiền, quyền phụ thuộc.
- Integration SQL Server riêng: trùng lịch cùng bác sĩ/cùng Pet, check-in lặp, hai Visit mở cùng Pet, hai lượt InProgress cùng bác sĩ.
- Kiểm tra cùng lúc bắt đầu/đổi bác sĩ/khóa tài khoản, chốt bệnh án/sửa đơn, checkout lặp; lỗi trước commit phải rollback cùng AuditLog.
- Kiểm tra người không phụ trách không đọc/sửa hồ sơ ngoài phạm vi được cấp; gọi thẳng endpoint cũng bị chặn.
- Báo cáo doanh thu theo PaidAt; lượt khám theo ngày tiếp nhận và trạng thái; dịch vụ doanh thu từ InvoiceItem, không lấy giá danh mục hiện tại.
- Không dùng EF InMemory để khẳng định filtered unique index, isolation hoặc rollback SQL Server hoạt động.
- Demo: tạo chủ/thú cưng → hẹn → check-in → khám → đơn + dịch vụ → hoàn tất → thu tiền → in → báo cáo/Excel. Có thêm walk-in, hủy hẹn, vắng, và kiểm thử request lặp.

## 12. Luật giao việc cho Codex

Mỗi yêu cầu phải chỉ rõ module, actor, permission, workflow, entity, validation, dữ liệu lịch sử và tiêu chí kiểm thử. Không giao cả hệ thống trong một lượt.

Không tự thay kiến trúc hoặc nghiệp vụ đã được duyệt. Không copy entity/luật Music Box vào dự án thú y. Không dựng module chưa đến mốc. Không sửa database bằng tay song song EF migrations; script SQL để review phải sinh từ migration tương ứng.

Không hard delete hồ sơ có lịch sử, không commit mật khẩu thật, không chạy integration test vào database dùng demo. Seed dùng UserManager/RoleManager và không ghi đè quyền Admin đã chỉnh. Commit theo module khi repository đã được thiết lập và kiểm tra phù hợp đã đạt.

Prompt khởi đầu nằm tại [Codex_Foundation_Prompt.md](Codex_Foundation_Prompt.md).

## 13. Nguồn kỹ thuật

- [Microsoft — EF Core quản lý migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing): dùng migration để theo dõi thay đổi schema.
- [Microsoft — Áp dụng migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying): tạo và kiểm tra script trước khi áp dụng khi cần.
- [Microsoft — Policy authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0): policy/requirement/handler cho kiểm tra quyền tại server.
