Tôi đồng ý cho sửa.

Giai đoạn BE: hoàn thiện Owner Race Info cho nút Race Info trong My Registrations.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File/API nào đã có và đúng thì giữ nguyên.
* Không tạo route mới nếu route cũ đã dùng được.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không tự bịa field model hoặc navigation.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:
Hoàn thiện API hiện có:

GET /api/owner/races/{raceId}

để Owner bấm nút Race Info có thể xem đủ:

1. Thông tin giải đấu.
2. Thông tin race.
3. Thông tin đăng ký của Owner.
4. Thông tin trọng tài đã được assign.

File cần sửa:

1. Controllers/Owner/OwnerRacesController.cs
2. DTOs/Owner/OwnerRaceDetailResponse.cs

File cần kiểm tra thêm:

* Models/Race.cs
* Models/Tournament.cs
* Models/RaceRegistration.cs
* Models/Horse.cs
* Models/Jockey.cs
* Models/User.cs
* Models/RefereeAssignment.cs
* Models/RaceReferee.cs
* Constants/RaceStatuses.cs
* Constants/RefereeAssignmentStatuses.cs

Yêu cầu 1: Mở rộng DTO OwnerRaceDetailResponse

File:
DTOs/Owner/OwnerRaceDetailResponse.cs

Giữ các field cũ để không phá FE:

* RaceId
* TournamentId
* TournamentName
* RaceName
* RaceDate
* Location
* Distance
* Status

Bổ sung field mới:

Nhóm Tournament:

* string? TournamentDescription
* string? TournamentStatus
* string? TournamentImageUrl
* decimal? PrizePool
* string? Rules
* string? TournamentStartDate
* string? TournamentEndDate

Nhóm Race:

* int MaxHorses
* string? JockeySelectionDeadline
* string? PredictionDeadline

Nhóm registration của Owner:

* int? RegistrationId
* string? RegistrationStatus
* string? HorseName
* string? OfficialJockeyName

Nhóm referee assignment:

* int? RefereeAssignmentId
* int? RefereeId
* string? RefereeName
* string? RefereeEmail
* string? RefereePhone
* string? RefereeLicenseNo
* int? RefereeExperienceYears
* string? RefereeAssignmentStatus
* string? RefereeAssignedAt

Yêu cầu:

* Không xóa field cũ.
* Không đổi tên field cũ.
* Nếu cần thêm field DistanceMeters thì có thể thêm, nhưng vẫn phải giữ Distance hiện có.

Yêu cầu 2: Sửa OwnerRacesController.GetRaceDetail

File:
Controllers/Owner/OwnerRacesController.cs

Giữ nguyên route:
GET /api/owner/races/{raceId}

Không tạo route mới.

Logic quyền xem phải giữ:
Owner được xem Race Info khi:

* Race còn có thể đăng ký:
  RaceStatuses.CanRegister(r.Status)
  hoặc
* Owner đã có RaceRegistration với race đó:
  r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)

Không được làm mất điều kiện này.

Query cần lấy thêm:

* Race
* Tournament
* RaceRegistrations của Owner hiện tại
* Horse của registration
* Jockey của registration nếu có
* User của Jockey nếu có
* RefereeAssignments
* RefereeAssignment.Referee
* User của Referee nếu có

Lưu ý rất quan trọng:

* Tên navigation phải lấy theo model hiện tại.
* Không tự bịa navigation như registration.Jockey.JockeyNavigation hoặc assignment.Referee.Referee nếu model không có.
* Nếu navigation khác tên thì dùng đúng navigation đang có trong source.
* Nếu thiếu navigation thì dùng join/projection phù hợp theo DbContext hiện tại.
* Không tự thêm field hoặc navigation vào entity.

Thông tin registration của Owner:

* Lấy registration của owner hiện tại trong race này.
* Nếu có nhiều registration thì lấy bản mới nhất theo SubmittedAt DESC nếu field tồn tại, hoặc RegistrationId DESC.
* RegistrationId = registration.RegistrationId.
* RegistrationStatus = registration.Status.
* HorseName = registration.Horse.HorseName.
* OfficialJockeyName = tên User của Jockey đang gắn với registration nếu có JockeyId, nếu không có thì null.
* Tên navigation Jockey/User phải theo model thật.

Thông tin referee:

* Chỉ lấy assignment đang có Status = RefereeAssignmentStatuses.Assigned.
* Nếu có nhiều assignment Assigned, lấy bản mới nhất theo AssignedAt DESC.
* Nếu chưa có referee assignment thì các field referee trả null.
* RefereeAssignmentId = assignment.RefereeAssignmentId.
* RefereeId = assignment.RefereeId.
* RefereeName = tên User của referee theo navigation/model thật.
* RefereeEmail = email raw string, ví dụ "[referee@test.com](mailto:referee@test.com)", không trả markdown.
* RefereePhone = phone raw string.
* RefereeLicenseNo = license no nếu model có field này, nếu không có thì null.
* RefereeExperienceYears = experience years nếu model có field này, nếu không có thì null.
* RefereeAssignmentStatus = assignment.Status.
* RefereeAssignedAt = assignment.AssignedAt format string nếu field có.

Thông tin Tournament:

* TournamentId = r.TournamentId.
* TournamentName = r.Tournament.TournamentName.
* TournamentDescription = r.Tournament.Description nếu field có.
* TournamentStatus = r.Tournament.Status.
* TournamentImageUrl = r.Tournament.ImageUrl nếu field có.
* PrizePool = r.Tournament.PrizePool nếu field có.
* Rules = r.Tournament.Rules nếu field có.
* TournamentStartDate = r.Tournament.StartDate.ToString("yyyy-MM-dd") nếu field có.
* TournamentEndDate = r.Tournament.EndDate.ToString("yyyy-MM-dd") nếu field có.
* Nếu Tournament không có StartDate/EndDate thì không tự tạo field model mới; chỉ map field có thật hoặc trả null.

Thông tin Race:

* RaceId = r.RaceId.
* RaceName = r.RaceName.
* RaceDate = r.RaceDate.ToString("yyyy-MM-dd").
* Location = r.Location nếu có, nếu null có thể fallback sang Tournament.Location nếu model có.
* Distance = r.DistanceMeters.
* MaxHorses = r.MaxHorses.
* Status = r.Status.
* JockeySelectionDeadline = r.JockeySelectionDeadline nullable, format "yyyy-MM-dd HH:mm" nếu field có.
* PredictionDeadline = r.PredictionDeadline nullable, format "yyyy-MM-dd HH:mm" nếu field có.
* Nếu model Race không có một field nào trong danh sách trên thì không tự tạo field mới, map null hoặc bỏ mapping theo DTO nullable.

Yêu cầu code:

* Có thể dùng projection Select để tránh Include quá nặng.
* Không trả thẳng entity.
* Không tự bịa field không có trong model.
* Nếu field nullable thì map null an toàn.
* Không để NullReferenceException khi chưa có registration, chưa có jockey, chưa có referee.
* Không đổi response NotFound hiện có nếu không cần.
* Không sửa InviteJockey trong OwnerRacesController nếu không liên quan.
* Response API phải là raw JSON string, không trả markdown trong value.

Response mẫu mong muốn:
{
"raceId": 4,
"tournamentId": 4,
"tournamentName": "Marco",
"tournamentDescription": "...",
"tournamentStatus": "OpenRegistration",
"tournamentImageUrl": "/uploads/tournaments/xxx.png",
"prizePool": 10000000,
"rules": "...",
"tournamentStartDate": "2026-06-20",
"tournamentEndDate": "2026-06-30",

"raceName": "Marco Race",
"raceDate": "2026-06-30",
"location": "HCM",
"distance": 1000,
"maxHorses": 10,
"status": "Scheduled",
"jockeySelectionDeadline": "2026-06-25 09:00",
"predictionDeadline": "2026-06-28 09:00",

"registrationId": 10,
"registrationStatus": "Approved",
"horseName": "Hello",
"officialJockeyName": null,

"refereeAssignmentId": 3,
"refereeId": 12,
"refereeName": "Referee Test",
"refereeEmail": "[referee@test.com](mailto:referee@test.com)",
"refereePhone": "0900000000",
"refereeLicenseNo": "RF001",
"refereeExperienceYears": 5,
"refereeAssignmentStatus": "Assigned",
"refereeAssignedAt": "2026-06-22 10:30"
}

Test cần đạt:

1. Owner gọi GET /api/owner/races/{raceId} với race còn CanRegister:

   * 200 OK.
   * Có Tournament info.
   * Có Race info.
   * Registration fields có thể null nếu Owner chưa đăng ký.
   * Referee fields null nếu chưa assign.

2. Owner gọi GET /api/owner/races/{raceId} với race đã đóng nhưng Owner đã có registration:

   * 200 OK.
   * Có RegistrationId, RegistrationStatus, HorseName.
   * Có OfficialJockeyName nếu registration đã có JockeyId.

3. Owner gọi race không mở đăng ký và không có registration:

   * 404 như hiện tại.

4. Race có referee assignment Assigned:

   * Trả đủ refereeName, email, phone, licenseNo, experienceYears nếu model có dữ liệu.

5. Race chưa có referee:

   * Không lỗi null.
   * Referee fields = null.

6. dotnet build pass.

Sau khi sửa xong chỉ trả lời tối đa 8 dòng:

1. File đã sửa
2. DTO đã thêm các field Race Info chưa
3. Route GET /api/owner/races/{raceId} có giữ nguyên không
4. Quyền xem race của Owner có giữ đúng không
5. Registration info đã map chưa
6. Referee info đã map chưa
7. Có tự tạo field/navigation/DB/migration/frontend không
8. Build command/lỗi còn lại nếu có
