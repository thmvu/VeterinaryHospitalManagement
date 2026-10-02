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

Mốc cập nhật: M11 — nghiệm thu hệ thống ngoại trú, ngày 02–03/10/2026. Đọc [M11_Acceptance.md](M11_Acceptance.md) và README để lấy bằng chứng mới nhất; kiểm tra Git trước khi kết luận trạng thái hiện tại.

Hệ thống đã triển khai:

- ASP.NET Core MVC .NET 10, EF Core SQL Server; kiến trúc controller → service → một ApplicationDbContext.
- Identity/RBAC với Admin, Manager, Receptionist, Veterinarian; login/logout, đổi mật khẩu, quản lý tài khoản, ma trận quyền và audit. Bảo vệ Admin cuối cùng, thu hồi phiên khi khóa/đổi role/reset mật khẩu.
- Chủ nuôi, loài/giống và thú cưng; mã chủ/thú/bác sĩ tự sinh. Tạo tài khoản Veterinarian đồng bộ hồ sơ bác sĩ.
- Danh mục bác sĩ, ca làm, dịch vụ và thuốc.
- Lịch hẹn, availability, lịch ngày/tuần, hủy và vắng; chống trùng bác sĩ/thú cưng trên SQL Server.
- Tiếp nhận từ lịch hẹn, walk-in, hàng đợi, phân công, bắt đầu/hủy lượt khám.
- Bệnh án, đơn thuốc, dịch vụ Pending/Performed/Cancelled, hoàn tất khám với kiểm tra nghiệp vụ và snapshot.
- Thanh toán tiền mặt/chuyển khoản, một hóa đơn mỗi lượt khám, trang in A4.
- Dashboard, giao diện Botanical, navigation theo quyền.
- Báo cáo doanh thu/lượt khám/dịch vụ, xuất .xlsx theo cùng khoảng ngày Việt Nam.
- Seed demo opt-in, hướng dẫn chạy/migration/demo trong README; ERD hiện tại ở DatabaseSchema_Current.md.

Database ứng dụng là `VeterinaryHospitalManagementDb`; database kiểm thử riêng là `localhost/VeterinaryHospitalManagement_Test`. Không coi dữ liệu test là dữ liệu ứng dụng. Không đọc hoặc in user-secrets để kiểm tra trạng thái.

### 4. Phạm vi hoàn tất và giới hạn

Plan v1.0 gồm M1–M11; không tự dựng M12 hoặc đổi sang Music Box. Kết quả nghiệm thu và những phần chưa kiểm chứng phải được ghi rõ trong M11_Acceptance.md. Không tự tuyên bố đã triển khai production, gửi hóa đơn điện tử thuế hoặc in bằng máy in vật lý.

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

### 7. Quyết định nghiệp vụ đã chốt và cách tiếp tục

**G1 / DEC-01 đã chốt** như sau; đây là quy tắc đang áp dụng, không phải module chưa triển khai:

- Chỉ nhận số di động Việt Nam. Sau khi trim khoảng trắng ngoài, chấp nhận `0` + 9 chữ số hoặc `84`/`+84` + 9 chữ số; chữ số đầu của phần 9 chữ số phải thuộc `3/5/7/8/9`.
- Chỉ nhận chữ số ASCII. Separator chỉ được là space, dấu gạch ngang (`-`) hoặc dấu chấm (`.`) và chỉ nằm giữa các nhóm chữ số; không hỗ trợ dấu ngoặc, extension, separator ở đầu/cuối hoặc separator liên tiếp.
- Canonical lưu database là `+84` + 9 chữ số, không separator.
- `Owner.PhoneNumber` không unique; nhiều Owner được phép dùng chung số. Exact search chuẩn hóa input bằng cùng quy tắc và trả danh sách tất cả Owner khớp để người dùng chọn, không tự lấy một bản ghi duy nhất.

Các quy tắc schema đã chốt thêm:

- `OwnerCode`/`PetCode` do hệ thống tự sinh đúng dạng `OWN-000001`/`PET-000001` bằng hai SQL sequence độc lập, bắt đầu 1, tăng 1, tối đa 999999, không cycle. Cho phép khoảng trống khi transaction rollback và không tái sử dụng số.
- `Species.Code` do quản trị viên nhập ở workflow danh mục sau; trim, uppercase invariant, tối đa 30 ký tự và unique, không áp regex. `Species.Name` không unique.
- Owner inactive không được dùng để tạo Pet mới; khóa Owner không cascade `Pet.IsActive`. Species/Breed giữ nguyên field trong DatabaseDesign, không thêm `CreatedAt` hoặc `RowVersion`.

Sau nghiệm thu M11, chỉ nhận một lỗi tái hiện được hoặc một yêu cầu cải tiến đã được người dùng chốt. Giữ nguyên các quy tắc trên. Nếu người dùng chỉ yêu cầu tiếp tục mà không có phần dở dang, kiểm tra tài liệu nghiệm thu và báo trạng thái trước; không tự tạo lại Owner/Pet hoặc sinh migration mới.

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
