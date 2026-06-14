Tôi đồng ý cho sửa.

Giai đoạn Owner: hoàn chỉnh logic nút Send Invitation trong trang Jockey Assignment.

Mục tiêu:

* Owner bấm Send Invitation để mời Jockey.
* Button trên từng Jockey card phải phản ánh đúng trạng thái.
* API gửi invitation nhận FeeAmount và Message.
* Không set RaceRegistration.JockeyId khi gửi invitation.
* Chỉ khi Jockey accept invitation mới gắn Jockey vào race registration.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API cần dùng:
Ưu tiên dùng API cũ nếu đã có:
POST /api/owner/races/registrations/{registrationId}/jockey-invitations

Nếu chưa có API cũ hoặc route chưa phù hợp thì tạo route mới:
POST /api/owner/jockey-assignment/{registrationId}/invite

Request body:
{
"jockeyId": 12,
"feeAmount": 500,
"message": "I would like to invite you to race with Desert Thunder."
}

DTO cần tạo/sửa nếu thiếu:

* DTOs/Owner/SendJockeyInvitationRequest.cs

Field cần có:

* JockeyId
* FeeAmount
* Message

Logic button trong API list Jockey candidates cần trả đủ field:

* AlreadyInvited
* InvitationStatus
* CanInvite
* CannotInviteReason
* AvailabilityStatus

Quy tắc hiển thị button:

1. Jockey active, healthy, available, chưa mời
   => Button: Send Invitation
   => CanInvite = true
   => CannotInviteReason = null

2. Đã gửi lời mời và invitation status = Pending
   => Button: Invitation Pending
   => CanInvite = false
   => CannotInviteReason = "Invitation pending"

3. Jockey đã accept invitation
   => Button: Assigned
   => CanInvite = false
   => CannotInviteReason = "Jockey already assigned"

4. Jockey unavailable ngày race
   => Button: Unavailable
   => CanInvite = false
   => CannotInviteReason = "Jockey is unavailable on race day"

5. Jockey đã có race khác cùng ngày
   => Button: Racing Day
   => CanInvite = false
   => CannotInviteReason = "Jockey already has a race on this day"

6. Jockey inactive
   => Button: Inactive
   => CanInvite = false
   => CannotInviteReason = "Jockey is inactive"

7. Registration đã có jockey
   => Disable toàn bộ
   => CanInvite = false
   => CannotInviteReason = "This registration already has an assigned jockey"

Backend cần làm khi lấy danh sách Jockey:

* Kiểm tra Jockey active:

  * user.Status = Active
  * jockey.IsActive = true
* Kiểm tra health:

  * Healthy hoặc Fit nếu project đang dùng Fit
* Kiểm tra availability theo ngày race:

  * Unavailable => không cho invite
* Kiểm tra Jockey có race khác cùng ngày:

  * RaceRegistration của Jockey có status ReadyToRace hoặc Completed trong cùng ngày race
  * Nếu có race cùng ngày và không phải registration hiện tại => AvailabilityStatus = RacingDay
* Kiểm tra invitation hiện có:

  * Nếu có Pending => Invitation Pending
  * Nếu có Accepted => Assigned
  * Nếu có Rejected thì có thể cho gửi lại nếu nghiệp vụ cho phép, hoặc chặn theo logic hiện có
* Kiểm tra registration đã có JockeyId:

  * Nếu đã có thì disable toàn bộ candidate

Logic POST Send Invitation:

1. Lấy current owner userId từ token.
2. Validate user role = HorseOwner.
3. Validate owner đang Active.
4. Lấy RaceRegistration theo registrationId.
5. Validate registration thuộc Owner hiện tại.
6. Validate registration status cho phép mời: Approved hoặc JockeyInvited.
7. Validate registration chưa có JockeyId.
8. Validate race chưa Completed hoặc Cancelled.
9. Validate JockeyId tồn tại.
10. Validate Jockey active:

    * user.Status = Active
    * jockey.IsActive = true
11. Validate Jockey health hợp lệ.
12. Validate Jockey không Unavailable ngày race.
13. Validate Jockey không có race khác cùng ngày.
14. Validate chưa có invitation Pending cho registrationId + jockeyId.
15. Tạo JockeyInvitation:

    * RegistrationId = registrationId
    * JockeyId = request.JockeyId
    * FeeAmount = request.FeeAmount nếu DB có field
    * Message = request.Message nếu DB có field
    * Status = Pending
    * CreatedAt/SentAt = now
16. Nếu RaceRegistration.Status đang Approved thì đổi sang JockeyInvited nếu project có status này.
17. Không set RaceRegistration.JockeyId.
18. Nếu project có Notification entity/service thì tạo notification cho Jockey.
19. SaveChanges.

Response sau khi gửi:
{
"message": "Đã gửi lời mời cho Jockey.",
"invitationStatus": "Pending"
}

Notification nếu có:

* Receiver/UserId = userId của Jockey.
* Title = "Bạn có lời mời tham gia cuộc đua"
* Message = request.Message hoặc nội dung mặc định.
* IsRead = false.
* CreatedAt = now.
* ReferenceId = invitationId hoặc raceId nếu DB có field.

Yêu cầu bảo vệ role khác:

* Chỉ HorseOwner được gọi API này.
* Jockey/Admin/Staff gọi phải trả 403.
* Không sửa AuthController.
* Không sửa Jockey accept/reject nếu không cần.
* Không ảnh hưởng Admin/Jockey flow khác.

Test cần đạt:

* Owner Active gửi invitation hợp lệ => tạo JockeyInvitation Pending.
* Gửi invitation không set RaceRegistration.JockeyId.
* Registration status Approved đổi sang JockeyInvited nếu có status này.
* Gửi trùng Pending invitation bị chặn.
* Registration đã có JockeyId bị chặn.
* Jockey inactive bị chặn.
* Jockey unavailable ngày race bị chặn.
* Jockey có race khác cùng ngày bị chặn.
* FeeAmount và Message được lưu nếu DB có field.
* Jockey nhận notification nếu project có Notification.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route send invitation đang dùng
3. DTO request đã có FeeAmount/Message chưa
4. Button state đã trả đủ field chưa
5. Có chặn duplicate Pending invitation không
6. Có chặn Jockey unavailable/racing day không
7. Có đảm bảo không set RaceRegistration.JockeyId không
8. Có tạo notification không
9. Có ảnh hưởng role khác không
10. Build command/lỗi còn lại
