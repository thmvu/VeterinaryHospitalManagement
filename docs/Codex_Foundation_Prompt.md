# Prompt bắt đầu Foundation

Chỉ dùng sau khi đã rà soát/đồng ý bản thiết kế và các giả định trong ProjectPlan v1.0. File này là prompt cho một lượt triển khai sau, không phải chỉ dẫn phải thực thi trong lượt lập plan.

```text
Tạo project mới VeterinaryHospitalManagement theo các tài liệu:
- docs/VeterinaryHospitalManagement_ProjectPlan_v1.0.md
- docs/DatabaseDesign_v1.0.md

Đây là hệ thống bệnh viện thú y NGOẠI TRÚ. Không tạo Music Box, không tái sử dụng entity/luật đặt phòng. Giữ cấu trúc và quy tắc đã duyệt trong plan.

Trong lượt này chỉ làm MỐC 1 — FOUNDATION:
1. Kiểm tra SDK, hướng dẫn repository và thư mục trước khi tạo file; giữ nguyên tài liệu hiện có.
2. Tạo solution VeterinaryHospitalManagement.
3. Tạo src/VeterinaryHospitalManagement.Web bằng ASP.NET Core MVC .NET 10 và tests/VeterinaryHospitalManagement.Tests dùng xUnit.
4. Cấu hình EF Core SQL Server và project references/package tương thích; ghi rõ phiên bản thực tế.
5. Chuẩn bị cấu trúc Controller → Service → DbContext theo plan. Không thêm repository chung, API/SPA hoặc microservice.
6. Cấu hình kết nối Windows Authentication tới SQL Server localhost, database dự kiến VeterinaryHospitalManagementDb. Kiểm tra database đích trước khi tạo/sửa; không xóa database có sẵn. Mật khẩu nếu phát sinh lưu user-secrets, không commit.
7. Làm trang khởi đầu tiếng Việt với tên Veterinary Hospital Management, layout và xử lý lỗi cơ bản.
8. Viết README: mở bằng Visual Studio, cấu hình local, chạy, test; nêu rõ Identity và migration đầu ở mốc 2.
9. Build và smoke test phù hợp; báo chính xác phần chạy được, chưa chạy và lỗi còn lại.

Không triển khai Owner/Pet, lịch hẹn, khám hoặc thanh toán trong lượt này. Không sinh tất cả entity/migration một lần. Không tự thay đổi nghiệp vụ chưa rõ: chỉ ra điểm chưa rõ trong báo cáo.

Tiêu chí hoàn tất: solution build được, ứng dụng khởi động được, có cấu hình SQL Server phù hợp và hướng dẫn chạy. Không báo đã có đăng nhập/CRUD khi chưa triển khai.
```

## Mẫu giao từng module tiếp theo

```text
Module/mốc:
Actor và phạm vi dữ liệu được truy cập:
Permission và quyền phụ thuộc:
Workflow/trạng thái trước → sau:
Entity, FK, snapshot, migration liên quan:
Validation và tình huống biên:
Transaction, chống request lặp và xung đột:
Màn hình/ViewModel cần làm:
Test và tiêu chí demo:
Phần không thuộc lượt này:

Đọc plan đã duyệt; kiểm tra code hiện tại; chỉ thực hiện module này.
Giữ kiến trúc. Không sửa snapshot/lịch sử hoặc thay workflow đã chốt.
Kết thúc bằng các file đã đổi, kết quả kiểm tra và phần còn thiếu.
```
