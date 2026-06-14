Tôi đồng ý cho sửa.

Giai đoạn: hoàn chỉnh Jockey Calendar và Availability.

Mục tiêu:

* Làm API Calendar cho Jockey Active.
* Làm API lấy/cập nhật availability.
* Không đổi database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

Quy tắc nghiệp vụ Calendar:

* Calendar chỉ dùng được khi Jockey đã được Admin duyệt.
* Điều kiện được vào Calendar:

  * user.Role == Jockey
  * user.EmailVerified == true
  * user.Status == Active
  * jockey.IsActive == true
* Jockey chưa verify email, Pending, Inactive, Banned, bị reject đều không được vào Calendar.
* Owner/Admin/Staff gọi API Calendar phải trả 403.
* API Calendar chỉ đọc dữ liệu, không ghi database.

Quy tắc status của ngày:

* Available: Jockey rảnh.
* Unavailable: Jockey bận.
* RacingDay: không lưu vào jockey_availabilities, backend tự tính từ race đã accept/ReadyToRace.
* Không tạo 31 dòng availability cho mỗi tháng.
* Ngày không có record availability thì mặc định là Available.

Cách tính ngày trên lịch:

1. Ngày có race đã accept/ReadyToRace => status = RacingDay.
2. Nếu không có race nhưng có availability Unavailable => status = Unavailable.
3. Nếu không có race và không có availability record => status = Available.
4. Nếu có availability Available => status = Available.
5. RacingDay ưu tiên cao nhất, không bị availability ghi đè.

Cách tính summary:

* availableDays = số ngày trong tháng - số ngày RacingDay - số ngày Unavailable không trùng RacingDay.
* racingDays = số ngày có ít nhất 1 race ReadyToRace/Accepted/Confirmed của Jockey.
* Ví dụ tháng 10 có 31 ngày, có 2 ngày race, không có unavailable:

  * availableDays = 29
  * racingDays = 2

API 1: GET /api/jockey/calendar?month=2024-10

Mục đích:

* Render toàn bộ màn hình Calendar.

Query:

* month dạng yyyy-MM, ví dụ 2024-10.
* Nếu thiếu month thì dùng tháng hiện tại.

Dữ liệu lấy từ:

* users: Status, EmailVerified, Role.
* jockeys: IsActive.
* jockey_availabilities: status Available/Unavailable theo ngày.
* race_registrations + races: ngày có race.
* race_registrations.RaceId => races.RaceDate.
* race name từ races.RaceName/Name/Title theo field hiện có.
* location từ races.Location nếu có.
* horse từ race_registrations.HorseId => horses.
* owner từ race_registrations.OwnerId hoặc horse.OwnerId => users/horse_owners theo model hiện có.

Chỉ hiển thị race trong Calendar khi:

* race_registration thuộc currentJockeyId.
* race_registration status là ReadyToRace hoặc trạng thái tương ứng hiện có như Accepted/Confirmed/Approved.
* Không hiển thị invitation Pending trong Calendar.
* Nếu race status là Cancelled thì không hiển thị nếu project có status này.

Response gợi ý:

{
"month": "2024-10",
"availableDays": 29,
"racingDays": 2,
"days": [
{
"date": "2024-10-06",
"dayNumber": 6,
"status": "RacingDay",
"isCurrentMonth": true,
"races": [
{
"raceId": 1,
"raceName": "Dubai Sprint Cup",
"raceDate": "2024-10-06T16:30:00",
"location": "Dubai Racecourse",
"horseName": "Thunderbolt",
"status": "ReadyToRace"
}
]
},
{
"date": "2024-10-07",
"dayNumber": 7,
"status": "Available",
"isCurrentMonth": true,
"races": []
}
],
"nextRaces": [
{
"raceId": 1,
"raceName": "Dubai Sprint Cup",
"raceDate": "2024-10-06T16:30:00",
"location": "Dubai Racecourse",
"horseName": "Thunderbolt",
"prizeText": null
}
]
}

API 2: GET /api/jockey/availabilities?from=2024-10-01&to=2024-10-31

Mục đích:

* Frontend lấy trạng thái rảnh/bận nếu không cần toàn bộ Calendar.

Logic:

* Validate Jockey Active.
* Lấy availability của currentJockeyId trong khoảng from/to.
* Chỉ trả dữ liệu từ jockey_availabilities.
* Không trả RacingDay ở API này, vì RacingDay tính từ race trong Calendar.

Response gợi ý:

{
"items": [
{
"date": "2024-10-08",
"status": "Unavailable"
},
{
"date": "2024-10-09",
"status": "Available"
}
]
}

API 3: PUT /api/jockey/availabilities

Request:

{
"items": [
{
"date": "2024-10-08",
"status": "Unavailable"
},
{
"date": "2024-10-09",
"status": "Available"
}
]
}

Logic bắt buộc:

1. Lấy JockeyId từ token.
2. Validate user.Role == Jockey.
3. Validate user.EmailVerified == true.
4. Validate user.Status == Active và jockey.IsActive == true.
5. Validate status chỉ được Available hoặc Unavailable.
6. Không cho update ngày đã có race ReadyToRace/Accepted/Confirmed của Jockey.
7. Nếu ngày đã có record availability thì update.
8. Nếu chưa có record thì insert.
9. Không lưu RacingDay vào jockey_availabilities.
10. Save DB.

Response gợi ý:

{
"message": "Cập nhật lịch rảnh/bận thành công.",
"updatedCount": 2
}

File cần tạo/sửa nếu chưa có:

* Controllers/Jockey/JockeyCalendarController.cs
* Controllers/Jockey/JockeyAvailabilitiesController.cs hoặc gộp vào JockeyCalendarController nếu project convention đang gộp.
* DTOs/Jockey/Calendar/JockeyCalendarResponse.cs
* DTOs/Jockey/Calendar/JockeyCalendarDayResponse.cs
* DTOs/Jockey/Calendar/JockeyCalendarRaceResponse.cs
* DTOs/Jockey/Calendar/JockeyNextRaceResponse.cs
* DTOs/Jockey/Calendar/JockeyAvailabilityResponse.cs
* DTOs/Jockey/Calendar/UpdateJockeyAvailabilitiesRequest.cs
* DTOs/Jockey/Calendar/UpdateJockeyAvailabilityItemRequest.cs
* Constants/JockeyAvailabilityStatuses.cs nếu chưa có.

Constants cần có:

* Available
* Unavailable
* RacingDay chỉ dùng response calendar, không dùng để lưu DB.

Yêu cầu code:

* Kiểm tra entity/model hiện có trước khi sửa:

  * User
  * Jockey
  * JockeyAvailability
  * RaceRegistration
  * Race
  * Horse
  * Owner/User nếu cần
* Nếu tên entity/table/field khác mô tả, dùng đúng tên hiện có.
* Nếu chưa có jockey_availabilities entity/table, không tự tạo migration, chỉ báo rõ thiếu infrastructure.
* Không tự bịa field DB.
* Không sửa AuthController nếu không cần.
* Không ảnh hưởng Owner/Admin/Staff.
* Không in toàn bộ code ra terminal.

Test cần đạt:

* Jockey Active gọi GET /api/jockey/calendar?month=2024-10 => 200 OK.
* Jockey Pending gọi Calendar => 403.
* Jockey chưa verify email gọi Calendar => 403.
* Owner/Admin gọi Calendar => 403.
* Ngày có race ReadyToRace => status RacingDay.
* Ngày Unavailable không có race => status Unavailable.
* Ngày không có availability record và không có race => status Available.
* RacingDay không được lưu vào jockey_availabilities.
* availableDays tính đúng theo số ngày trong tháng.
* racingDays tính đúng theo số ngày có race.
* PUT availabilities không cho update ngày đã có race.
* PUT availabilities save Available/Unavailable đúng.
* Build không lỗi.

Sau khi xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route calendar
3. Route get availabilities
4. Route update availabilities
5. RacingDay có lưu DB không
6. Cách tính availableDays/racingDays
7. Có chặn Jockey chưa Active không
8. Có ảnh hưởng role khác không
9. Build command
10. Lỗi còn lại nếu có
