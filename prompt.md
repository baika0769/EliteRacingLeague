Tôi đồng ý cho sửa.

Chỉ sửa 2 lỗi còn sót trong BE hiện tại.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không sửa lan man file khác.
* Không in code dài ra terminal.
* File nào đã đúng thì giữ nguyên.

Mục tiêu 1: Sửa OwnerRacesController

File:

* Controllers/Owner/OwnerRacesController.cs

Việc cần làm:

* Tìm đoạn đang kiểm tra:
  r.Status == "Open"

* Đổi thành:
  RaceStatuses.CanRegister(r.Status)

Yêu cầu:

* Thêm using Constants nếu thiếu.
* Giữ nguyên điều kiện cho phép Owner xem race nếu đã có RaceRegistration.
* Không đổi logic khác.
* Không đổi route/response.

Mục tiêu 2: Sửa AdminTournamentsController

File:

* Controllers/Admin/AdminTournamentsController.cs

Việc cần làm:

* Tìm đoạn đang set:
  Status = "Assigned"

* Đổi thành:
  Status = RefereeAssignmentStatuses.Assigned

Yêu cầu thêm:

* Khi existing assignment != null, ngoài cập nhật:

  * RefereeId
  * AssignedAt

  phải set thêm:

  * existing.Status = RefereeAssignmentStatuses.Assigned

Yêu cầu:

* Thêm using Constants nếu thiếu.
* Không đổi logic assign referee khác.
* Không đổi database.
* Không migration.

Sau khi sửa:
* Search lại để xác nhận không còn r.Status == "Open" và không còn Status = "Assigned" trong Controllers.
* Chạy dotnet build.


Sau khi xong chỉ trả lời tối đa 8 dòng:

1. File đã sửa
2. OwnerRacesController đã đổi "Open" sang RaceStatuses.CanRegister chưa
3. AdminTournamentsController đã dùng RefereeAssignmentStatuses.Assigned chưa
4. existing assignment đã set lại Status chưa
5. Build command
6. Kết quả build/lỗi còn lại nếu có
