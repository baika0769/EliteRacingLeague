Tôi đồng ý cho sửa.

Giai đoạn BE: làm API Owner Result & Reward cho trang Reward Center.

Lưu ý quan trọng:

* Chỉ đọc và sửa trên source code hiện tại.
* File nào đã có và đúng thì giữ nguyên, không sửa lại.
* Không tạo trùng controller/DTO/constants.
* Không sửa database.
* Không migration.
* Không sửa FE.
* Không refactor lớn.
* Không sửa SpectatorRewardsController.
* Không dùng nhầm PredictionRewardStatuses cho PrizeAward.
* Không in code dài ra terminal.

Mục tiêu tổng:

1. Kiểm tra nền build trước khi thêm chức năng.
2. Tạo constants riêng cho PrizeAward.
3. Tạo DTO riêng cho Owner Result & Reward.
4. Khi Admin approve race result thì tự tạo/cập nhật PrizeAward.
5. Tạo API Owner Rewards.
6. Tạo API Owner Results.
7. Đảm bảo không ảnh hưởng Admin, Referee, Jockey, Spectator.

Bổ sung:
- Khi tạo/cập nhật PrizeAward, ưu tiên kiểm tra theo unique index hiện có RaceId + RankPosition để tránh lỗi unique key. Nếu đã có award cho RaceId + RankPosition thì update, không insert mới.
- Nếu PrizeAward có UpdatedAt thì cập nhật UpdatedAt = DateTime.UtcNow khi update.
- Nếu entity dùng FinishTimeSeconds thì map sang DTO FinishTime, không đổi schema/model.
- Chỉ dùng RaceResultStatuses.Published nếu constants hiện tại đã có. Không tự tạo status mới nếu project chưa có.

Giai đoạn 0: Kiểm tra nền trước khi sửa

File cần kiểm tra:

* Controllers/Admin/AdminRaceResultsController.cs
* Controllers/Owner
* Models/PrizeAward.cs
* Models/RaceResult.cs
* Models/RaceRegistration.cs
* Models/PrizeRule.cs
* Models/Tournament.cs
* Models/Race.cs
* Data/EliteRacingLeagueContext.cs

Việc cần làm:

* Chạy dotnet build trước.
* Nếu có lỗi build cũ thì sửa lỗi build trước.
* Ví dụ lỗi object initializer trùng field như:
  Id = tournament.TournamentId,
  Id = tournament.TournamentId,
* Không thêm API mới nếu nền đang build lỗi.
* Chỉ sửa lỗi build trực tiếp, không refactor.

Giai đoạn 1: Tạo constants riêng cho PrizeAward

File cần tạo nếu chưa có:

* Constants/PrizeAwardStatuses.cs

Nội dung logic:
namespace Eliteracingleague.API.Constants;

public static class PrizeAwardStatuses
{
public const string ReadyToClaim = "ReadyToClaim";
public const string UnderReview = "UnderReview";
public const string Paid = "Paid";
public const string Rejected = "Rejected";

```
public static readonly string[] All =
{
    ReadyToClaim,
    UnderReview,
    Paid,
    Rejected
};

public static bool IsValid(string? status)
{
    return !string.IsNullOrWhiteSpace(status)
        && All.Contains(status);
}

public static bool CanClaim(string? status)
{
    return status == ReadyToClaim;
}
```

}

Ý nghĩa:

* ReadyToClaim: Owner có thể bấm Claim.
* UnderReview: Owner đã claim, chờ xử lý.
* Paid: Đã thanh toán.
* Rejected: Claim bị từ chối nếu sau này có admin xử lý.

Không dùng:

* PredictionRewardStatuses
* RaceRegistrationStatuses
* TournamentStatuses

Giai đoạn 2: Tạo DTO riêng cho Owner Result & Reward

Tạo thư mục nếu chưa có:

* DTOs/Owner/Rewards

Tạo các DTO nếu chưa có:

1. DTOs/Owner/Rewards/OwnerRewardSummaryResponse.cs

Fields:

* decimal TotalPrizeEarned
* decimal ClaimedRewards
* int TournamentWins

2. DTOs/Owner/Rewards/OwnerAvailableRewardResponse.cs

Fields:

* int PrizeAwardId
* string TournamentName
* DateTime RaceDate
* string HorseName
* int RankPosition
* decimal PrizeAmount
* string Status
* bool CanClaim

3. DTOs/Owner/Rewards/OwnerHorseResultResponse.cs

Fields:

* int ResultId
* int RaceId
* int RegistrationId
* int? RankPosition
* string HorseName
* string HorseBreed
* string TournamentName
* string? JockeyName
* decimal? FinishTime
* string Status

4. DTOs/Owner/Rewards/OwnerHorseResultDetailResponse.cs

Fields:

* int ResultId
* int RaceId
* int RegistrationId
* string TournamentName
* string RaceName
* DateTime RaceDate
* string HorseName
* string HorseBreed
* string? JockeyName
* int? RankPosition
* decimal? FinishTime
* decimal? Score
* string ResultStatus
* decimal? PrizeAmount
* string? RewardStatus
* DateTime? PublishedAt

Yêu cầu:

* Không trả thẳng entity database ra FE.
* Dùng PascalCase theo convention C# hiện tại, JSON serializer sẽ tự map nếu project đang cấu hình camelCase.

Giai đoạn 3: Khi Admin approve result, tự tạo hoặc cập nhật PrizeAward

File cần sửa:

* Controllers/Admin/AdminRaceResultsController.cs

Endpoint cần sửa:
PUT /api/admin/results/{id}/approve

Logic hiện tại chỉ set:

* RaceResult.Status = AdminApproved
* PublishedAt = DateTime.UtcNow

Cần bổ sung sau khi approve:

1. Tìm RaceResult theo id.
2. Kiểm tra result có FinishPosition.
3. Nếu FinishPosition null thì chỉ approve result, không tạo PrizeAward.
4. Load RaceRegistration tương ứng theo result.RegistrationId.
5. Lấy OwnerId, JockeyId từ RaceRegistration.
6. Tìm PrizeRule theo:

   * RaceId = result.RaceId
   * RankPosition = result.FinishPosition
7. Nếu không có PrizeRule thì không tạo PrizeAward, vẫn approve result.
8. Nếu có PrizeRule thì tạo hoặc cập nhật PrizeAward.
9. PrizeAward.Status mặc định = PrizeAwardStatuses.ReadyToClaim.

Dữ liệu cần set trong PrizeAward:

* RaceId = result.RaceId
* RegistrationId = result.RegistrationId
* OwnerId = registration.OwnerId
* JockeyId = registration.JockeyId
* RankPosition = result.FinishPosition.Value
* PrizeAmount = prizeRule.PrizeAmount
* Status = PrizeAwardStatuses.ReadyToClaim
* CreatedAt = DateTime.UtcNow nếu tạo mới

Chống tạo trùng:

* Trước khi insert, kiểm tra PrizeAward theo RegistrationId hoặc theo RaceId + RegistrationId nếu hợp lý với model hiện tại.
* Lưu ý Data/EliteRacingLeagueContext.cs hiện có unique index PrizeAward theo RaceId + RankPosition.
* Cần tránh insert trùng gây lỗi unique key.
* Nếu award đã có thì update:

  * RankPosition
  * PrizeAmount
  * JockeyId
  * Status chỉ đưa về ReadyToClaim nếu chưa Paid.
* Không tự tạo award mới nếu đã có award phù hợp.

Không làm:

* Không tạo PrizeAward khi result chưa được admin approve.
* Không tạo PrizeAward nếu không có PrizeRule.
* Không sửa SpectatorRewardsController.
* Không sửa database/index.

Response approve có thể giữ như hiện tại, nhưng nên thêm thông tin ngắn nếu tạo được award:

* prizeAwardCreated hoặc prizeAwardId nếu tiện.
* Không bắt buộc đổi response contract lớn.

Giai đoạn 4: Tạo OwnerRewardsController

File cần tạo nếu chưa có:

* Controllers/Owner/OwnerRewardsController.cs

Route:

* api/owner/rewards

Yêu cầu:

* Dùng [Authorize(Roles = UserRoles.HorseOwner)].
* Kế thừa OwnerBaseController nếu phù hợp.
* Dùng GetCurrentUserId().
* Dùng ValidateOwnerProfileAsync(ownerId).
* Chỉ Owner Active mới được gọi.
* Owner chỉ xem reward của chính mình.

API 1:
GET /api/owner/rewards/summary

Logic:

* ownerId = current user id theo pattern OwnerBaseController.
* totalPrizeEarned = SUM PrizeAwards.PrizeAmount WHERE OwnerId = ownerId.
* claimedRewards = SUM PrizeAwards.PrizeAmount WHERE OwnerId = ownerId AND Status = Paid.
* tournamentWins = COUNT PrizeAwards WHERE OwnerId = ownerId AND RankPosition = 1.

Response:
OwnerRewardSummaryResponse

Ví dụ:
{
"totalPrizeEarned": 125000,
"claimedRewards": 95000,
"tournamentWins": 8
}

API 2:
GET /api/owner/rewards/available?limit=10

Logic:

* Lấy các PrizeAward của owner hiện tại.
* Status IN ReadyToClaim, UnderReview, Paid.
* Join qua PrizeAward.Registration -> Horse.
* Join qua PrizeAward.Race -> Tournament.
* Sort mới nhất theo RaceDate hoặc CreatedAt giảm dần.
* limit mặc định 10, clamp hợp lý 1-50.

Response:
List<OwnerAvailableRewardResponse>

Fields:

* PrizeAwardId
* TournamentName
* RaceDate
* HorseName
* RankPosition
* PrizeAmount
* Status
* CanClaim = PrizeAwardStatuses.CanClaim(status)

API 3:
PUT /api/owner/rewards/{prizeAwardId}/claim

Logic:

1. Lấy ownerId.
2. Validate Owner Active.
3. Tìm PrizeAward theo prizeAwardId.
4. Nếu không tồn tại hoặc OwnerId != current owner thì trả 404 hoặc 403 theo pattern hiện có.
5. Chỉ cho claim nếu Status = ReadyToClaim.
6. Nếu status khác ReadyToClaim thì trả 400.
7. Set Status = UnderReview.
8. SaveChanges.

Response:
{
"message": "Reward claim submitted successfully.",
"prizeAwardId": 1,
"status": "UnderReview"
}

Giai đoạn 5: Tạo OwnerResultsController

File cần tạo nếu chưa có:

* Controllers/Owner/OwnerResultsController.cs

Route:

* api/owner/results

Yêu cầu:

* Dùng [Authorize(Roles = UserRoles.HorseOwner)].
* Kế thừa OwnerBaseController nếu phù hợp.
* Dùng GetCurrentUserId().
* Dùng ValidateOwnerProfileAsync(ownerId).
* Chỉ Owner Active mới được gọi.
* Owner chỉ xem result của ngựa thuộc mình.

API 1:
GET /api/owner/results?season=2026&tournamentId=&limit=10

Logic:

* Lấy RaceResult của các RaceRegistration có OwnerId = current owner.
* Chỉ lấy result đã được Admin duyệt hoặc published:

  * RaceResultStatuses.AdminApproved
  * RaceResultStatuses.Published
* Join:

  * race_results
  * race_registrations
  * horses
  * horse_breeds
  * jockeys
  * users
  * races
  * tournaments
* Filter season nếu có:

  * theo Race.RaceDate.Year == season.
* Filter tournamentId nếu có:

  * Race.TournamentId == tournamentId.
* limit mặc định 10, clamp hợp lý 1-50.
* Sort theo RaceDate giảm dần hoặc FinishPosition tăng dần nếu phù hợp.

Response:
List<OwnerHorseResultResponse>

Fields:

* ResultId
* RaceId
* RegistrationId
* RankPosition = FinishPosition
* HorseName
* HorseBreed
* TournamentName
* JockeyName
* FinishTime = FinishTimeSeconds
* Status

API 2:
GET /api/owner/results/{resultId}

Logic:

1. Lấy ownerId.
2. Validate Owner Active.
3. Tìm RaceResult theo resultId.
4. Chỉ cho xem nếu RaceResult.Registration.OwnerId == current owner.
5. Chỉ cho xem nếu result status là AdminApproved hoặc Published.
6. Join PrizeAward nếu có theo RegistrationId hoặc RaceId + RegistrationId.
7. Trả detail gồm tournament, race, horse, jockey, rank, finish time, score, prizeAmount, rewardStatus, publishedAt.

Response:
OwnerHorseResultDetailResponse

Giai đoạn 6: Dữ liệu test

Không bắt buộc tạo seed nếu project hiện không có seed.
Nhưng sau khi code xong cần ghi rõ để test flow cần có data:

1. Owner active.
2. Horse của Owner.
3. Jockey active và Fit.
4. Tournament.
5. Race.
6. PrizeRule cho race.
7. RaceRegistration approved hoặc ready/completed.
8. RaceResult admin approved.
9. PrizeAward được tạo tự động.

Nếu thiếu PrizeRule thì Admin approve result xong không tạo reward.
Cần kiểm tra DB:
SELECT * FROM prize_rules;

Nếu bảng trống, tạo prize rule mẫu thủ công:

* rank_position = 1, prize_amount = 50000
* rank_position = 2, prize_amount = 30000
* rank_position = 3, prize_amount = 15000

Không tạo migration để thêm seed.

Giai đoạn 7: Test toàn bộ flow

Test BE:

1. dotnet build.
2. Admin approve result.
3. Kiểm tra bảng prize_awards có dữ liệu mới.
4. Status award = ReadyToClaim.
5. Owner gọi GET /api/owner/rewards/summary.
6. Owner gọi GET /api/owner/rewards/available.
7. Owner gọi PUT /api/owner/rewards/{id}/claim.
8. Sau claim status = UnderReview.
9. Owner gọi GET /api/owner/results.
10. Owner gọi GET /api/owner/results/{id}.

Test ảnh hưởng role khác:

* Admin approve result vẫn chạy.
* Referee nhập result không bị ảnh hưởng.
* Jockey không bị ảnh hưởng.
* SpectatorRewards vẫn chạy riêng.
* Owner không xem được reward/result của owner khác.

Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. File đã tạo mới
3. File đã sửa
4. PrizeAwardStatuses đã tạo chưa
5. DTO Rewards đã tạo chưa
6. Admin approve result đã tự tạo/cập nhật PrizeAward chưa
7. OwnerRewardsController routes
8. OwnerResultsController routes
9. Có dùng OwnerBaseController/ValidateOwnerProfileAsync không
10. Có ảnh hưởng SpectatorRewards không
11. Build command
12. Lỗi còn lại nếu có
