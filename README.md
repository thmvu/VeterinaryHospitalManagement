# Veterinary Hospital Management

Ứng dụng ASP.NET Core MVC quản lý bệnh viện thú y ngoại trú. Repository hiện ở mốc 1 — Foundation: solution, project web, project test, cấu hình EF Core SQL Server và trang khởi đầu tiếng Việt.

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

## Build và test

```powershell
dotnet restore VeterinaryHospitalManagement.slnx
dotnet build VeterinaryHospitalManagement.slnx --no-restore
dotnet test VeterinaryHospitalManagement.slnx --no-build
```

## Phạm vi hiện tại

Foundation chỉ chuẩn bị luồng Controller → Service → `ApplicationDbContext`, contract thời gian Việt Nam và trang Access Denied cơ bản. Chưa có entity nghiệp vụ, CRUD, đăng nhập, phân quyền hoặc database migration. Identity, RBAC và migration đầu tiên thuộc mốc 2 theo tài liệu trong `docs/`.

Không chạy `database update` ở mốc này. Trước khi tạo hoặc áp dụng migration phải kiểm tra đúng SQL Server và tên database đích, đồng thời không xóa hay ghi đè database có sẵn.

## Bằng chứng kiểm thử Foundation

- `WebApplicationFactory<Program>` khởi động ứng dụng bằng TestHost với Data Protection tạm thời, kiểm tra trang chủ trả HTTP 200, HTML dùng `lang="vi"` và có đúng tên hệ thống.
- Test Access Denied kiểm tra `/Home/AccessDenied` trả HTTP 403.
- Test clock dùng một `TimeProvider` cố định để kiểm tra UTC được đổi sang múi giờ Việt Nam UTC+7.
- Guard của test SQL chỉ chấp nhận đúng server `localhost`, database `VeterinaryHospitalManagement_Test`, Windows Authentication và các tùy chọn kết nối giống ứng dụng (`Encrypt=True`, `TrustServerCertificate=True`, `MultipleActiveResultSets=False`). Guard thứ hai yêu cầu opt-in riêng trước mọi thao tác reset/xóa dữ liệu trong fixture tương lai.
- Test kết nối SQL là test opt-in và được xUnit đánh dấu **Skipped** thật trong bộ test mặc định. Test này không tạo hoặc xóa database; nó chỉ gọi `ApplicationDbContext.Database.CanConnectAsync()` tới database test đã tồn tại với đúng connection contract.

Để chạy test SQL opt-in sau khi đã chuẩn bị `VeterinaryHospitalManagement_Test` an toàn:

```powershell
$env:VETERINARY_SQL_INTEGRATION_TESTS = "1"
dotnet test VeterinaryHospitalManagement.slnx --filter FullyQualifiedName~SqlServerOptInConnectivityTests
```

Không đặt biến `VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS=YES_I_UNDERSTAND` trừ khi một fixture tương lai thực sự cần reset database test. Foundation chưa có fixture hoặc thao tác phá hủy nào.

Kết quả test mặc định không chứng minh SQL Server hoặc database ứng dụng kết nối được. Trước migration phải chạy test opt-in với đúng connection contract; không dùng probe `Encrypt=False` thay cho bằng chứng này.
