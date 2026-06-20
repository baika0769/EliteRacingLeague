Tôi đồng ý cho sửa.

Giai đoạn BE: khóa Owner Jockey Assignment sau khi Owner bấm Sign official jockey.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File/API nào đã có và đúng thì giữ nguyên.
* Không tạo trùng controller/DTO/constants.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không throw exception không kiểm soát.
* Không ghi đè RaceRegistration.JockeyId sau khi đã Sign.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu nghiệp vụ:
Sau khi Owner bấm Sign, registration đó phải chuyển sang trạng thái đã chốt official jockey.

Rule khóa chính:
RaceRegistration.JockeyId != null
=> đã có official jockey
=> không cho gửi invitation mới
=> không cho Sign jockey khác
=> không ghi đè JockeyId
=> không phát sinh exception
=> chỉ cho xem dữ liệu hoặc Change Tournament.

File cần kiểm tra/sửa:

* Constants/RaceRegistrationStatuses.cs
* Constants/InvitationStatuses.cs
* Constants/JockeyHealthStatuses.cs
* Models/RaceRegistration.cs
* Models/JockeyInvitation.cs
* Controllers/Owner/OwnerJockeyAssignmentController.cs
* Controllers/Jockey/JockeyInvitationsController.cs
* DTOs/Owner/OwnerJockeyAssignmentDtos.cs hoặc DTO hiện có tương ứng

Cần xác định đúng field hiện có:

* RaceRegistration.JockeyId
* RaceRegistration.Status
* RaceRegistration.JockeyConfirmedAt
* JockeyInvitation.Status
* JockeyInvitation.RegistrationId
* JockeyInvitation.JockeyId
* JockeyInvitation.SentAt
* JockeyInvitation.RespondedAt

Không tự tạo status mới.
Dùng constants hiện có:

* InvitationStatuses.Pending
* InvitationStatuses.Accepted
* InvitationStatuses.Rejected
* InvitationStatuses.Cancelled nếu cần
* RaceRegistrationStatuses.ReadyToRace
* JockeyHealthStatuses.CanRace(...)

Giai đoạn 0: Kiểm tra build nền

Việc đầu tiên:

* Chạy dotnet build.
* Nếu có lỗi build cũ thì sửa lỗi build trực tiếp trước.
* Không thêm logic mới khi project đang build lỗi.

Giai đoạn 1: Tạo rule dùng chung trong OwnerJockeyAssignmentController

Trong OwnerJockeyAssignmentController, thống nhất rule:

hasOfficialJockey = registration.JockeyId != null

Dùng rule này cho các API:

* GET /api/owner/jockey-assignment/{registrationId}/context
* GET /api/owner/jockey-assignment/{registrationId}/candidates
* GET /api/owner/jockey-assignment/{registrationId}/invitations
* GET /api/owner/jockey-assignment/invitations/{invitationId}
* POST /api/owner/jockey-assignment/{registrationId}/invitations
* PUT /api/owner/jockey-assignment/{registrationId}/official-jockey/{invitationId}

Ý nghĩa:

* hasOfficialJockey = true
* canSendInvitation = false
* canSignJockey = false
* canChangeTournament = true

Có thể tạo private helper trong controller nếu cần, nhưng không refactor lớn.

Giai đoạn 2: Chỉnh GET /context

API:
GET /api/owner/jockey-assignment/{registrationId}/context

Nếu DTO context chưa có thì bổ sung các field:

* bool HasOfficialJockey
* int? OfficialJockeyId
* string? OfficialJockeyName
* string AssignmentStatus
* bool CanSendInvitation
* bool CanSignJockey
* bool CanChangeTournament

Mapping:

* HasOfficialJockey = registration.JockeyId != null
* OfficialJockeyId = registration.JockeyId
* OfficialJockeyName = AssignedJockeyName hoặc tên Jockey đang được gán
* AssignmentStatus = registration.Status
* CanSendInvitation = registration.JockeyId == null
* CanSignJockey = registration.JockeyId == null
* CanChangeTournament = true

Mục tiêu:
FE biết màn hình đã khóa, không hiện nút gửi invitation hoặc Sign sai.

Không phá các field context cũ.

Giai đoạn 3: Chỉnh POST /invitations

API:
POST /api/owner/jockey-assignment/{registrationId}/invitations

Nếu registration đã có official jockey:

* Không tạo invitation mới.
* Không throw exception.
* Trả 400 BadRequest rõ ràng.

Response:
{
"message": "Official jockey has already been selected for this registration. Please use Change Tournament to work with another registration."
}

Các rule cũ vẫn giữ:

* Chặn gửi trùng invitation Pending.
* Chặn race completed/cancelled.
* Chặn Jockey inactive.
* Chặn Jockey health_status không Fit.
* Không set RaceRegistration.JockeyId ở bước gửi invitation.

Giai đoạn 4: Chỉnh GET /candidates

API:
GET /api/owner/jockey-assignment/{registrationId}/candidates

Nếu registration đã có official jockey:

* Không cho mời thêm.
* Không lỗi FE.

Cách ưu tiên:

* Vẫn trả response đúng format list hiện có.
* Items rỗng hoặc candidates rỗng.
* Có message nếu response DTO hiện tại hỗ trợ.
* Nếu vẫn trả candidate cũ thì tất cả CanInvite = false và CannotInviteReason = "This registration already has an assigned jockey".

Không đổi route.
Không phá format response cũ nếu FE đang dùng.

Giai đoạn 5: Chỉnh GET /invitations

API:
GET /api/owner/jockey-assignment/{registrationId}/invitations

Nếu đã Sign rồi:

* Vẫn trả danh sách invitation cũ.
* CanSign = false cho tất cả dòng.
* IsOfficial = true cho Jockey đang là RaceRegistration.JockeyId.
* IsOfficial = false cho các Jockey khác.

Rule:
CanSign = true chỉ khi:

* registration.JockeyId == null
* invitation.Status == InvitationStatuses.Accepted

IsOfficial = true khi:

* registration.JockeyId == invitation.JockeyId

Ví dụ đúng sau Sign:

* Jockey A Accepted và là official => CanSign = false, IsOfficial = true
* Jockey B Accepted nhưng không official => CanSign = false, IsOfficial = false

Giai đoạn 6: Chỉnh GET /invitations/{invitationId}

API:
GET /api/owner/jockey-assignment/invitations/{invitationId}

Detail vẫn xem được.

Rule:

* Nếu invitation thuộc registration đã có official jockey:

  * CanSign = false
* Nếu invitation.JockeyId == registration.JockeyId:

  * IsOfficial = true
* Nếu registration đã có official jockey khác:

  * IsOfficial = false
  * CanSign = false

Không cho FE hiểu nhầm là còn Sign được.

Bảo mật:

* Owner chỉ xem được invitation thuộc registration của mình.
* Owner A không xem được invitation của Owner B.

Giai đoạn 7: Chỉnh PUT /official-jockey/{invitationId}

API:
PUT /api/owner/jockey-assignment/{registrationId}/official-jockey/{invitationId}

Đây là phần quan trọng nhất.

Case 1: Chưa có official jockey

Cho Sign nếu:

* registration thuộc Owner hiện tại
* invitation thuộc registration đó
* invitation.Status = InvitationStatuses.Accepted
* Jockey còn active:

  * user.Status = UserStatuses.Active
  * jockey.IsActive = true
* Jockey health_status được đua:

  * JockeyHealthStatuses.CanRace(jockey.HealthStatus) = true
* registration.Status còn hợp lệ để chọn jockey, ví dụ Approved hoặc JockeyInvited

Sau đó:

* Set RaceRegistration.JockeyId = invitation.JockeyId
* Set RaceRegistration.Status = RaceRegistrationStatuses.ReadyToRace
* Set RaceRegistration.JockeyConfirmedAt = DateTime.UtcNow nếu field này có
* Cancel Pending invitation khác cùng registration nếu InvitationStatuses.Cancelled có sẵn
* Không đổi invitation Accepted đang được chọn
* SaveChanges

Case 2: Đã Sign đúng Jockey đó rồi

Không lỗi.
Không ghi DB lại nếu không cần.
Trả 200 OK:

{
"message": "Official jockey already selected.",
"registrationId": 12,
"jockeyId": 8,
"jockeyName": "Marcus Vane",
"registrationStatus": "ReadyToRace"
}

Case 3: Đã có official jockey khác

Không ghi đè.
Không clear JockeyId.
Không đổi official jockey.
Trả 400 BadRequest:

{
"message": "Official jockey has already been selected. Please use Change Tournament to work with another registration."
}

Giai đoạn 8: Sửa Jockey Accept nếu còn auto assign

File:
Controllers/Jockey/JockeyInvitationsController.cs

Kiểm tra hàm AcceptInvitation.

Nếu hiện tại Jockey Accept đang làm:

* registration.JockeyId = jockeyId
* registration.Status = ReadyToRace
* registration.JockeyConfirmedAt = now
* cancel invitation khác

Thì sửa lại.

Logic đúng khi Jockey bấm Accept:

* Chỉ set invitation.Status = InvitationStatuses.Accepted
* Set invitation.RespondedAt = DateTime.UtcNow
* Không set RaceRegistration.JockeyId
* Không set RaceRegistration.Status = ReadyToRace
* Không set RaceRegistration.JockeyConfirmedAt
* Không cancel invitation khác
* SaveChanges

Response:
{
"message": "Đã chấp nhận lời mời. Vui lòng chờ Owner xác nhận chính thức.",
"status": "Accepted"
}

RejectInvitation:

* Chỉ set invitation.Status = Rejected
* Set RespondedAt
* Không clear RaceRegistration.JockeyId

Lý do:
Accepted = Jockey đồng ý.
Sign = Owner xác nhận chính thức.

Giai đoạn 9: Không tạo workflow đổi official jockey

Không làm ở giai đoạn này:

* Không ghi đè JockeyId.
* Không đổi official jockey.
* Không clear JockeyId.
* Không mở lại invitation.
* Không tạo API đổi official jockey.

Nếu sau này muốn đổi Jockey đã Sign thì sẽ làm workflow riêng:
Cancel official jockey
→ Reopen assignment
→ Send new invitations
→ Sign new jockey

Giai đoạn 10: Test flow đúng

Test theo thứ tự:

1. dotnet build.
2. Owner gửi invitation cho 2–3 Jockey.
3. Jockey A Accepted.
4. Jockey B Accepted.
5. Owner gọi GET /invitations thấy 2 Accepted, CanSign = true.
6. Owner bấm Sign Jockey A.
7. RaceRegistration.JockeyId = Jockey A.
8. RaceRegistration.Status = ReadyToRace.
9. Gọi lại GET /invitations.
10. Tất cả CanSign = false.
11. Jockey A IsOfficial = true.
12. Owner bấm Sign lại Jockey A => 200 OK already selected.
13. Owner bấm Sign Jockey B => 400, không ghi đè.
14. Owner gửi invitation mới => 400.
15. Owner dùng Change Tournament để chuyển sang registration khác.

Giai đoạn 11: Test chống bug và bảo mật

Cần test:

* Owner A không sign registration của Owner B.
* Owner A không xem invitations của Owner B.
* Không sign invitation Pending.
* Không sign invitation Rejected.
* Không sign invitation không thuộc registrationId.
* Không sign Jockey inactive.
* Không sign Jockey health_status không Fit.
* Sau Sign không gửi invitation mới.
* Sau Sign không Sign Jockey khác.
* Sau Sign không phát sinh exception.
* Jockey không accept invitation của Jockey khác.

Yêu cầu cuối:

* Không ảnh hưởng API context/candidates/send invitation cũ ngoài việc thêm trạng thái khóa.
* Không ảnh hưởng Owner Dashboard/My Horse/My Registrations.
* Không ảnh hưởng Admin/Referee/Spectator.
* Jockey Invitations chỉ sửa đúng nghiệp vụ Accept không auto assign.
* dotnet build phải pass.



Bổ sung an toàn:

1. Khi xử lý PUT /official-jockey/{invitationId}, nếu có cập nhật RaceRegistration và các JockeyInvitation khác thì dùng transaction để tránh dữ liệu bị cập nhật nửa chừng.

2. Trong Jockey AcceptInvitation, nếu invitation.Registration.JockeyId != null thì không cho Accept nữa, trả BadRequest rõ ràng:
"Official jockey has already been selected for this registration."

3. Nếu InvitationStatuses.Cancelled không tồn tại thì không tự tạo status mới. Khi đó không cancel pending invitation khác, nhưng GET /invitations và GET /invitations/{invitationId} vẫn phải trả CanSign = false cho tất cả vì registration đã có official jockey.
Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. File đã sửa
3. DTO context/invitation có bổ sung field khóa không
4. GET context đã trả hasOfficialJockey/canSendInvitation/canSignJockey chưa
5. POST invitations đã chặn sau Sign chưa
6. GET candidates đã khóa canInvite sau Sign chưa
7. GET invitations/detail đã tính CanSign/IsOfficial đúng chưa
8. PUT official-jockey có chặn ghi đè JockeyId chưa
9. Sign lại đúng Jockey đã selected trả OK chưa
10. Jockey Accept còn auto assign không
11. Có ảnh hưởng API cũ/role khác không
12. Build command/lỗi còn lại
