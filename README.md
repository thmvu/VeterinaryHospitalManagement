# Veterinary Hospital Management

Ứng dụng ASP.NET Core MVC quản lý bệnh viện thú y ngoại trú: tài khoản nội bộ, chủ nuôi/thú cưng, lịch bác sĩ/lịch hẹn, tiếp nhận, khám và đơn thuốc, dịch vụ, hóa đơn, dashboard, nhật ký kiểm toán và báo cáo Excel.

## Yêu cầu

- .NET SDK 10
- Visual Studio có hỗ trợ .NET 10 và workload ASP.NET and web development
- SQL Server chạy tại `localhost`, dùng Windows Authentication

## Mở và chạy

1. Mở `VeterinaryHospitalManagement.slnx` bằng Visual Studio.
2. Chọn `VeterinaryHospitalManagement.Web` làm Startup Project.
3. Kiểm tra `ConnectionStrings:DefaultConnection` trong `src/VeterinaryHospitalManagement.Web/appsettings.Development.json`.
4. Chạy bằng HTTPS trong Visual Studio, hoặc tại thư mục gốc dùng:

   ```powershell
   dotnet run --project src/VeterinaryHospitalManagement.Web --launch-profile https
   ```

Chuỗi kết nối local mặc định dùng database dự kiến `VeterinaryHospitalManagementDb`, Integrated Security và `TrustServerCertificate=True`. Không lưu mật khẩu trong file cấu hình. Nếu môi trường sau này cần bí mật, dùng .NET user-secrets hoặc biến môi trường.

## Mã tự động và dữ liệu mẫu

Mã chủ nuôi (`OWN-`), thú cưng (`PET-`) và bác sĩ (`VET-`) được cấp khi lưu hồ sơ; người dùng không phải nhập mã. Thay đổi mã bác sĩ cần áp migration mới một lần:

```powershell
dotnet ef database update --project src/VeterinaryHospitalManagement.Web --startup-project src/VeterinaryHospitalManagement.Web
```

Sau khi đã có tài khoản Admin, có thể chèn dữ liệu thử vào database Development đang cấu hình bằng lệnh sau tại thư mục gốc:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/VeterinaryHospitalManagement.Web --no-launch-profile -- --seed-demo-only
```

Seed chỉ chạy trong Development và tự thoát sau khi hoàn tất. Với database đã có Admin, role và permissions, seed bổ sung 3 tài khoản nhân viên, 2 chủ nuôi, 2 thú cưng, danh mục Chó/Mèo, 2 giống, 2 dịch vụ và 1 thuốc mẫu. Nếu có hồ sơ bác sĩ mẫu, seed thử tạo ca 08:00–17:00; nếu có thêm thú cưng Milu của đúng chủ nuôi mẫu, seed thử tạo lịch hẹn 09:00–09:30. Các mốc giờ thuộc **ngày chạy seed theo giờ Việt Nam** và phải thỏa điều kiện hoạt động/không trùng lịch ở service.

| Vai trò mẫu | Email đăng nhập | Mật khẩu ban đầu |
| --- | --- | --- |
| Receptionist | `receptionist@hospital.local` | `Receptionist123!` |
| Veterinarian | `doctor.tam@hospital.local` | `Doctor123!` |
| Manager | `manager@hospital.local` | `Manager123!` |

Đây là thông tin tài khoản demo có sẵn trong `DemoDataSeed`, chỉ dùng khi thử trên máy Development. Nếu email đã tồn tại, seed giữ nguyên tài khoản, role và mật khẩu hiện có; bảng trên chỉ áp dụng cho tài khoản mới do seed tạo. Nếu số điện thoại mẫu đã thuộc chủ nuôi khác, seed bỏ qua chủ nuôi đó. Dữ liệu có sẵn không được sửa; nếu có xung đột ca/lịch, service có thể từ chối và lệnh seed dừng. Chạy lại cùng ngày bổ sung phần còn thiếu; chạy vào ngày khác có thể tạo ca và lịch của ngày mới.

Admin cũng có thể tạo tài khoản tại **Quản lý tài khoản** với vai trò `Veterinarian`; hệ thống tự tạo hồ sơ và cấp mã bác sĩ để hiện trong **Bác sĩ thú y**. Đổi tài khoản hiện có sang vai trò `Veterinarian` cũng tự tạo hồ sơ nếu chưa có.

Với tài khoản Veterinarian đã tạo trước phiên bản đồng bộ này nhưng chưa có hồ sơ, chạy một lần trong Development:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/VeterinaryHospitalManagement.Web --no-launch-profile -- --sync-veterinarians-only
```

Lệnh có thể chạy lại; chỉ tạo hồ sơ còn thiếu. Không cần migration mới cho thay đổi đồng bộ này.

## Build và test

```powershell
dotnet restore VeterinaryHospitalManagement.slnx
dotnet build VeterinaryHospitalManagement.slnx --no-restore
dotnet test VeterinaryHospitalManagement.slnx --no-build
```

## Phạm vi hiện tại

Code đã triển khai M1–M11 của plan ngoại trú: Identity/RBAC, hồ sơ, danh mục/bác sĩ, lịch hẹn, tiếp nhận, lâm sàng, thanh toán/in hóa đơn, dashboard/kiểm toán/ma trận quyền, báo cáo web/Excel, seed demo, ERD và nghiệm thu trên SQL Server. Bằng chứng và giới hạn kiểm chứng nằm trong [M11_Acceptance.md](docs/M11_Acceptance.md). Đăng nhập dành cho tài khoản nội bộ; không có đăng ký công khai.

Schema hiện tại có 26 bảng entity; xem [ERD theo EF model hiện tại](docs/DatabaseSchema_Current.md). Ứng dụng không tự áp migrations khi khởi động. Trước khi chạy `database update`, kiểm tra đúng server và database `VeterinaryHospitalManagementDb`.

## Demo toàn luồng

1. Áp dụng migrations, khởi tạo Admin/role/quyền theo mục bên dưới, rồi chạy `--seed-demo-only`.
2. Khởi động bằng `dotnet run --project src/VeterinaryHospitalManagement.Web --launch-profile https`, mở `https://localhost:7164/Account/Login`. Ứng dụng cấu hình cookie đăng nhập chỉ gửi qua HTTPS.
3. Đăng nhập lễ tân mẫu, chọn lịch hẹn Milu ngày chạy seed và check-in để tạo lượt khám `Waiting`.
4. Đăng nhập bác sĩ mẫu, mở lượt khám được phân công và **Bắt đầu khám**. Ghi bệnh án có chẩn đoán, thêm dịch vụ **Khám tổng quát (mẫu)** rồi xác nhận **Đã thực hiện**. Có thể lập đơn thuốc mẫu để thử giao diện; chỉ dùng dữ liệu giả.
5. **Hoàn tất khám** khi không còn dịch vụ Pending và các hồ sơ hợp lệ; trạng thái lượt khám chuyển `Completed`.
6. Đăng nhập lễ tân mẫu, vào **Thanh toán & hóa đơn**, xem tạm tính, chọn tiền mặt/chuyển khoản và xác nhận. Với một lần khám tổng quát có số lượng 1 và giá seed ban đầu chưa đổi, tổng là **150.000 ₫**. Mở chi tiết để in hóa đơn.
7. Đăng nhập quản lý mẫu, vào báo cáo doanh thu/dịch vụ theo **ngày thanh toán**, báo cáo lượt khám theo **ngày tiếp nhận**; đối chiếu dữ liệu web và file Excel.

Quyền trong demo phụ thuộc ma trận đang cấu hình. Seed không khôi phục các quyền đã bị Admin gỡ. Seed tạo lịch hẹn, không tự check-in, ghi bệnh án hay thanh toán; dữ liệu báo cáo xuất hiện sau khi bạn thực hiện các bước tương ứng. Kịch bản trên là hướng dẫn nghiệm thu qua giao diện; kết quả test service được ghi ở mục E2E SQL Server bên dưới.

## Khởi tạo Identity và Admin lần đầu

Seed không chạy mặc định. Chỉ bật sau khi migration Identity đã được áp vào đúng database. Bốn role hệ thống (`Receptionist`, `Veterinarian`, `Manager`, `Admin`) và danh mục permission được seed idempotent. Lần baseline đầu tiên có thể hoàn tất một catalog dở; sau đó, chỉ permission code mới nhận quyền mặc định. Chạy lại không phục hồi các quyền cũ mà quản trị viên đã gỡ.

Trong môi trường Development, đặt ba giá trị Admin bằng user-secrets và bật seed cho đúng một lần:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.com" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets set "BootstrapAdmin:Password" "THAY_BANG_MAT_KHAU_MANH" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets set "BootstrapAdmin:FullName" "Quản trị hệ thống" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets set "BootstrapAdmin:Enabled" "true" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets set "IdentitySeed:RunOnStartup" "true" --project src/VeterinaryHospitalManagement.Web
dotnet run --project src/VeterinaryHospitalManagement.Web
```

Ứng dụng dừng ngay khi bootstrap được bật mà thiếu/sai `BootstrapAdmin:Email`, `BootstrapAdmin:Password` hoặc `BootstrapAdmin:FullName`; lỗi chỉ nêu tên key, không in giá trị. Seed dùng `UserManager`/`RoleManager`, không tự tạo `PasswordHash`. Nếu email đã thuộc một user không phải Admin, hoặc Admin đang bị khóa, seed dừng thay vì tự nâng quyền hay tự mở khóa.

Sau lần chạy thành công, dừng ứng dụng rồi tắt seed và xóa bí mật bootstrap:

```powershell
dotnet user-secrets set "BootstrapAdmin:Enabled" "false" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets set "IdentitySeed:RunOnStartup" "false" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets remove "BootstrapAdmin:Email" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets remove "BootstrapAdmin:Password" --project src/VeterinaryHospitalManagement.Web
dotnet user-secrets remove "BootstrapAdmin:FullName" --project src/VeterinaryHospitalManagement.Web
```

Trang đăng nhập ở `/Account/Login`. User bị khóa (`IsActive = false`) không đăng nhập được; `/Account/Register` không tồn tại. Production phải lấy cấu hình bootstrap từ provider bí mật phù hợp và tắt bootstrap sau khi tạo Admin đầu tiên.

## Quản trị tài khoản

Sau khi đăng nhập bằng Admin, mở `/BackOffice/Users`. Admin có thể tạo tài khoản nội bộ, sửa họ tên/email, đổi đúng một role, khóa/mở khóa và đặt lại mật khẩu. Không có xóa cứng tài khoản vì audit cần giữ lịch sử. Mọi thay đổi role, khóa hoặc reset mật khẩu thu hồi cookie cũ; hệ thống không cho khóa hoặc hạ quyền Admin đang hoạt động cuối cùng. Các thao tác này ghi AuditLog cùng transaction.

## Khám, đơn thuốc và dịch vụ

Bác sĩ phụ trách mở lượt khám đang `InProgress` để ghi bệnh án, lập đơn thuốc nếu cần và thêm dịch vụ. Dịch vụ được ghi `Pending`, rồi xác nhận `Performed` hoặc `Cancelled`; tên và giá được giữ theo thời điểm thêm. Chỉ dịch vụ `Performed` được tính tiền. Bệnh án phải có chẩn đoán, không còn dịch vụ `Pending`, và đơn thuốc nếu đã lập phải có dòng hợp lệ thì bác sĩ mới được **Hoàn tất khám**. Bệnh án và đơn thuốc được chốt cùng lượt khám trong một giao dịch; đơn thuốc là chỉ định điều trị, không tự cộng vào hóa đơn. Schema lâm sàng nằm trong migration `AddClinicalRecords`.

## In đơn thuốc

Sau khi bác sĩ hoàn tất khám và đơn được chốt, mở **Đơn thuốc → In đơn thuốc** để xem bản in A4. Chỉ Admin hoặc bác sĩ đang hoạt động phụ trách lượt khám, có đủ `Prescription.View` và `Prescription.Print`, được truy cập. Đơn nháp, đơn chưa có thuốc hoặc lượt khám chưa hoàn tất không có nút in. Bản in giữ tên/đơn vị thuốc và chỉ dẫn đã lưu; không lấy lại thông tin thuốc hiện tại hoặc cộng tiền thuốc vào hóa đơn. Dùng nút **In đơn thuốc** trên trang in để mở hộp thoại in của trình duyệt.

## Thu tiền và hóa đơn

Sau khi lượt khám `Completed`, lễ tân vào **Thanh toán & hóa đơn** trong sidebar. Trang tạm tính chỉ hiển thị dịch vụ đã thực hiện; khi xác nhận tiền mặt hoặc chuyển khoản, server đọc lại và tính lại tổng, lưu hóa đơn và audit trong một giao dịch. Mỗi lượt khám có tối đa một hóa đơn; bấm xác nhận lặp trả hóa đơn cũ và không đổi phương thức thanh toán. Lượt không có dịch vụ vẫn có thể có hóa đơn 0 ₫. Từ chi tiết hóa đơn, tài khoản có quyền `Invoice.Print` có thể mở trang in A4 của trình duyệt. Bản in này không phải hóa đơn điện tử tích hợp thuế.

Trước khi dùng chức năng thu tiền trên database ứng dụng, áp dụng migration `AddInvoices` một lần trong thư mục gốc repository (kiểm tra connection string đang trỏ đúng database ứng dụng, không phải database test):

```powershell
dotnet tool restore
dotnet ef database update --project src/VeterinaryHospitalManagement.Web --startup-project src/VeterinaryHospitalManagement.Web
```

## Báo cáo và Excel (M10)

Tài khoản có quyền `Report.View` mở **Báo cáo → Doanh thu** trong sidebar. Chọn khoảng ngày theo giờ Việt Nam; doanh thu lấy hóa đơn đã thanh toán theo `PaidAt`, còn báo cáo dịch vụ lấy snapshot tên, số lượng và thành tiền từ `InvoiceItems`. Báo cáo lượt khám lọc theo `CheckedInAt` và chia theo trạng thái. Người có quyền `Report.Export` có thể tải Excel cho cùng khoảng ngày và số liệu đang xem.

## E2E SQL Server (M11)

Ngày 02/10/2026: kiểm tra kết nối bằng EF tới database test pass **1/1**; hai kịch bản `EndToEndSqlServerTests` pass **2/2**, không skip. Bộ test mặc định pass **218**, skip **119** test SQL opt-in, fail **0**. Hai kịch bản E2E kiểm tra:

- Tạo tài khoản/hồ sơ → lịch hẹn → check-in → khám → lưu bệnh án/đơn thuốc bằng service → thực hiện/hủy dịch vụ → hoàn tất → thu tiền → báo cáo. Check-in và checkout lặp không tạo thêm bản ghi/audit; checkout lặp giữ phương thức thanh toán đầu tiên. Giá/tên dịch vụ giữ snapshot dù danh mục đổi, dịch vụ hủy không tính tiền; doanh thu dùng ngày thanh toán và lượt khám dùng ngày tiếp nhận theo giờ Việt Nam.
- Walk-in, hủy lượt khám, hủy lịch hẹn và đánh dấu vắng sau giờ kết thúc; báo cáo lượt khám phản ánh trạng thái hủy.

Test dùng clock cố định và database SQL Server riêng. Để chạy lại **chỉ hai kịch bản này**, cho phép fixture xóa/tạo lại `localhost/VeterinaryHospitalManagement_Test` trước mỗi test; dữ liệu đang có trong database test sẽ bị xóa:

```powershell
$env:VETERINARY_SQL_INTEGRATION_TESTS = "1"
$env:VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS = "YES_I_UNDERSTAND"
dotnet test VeterinaryHospitalManagement.slnx --no-restore --filter FullyQualifiedName~EndToEndSqlServerTests
```

Nghiệm thu bổ sung ngày 02–03/10/2026: toàn bộ suite SQL trước các sửa UI/lịch sử cuối đạt **337/337**, không fail/skip, gồm 9 kiểm thử có tên concurrency. E2E đã kiểm tra thêm endpoint báo cáo/bản in, nội dung ba file Excel, ngày thanh toán khác ngày tiếp nhận, HTTP 400 cho bộ lọc sai và HTTP 403 khi thiếu quyền. Hồ sơ thú cưng hiển thị tối đa 50 lượt khám gần nhất nếu có `Visit.View`; chỉ có `Pet.View` không được thấy lịch sử.

Smoke test bằng trình duyệt đã kiểm tra đăng nhập/đăng xuất, navigation Admin, hóa đơn/bản in A4, báo cáo và tải ba file `.xlsx`. File tải xuống được mở bằng bộ đọc OpenXML độc lập để đối chiếu tổng và kiểu số. Chưa nghiệm thu máy in vật lý, Microsoft Excel desktop, hoặc triển khai production. Chi tiết các lượt kiểm tra và thay đổi cuối ở [M11_Acceptance.md](docs/M11_Acceptance.md).

## Bằng chứng kiểm thử Foundation

- `WebApplicationFactory<Program>` khởi động ứng dụng bằng TestHost với Data Protection tạm thời, kiểm tra trang chủ trả HTTP 200, HTML dùng `lang="vi"` và có đúng tên hệ thống.
- Test Access Denied kiểm tra `/Home/AccessDenied` trả HTTP 403.
- Test clock dùng một `TimeProvider` cố định để kiểm tra UTC được đổi sang múi giờ Việt Nam UTC+7.
- Guard của test SQL chỉ chấp nhận đúng server `localhost`, database `VeterinaryHospitalManagement_Test`, Windows Authentication và các tùy chọn kết nối giống ứng dụng (`Encrypt=True`, `TrustServerCertificate=True`, `MultipleActiveResultSets=False`). Guard thứ hai yêu cầu opt-in riêng trước mọi thao tác reset/xóa dữ liệu trong fixture tương lai.
- Test kết nối SQL là test opt-in và được xUnit đánh dấu **Skipped** thật trong bộ test mặc định. Test này không tạo hoặc xóa database; nó chỉ gọi `ApplicationDbContext.Database.CanConnectAsync()` tới database test đã tồn tại với đúng connection contract.

Để chạy kiểm tra kết nối SQL không phá hủy sau khi đã chuẩn bị `VeterinaryHospitalManagement_Test`:

```powershell
$env:VETERINARY_SQL_INTEGRATION_TESTS = "1"
dotnet test VeterinaryHospitalManagement.slnx --filter FullyQualifiedName~SqlServerOptInConnectivityTests
```

Để chạy toàn bộ Identity SQL suite, gồm migration/schema/seed/workflow, cần xác nhận riêng việc cho phép fixture xóa và tạo lại **đúng database test** `localhost/VeterinaryHospitalManagement_Test`:

```powershell
$env:VETERINARY_SQL_INTEGRATION_TESTS = "1"
$env:VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS = "YES_I_UNDERSTAND"
dotnet test VeterinaryHospitalManagement.slnx --no-restore
```

Không đặt `VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS=YES_I_UNDERSTAND` khi chuỗi kết nối không trỏ đúng `VeterinaryHospitalManagement_Test`. Test suite giữ khóa SQL cross-process cho database test, nhưng không được dùng nó với database ứng dụng hoặc database demo.

Kết quả test mặc định không chứng minh SQL Server hoặc database ứng dụng kết nối được. Trước migration phải chạy test opt-in với đúng connection contract; không dùng probe `Encrypt=False` thay cho bằng chứng này.
