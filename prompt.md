Tôi đồng ý cho sửa.

Giai đoạn BE: thêm API Owner xem Performance của từng ngựa.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* Chỉ sửa file cần thiết.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không tạo DTO mới nếu DTO đã có.
* Không refactor lớn.
* Không đổi nghiệp vụ cũ.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:
Thêm endpoint:

GET /api/owner/horses/{horseId}/performance

để Owner xem:

* Thông tin ngựa.
* Ảnh ngựa.
* Giống ngựa.
* Tuổi.
* Cân nặng.
* Owner.
* Assigned Jockey gần nhất nếu có.
* Champion Titles.
* Best Time.
* Current Win Streak.
* Award.
* Race History.

Giai đoạn 1: Xác định đúng file cần sửa

Chỉ sửa file:

Controllers/Owner/OwnerHorsesController.cs

Không sửa:

* Database
* Migration
* Frontend
* DTO mới
* Controller khác nếu không bắt buộc

DTO đã có sẵn trong:

DTOs/Owner/Results/

Cần kiểm tra đúng tên class/field DTO hiện tại, ví dụ:

* OwnerHorsePerformanceResponse
* OwnerHorsePerformanceInfoResponse
* OwnerHorseAchievementResponse
* OwnerHorseRaceHistoryResponse

Nếu tên field DTO khác với prompt thì dùng đúng tên field thật trong source, không tự đoán.

Giai đoạn 2: Thêm using nếu thiếu

Mở file:

Controllers/Owner/OwnerHorsesController.cs

Nếu chưa có thì thêm:

using Eliteracingleague.API.DTOs.Owner.Results;
using Eliteracingleague.API.Constants;

Lý do:
API mới dùng DTO trong DTOs/Owner/Results và dùng:

* RaceResultStatuses.AdminApproved
* RaceResultStatuses.Published

Giai đoạn 3: Tìm vị trí đặt method mới

Trong file:

Controllers/Owner/OwnerHorsesController.cs

Tìm method:

GetHorseStats

Sau khi method GetHorseStats kết thúc, thêm method mới vào trong class OwnerHorsesController, trước dấu } cuối cùng của class.

Nếu không có GetHorseStats thì đặt gần các endpoint GET detail/stats hiện có của OwnerHorsesController.

Không được đặt method ngoài class.

Giai đoạn 4: Thêm endpoint GetHorsePerformance

Thêm method:

[HttpGet("{horseId:int}/performance")]
public async Task<IActionResult> GetHorsePerformance(int horseId)

Logic bắt buộc:

1. Lấy Owner hiện tại:

var ownerId = GetCurrentUserId();

Nếu ownerId == null:
return InvalidToken();

2. Validate quyền Owner quản lý ngựa:

var ownerError = await ValidateOwnerCanManageHorsesAsync(ownerId.Value);

Nếu ownerError != null:
return ownerError;

Nếu project đang dùng tên method validate khác trong OwnerHorsesController thì dùng đúng method hiện có, không tự tạo method mới nếu không cần.

3. Lấy thông tin ngựa thuộc Owner hiện tại:

Query Horses:

* HorseId == horseId
* OwnerId == ownerId.Value

Select:

* HorseId
* HorseName
* BreedName
* ImageUrl
* Age
* WeightKg
* AchievementSummary hoặc field award tương ứng

Nếu không tìm thấy:
return NotFound(new
{
message = "Không tìm thấy ngựa hoặc bạn không có quyền xem ngựa này."
});

Yêu cầu bảo mật:
Owner chỉ xem được ngựa của chính mình.
Không được để Owner A xem ngựa của Owner B.

4. Lấy OwnerName từ Users theo ownerId.

5. Lấy AssignedJockeyName gần nhất:

* Từ RaceRegistrations
* HorseId == horseId
* JockeyId != null
* OrderByDescending Race.RaceDate
* Lấy tên User của Jockey theo navigation thật trong model hiện tại

Lưu ý:

* Không tự bịa navigation như JockeyNavigation nếu model không có.
* Mở Models/Jockey.cs và Models/RaceRegistration.cs để xem navigation thật.
* Nếu navigation là Jockey.User thì dùng Jockey.User.FullName.
* Nếu navigation là Jockey.JockeyNavigation thì dùng Jockey.JockeyNavigation.FullName.
* Nếu không có Jockey thì trả null.

6. Lấy RaceHistory đã được duyệt

visibleStatuses gồm:

* RaceResultStatuses.AdminApproved
* RaceResultStatuses.Published

Query RaceResults:

* r.Registration.HorseId == horseId
* r.Registration.OwnerId == ownerId.Value
* visibleStatuses.Contains(r.Status)

Order:

* Race.RaceDate DESC
* ResultId DESC

Map sang OwnerHorseRaceHistoryResponse:

* RaceId
* ResultId
* TournamentName
* RaceDate
* Track hoặc Location theo DTO thật
* DistanceMeters
* JockeyName
* Position
* FinishTime hoặc FinishTimeSeconds theo DTO thật
* Status

Lưu ý rất quan trọng:

* Kiểm tra DTOs/Owner/Results/OwnerHorseRaceHistoryResponse.cs để biết tên field thật.
* Nếu DTO dùng FinishTimeSeconds thì map FinishTimeSeconds = r.FinishTimeSeconds.
* Nếu DTO dùng FinishTime thì map FinishTime = r.FinishTimeSeconds hoặc field phù hợp trong RaceResult.
* Không tự thêm field DTO mới.

7. Tính Achievements

Tính:

* ChampionTitles = số raceHistory có Position == 1.
* BestTime = thời gian nhỏ nhất trong raceHistory có FinishTime.
* CurrentWinStreak = đếm từ raceHistory mới nhất trở xuống, dừng khi Position != 1.
* Award = horseInfo.Award hoặc horseInfo.AchievementSummary.

Nếu raceHistory rỗng:

* ChampionTitles = 0.
* BestTime = null.
* CurrentWinStreak = 0.
* RaceHistory = [].
* Không được lỗi 500.

8. Trả response OwnerHorsePerformanceResponse

Response gồm:

* Horse
* Achievements
* RaceHistory

Không trả thẳng entity.

Giai đoạn 5: Yêu cầu code an toàn

* Dùng AsNoTracking cho query read-only.
* Không để NullReferenceException.
* Không tự bịa field hoặc navigation.
* Nếu model/DTO khác tên field thì dùng đúng source hiện tại.
* Không tạo DTO mới.
* Không sửa database.
* Không migration.
* Không ảnh hưởng API cũ của OwnerHorsesController.
* Không lấy race result Draft, RefereeConfirmed, Returned, Cancelled.
* Chỉ lấy RaceResult AdminApproved hoặc Published.

Giai đoạn 6: Build kiểm tra lỗi

Sau khi thêm code, chạy:

dotnet build

Nếu build lỗi, xử lý theo nguyên nhân:

1. Thiếu using DTO:
   Thêm:
   using Eliteracingleague.API.DTOs.Owner.Results;

2. Thiếu using constants:
   Thêm:
   using Eliteracingleague.API.Constants;

3. Sai tên field DTO:
   Mở file DTOs/Owner/Results tương ứng và sửa mapping theo tên field thật.

4. Sai navigation property:
   Mở Models/Jockey.cs, Models/RaceRegistration.cs, Models/User.cs để dùng đúng navigation hiện có.

Không sửa bằng cách thêm field mới vào model/entity.

Giai đoạn 7: Test bằng Swagger/Postman

Test 1: Owner gọi đúng ngựa của mình

GET /api/owner/horses/1/performance
Authorization: Bearer <owner_token>

Kết quả:

* 200 OK
* Có horse
* Có achievements
* Có raceHistory
* Nếu chưa có race result thì raceHistory = []

Test 2: Owner gọi ngựa không thuộc mình

GET /api/owner/horses/999/performance

Kết quả:
404 Not Found

Response:
{
"message": "Không tìm thấy ngựa hoặc bạn không có quyền xem ngựa này."
}

Test 3: Ngựa chưa từng đua

Kết quả phải trả:

* ChampionTitles = 0
* BestTime = null
* CurrentWinStreak = 0
* RaceHistory = []

Không lỗi 500.

Giai đoạn 8: Điều kiện hoàn thành

Hoàn thành khi đạt đủ:

1. dotnet build pass.
2. Swagger thấy endpoint GET /api/owner/horses/{horseId}/performance.
3. Owner chỉ xem được ngựa của chính mình.
4. Ngựa chưa có race history vẫn không lỗi.
5. RaceHistory chỉ lấy kết quả AdminApproved hoặc Published.
6. Response trả đủ Horse, Achievements, RaceHistory.
7. Không tạo DTO mới.
8. Không sửa database.
9. Không migration.
10. Không ảnh hưởng API cũ của OwnerHorsesController.

Sau khi sửa xong chỉ trả lời tối đa 8 dòng:

1. File đã sửa
2. Endpoint đã thêm
3. DTO đang dùng
4. Owner security check đã đúng chưa
5. RaceHistory lọc AdminApproved/Published chưa
6. Ngựa chưa có race history có xử lý null/empty chưa
7. Có sửa DB/migration/DTO/frontend không
8. Build command/kết quả build
