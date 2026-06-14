Tôi đồng ý cho sửa.

Giai đoạn: làm Jockey Calendar.

Mục tiêu:

* Calendar chỉ dùng được sau khi Jockey đã được Admin duyệt.
* Jockey chưa verify email, đang chờ duyệt, bị reject/inactive không được vào Calendar.
* Race chỉ hiện trong Calendar khi Jockey đã nhận lời mời và race registration chuyển sang ReadyToRace hoặc trạng thái tương ứng.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

Luồng nghiệp vụ đúng:
Jockey đăng ký
=> Verify email
=> Bổ sung hồ sơ
=> Admin duyệt
=> Jockey Active
=> Jockey nhận lời mời race
=> RaceRegistration chuyển ReadyToRace
=> Race hiện trong Calendar

API cần kiểm tra/tạo:

* GET /api/jockey/calendar
* Nếu project đã có controller calendar thì sửa controller hiện có.
* Nếu chưa có thì tạo Controllers/Jockey/JockeyCalendarController.cs.

Query params gợi ý:

* month
* year
  Hoặc:
* startDate
* endDate
  Chọn theo convention hiện có, ưu tiên month/year cho màn hình calendar tháng.

Quyền truy cập:

* Token thiếu/sai => 401 theo pattern hiện có.
* User không phải Jockey => 403 Forbidden.
* Jockey chưa verify email => 403 Forbidden.
* user.Status != Active => 403 Forbidden.
* jockey.IsActive != true => 403 Forbidden.
* Chỉ Jockey Active mới xem được Calendar.

Dữ liệu Calendar lấy từ:

1. Available Days:

* jockey_availabilities + ngày trong tháng.
* Nếu chưa có bảng/entity jockey_availabilities thì không tự tạo DB, chỉ ghi rõ thiếu infrastructure.

2. Racing Day:

* race_registrations + races.
* Ngày có race lấy từ race_registrations.RaceId => races.RaceDate hoặc field ngày tương ứng.
* Chỉ lấy race registration của currentJockeyId.
* Chỉ lấy registration status ReadyToRace hoặc trạng thái tương ứng hiện có như Accepted/Confirmed/Approved.
* Không lấy race Cancelled nếu project có race status.

3. Race detail:

* RaceName từ races.RaceName/Name/Title.
* Location từ races.Location.
* Horse từ race_registrations.HorseId => horses.
* Owner từ race_registrations.OwnerId hoặc Horse.OwnerId => users/horse_owners theo model hiện có.
* Profile status từ users.Status.
* Jockey active từ jockeys.IsActive.

Response gợi ý:

{
"month": 7,
"year": 2024,
"profileStatus": "Active",
"isActive": true,
"days": [
{
"date": "2024-07-14",
"isAvailable": true,
"hasRace": true,
"items": [
{
"type": "Race",
"raceId": 5,
"raceName": "Grand Ascot Cup",
"raceDate": "2024-07-14T16:30:00Z",
"location": "Ascot Racecourse",
"horseId": 10,
"horseName": "Thunderbolt",
"ownerId": 3,
"ownerName": "Arthur Pendleton",
"registrationStatus": "ReadyToRace"
}
]
}
]
}

DTO cần tạo nếu chưa có:

* DTOs/Jockey/Calendar/JockeyCalendarResponse.cs
* DTOs/Jockey/Calendar/JockeyCalendarDayResponse.cs
* DTOs/Jockey/Calendar/JockeyCalendarItemResponse.cs

Yêu cầu xử lý Available Days:

* Với mỗi ngày trong tháng, kiểm tra có record jockey_availabilities tương ứng không.
* Nếu có thì isAvailable = true hoặc theo field status/isAvailable hiện có.
* Nếu không có record thì isAvailable = false hoặc null tùy convention, chọn cách ít phá frontend nhất.

Yêu cầu xử lý Racing Day:

* Nếu ngày có race của Jockey thì hasRace = true.
* items chứa danh sách race trong ngày đó.
* Race chỉ hiện nếu race registration thuộc currentJockeyId.
* Race chỉ hiện nếu registration đã được Jockey accept/ReadyToRace.
* Không hiển thị invitation Pending trong Calendar. Invitation Pending nằm ở Pending Invitations/Notifications.

Yêu cầu bảo vệ role khác:

* API này chỉ áp dụng cho role Jockey.
* Không sửa AuthController nếu không bắt buộc.
* Không sửa Owner/Admin/Staff flow.
* Không ảnh hưởng Dashboard, Notifications, Invitations nếu không cần.

Test cần đạt:

* Jockey Active gọi GET /api/jockey/calendar => 200 OK.
* Jockey chưa verify email gọi calendar => 403.
* Jockey Pending chờ duyệt gọi calendar => 403.
* Jockey bị reject/inactive gọi calendar => 403.
* Owner/Admin gọi calendar => 403.
* Jockey có race ReadyToRace => ngày đó hiện race.
* Jockey chỉ có invitation Pending nhưng chưa accept => race chưa hiện trong Calendar.
* Available Days lấy đúng từ jockey_availabilities nếu bảng/entity có sẵn.
* Build không lỗi.

Nếu thiếu entity/table/status:

* Không tự bịa database.
* Dùng đúng field hiện có.
* Ghi rõ phần thiếu và cần làm ở giai đoạn DB riêng.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route calendar
3. Query params đã dùng
4. Điều kiện chặn Jockey chưa Active
5. Available Days lấy từ đâu
6. Racing Day lấy từ đâu
7. Race chỉ hiện sau accept/ReadyToRace chưa
8. Có ảnh hưởng role khác không
9. Build command
10. Lỗi còn lại nếu có
