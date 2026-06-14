Tôi đồng ý cho sửa.

Giai đoạn 9: làm Pending Invitations cho Jockey.

Mục tiêu:

* Jockey xem được danh sách lời mời đang chờ từ Owner.
* Jockey có thể accept hoặc reject lời mời.
* Khi accept, cập nhật invitation và gắn Jockey vào race registration.
* Khi reject, cập nhật invitation để Owner có thể mời Jockey khác.
* Chỉ Jockey Active mới được dùng API này.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API cần có:

1. GET /api/jockey/invitations/pending
2. PUT /api/jockey/invitations/{id}/accept
3. PUT /api/jockey/invitations/{id}/reject

Trước khi sửa:

* Kiểm tra controller Jockey hiện có.
* Kiểm tra auth pattern lấy userId/currentJockeyId từ token.
* Kiểm tra entity/model:

  * User
  * Jockey
  * JockeyInvitation hoặc jockey_invitations
  * RaceRegistration hoặc race_registrations
  * Race hoặc races
  * Owner/User nếu invitation có owner info
* Kiểm tra constants/status hiện có cho:

  * invitation Pending/Accepted/Rejected
  * race registration ReadyToRace/Accepted/Confirmed/Pending hoặc trạng thái tương ứng
* Nếu tên entity/status khác mô tả, dùng đúng tên hiện có trong project.

Quyền truy cập chung:

* Token thiếu/sai: 401 theo pattern hiện có.
* User không phải Jockey: 403 Forbidden.
* Jockey chưa được Admin approve:

  * user.Status != Active hoặc jockey.IsActive != true
  * trả 403 Forbidden.
* Jockey Active mới được gọi API.

API 1: GET /api/jockey/invitations/pending

Logic:

* Lấy currentJockeyId từ token.
* Chỉ trả invitations của Jockey hiện tại.
* Chỉ trả invitations có Status = Pending hoặc trạng thái pending tương ứng hiện có.
* Include đủ thông tin để frontend render:

  * invitationId
  * raceId nếu có
  * raceName/title nếu có
  * raceDate/raceTime nếu có
  * location nếu có
  * ownerId/ownerName nếu có
  * status
  * createdAt/sentAt nếu có
  * registrationId nếu có

DTO cần tạo nếu chưa có:

* DTOs/Jockey/JockeyPendingInvitationResponse.cs

API 2: PUT /api/jockey/invitations/{id}/accept

Logic bắt buộc:

1. Lấy currentJockeyId từ token.
2. Kiểm tra invitation tồn tại.
3. Invitation phải thuộc currentJockeyId.
4. Invitation phải đang Pending, nếu không thì trả BadRequest/Conflict theo pattern hiện có.
5. Cập nhật jockey_invitations:

   * Status = Accepted
   * RespondedAt = now
6. Cập nhật race_registrations liên quan:

   * JockeyId = currentJockeyId
   * Status = ReadyToRace hoặc trạng thái tương ứng hiện có như Accepted/Confirmed/Approved.
7. Nếu project có nhiều invitation cho cùng raceRegistration, sau khi accept có thể reject/cancel các invitation pending còn lại cho cùng registration để tránh nhiều Jockey nhận cùng slot.
8. Dùng transaction nếu pattern project cho phép, để invitation và race_registration update đồng bộ.
9. SaveChanges.
10. Trả response message thành công.

Response gợi ý:
{
"message": "Đã chấp nhận lời mời.",
"status": "Accepted"
}

API 3: PUT /api/jockey/invitations/{id}/reject

Logic bắt buộc:

1. Lấy currentJockeyId từ token.
2. Kiểm tra invitation tồn tại.
3. Invitation phải thuộc currentJockeyId.
4. Invitation phải đang Pending, nếu không thì trả BadRequest/Conflict theo pattern hiện có.
5. Cập nhật jockey_invitations:

   * Status = Rejected
   * RespondedAt = now
6. Không gắn Jockey vào race_registrations.
7. Nếu race_registration đang giữ currentJockeyId do invitation này thì clear JockeyId hoặc đưa status về trạng thái chờ mời lại theo pattern hiện có.
8. Mục tiêu sau reject: Owner có thể mời Jockey khác.
9. SaveChanges.
10. Trả response message thành công.

Response gợi ý:
{
"message": "Đã từ chối lời mời.",
"status": "Rejected"
}

Yêu cầu bảo vệ role khác:

* API này chỉ áp dụng cho role Jockey.
* Không sửa flow Owner tạo invitation nếu không bắt buộc.
* Không sửa AuthController nếu không cần.
* Không sửa AdminController.
* Không ảnh hưởng Owner/Admin/Staff.

Yêu cầu code:

* Giữ route convention hiện có.
* Có thể tạo Controllers/Jockey/JockeyInvitationsController.cs nếu chưa có.
* Có thể tạo DTO response nếu project có convention DTO.
* Có thể tạo constants invitation/registration status nếu chưa có, nhưng không tạo trùng nếu đã có.
* Không tự bịa field DB. Nếu field khác tên, dùng đúng field hiện có.
* Không in toàn bộ code ra terminal.

Test cần đạt:

* Owner gửi lời mời => Jockey thấy trong GET /api/jockey/invitations/pending.
* Jockey Active gọi pending => 200 OK.
* Jockey Pending gọi pending => 403.
* Owner/Admin gọi pending => 403.
* Jockey accept invitation => invitation Status = Accepted, RespondedAt có giá trị.
* Jockey accept invitation => race_registration gắn JockeyId = currentJockeyId.
* Jockey accept lại invitation đã accept/reject => bị chặn.
* Jockey reject invitation => invitation Status = Rejected, RespondedAt có giá trị.
* Jockey reject => Owner có thể mời Jockey khác.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route pending invitations
3. Route accept
4. Route reject
5. Accept đã update invitation chưa
6. Accept đã update race registration chưa
7. Reject có cho Owner mời người khác chưa
8. Có ảnh hưởng Owner/Admin/Staff không
9. Build command
10. Lỗi còn lại nếu có
