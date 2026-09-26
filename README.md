# Veterinary Hospital Management

Ứng dụng ASP.NET Core MVC quản lý bệnh viện thú y ngoại trú. Repository đã có Foundation và phần lõi Identity/RBAC: đăng nhập nội bộ, policy theo permission, schema Identity và seed có kiểm soát.

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
   dotnet run --project src/VeterinaryHospitalManagement.Web
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

Seed tạo 2 chủ nuôi, 2 thú cưng, danh mục Chó/Mèo, 2 giống, 1 dịch vụ và 1 thuốc mẫu. Các bản ghi mẫu được nhận diện bằng mã `DEMO-` hoặc tên có “dữ liệu mẫu”; chạy lại không tạo trùng và không sửa bản ghi đã có. Nếu số điện thoại mẫu đã thuộc người khác, seed bỏ qua chủ nuôi đó. Seed không tạo tài khoản hay mật khẩu bác sĩ. Admin vào mục **Bác sĩ thú y → Thêm bác sĩ**, nhập họ tên, email đăng nhập, mật khẩu ban đầu và chuyên khoa; tài khoản Veterinarian và hồ sơ được tạo cùng lúc, mã bác sĩ tự được cấp. Lệnh seed chỉ chạy trong Development và tự thoát sau khi hoàn tất.

## Build và test

```powershell
dotnet restore VeterinaryHospitalManagement.slnx
dotnet build VeterinaryHospitalManagement.slnx --no-restore
dotnet test VeterinaryHospitalManagement.slnx --no-build
```

## Phạm vi hiện tại

Foundation chuẩn bị luồng Controller → Service → `ApplicationDbContext`, contract thời gian Việt Nam và trang Access Denied. Mốc Identity/RBAC bổ sung tài khoản nội bộ, đăng nhập/đổi mật khẩu, role, permission động, audit schema và quản trị tài khoản lõi. Không có đăng ký tài khoản công khai.

Migration Identity đã được sinh để review nhưng không tự động áp vào SQL Server. Trước khi chạy `database update` phải kiểm tra đúng server, đúng `VeterinaryHospitalManagementDb` và chắc chắn không ghi đè database có sẵn.

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
