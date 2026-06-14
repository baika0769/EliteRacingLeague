Tôi đồng ý cho sửa.

Giai đoạn 3: làm API Accepted Races cho Jockey.

Mục tiêu:

* Tạo API riêng cho trang Accepted Races trên sidebar Jockey.
* Chỉ Jockey Active mới được xem.
* Dùng JockeyAccessService đã tạo ở giai đoạn trước nếu có.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

File cần thêm:

* Controllers/Jockey/JockeyRacesController.cs
* DTOs/Jockey/JockeyAcceptedRaceResponse.cs
* DTOs/Jockey/JockeyRaceDetailResponse.cs

API 1:
GET /api/jockey/races/accepted

Logic:

* Lấy currentJockeyId từ JockeyAccessService hoặc auth pattern hiện có.
* Chỉ lấy RaceRegistration của Jockey hiện tại.
* Status hợp lệ: ReadyToRace, Completed.
* Nếu project dùng tên status khác thì dùng đúng constants hiện có.
* Không lấy Pending, JockeyInvited, Rejected, Cancelled.

Query tương đương:
RaceRegistration.JockeyId = currentJockeyId
AND Status IN ReadyToRace, Completed

Response gợi ý:
{
"items": [
{
"raceRegistrationId": 10,
"raceId": 3,
"raceName": "Dubai Sprint Cup",
"raceDate": "2024-10-06T16:30:00",
"location": "Dubai Racecourse",
"horseId": 5,
"horseName": "Thunderbolt",
"ownerName": "Arthur Pendleton",
"status": "ReadyToRace"
}
]
}

DTO JockeyAcceptedRaceResponse cần có:

* RaceRegistrationId
* RaceId
* RaceName
* RaceDate
* Location
* HorseId
* HorseName
* OwnerName
* Status

API 2:
GET /api/jockey/races/{raceId}

Logic validate:

* Jockey phải Active.
* Race phải thuộc Jockey hiện tại thông qua RaceRegistration.
* Registration status phải là ReadyToRace hoặc Completed.
* Không cho xem race của Jockey khác.
* Nếu không tìm thấy hoặc không thuộc Jockey hiện tại thì trả 404 hoặc 403 theo pattern hiện có.

Response detail cần có tối thiểu:

* RaceRegistrationId
* RaceId
* RaceName
* RaceDate
* Location
* HorseId
* HorseName
* OwnerName
* Status
* Có thể thêm các field race detail hiện có nếu project đã có dữ liệu.

Yêu cầu code:

* Dùng JockeyAccessService nếu đã có.
* Dùng DbContext/repository theo convention hiện có.
* Include Race, Horse, Owner/User nếu model có navigation.
* Nếu field tên khác mô tả thì dùng đúng field hiện có.
* Không tự bịa field DB.
* Không ảnh hưởng Dashboard, Calendar, Notifications, Invitations.
* Không ảnh hưởng Owner/Admin/Staff.

Test cần đạt:

* Jockey Active gọi GET /api/jockey/races/accepted => 200.
* Jockey Pending gọi => 403.
* Owner/Admin gọi => 403.
* List chỉ có ReadyToRace và Completed.
* Detail race thuộc Jockey hiện tại => 200.
* Detail race của Jockey khác => bị chặn.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 9 dòng:

1. File đã tạo/sửa
2. Route list accepted races
3. Route detail race
4. Status nào được lấy
5. Có dùng JockeyAccessService không
6. Có chặn race người khác không
7. Có ảnh hưởng role khác không
8. Build command
9. Lỗi còn lại nếu có
