# Prompt tiếp tục dự án Veterinary Hospital Management

Sao chép toàn bộ nội dung từ phần **PROMPT** bên dưới vào một task Codex mới.

---

## PROMPT

Bạn đang tiếp tục dự án **Veterinary Hospital Management**, không tạo lại project và không chuyển sang Music Box.

### 1. Vị trí và nguồn sự thật

- Workspace: `D:\vu\hoctap\webC#\VeterinaryHospitalManagement`
- Repository: `https://github.com/thmvu/VeterinaryHospitalManagement.git`
- Nhánh tích hợp: `main`
- Tài liệu bắt buộc phải đọc trước khi sửa code:
  - `docs/VeterinaryHospitalManagement_ProjectPlan_v1.0.md`
  - `docs/DatabaseDesign_v1.0.md`
  - `docs/Codex_Foundation_Prompt.md`
  - `docs/agent-work/main-delivery-plan.md` nếu file này tồn tại trong workspace

Các tài liệu trên là luật gốc. Không tự đổi kiến trúc, phạm vi nghiệp vụ, permission matrix, quy tắc transaction hoặc thứ tự migration.

### 2. Việc bắt buộc làm đầu mỗi task

Trước khi lên kế hoạch hoặc sửa file, hãy thực hiện một **recovery audit**:

1. Đọc `git status`, branch hiện tại và ít nhất 5 commit gần nhất.
2. Đọc code, migration, test và tài liệu liên quan trực tiếp đến lát công việc sắp làm.
3. Kiểm tra phần nào đã hoàn thành, phần nào dở dang và thay đổi nào là của người dùng.
4. Không xóa, reset, ghi đè hoặc commit thay đổi cục bộ không thuộc task.
5. Chạy kiểm tra nền phù hợp để xác nhận trạng thái thực tế trước khi sửa.
6. Nếu thấy task trước đang dở, phải hoàn tất hoặc đưa project về trạng thái build/test rõ ràng trước khi mở task mới.

Không tin hoàn toàn vào bản tóm tắt này nếu code hiện tại cho thấy khác biệt; code, migration, Git và kết quả test mới nhất là bằng chứng cuối cùng.

### 3. Trạng thái đã hoàn thành và đã xác minh

Mốc hiện tại trên `main`:

- Merge commit: `fa01593` — `merge: complete identity and rbac milestone`
- Feature commit: `b175e8b` — `feat: add identity and permission management`
- Foundation commit: `5c1de34`
- Lần xác minh gần nhất: build sạch, **0 warning, 0 error**; bộ test SQL opt-in **100 passed, 0 failed, 0 skipped**.

Hệ thống hiện đã có:

- ASP.NET Core MVC trên .NET 10.
- EF Core 10 với SQL Server và Code First migrations.
- Database ứng dụng `VeterinaryHospitalManagementDb`.
- Identity schema, `ApplicationUser`, `Permission`, `RolePermission`, `AuditLog`.
- 4 role: `Receptionist`, `Veterinarian`, `Manager`, `Admin`.
- 33 permission codes và dynamic permission policies.
- Đăng nhập, đăng xuất, đổi mật khẩu; không có public registration.
- BackOffice quản lý user: tạo/sửa, đổi role, khóa/mở, reset mật khẩu.
- Mỗi user đúng một role.
- User bị khóa sẽ bị từ chối ở request kế tiếp.
- Security stamp bị thu hồi khi reset mật khẩu hoặc đổi role.
- Bảo vệ Admin cuối cùng khi thao tác đồng thời bằng SQL application lock phạm vi hẹp.
- Seed role/permission idempotent và có khóa khi nhiều instance seed đồng thời.
- Cookie có `Secure`, `HttpOnly`, `SameSite`.
- Permission không tồn tại trả deny thay vì lỗi 500.
- Authorization endpoint đã được test: anonymous bị chuyển tới login; Receptionist/Manager nhận 403 tại trang quản lý user; Admin được phép.
- Test coordinator ngăn các tiến trình integration test phá database của nhau.
- Local `dotnet-ef` tool manifest.
- Migration đầu tiên `InitialIdentityAndPermissions` và model snapshot.

Database đã từng được kiểm tra có:

- `Users = 1`
- `Roles = 4`
- `Permissions = 33`

Admin bootstrap đã bị tắt sau khi tạo tài khoản và mật khẩu bootstrap đã được xóa khỏi user-secrets. Không hiển thị, tìm lại hoặc commit secret.

Ứng dụng đã chạy tại `http://localhost:5133`. Các route từng được xác minh:

- `/` trả 200.
- `/Account/Login` trả 200.
- `/BackOffice/Users` chuyển tới login khi chưa đăng nhập.

Trang chủ vẫn còn dòng chữ “Foundation” và layout chưa có liên kết login/admin. Đây là UI cũ, không phải migration lỗi.

### 4. Phần chưa làm

Chưa coi các phần sau là đã triển khai:

- Owner, Pet, Species, Breed.
- Veterinarian profile và shifts.
- Appointment và Availability.
- Visit, check-in, walk-in và tiếp nhận.
- MedicalRecord, Prescription, Medicine và clinical workflow.
- Service catalog và VisitService.
- Checkout, Invoice và in hóa đơn.
- Dashboard, Calendar.
- Reports và Excel.
- Cập nhật trang chủ/menu cho các module mới.
- ERD nghiệp vụ ngoài các bảng Identity/RBAC hiện tại.

### 5. Quy tắc kỹ thuật không được vi phạm

- Giữ kiến trúc MVC + service + một `ApplicationDbContext`; không tự thêm generic repository, microservice, CQRS, SPA hoặc Web API.
- EF Core migrations là nguồn schema duy nhất; không sửa schema thủ công trong SSMS.
- Không bao giờ chạy integration test hoặc reset trên `VeterinaryHospitalManagementDb`; chỉ dùng database test có hậu tố `_Test`.
- Không commit secret, password hoặc connection string chứa credential.
- Dùng transaction, concurrency, audit, permission và snapshot đúng theo project plan/database design.
- Controller nhận ViewModel và gọi service; không đặt nghiệp vụ trực tiếp trong controller/view.
- Viết test có ý nghĩa cho rule nghiệp vụ trước hoặc cùng lúc với implementation; lỗi phát hiện phải có test tái hiện khi phù hợp.
- Chỉ tạo migration sau khi model/configuration và schema expectations của lát đó đã được review.
- Mỗi task kết thúc bằng build/test phù hợp và báo cáo bằng chứng thực tế; không tuyên bố hoàn tất chỉ dựa trên đọc code.

### 6. Cách chia việc để không vượt giới hạn 5 giờ

Mỗi task Codex mới chỉ làm **một lát dọc nhỏ**, mục tiêu hoàn tất trong 45–90 phút và tối đa khoảng 2 giờ. Không nhận cả milestone hoặc cả module lớn trong một task.

Mỗi lát phải có:

- Một mục tiêu nghiệp vụ duy nhất.
- Danh sách file/phạm vi sở hữu rõ ràng.
- Tối đa một migration, và chỉ khi lát đó thật sự cần schema.
- Acceptance criteria ngắn, kiểm chứng được.
- Test/build cần chạy.
- Một điểm dừng sạch để task sau tiếp tục.

Nhịp đề xuất:

1. **Audit + quyết định:** kiểm tra trạng thái và đóng băng đúng một quyết định nghiệp vụ.
2. **Domain/schema:** entity, configuration, test schema; migration nếu đủ điều kiện.
3. **Service:** rule nghiệp vụ, permission, concurrency và audit.
4. **MVC UI:** controller, ViewModel, view và navigation.
5. **Review/fix:** regression, security, migration script và tài liệu trạng thái.

Mặc định chỉ dùng **1 tác nhân triển khai + 1 tác nhân review** để tiết kiệm giới hạn. Chỉ dùng mô hình 6 tác nhân khi người dùng yêu cầu rõ hoặc ở cổng nghiệm thu milestone. Nếu dùng 6 tác nhân, chạy theo đợt vì giới hạn đồng thời:

- Đợt 1: ba tác nhân chính phân tích ba góc độc lập.
- Đợt 2: hai reviewer bắt lỗi kết quả đợt 1.
- Đợt 3: tác nhân chính tổng hợp, sửa và xác minh.

Báo cáo lỗi phải nêu: mã lỗi, tác nhân gây ra/phần chịu trách nhiệm, reviewer phát hiện, bằng chứng, cách sửa và kết quả retest. Sáu tác nhân vẫn chỉ xử lý **một lát nhỏ**, không mở rộng thành cả milestone.

### 7. Lát tiếp theo được khuyến nghị

Không bắt đầu đồng thời Owner + Pet + Species + Breed. Trước hết xử lý **G1 / DEC-01** trong delivery plan:

- Tập số điện thoại Owner hợp lệ và không hợp lệ.
- Dạng canonical lưu trong database.
- Có cho nhiều Owner dùng chung một số hay không.

Quyết định này hiện **chưa chốt** và không được tự giả định `+84`, 10 chữ số hoặc unique. Hãy trình bày lựa chọn ngắn gọn cho người dùng duyệt. Sau khi duyệt, task code đầu tiên chỉ nên là:

> **M2.1A — Owner domain và phone normalization:** tạo contract test cho quyết định G1, entity/configuration Owner, service chuẩn hóa/tìm kiếm lõi và các test tương ứng. Chưa làm Pet/Species/Breed và chưa làm toàn bộ UI Owner trong cùng task.

### 8. Báo cáo bắt buộc cuối mỗi task

Kết thúc task bằng một báo cáo ngắn gồm:

1. Trạng thái trước khi làm và phần dở dang đã xử lý.
2. Chức năng vừa hoàn thành.
3. File đã thay đổi.
4. Migration/database impact.
5. Lệnh test/build đã chạy và số pass/fail/skipped.
6. Lỗi được reviewer phát hiện, ai tạo ra và ai phát hiện.
7. Rủi ro hoặc quyết định còn mở.
8. Đề xuất đúng **một lát nhỏ** cho task kế tiếp.

Không tự push, merge hoặc sửa dữ liệu ứng dụng ngoài phạm vi được người dùng giao. Giữ workspace ở trạng thái có thể tiếp tục và không che giấu thay đổi cục bộ.

## HẾT PROMPT
