# M11 — Nghiệm thu hệ thống ngoại trú

Ngày kiểm tra: 02–03/10/2026. Phạm vi: plan v1.0 M1–M11, chạy local với SQL Server. Đây là kết quả nghiệm thu phát triển; chưa phải nghiệm thu triển khai production.

## Kết quả kiểm thử

| Lượt kiểm tra | Kết quả | Phạm vi bằng chứng |
| --- | --- | --- |
| Toàn solution, bật SQL opt-in | 337 passed, 0 failed, 0 skipped | Assembly trước phần polish cuối; schema/migration, Identity, tài khoản/quyền, seed, nghiệp vụ, E2E và concurrency |
| Hồi quy sau sửa lịch sử thú cưng và mở rộng E2E | 29 passed, 0 failed, 0 skipped | Toàn nhóm OwnersPets và hai E2E SQL, gồm HTTP/Excel và quyền xem lịch sử |
| Happy path cuối để nghiệm thu UI | 1 passed, 0 failed, 0 skipped | SQL thật; dữ liệu chủ/thú, ca/lịch, khám, đơn/dịch vụ, hóa đơn và báo cáo |
| Toàn solution mặc định sau sửa | 218 passed, 0 failed, 119 skipped | Test SQL chỉ chạy khi bật opt-in; skip không được coi là pass SQL |
| Build cuối | 0 warning, 0 error | `dotnet build VeterinaryHospitalManagement.slnx --no-restore` |

Full SQL suite chạy một lần, khoảng 8 phút 19 giây. Chín test có tên concurrency đều pass: checkout, trùng lịch hẹn, danh mục trùng, hồ sơ bác sĩ trùng, khóa Admin cuối cùng, đổi role, seed đồng thời và bắt đầu khám cùng bác sĩ. Bằng chứng là SQL Server thật, không dùng EF InMemory để kết luận concurrency hoặc rollback.

E2E happy path kiểm tra: tạo tài khoản/hồ sơ → đặt lịch → check-in → bệnh án và đơn thuốc → thực hiện/hủy dịch vụ → hoàn tất → thanh toán → báo cáo. Check-in/checkout lặp không tạo bản ghi hoặc audit trùng. Tên/giá dịch vụ giữ snapshot dù danh mục thay đổi; dịch vụ hủy không được tính tiền. Lượt khám ngày 02/10 và thanh toán ngày 03/10 xuất hiện ở đúng ngày của từng báo cáo theo giờ Việt Nam.

E2E thứ hai kiểm tra walk-in, hủy lượt khám, hủy lịch hẹn, tạo lại trong slot đã giải phóng và đánh dấu vắng sau giờ kết thúc. Endpoint export từ chối bộ lọc đảo ngày/sai định dạng bằng HTTP 400; bác sĩ thiếu quyền báo cáo/export/in hóa đơn nhận HTTP 403.

## Kiểm tra qua trình duyệt

Chạy instance HTTPS riêng trên database test, seed/bootstrap startup tắt. Đã kiểm tra:

- Đăng nhập/đăng xuất Admin; sidebar mở được dashboard, chủ nuôi, lịch hẹn/calendar, ca trực, bác sĩ, dịch vụ/thuốc, tài khoản, ma trận quyền, audit, hàng đợi và hóa đơn.
- Tìm chủ theo số điện thoại/mã, mở thú cưng và lịch sử khám, mở chi tiết lượt khám/bệnh án/đơn thuốc đã chốt.
- Hóa đơn `INV-20261003-000001` hiển thị một dịch vụ đã thực hiện và tổng **120.000 ₫**, phương thức chuyển khoản; bản in có thông tin chủ/thú/lượt khám, người thu và CSS A4.
- Ba báo cáo có đúng bộ lọc và số liệu; tải file từ nút **Tải Excel** trong giao diện.

Các file tải xuống được mở bằng `openpyxl` ở chế độ chỉ đọc, độc lập với exporter của ứng dụng: doanh thu F6 = 120000, lượt khám B9 = 1, dịch vụ D6 = 120000. Ô tổng có kiểu số; workbook không chứa công thức ngoài dự kiến. Hồi quy HTTP còn đối chiếu ngày, số hóa đơn, tên dịch vụ snapshot, dòng dữ liệu và dòng tổng.

Đây là smoke test trình duyệt trên dữ liệu E2E đã tạo; không tuyên bố đã nhập toàn bộ quy trình qua form bằng tay. Toàn luồng nghiệp vụ được kiểm tra tự động qua service SQL và các endpoint liên quan.

## Sửa lỗi cuối và review

| Vấn đề | Người phát hiện | Xử lý và kiểm chứng |
| --- | --- | --- |
| Pet Details luôn báo chưa có lượt khám dù đã có lượt Completed | Tác nhân chính khi nghiệm thu trình duyệt | Truy vấn tối đa 50 lượt gần nhất theo PetId, sắp xếp ổn định, dùng snapshot bác sĩ. Chỉ nạp khi có Visit.View; không đưa bệnh án/đơn thuốc vào lịch sử. E2E xác minh hiển thị và ẩn khi gỡ Visit.View |
| Mã lượt khám bị xuống dòng làm vùng bấm khó sử dụng | Tác nhân chính kiểm tra DOM/giao diện | Mã hiển thị trong liên kết một dòng, vùng bấm liền nhau; kiểm tra lại bằng trình duyệt |
| Thông báo Owner/Landing và prompt bàn giao còn nội dung cũ | Tác nhân chính khi đối chiếu UI/tài liệu | Bỏ thông báo chưa triển khai; gắn nhãn dữ liệu minh họa, ghi đúng xuất .xlsx, cập nhật trạng thái M1–M11 |
| Email/address fixture E2E truyền sai ý nghĩa tham số | Tác nhân chính khi xem hồ sơ | Sửa email mẫu hợp lệ và địa chỉ đúng vị trí; E2E SQL chạy lại đạt |

Reviewer `m11_sql_suite` chạy full SQL suite và đọc diff độc lập, không thấy lỗi chặn trong phần HTTP/Excel, kiểm tra quyền/lịch sử và copy cuối. Không có bằng chứng để quy lỗi cũ cho một tác nhân cụ thể; lỗi được ghi theo phần code chịu trách nhiệm.

## Database và tài liệu bàn giao

Lượt này **không thêm migration hoặc thay schema**. Chỉ fixture đã được cho phép xóa/tạo lại `localhost/VeterinaryHospitalManagement_Test`; không sửa dữ liệu `VeterinaryHospitalManagementDb`. Repository có 11 migration, cuối là `20260926181724_AddInvoices`. Nếu database ứng dụng đã áp đầy đủ, không cần migration mới cho các sửa lỗi này.

- [README](../README.md): chạy trên máy, bootstrap Admin, migration, seed và demo toàn luồng.
- [ERD hiện tại](DatabaseSchema_Current.md): model 26 bảng và quan hệ hiện hành.
- [Prompt tiếp tục](Codex_Continuation_Prompt.md): trạng thái hiện tại, quy tắc và giới hạn task nhỏ.

Log/TRX/screenshot được giữ local trong `docs/agent-work/m11-verification`, đã bị Git ignore theo yêu cầu. Không push các file làm việc của tác nhân.

## Giới hạn

Chưa kiểm tra Microsoft Excel desktop, máy in vật lý, triển khai production hoặc tải lớn thực tế. Đã kiểm tra file OpenXML và trang in/CSS A4; không đồng nhất những kiểm tra này với in trên thiết bị thật. Bản in hóa đơn không phải hóa đơn điện tử thuế. Việc cài đặt trên một máy mới được hướng dẫn trong README nhưng chưa được tái hiện trên máy sạch riêng.

M11 hoàn tất trong phạm vi nghiệm thu local nêu trên. Task tiếp theo chỉ nên là một yêu cầu cải tiến hoặc lỗi tái hiện được do người dùng chốt; không tự mở thêm milestone ngoài plan v1.0.
