Tôi đồng ý cho sửa.

Giai đoạn Owner: làm trang Jockey Assignment.

Mục tiêu:

* Hoàn chỉnh API cho Owner chọn Jockey phù hợp cho một race registration.
* Owner xem thông tin race/horse hiện tại.
* Owner xem danh sách Jockey candidate.
* Owner gửi invitation cho Jockey.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

Phạm vi cần kiểm tra trước khi sửa:

* Controller Owner hiện có.
* Auth pattern lấy current userId từ token.
* Entity/model:

  * User
  * HorseOwner
  * Horse
  * HorseBreed
  * Race
  * Tournament
  * RaceRegistration
  * Jockey
  * JockeyDistanceExperience
  * JockeyBreedExperience
  * JockeyInvitation
  * JockeyAvailability nếu có
  * Notification nếu có
* Constants/status hiện có:

  * User role HorseOwner/Jockey
  * UserStatuses Active/Pending/Inactive/Banned
  * RaceRegistration status Approved, JockeyInvited, ReadyToRace, Completed, Cancelled hoặc tên tương ứng hiện có
  * JockeyInvitation status Pending, Accepted, Rejected
  * Health status, distance skill, breed skill

API cần có:

1. GET /api/owner/jockey-assignment/{registrationId}/context
   Mục đích:

* Lấy context bên trái/trên đầu trang: tournament, race, horse, assigned jockey nếu có.

2. GET /api/owner/jockey-assignment/{registrationId}/candidates
   Query gợi ý:

* search
* healthStatus
* status: Active/Inactive/All nếu frontend cần
* page
* pageSize

Mục đích:

* Trả danh sách Jockey candidate để Owner chọn.

3. POST /api/owner/jockey-assignment/{registrationId}/invitations
   Request gợi ý:
   {
   "jockeyId": 5,
   "message": "Please join this race."
   }

Mục đích:

* Owner gửi invitation cho Jockey.
* Nếu project đã có API gửi invitation rồi thì ưu tiên sửa/dùng lại API hiện có, không tạo trùng route nếu không cần.

DTO cần tạo trong DTOs/Owner nếu chưa có:

1. OwnerJockeyAssignmentContextResponse
   Field:

* RegistrationId
* RegistrationStatus
* TournamentId
* TournamentName
* RaceId
* RaceName
* RaceDate
* Location
* DistanceMeters
* HorseId
* HorseName
* BreedName
* Age
* HeightCm
* WeightKg
* HealthStatus
* HorseIsActive
* AssignedJockeyId
* AssignedJockeyName

2. OwnerJockeyCandidateResponse
   Field:

* JockeyId
* FullName
* ProfileImageUrl
* WeightKg
* YearsOfExperience
* HealthStatus
* IsActive
* UserStatus
* AvailabilityStatus
* DistanceSkillLevel
* BreedSkillLevel
* AvailabilityScore
* WeightScore
* ExperienceScore
* DistanceScore
* BreedExperienceScore
* TotalScore
* RankNo
* RecommendationLevel
* AlreadyInvited
* InvitationStatus
* CanInvite
* CannotInviteReason

3. OwnerJockeyCandidateListResponse
   Field:

* Page
* PageSize
* TotalItems
* TotalPages
* Items: List<OwnerJockeyCandidateResponse>

4. SendJockeyInvitationRequest nếu chưa có
   Field:

* JockeyId
* Message optional

Điều kiện Owner được vào trang:

* Token hợp lệ.
* User role = HorseOwner.
* Owner đang Active.
* Registration thuộc Owner hiện tại.
* Registration status phải là Approved hoặc JockeyInvited.
* Race chưa Completed hoặc Cancelled.
* Registration chưa có JockeyId.

Nếu registration đã có JockeyId và status là ReadyToRace:

* Không trả list candidate để invite tiếp.
* Trả message hoặc context rõ:
  "This registration already has an assigned jockey."
* Không cho gửi invitation mới.

Logic GET context:

* Validate Owner access.
* Load registration theo registrationId.
* Include Race, Tournament nếu có, Horse, Breed, AssignedJockey nếu có.
* Trả đủ dữ liệu cho OwnerJockeyAssignmentContextResponse.
* Nếu field nào không có trong DB thì map null hoặc dùng field tương ứng hiện có, không tự bịa field.

Logic GET candidates:

* Validate Owner access như context.
* Chỉ lấy Jockey có:

  * user.Role = Jockey
  * user.Status = Active
  * jockey.IsActive = true
* Có thể filter healthStatus nếu query có.
* Có thể search theo fullName nếu query có.
* Tính DistanceSkillLevel theo distance của race.
* Tính BreedSkillLevel theo breed của horse.
* AlreadyInvited = true nếu đã có JockeyInvitation cho registrationId + jockeyId.
* InvitationStatus lấy từ invitation hiện có nếu có.
* CanInvite = true khi:

  * Jockey Active
  * Jockey health hợp lệ để đua
  * Registration chưa có JockeyId
  * Chưa có pending/accepted invitation cho Jockey đó
  * Race chưa completed/cancelled
* CannotInviteReason ghi lý do ngắn nếu không thể mời.

Scoring gợi ý, làm đơn giản trước:

* AvailabilityScore:

  * Available hoặc không có unavailable record: 100
  * Unavailable đúng ngày race: 0
* WeightScore:

  * Nếu weight phù hợp với horse/race theo logic hiện có thì cao, nếu chưa có rule thì cho 100 mặc định.
* ExperienceScore:

  * Dựa theo YearsOfExperience, càng cao càng tốt.
* DistanceScore:

  * NoExperience = 0
  * Basic = 50
  * Good = 75
  * Expert = 100
* BreedExperienceScore:

  * Không có breed experience = 0 hoặc 50 tùy convention, chọn cách hợp lý và ghi rõ.
  * Basic = 50
  * Good = 75
  * Expert = 100
* TotalScore = tổng hoặc trung bình các score.
* RankNo = thứ tự sau khi sort TotalScore giảm dần.
* RecommendationLevel:

  * TotalScore >= 80: HighlyRecommended
  * TotalScore >= 60: Recommended
  * Còn lại: Normal

Logic POST send invitation:

* Validate Owner access.
* Validate registration thuộc Owner hiện tại.
* Validate registration status Approved hoặc JockeyInvited.
* Validate registration chưa có JockeyId.
* Validate race chưa Completed/Cancelled.
* Validate Jockey tồn tại và Active:

  * user.Status = Active
  * jockey.IsActive = true
* Không cho gửi trùng invitation Pending cho cùng registrationId + jockeyId.
* Nếu hợp lệ:

  * Insert JockeyInvitation với Status = Pending.
  * Nếu registration status đang Approved thì đổi thành JockeyInvited nếu project có status này.
  * Nếu project có Notification entity/service thì tạo notification cho Jockey.
* Không set RaceRegistration.JockeyId ở bước gửi invitation.
* Chỉ khi Jockey accept invitation mới set RaceRegistration.JockeyId.

Notification khi Owner gửi invitation:

* Nếu project có Notification entity/table/service:

  * Receiver/UserId = userId của Jockey.
  * Title = "Bạn có lời mời tham gia cuộc đua"
  * Message = Owner hoặc horse/race đã mời bạn tham gia.
  * IsRead = false.
  * CreatedAt = now.
  * ReferenceId = invitationId hoặc raceId nếu DB có field.
* Nếu chưa có Notification infrastructure thì không tự tạo migration, chỉ ghi rõ.

Yêu cầu bảo vệ role khác:

* Chỉ Owner/HorseOwner được dùng API này.
* Jockey/Admin/Staff gọi API này phải trả 403.
* Không sửa flow login/me.
* Không sửa Jockey Settings, Dashboard, Calendar, Notifications nếu không cần.
* Không ảnh hưởng role khác.

Test cần đạt:

* Owner Active vào đúng registration của mình => lấy context thành công.
* Owner gọi registration của người khác => 403 hoặc 404 theo pattern hiện có.
* Registration đã có JockeyId + ReadyToRace => không cho invite thêm.
* Race Completed/Cancelled => không cho invite.
* GET candidates trả Jockey Active.
* Candidate có distance/breed skill đúng.
* AlreadyInvited đúng theo invitation hiện có.
* POST send invitation tạo JockeyInvitation Pending.
* POST send invitation không set RaceRegistration.JockeyId.
* Gửi trùng invitation Pending bị chặn.
* Jockey nhận được notification nếu project có Notification.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route context
3. Route candidates
4. Route send invitation
5. DTO đã tạo
6. Điều kiện Owner access đã xử lý
7. Candidate scoring đã làm
8. Có tạo notification không
9. Có ảnh hưởng role khác không
10. Build command/lỗi còn lại
