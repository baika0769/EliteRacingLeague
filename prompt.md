Tôi đồng ý cho sửa.

Giai đoạn 5: tạo Jockey Lookup API cho trang Jockey Settings.

Mục tiêu:

* Frontend không hard-code health status, distance, skill level, horse breed.
* Tạo API lookup cho frontend/Figma.
* Không sửa API GET profile/me.
* Không sửa API PUT verification.
* Không sửa AuthController.
* Không sửa AdminController.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API cần có:

1. GET /api/jockey/lookups/settings-options

Response cần có dạng:

{
"healthStatuses": [
"Healthy",
"NeedsCheck",
"Sick",
"Injured",
"Recovering",
"UnfitToRace"
],
"distanceOptions": [
{
"distanceMeters": 1000,
"label": "1000m Sprint"
},
{
"distanceMeters": 1500,
"label": "1500m Mid"
},
{
"distanceMeters": 2400,
"label": "2400m Endurance"
}
],
"skillLevels": [
"NoExperience",
"Basic",
"Good",
"Expert"
]
}

2. GET /api/jockey/lookups/horse-breeds

Response:

* Trả danh sách giống ngựa từ DB.
* Mỗi item gồm:

  * breedId
  * breedName
* Nếu bảng horse_breeds có IsActive/Status thì chỉ trả breed active.
* Nếu bảng horse_breeds không có IsActive/Status thì trả toàn bộ breed.

File cần thêm/sửa:

* Controllers/Jockey/JockeyLookupsController.cs
* Constants/JockeyDistances.cs
* Constants/JockeySkillLevels.cs
* Constants/JockeyHealthStatuses.cs
* DTO lookup response nếu project có convention DTO riêng.

Yêu cầu constants:

1. JockeyDistances

* 1000 => 1000m Sprint
* 1500 => 1500m Mid
* 2400 => 2400m Endurance

2. JockeySkillLevels

* NoExperience
* Basic
* Good
* Expert

3. JockeyHealthStatuses

* Healthy
* NeedsCheck
* Sick
* Injured
* Recovering
* UnfitToRace

Yêu cầu code:

* Kiểm tra project đã có constants tương tự chưa.
* Nếu đã có constants thì dùng lại hoặc bổ sung, không tạo trùng.
* Kiểm tra convention controller route hiện có.
* Route nên là:

  * [Route("api/jockey/lookups")]
  * [HttpGet("settings-options")]
  * [HttpGet("horse-breeds")]
* Dùng DbContext/repository theo pattern hiện có.
* Không làm thay đổi role/login flow.
* Không làm thay đổi status/isActive của Jockey.
* API lookup chỉ đọc dữ liệu, không ghi database.
* Nếu project đang dùng Authorize cho Jockey API thì áp dụng Authorize theo convention hiện có.
* Nếu lookup hiện tại của project public thì giữ theo convention hiện có.

Test cần đạt:

* Gọi GET /api/jockey/lookups/settings-options trả đúng healthStatuses, distanceOptions, skillLevels.
* Gọi GET /api/jockey/lookups/horse-breeds trả breed active từ DB.
* Frontend có thể lấy breed từ DB, không cần hard-code breed.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 8 dòng:

1. File đã tạo/sửa
2. Route settings-options
3. Route horse-breeds
4. Constants đã tạo/sửa
5. Horse breeds lấy active hay toàn bộ
6. Có ảnh hưởng role/login không
7. Build command
8. Lỗi còn lại nếu có
