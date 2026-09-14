# Tác nhân chính 2/3 — Rà soát Database / EF Core

Ngày rà soát: 14/09/2026  
Phạm vi: `VeterinaryHospitalManagement_ProjectPlan_v1.0.md` và `DatabaseDesign_v1.0.md`. `MusicBoxManagement_ProjectPlan_v1.2.md` chỉ được dùng để đối chiếu phương pháp khóa invariant, transaction, migration và lịch sử dữ liệu. Không rà code, không sửa tài liệu gốc và không tạo database.

## Kết luận nhanh

Thiết kế hiện tại có chuỗi phụ thuộc migration hợp lý, dùng đúng ASP.NET Core Identity thay vì tạo hệ role riêng, và đã chọn đúng các filtered unique index quan trọng: một Visit trên Appointment, một Visit đang mở trên Pet, một Visit InProgress trên bác sĩ, một Invoice trên Visit và một InvoiceItem trên VisitService.

Trước khi sinh entity/configuration/migration, cần sửa hoặc chốt các điểm dưới đây. Các mục **DB-01 đến DB-04** là lỗi hoặc contract nhất quán còn thiếu có thể làm database chấp nhận trạng thái trái với plan. Các mục **DEC-01 đến DEC-03** là quyết định cần người dùng xác nhận. **PERF-01** và **RISK-01** là đề xuất về hiệu năng/locking và rủi ro cấu hình, không phải lỗi consistency đã được chứng minh.

## A. Lỗi thực sự / lỗ hổng nhất quán

### DB-01 — Bộ CHECK trạng thái mới mô tả điều kiện một chiều

Tài liệu yêu cầu, ví dụ, `Finalized` thì có `FinalizedAt`, `Performed` thì có thời điểm/người thực hiện, nhưng chưa yêu cầu chiều ngược lại: bản ghi `Draft` phải có `FinalizedAt IS NULL`; `Pending` phải có `PerformedAt IS NULL`, `PerformedByVeterinarianId IS NULL` và `CancellationReason IS NULL`; `Cancelled` phải không có dữ liệu thực hiện. Vì vậy database vẫn có thể nhận các trạng thái lai như Prescription Draft đã có `FinalizedAt`, hoặc VisitService Pending đã có `PerformedAt`.

Nên viết CHECK theo toàn bộ tuple trạng thái thay vì các mệnh đề kéo theo rời rạc. Ví dụ logic cho VisitService:

- Pending: `PerformedAt`, `PerformedByVeterinarianId`, `CancellationReason` đều null.
- Performed: `PerformedAt` và `PerformedByVeterinarianId` khác null, `CancellationReason` null.
- Cancelled: `CancellationReason` khác null, dữ liệu thực hiện null.

Tương tự, Visit cần khóa đủ tuple timestamp/reason cho Waiting, InProgress, Completed, Cancelled; MedicalRecord và Prescription cần khóa cả hai chiều Draft/Finalized.

### DB-02 — CHECK của InvoiceItem chưa ép công thức LineTotal

Schema chỉ nêu `LineTotal >= 0` và tiền nguyên VND. Nó không ngăn `Quantity = 2`, `UnitPrice = 100000`, `LineTotal = 1`. Tổng Invoice so với tổng dòng là invariant nhiều bảng nên đúng là phải kiểm tra trong transaction, nhưng công thức **trong cùng một dòng** có thể và nên được DB bảo vệ.

Thêm CHECK tương đương `LineTotal = ROUND(Quantity * UnitPrice, 0)` theo đúng quy tắc chỉ dùng số không âm và `MidpointRounding.AwayFromZero`; migration cần xác minh biểu thức SQL Server khớp các test biên. `TotalAmount = SUM(InvoiceItems.LineTotal)` vẫn là invariant nhiều bảng do service/transaction bảo vệ.

### DB-03 — “Mỗi user có đúng một role” chưa được đảm bảo đầy đủ

Unique index trên `AspNetUserRoles.UserId` chỉ đảm bảo **tối đa một** role, không đảm bảo user luôn có **ít nhất một** role. Câu “nếu duyệt mô hình này” cũng khiến một invariant đã được ProjectPlan chốt thành tùy chọn kỹ thuật.

Nếu baseline giữ đúng một role/user, cần chốt unique index này là bắt buộc và mọi luồng tạo user/đổi role phải chạy trong transaction. Khi đổi role, service mở transaction, khóa/đọc lại user-role và điều kiện Admin cuối cùng, **xóa role cũ rồi thêm role mới trong cùng transaction**, sau đó commit; nếu bước thêm thất bại thì rollback khôi phục role cũ, nên không commit trạng thái zero-role. Không thể thêm role mới trước vì unique index `AspNetUserRoles.UserId` sẽ từ chối hai role cùng lúc. Luồng tạo user cũng phải tạo user và gán role trong một transaction hoặc dọn/rollback an toàn nếu gán role thất bại. SQL Server không thể dùng CHECK để ép mỗi AspNetUsers phải có một hàng con; cần service + integration test, gồm cạnh tranh hạ quyền/khóa Admin cuối cùng.

### DB-04 — Contract kiểm tra InvoiceItem cùng Visit và test chéo Visit chưa đủ cụ thể

Tài liệu **đã có** nền chống race phù hợp: Checkout tính lại trên server, dùng transaction Serializable, và các endpoint thay đổi nghiệp vụ cạnh tranh cũng phải dùng transaction. Điểm còn thiếu không phải toàn bộ chiến lược, mà là contract và test cụ thể cho quan hệ chéo Visit. Checkout phải bảo đảm mỗi `InvoiceItem.VisitServiceId` thuộc chính `Invoice.VisitId`, có Status Performed, và không thể đổi/cancel đồng thời trong lúc lập hóa đơn.

Unique `InvoiceItem.VisitServiceId` chỉ chống tính tiền một VisitService hai lần, không chống gắn dịch vụ của Visit A vào Invoice của Visit B. Cần bổ sung contract: bên trong transaction đã quy định, đọc lại Visit và tập VisitServices bằng `VisitId` từ server; tạo Invoice/items chỉ từ tập đó; không nhận VisitServiceId/giá/tổng từ form; mọi endpoint chuyển trạng thái VisitService tuân thủ cùng transaction strategy. Thêm integration test cố tình trộn VisitService của Visit A vào checkout Visit B và test checkout chạy đồng thời với chuyển trạng thái dịch vụ. Đây là invariant không thể ép bằng CHECK đơn bảng.

## A.1. Đề xuất hiệu năng/locking và rủi ro cấu hình

### PERF-01 — Nên thêm index phục vụ checkout và phạm vi khóa

Danh sách index không có `VisitServices(VisitId, Status)` dù checkout phải đọc toàn bộ Performed services của một Visit và khóa ổn định tập đó. Thiếu index này vừa làm query scan, vừa khiến phạm vi khóa Serializable rộng và khó dự đoán hơn.

Thêm index tối thiểu `VisitServices(VisitId, Status)` INCLUDE các cột cần tính tiền/snapshot như `ServiceNameSnapshot, Quantity, UnitPrice`; cấu hình INCLUDE tùy truy vấn cuối. Unique `InvoiceItem.VisitServiceId` giữ nguyên.

Đây là vấn đề hiệu năng và độ chính xác của phạm vi locking, **không phải lỗi consistency trực tiếp**: transaction/server-side recomputation vẫn là contract nhất quán chính. Mức ảnh hưởng thực tế cần xác minh bằng migration, execution plan và integration test SQL Server.

### RISK-01 — DeleteBehavior cần được cấu hình và kiểm tra rõ

ASP.NET Core Identity mặc định có cascade ở một số quan hệ bảng phụ; câu “mọi FK mặc định Restrict/NoAction” cần được diễn giải tường minh khi viết configuration. Hiện chưa có implementation/migration nên chưa thể kết luận convention đã sinh sai delete behavior. Rủi ro là áp `Restrict/NoAction` máy móc cho Identity sẽ làm schema khác kỳ vọng framework, hoặc bỏ cấu hình FK nghiệp vụ sẽ để convention sinh cascade không phù hợp, gặp multiple cascade paths hay xóa mất lịch sử.

Cần ghi rõ: giữ schema/delete behavior chuẩn của Identity cho các bảng Identity phụ; mọi FK từ dữ liệu nghiệp vụ, audit và snapshot tới user/catalog/visit dùng `NoAction`/`Restrict`; các dòng con chỉ được Cascade khi thật sự là aggregate chưa chốt và nghiệp vụ cho phép hard delete. Với baseline “không hard delete dữ liệu có lịch sử”, lựa chọn an toàn là NoAction cho PrescriptionItems, InvoiceItems và các bảng lâm sàng, rồi không cung cấp luồng xóa sau khi đã phát sinh lịch sử.

## B. Invariant đúng là không thể ép bằng CHECK đơn bảng

Các mục sau đã được tài liệu nhận diện hoặc cần được làm rõ là trách nhiệm service + transaction + integration test:

1. Appointment nằm hoàn toàn trong một VeterinarianShift active và không overlap theo Veterinarian/Pet.
2. Các VeterinarianShift của cùng bác sĩ không overlap.
3. Visit từ Appointment phải cùng Pet và bác sĩ nguồn tại check-in; nếu tái phân công Waiting thì Appointment giữ bác sĩ gốc và Visit/snapshot/audit đổi nguyên tử.
4. Bác sĩ/profile/user đang active và người sửa/chốt lâm sàng là bác sĩ phụ trách Visit.
5. Completed Visit có MedicalRecord Finalized; Prescription nếu tồn tại phải Finalized; Prescription Finalized có ít nhất một PrescriptionItem.
6. Waiting/Cancelled Visit không có nội dung lâm sàng hoặc VisitService Performed trái workflow.
7. Invoice chỉ lập cho Visit Completed; item thuộc cùng Visit, chỉ lấy VisitService Performed; TotalAmount bằng tổng LineTotal.
8. Luôn còn ít nhất một Admin active và role của mỗi user là duy nhất/không rỗng.
9. Khóa bác sĩ, ca, danh mục hoặc user không làm mất hiệu lực các nghiệp vụ tương lai/đang mở mà plan cấm.
10. Bản ghi đã Finalized/Completed/đã xuất Invoice là immutable. `RowVersion` chỉ phát hiện ghi đè; bản thân nó không cấm một endpoint sai sửa dữ liệu.

Serializable chỉ có tác dụng nếu mọi đường ghi cạnh tranh đều tuân thủ cùng transaction strategy, đọc lại dữ liệu **sau khi mở transaction**, và truy vấn dùng index phù hợp. Filtered unique index vẫn là lớp bảo vệ cuối cho các invariant có thể biểu diễn bằng uniqueness.

## C. Rà filtered unique index và EF Core mapping

Các index sau phù hợp với workflow và nên giữ:

| Index | Đánh giá |
| --- | --- |
| `Visits(AppointmentId)` unique, filter non-null | Đúng: check-in lặp không sinh Visit thứ hai; service trả Visit đã có sau unique violation/race. |
| `Visits(PetId)` unique, filter Waiting/InProgress | Đúng: bảo vệ một Visit đang mở trên Pet. Chuỗi filter phải dùng đúng giá trị enum được lưu. |
| `Visits(VeterinarianId)` unique, filter InProgress | Đúng: bảo vệ một ca khám đang diễn ra trên bác sĩ. |
| `MedicalRecords(VisitId)`, `Prescriptions(VisitId)`, `Invoices(VisitId)` unique | Đúng với quan hệ 0..1. |
| `InvoiceItems(VisitServiceId)` unique | Đúng để một dòng dịch vụ không bị tính vào hai hóa đơn; vẫn cần invariant cùng Visit ở DB-04. |

Khi cấu hình EF Core SQL Server, filter nên dùng tên cột vật lý có ngoặc vuông và literal Unicode nếu cần, ví dụ `[Status] IN (N'Waiting', N'InProgress')`. Status phải có `HasMaxLength(20)` và conversion nhất quán trước khi tạo migration. Tên index nên đặt tường minh để migration/review ổn định.

Index lịch hiện tại hợp lý cho truy vấn overlap theo doctor/pet. Cần đảm bảo LINQ lọc `Status` và hai điều kiện khoảng ngay trong SQL; không tải về rồi lọc C#. Với ca bác sĩ, index hiện tại hỗ trợ truy vấn nhưng overlap vẫn là invariant transaction, không phải unique constraint.

## D. Quyết định cần người dùng xác nhận

### DEC-01 — Unique số điện thoại Owner

Plan đang giả định một SĐT đại diện đúng một Owner. Đây là quyết định nghiệp vụ, không chỉ là index. Nếu gia đình dùng chung số hoặc một người liên hệ đại diện nhiều hồ sơ, unique này sẽ chặn dữ liệu hợp lệ. Cần xác nhận trước module Owner. Nếu giữ, lưu duy nhất số đã chuẩn hóa và unique trên cột đó; không unique trên input thô.

### DEC-02 — Chính sách xóa các dòng Draft chưa tạo lịch sử

Plan nói không hard delete dữ liệu có lịch sử nhưng chưa chốt có cho xóa Prescription Draft/PrescriptionItem/VisitService Pending trước khi hoàn tất khám hay chỉ chuyển trạng thái/hủy. Quyết định này ảnh hưởng DeleteBehavior và audit. Khuyến nghị cho phép xóa/sửa item nháp trong aggregate khi Visit InProgress, nhưng VisitService đã tạo nên chuyển Cancelled để giữ vết nếu nghiệp vụ muốn audit đầy đủ.

### DEC-03 — Có lưu thời điểm và tác nhân Cancel/NoShow không

Appointment hiện chỉ có CancellationReason; không có `CancelledAt`, `CancelledByUserId`, `NoShowAt`, `NoShowByUserId`. AuditLog có thể truy vết, nhưng báo cáo/truy vấn trực tiếp sẽ phụ thuộc audit text và khó giữ quan hệ rõ ràng. Cần xác nhận mức lịch sử mong muốn. Khuyến nghị thêm các cột nullable có CHECK theo status, ít nhất `StatusChangedAt` và `StatusChangedByUserId`, hoặc các cột sự kiện riêng.

## E. Đề xuất kỹ thuật, không phải lỗi blocker

- Thêm `RowVersion` cho `ApplicationUser` nếu luồng quản trị cần optimistic concurrency ngoài `ConcurrencyStamp` của Identity; nếu dùng đúng `ConcurrencyStamp` thì tài liệu nên nêu rõ không tạo token thứ hai.
- Đặt default SQL cho `CreatedAt` chỉ khi toàn bộ ứng dụng thống nhất dùng UTC; nếu service truyền thời gian qua `TimeProvider` để test thì tránh trộn hai nguồn thời gian trong cùng workflow.
- Các code/number unique cần chốt collation/case sensitivity và cách sinh mã. SQL Server collation thường không phân biệt hoa thường, nên `vet01` và `VET01` có thể trùng; đây thường là hành vi mong muốn.
- `decimal(10,2)` cho Quantity cho phép phần lẻ. Phù hợp thuốc/dịch vụ đo theo đơn vị lẻ, nhưng UI phải validate theo loại đơn vị nếu có dịch vụ chỉ nhận số nguyên.
- Có thể thêm CHECK biên hợp lý cho `TemperatureC` và `BirthDate <= ngày hiện tại` ở service. Ngày hiện tại không nên dùng trong CHECK vì phụ thuộc thời gian và khó ổn định; kiểm tra tại service.
- AuditLog nên cân nhắc `ActorNameSnapshot` nếu cần giữ tên người thao tác tại thời điểm phát sinh; UserId FK chỉ cho biết định danh và FullName hiện tại có thể đổi.
- Mọi FK và index cần được kiểm tra trực tiếp trên migration script sinh ra. Không suy ra rằng Fluent API đúng chỉ vì model compile.

## F. Thứ tự migration

Thứ tự hiện tại không có lỗi phụ thuộc:

1. `InitialIdentityAndPermissions`: Identity trước RolePermissions/AuditLogs.
2. `AddOwnersAndPets`: Species/Breeds trước Pets trong cùng migration.
3. `AddVeterinariansAndCatalogs`: profile, shift và catalog trước Appointment/Clinical.
4. `AddAppointments`.
5. `AddVisits`: sau Appointment/Pet/Profile; tạo filtered unique indexes tại đây.
6. `AddClinicalRecords`: sau Visit, Medicine, ServiceCatalog, Profile.
7. `AddInvoices`: sau VisitServices.

Lưu ý seed phải chạy theo schema đã tồn tại. Nếu seed được đặt trong `HasData`, dữ liệu Species không thể nằm trong migration Identity đầu tiên; nó phải xuất hiện từ `AddOwnersAndPets`. User/role seed nên dùng UserManager/RoleManager ở startup hoặc initializer idempotent như plan yêu cầu, còn dữ liệu lookup ổn định có thể dùng migration/HasData nếu khóa chính cố định.

## G. Tiêu chí kiểm thử database tối thiểu

- Hai request đồng thời tạo Appointment overlap cùng doctor và cùng Pet: chỉ một thành công.
- Hai check-in cùng Appointment; hai Visit mở cùng Pet; hai Start cùng doctor: mỗi trường hợp chỉ một trạng thái hợp lệ tồn tại.
- Checkout đồng thời đổi trạng thái VisitService: invoice hoặc lấy đúng tập Performed đã commit trước điểm tuần tự hóa, hoặc thao tác dịch vụ thất bại; không bỏ sót/tính chéo âm thầm.
- Chốt Visit đồng thời sửa MedicalRecord/Prescription/VisitService: không có Completed Visit chứa Draft/Pending hay dữ liệu sửa sau chốt.
- Đổi role/khóa user/Admin cuối cùng đồng thời: không tạo zero-role user đã commit và không mất Admin active cuối cùng.
- Cố tình insert/update trực tiếp các tuple trạng thái sai để xác minh CHECK; cố tình tạo filtered unique conflicts để xác minh migration thực sự sinh filter đúng trên SQL Server.
- Xác minh delete behavior bằng migration script và thử xóa catalog/user/visit đã được tham chiếu; dữ liệu lịch sử phải còn nguyên.

## Đánh giá cuối

Schema đủ tốt để làm baseline sau khi xử lý DB-01 đến DB-04 và chốt DEC-01 đến DEC-03. Trước migration nghiệp vụ cần cụ thể hóa CHECK tuple trạng thái, chiến lược một-role-per-user và contract/test InvoiceItem cùng Visit. PERF-01 nên được kiểm tra bằng truy vấn thực tế; RISK-01 phải được xác minh trên migration script trước khi áp dụng. Các invariant liên bảng còn lại phải được ghi thành contract của service và integration test SQL Server, vì CHECK constraint không thể bảo vệ chúng.
