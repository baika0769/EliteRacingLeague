Tôi đồng ý cho sửa.

Giai đoạn dọn code nhỏ: chỉ sửa 3 điểm còn sót trong BE hiện tại.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* File nào đã đúng thì giữ nguyên.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi nghiệp vụ.
* Không sửa lan man file khác.
* Không in code dài ra terminal.
* Code sạch, dễ bảo trì.

Mục tiêu 1: OwnerRegistrationsController dùng helper CanRegister

File:

* Controllers/Owner/OwnerRegistrationsController.cs

Hiện còn check trực tiếp:

* t.Race.Status == RaceStatuses.Scheduled
* race.Status != RaceStatuses.Scheduled

Yêu cầu sửa:

* Đổi:
  t.Race.Status == RaceStatuses.Scheduled

  thành:
  RaceStatuses.CanRegister(t.Race.Status)

* Đổi:
  race.Status != RaceStatuses.Scheduled

  thành:
  !RaceStatuses.CanRegister(race.Status)

Lưu ý:

* Không đổi message.
* Không đổi route.
* Không đổi response.
* Không đổi logic khác.
* Mục tiêu chỉ là gom logic đăng ký race về helper chung.

Mục tiêu 2: AdminTournamentsController xóa dòng set Assigned bị lặp

File:

* Controllers/Admin/AdminTournamentsController.cs

Hiện có dạng:
existing.Status = RefereeAssignmentStatuses.Assigned;
existing.AssignedAt = DateTime.UtcNow;
existing.Status = RefereeAssignmentStatuses.Assigned;

Yêu cầu sửa:

* Xóa 1 dòng lặp.
* Sau khi sửa, existing assignment vẫn phải cập nhật đủ:

  * existing.RefereeId
  * existing.AssignedAt
  * existing.Status = RefereeAssignmentStatuses.Assigned

Lưu ý:

* Không đổi logic assign referee khác.
* Không đổi route/response.
* Không sửa entity/database.

Mục tiêu 3: OwnerNotificationsController xử lý chuỗi Approved/Pending cho sạch hơn

File:

* Controllers/Owner/OwnerNotificationsController.cs

Hiện còn các chuỗi:

* n.Title.Contains("Approved")
* n.Message.Contains("Approved")
* return "Approved"
* return "Pending"

Yêu cầu:

* Kiểm tra ngữ cảnh các chuỗi này.
* Nếu "Approved" / "Pending" đang được dùng như status nghiệp vụ thì đổi sang constants phù hợp, ví dụ:

  * RaceRegistrationStatuses.Approved
  * RaceRegistrationStatuses.Pending
  * hoặc constants đúng theo nghiệp vụ hiện có.
* Nếu "Approved" nằm trong text phân loại notification theo title/message, có thể giữ vì đây là keyword đọc từ nội dung notification, không phải status DB.
* Nếu return "Approved" / return "Pending" là statusLabel nghiệp vụ thì đổi sang constants phù hợp nếu có.
* Không sửa chuỗi message hiển thị nếu chỉ là text cho người dùng.
* Không làm phức tạp controller.

Sau khi sửa:

* Chạy dotnet build.
* Chạy search kiểm tra nhanh:
  rg -n "RaceStatuses\.Scheduled" Controllers/Owner/OwnerRegistrationsController.cs
  rg -n "existing\.Status = RefereeAssignmentStatuses\.Assigned" Controllers/Admin/AdminTournamentsController.cs
  rg -n ""Approved"|"Pending"" Controllers/Owner/OwnerNotificationsController.cs

Yêu cầu kết quả:

* OwnerRegistrationsController không còn check trực tiếp RaceStatuses.Scheduled cho logic đăng ký.
* AdminTournamentsController không còn set Assigned bị lặp.
* OwnerNotificationsController chỉ còn chuỗi Approved/Pending nếu đó là keyword text hợp lệ; nếu là status nghiệp vụ thì phải dùng constants.
* dotnet build pass.

Sau khi xong chỉ trả lời tối đa 7 dòng:

1. File đã sửa
2. OwnerRegistrationsController đã dùng CanRegister chưa
3. AdminTournamentsController đã xóa dòng Assigned lặp chưa
4. OwnerNotificationsController còn chuỗi Approved/Pending không, lý do nếu còn
5. Có sửa DB/migration/frontend không
6. Build command
7. Kết quả build/lỗi còn lại
