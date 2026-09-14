# Báo cáo tổng hợp 6 tác nhân — Foundation

Ngày xác minh: 15/09/2026.

## Phân vai

| Vai trò | Tác nhân | Kết quả |
| --- | --- | --- |
| Chính 1 — C# Foundation | `main_foundation` | Solution MVC + xUnit + EF Core SQL Server, UI nền, TestHost, clock VN, guard DB test |
| Chính 2 — Database/EF | `main_database` | Rà soát schema, constraints, index, transaction và decision gates |
| Chính 3 — Delivery/QA | `main_delivery_plan` | Dependency graph M0–M10, 20 task, acceptance/test/migration |
| Bắt lỗi 1 — Spec/Code | `reviewer_spec_code` | So sai lệch với đặc tả và kiểm tra code/test |
| Bắt lỗi 2 — Security/DB | `reviewer_security_db` | Review đối kháng về DB, test isolation, security và concurrency |
| Tổng hợp | `/root` | Phân xử bất đồng, giao fix, tự chạy build/test và lập báo cáo này |

Do nền tảng giới hạn bốn tác nhân hoạt động cùng lúc kể cả `/root`, sáu vai trò chạy theo hai đợt: ba tác nhân chính song song, sau đó hai reviewer song song, cuối cùng `/root` tổng hợp.

## Ai sai và ai bắt được lỗi

| Tác nhân có lỗi/thiếu | Lỗi ban đầu | Tác nhân bắt lỗi | Kết quả |
| --- | --- | --- | --- |
| `main_foundation` | Test EF chỉ kiểm tra ProviderName, không chứng minh HTTP/SQL; thiếu clock, AccessDenied và DB guard | Cả hai reviewer | Đã thêm TestHost, clock VN, 403 và guard; SQL integration chuyển thành opt-in thật |
| `main_foundation` | HTML tiếng Việt vẫn khai báo `lang="en"` | `reviewer_spec_code` | Đã sửa `lang="vi"` và có HTTP test |
| `main_foundation` | Vòng sửa 1 dùng probe `Encrypt=False`, khác contract app `Encrypt=True` | `reviewer_security_db`; `reviewer_spec_code` xác nhận | Đã bỏ probe yếu khỏi suite; opt-in dùng đúng contract app |
| `main_foundation` | Vòng sửa 1 `return` khi DB test thiếu nhưng xUnit tính Passed | Cả hai reviewer | Đã dùng skip discovery thật; báo cáo hiện 1 skipped |
| `main_foundation` | Guard chỉ kiểm tra hậu tố `_Test`, có thể chấp nhận DB khác | `reviewer_security_db` | Đã khóa đúng localhost + `VeterinaryHospitalManagement_Test` + tùy chọn bảo mật; destructive fixture cần opt-in riêng |
| `main_foundation` | TestHost dùng Data Protection profile thật, gây DPAPI/permission noise | Cả hai reviewer | Đã dùng provider và repository trong bộ nhớ |
| `main_delivery_plan` | Tự chốt chuẩn hóa SĐT `+84 → 0`, 10 số, unique khi người dùng chưa duyệt | `reviewer_spec_code` | Đã chuyển thành DEC-01 gate |
| `main_delivery_plan` | Tự thêm constraint FollowUpDate | `reviewer_spec_code` | Đã chuyển thành decision gate trước Clinical |
| `main_delivery_plan` | Chưa mang CHECK trạng thái hai chiều, LineTotal, index checkout và cross-Visit contract vào task | `reviewer_security_db` | Đã bổ sung task migration, negative tests và acceptance cụ thể |
| `main_delivery_plan` | Thuật toán đổi role, contract bootstrap secrets và các quyết định DEC-02/03 chưa đủ cụ thể | `reviewer_security_db` | Đã cụ thể hóa transaction/rollback/test, tên key và decision gates |
| `main_database` | DB-04 ban đầu nói thiếu chiến lược dù spec đã có transaction/server recalc | `reviewer_spec_code` | Đã sửa: nền chiến lược có sẵn, thiếu contract/test chéo Visit |
| `main_database` | DB-05 bị mô tả gần như lỗi nhất quán; thực tế là hiệu năng/phạm vi khóa | `reviewer_spec_code` | Đã phân loại `PERF-01` |
| `main_database` | DB-06 kết luận rộng khi chưa có migration/config để chứng minh | `reviewer_spec_code` | Đã phân loại `RISK-01`, chờ kiểm tra migration |
| `main_database` | Gợi ý “thêm role mới trước” xung đột unique UserId | `reviewer_security_db` | Đã sửa thuật toán: transaction xóa cũ → thêm mới → audit → commit; lỗi rollback |

## Những phát hiện được xác nhận đúng

Hai reviewer xác nhận `main_database` bắt đúng DB-01 (CHECK trạng thái một chiều), DB-02 (LineTotal chưa ép công thức) và DB-03 (unique UserId chỉ bảo đảm tối đa một role). DB-04 đúng ở phần thiếu contract/test cụ thể, không đúng nếu nói hệ thống chưa có chiến lược transaction. PERF-01 cần kiểm tra query plan khi có code thật. RISK-01 phải kiểm tra migration trước khi kết luận lỗi.

## Xác minh của tác nhân tổng hợp

Chạy bằng đường dẫn solution tuyệt đối vì ký tự `#` trong workspace làm lớp chạy lệnh bỏ qua working directory:

```text
dotnet build <absolute-path> --no-restore
Build succeeded. 0 Warning(s), 0 Error(s).

dotnet test <absolute-path> --no-build
Failed: 0, Passed: 12, Skipped: 1, Total: 13.
```

Test skipped là SQL integration opt-in. Khi tác nhân Foundation bật opt-in với đúng connection contract, `CanConnectAsync=False`; do đó chưa được tuyên bố database ứng dụng/test kết nối thành công. Không database hoặc migration nào được tạo trong giai đoạn này.

## Trạng thái phạm vi

Foundation đã có mã nguồn và kiểm thử nền. Identity/RBAC, entity nghiệp vụ, migrations, Owner/Pet, Appointment, Visit, Clinical, Invoice, Dashboard và Reports vẫn nằm trong kế hoạch, chưa được triển khai. Trước khi bắt đầu các module phụ thuộc cần chốt DEC-01, DEC-02 và DEC-03 trong `main-delivery-plan.md`.
