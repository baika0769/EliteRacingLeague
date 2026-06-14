Tôi đồng ý cho sửa.

Giai đoạn Owner: hoàn chỉnh logic lấy danh sách Jockey phù hợp cho trang Jockey Assignment.

Mục tiêu:

* Tạo/hoàn chỉnh API lấy danh sách Jockey phù hợp cho Owner chọn.
* API dùng cho màn hình Jockey Assignment.
* Backend tự join dữ liệu, tính điểm match, sort theo TotalScore giảm dần.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API cần làm:
GET /api/owner/jockey-assignment/{registrationId}/jockeys

Nếu project đã có route candidates tương đương như:
GET /api/owner/jockey-assignment/{registrationId}/candidates
thì dùng route hiện có, không tạo trùng.

Query params nếu có:

* search
* healthStatus
* status: Active, Inactive, All
* page
* pageSize

Trước khi sửa:

* Kiểm tra controller Owner Jockey Assignment hiện có.
* Kiểm tra auth pattern lấy current owner userId từ token.
* Kiểm tra entity/model:

  * User
  * HorseOwner
  * Horse
  * HorseBreed
  * Race
  * Tournament
  * RaceRegistration
  * Jockey
  * JockeyAvailability
  * JockeyDistanceExperience
  * JockeyBreedExperience
  * JockeyInvitation
  * JockeyRecommendation nếu project có bảng này
* Kiểm tra constants/status hiện có cho:

  * UserRoles.HorseOwner
  * UserRoles.Jockey
  * UserStatuses.Active
  * RaceRegistrationStatuses
  * JockeyInvitationStatuses
  * JockeySkillLevels
  * JockeyAvailabilityStatuses

Logic lấy danh sách Jockey phù hợp:

1. Lấy race registration theo registrationId.
2. Kiểm tra registration thuộc Owner đang login.
3. Lấy thông tin race date, race distance, horse breed, horse weight nếu có.
4. Chỉ lấy Jockey đã được admin duyệt:

   * user.Role == Jockey
   * user.Status == Active
   * jockey.IsActive == true
5. Join Users để lấy tên Jockey, avatar/profile image.
6. Join JockeyAvailabilities theo ngày race:

   * Nếu có record Unavailable đúng ngày race => AvailabilityStatus = Unavailable.
   * Nếu không có record hoặc Available => AvailabilityStatus = Available.
   * Nếu ngày đó có race ReadyToRace/Completed của Jockey khác thì có thể xem là RacingDay nếu project đã có logic check lịch.
7. Join JockeyDistanceExperiences theo Race.DistanceMeters.
8. Join JockeyBreedExperiences theo Horse.BreedId.
9. Kiểm tra Jockey đã được mời chưa:

   * AlreadyInvited = true nếu đã có JockeyInvitation cho registrationId + jockeyId.
   * InvitationStatus = status hiện có.
10. Tính điểm match.
11. Sort theo TotalScore giảm dần.
12. Trả về danh sách card cho frontend.

Điều kiện Owner được gọi API:

* Token hợp lệ.
* Role = HorseOwner.
* Owner đang Active.
* Registration thuộc Owner hiện tại.
* Registration status phải cho phép mời Jockey, ví dụ Approved hoặc JockeyInvited.
* Race chưa Completed hoặc Cancelled.
* Nếu registration đã có JockeyId và status là ReadyToRace thì không trả list invite, trả message:
  "This registration already has an assigned jockey."

Rule tính điểm recommendation từ 0 đến 100:

AvailabilityScore tối đa 30:

* Available = 30
* Unavailable = 0
* RacingDay = 0

DistanceScore tối đa 25:

* Expert = 25
* Good = 18
* Basic = 10
* NoExperience = 0
* Không có record = 0

BreedExperienceScore tối đa 20:

* Expert = 20
* Good = 14
* Basic = 8
* Không có record = 0

ExperienceScore tối đa 15:

* YearsOfExperience >= 5 => 15
* YearsOfExperience >= 3 => 10
* YearsOfExperience >= 1 => 5
* Còn lại => 0

WeightScore tối đa 10:

* 45kg đến 60kg => 10
* > 60kg đến 65kg => 7
* Còn lại => 4

TotalScore =
AvailabilityScore

* DistanceScore
* BreedExperienceScore
* ExperienceScore
* WeightScore

RecommendationLevel:

* TotalScore >= 80 => Excellent
* TotalScore >= 60 => Good
* TotalScore >= 40 => Fair
* TotalScore < 40 => Low

PrimaryReason:

* Nếu DistanceScore và BreedExperienceScore cao: "Strong distance skill and breed experience"
* Nếu AvailabilityScore = 0: "Jockey is not available on race day"
* Nếu DistanceScore cao: "Strong distance skill"
* Nếu BreedExperienceScore cao: "Strong breed experience"
* Nếu ExperienceScore cao: "Experienced jockey"
* Nếu không có điểm nổi bật: "Basic match"

Response mỗi Jockey card cần có:

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
* PrimaryReason
* AlreadyInvited
* InvitationStatus
* CanInvite
* CannotInviteReason

CanInvite = true khi:

* Jockey active.
* AvailabilityScore > 0.
* HealthStatus hợp lệ để đua, ưu tiên Healthy hoặc Fit nếu project đang map Fit.
* Registration chưa có JockeyId.
* Race chưa Completed/Cancelled.
* Không có invitation Pending/Accepted cho Jockey đó trong registration này.

CannotInviteReason gợi ý:

* "Jockey is unavailable on race day"
* "Jockey is not active"
* "Jockey has already been invited"
* "Registration already has an assigned jockey"
* "Race is completed or cancelled"
* "Jockey health status is not eligible"

Nếu project có bảng JockeyRecommendations:

* Không bắt buộc ghi DB ở API GET.
* Có thể chỉ tính điểm runtime.
* Nếu code hiện tại đã dùng bảng JockeyRecommendations thì giữ pattern hiện có, nhưng không làm phức tạp.

Yêu cầu bảo vệ role khác:

* Chỉ HorseOwner được gọi API này.
* Jockey/Admin/Staff gọi API này trả 403.
* Không sửa AuthController.
* Không sửa Jockey flow.
* Không sửa Admin flow.
* Không ảnh hưởng role khác.

Test cần đạt:

* Owner gọi registration của mình => 200.
* Owner gọi registration của người khác => 403 hoặc 404 theo pattern hiện có.
* Registration đã có JockeyId ReadyToRace => không cho invite tiếp.
* Chỉ Jockey Active xuất hiện.
* Jockey Unavailable ngày race có AvailabilityScore = 0.
* Distance skill đúng theo Race.DistanceMeters.
* Breed skill đúng theo Horse.BreedId.
* TotalScore = tổng 5 score.
* Danh sách sort theo TotalScore giảm dần.
* AlreadyInvited đúng.
* CanInvite/CannotInviteReason đúng.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route API Jockey list
3. Đã validate Owner access chưa
4. Đã join availability/distance/breed/invitation chưa
5. Đã tính score 100 điểm chưa
6. Đã sort TotalScore giảm dần chưa
7. Đã có RecommendationLevel/PrimaryReason chưa
8. Có ảnh hưởng role khác không
9. Build command
10. Lỗi còn lại nếu có
