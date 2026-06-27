Tôi đồng ý cho sửa.

Giai đoạn BE: hoàn thiện Spectator phase gồm Season, prediction theo tournament, auto evaluate prediction, rewards, dashboard myRank, leaderboard và tournament horses.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code BE hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã có và đúng thì giữ nguyên.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi status flow hiện tại.
* Không tạo trùng controller/service/DTO.
* Không tạo class rỗng chỉ để hết lỗi.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Lưu ý database:

* Không tạo migration.
* Không tự sửa DB/schema bằng code.
* Giả định SQL Server đã được cập nhật riêng, gồm bảng `seasons` và cột `tournaments.season_id`.
* Nếu khi build/test runtime phát hiện DB thật chưa có `seasons` hoặc `tournaments.season_id`, chỉ báo rõ cần chạy SQL update riêng.
* Trong code chỉ map model/entity theo schema đã có.
* Không thêm `tournament_id` vào `race_predictions`. Prediction vẫn lưu bằng `race_id` và `predicted_registration_id`.

Điểm cần đúng theo dự án hiện tại:

* `Scheduled` là Race status, không phải Tournament status.
* Không dùng `Tournament.Status = Scheduled`.
* Tournament status hợp lệ phải dùng constant hiện có trong dự án, ví dụ `OpenRegistration`, `ClosedRegistration`, `Ongoing`, `Completed`, `Cancelled` nếu có.
* Race status phải dùng `RaceStatuses` hiện có.
* Prediction status phải dùng `RacePredictionStatuses`, không dùng `PredictionStatuses`.
* Reward status phải dùng `PredictionRewardStatuses` nếu đã có.
* Registration status phải dùng `RaceRegistrationStatuses` hiện có.
* Không tự tạo status mới nếu project đã có constants.
* Không đổi kiểu `DateOnly`/`DateTime` hiện có của model cũ nếu source đang dùng ổn định.

Mục tiêu API cần đạt:

API mới:

1. `GET /api/spectator/season/current`
2. `GET /api/spectator/tournaments/{id}/horses`
3. `GET /api/spectator/leaderboard/horses`
4. `GET /api/spectator/leaderboard/predictors`

API cũ cần sửa:

1. `GET /api/spectator/dashboard` thêm `myRank`
2. `GET /api/spectator/tournaments` thêm `hasPredicted`, `myPrediction`
3. `GET /api/spectator/predictions/my` thêm tournament fields
4. `POST /api/spectator/predictions` đổi sang `tournamentId + predictedHorseId`
5. `GET /api/spectator/rewards` thêm `myRank`, `totalDays`, `pointHistory` theo tournament

Giai đoạn 0: Kiểm tra source và build nền

Chạy:

```bash
dotnet build
```

Nếu build nền đang lỗi:

* Chỉ sửa lỗi build liên quan trực tiếp source hiện tại.
* Không thêm chức năng mới nếu build nền lỗi nặng chưa xử lý được.
* Báo rõ lỗi build nền nếu không xử lý được.

Trước khi sửa, kiểm tra nhanh:

```bash
rg -n "class Tournament|SeasonId|season_id" Models Data
rg -n "RacePredictionStatuses|PredictionStatuses|PredictionRewardStatuses" Constants Models Controllers Services
rg -n "RaceStatuses|TournamentStatuses|RaceRegistrationStatuses|RaceResultStatuses" Constants Models Controllers Services
rg -n "SpectatorDashboardController|SpectatorTournamentsController|SpectatorPredictionsController|SpectatorRewardsController" Controllers
rg -n "AddScoped" Program.cs
```

Giai đoạn 1: Thêm Season model và mapping DbContext

Nếu chưa có thì tạo:

`Models/Season.cs`

Model:

```csharp
public partial class Season
{
    public int SeasonId { get; set; }
    public string SeasonName { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = null!;
    public int PointsPerCorrectPrediction { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}
```

Nếu chưa có thì tạo:

`Constants/SeasonStatuses.cs`

```csharp
public static class SeasonStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    {
        Draft,
        Active,
        Closed,
        Cancelled
    };
}
```

Sửa `Models/Tournament.cs`:

* Thêm `SeasonId` và navigation `Season`.
* Nếu SQL đã ép `tournaments.season_id NOT NULL` thì dùng:

```csharp
public int SeasonId { get; set; }
public virtual Season Season { get; set; } = null!;
```

* Nếu source/DB hiện tại vẫn để nullable thì dùng nullable, nhưng không tự sửa DB trong task này.
* Không xóa field cũ.
* Không đổi kiểu ngày tháng hiện có của Tournament.

Sửa `Data/EliteRacingLeagueContext.cs`:

* Thêm `DbSet<Season> Seasons`.
* Map table `seasons`:

  * `season_id`
  * `season_name`
  * `start_date`
  * `end_date`
  * `status`
  * `points_per_correct_prediction`
  * `created_at`
  * `updated_at`
* Map `Tournament.SeasonId` với column `season_id`.
* Map relationship `Season -> Tournaments`.
* Không tạo migration.

Giai đoạn 2: Sửa DTO Spectator request

File:

`DTOs/Spectator/SpectatorRequests.cs`

Sửa hoặc thêm request đúng:

```csharp
public class CreatePredictionRequest
{
    public int TournamentId { get; set; }
    public int PredictedHorseId { get; set; }
}
```

Yêu cầu:

* `POST /api/spectator/predictions` không còn yêu cầu FE gửi `raceId`.
* `POST /api/spectator/predictions` không còn yêu cầu FE gửi `predictedRegistrationId`.
* BE tự tìm race và registration từ `tournamentId + predictedHorseId`.

Giai đoạn 3: Thêm DTO Spectator response nếu chưa có

Nếu chưa có DTO phù hợp thì tạo:

`DTOs/Spectator/SpectatorLeaderboardDtos.cs`

Tối thiểu cần các DTO response sau, đặt tên theo convention source hiện tại:

Current season response:

* `int SeasonId`
* `string SeasonName`
* `DateTime StartDate`
* `DateTime EndDate`
* `int DaysLeft`
* `int TotalDays`
* `int TotalPredictors`
* `int TotalPredictions`

Predictor leaderboard item:

* `int Rank`
* `int SpectatorId`
* `string SpectatorName`
* `int Points`
* `int CorrectPredictions`
* `decimal Accuracy`
* `int TotalPredictions`

Horse leaderboard item:

* `int Rank`
* `int HorseId`
* `string HorseName`
* `string? OwnerName`
* `string? ImageUrl`
* `string? BreedName`
* `int Wins`
* `int TotalRaces`
* `decimal WinRate`

Tournament horse item:

* `int RegistrationId`
* `int HorseId`
* `string HorseName`
* `string? ImageUrl`
* `string? BreedName`
* `int? Age`
* `string? HealthStatus`
* `string RegistrationStatus`
* `string? OwnerName`
* `string? JockeyName`

Không tạo trùng DTO nếu source đã có DTO tương tự.

Giai đoạn 4: Thêm SpectatorLeaderboardService

Nếu chưa có thì tạo:

`Services/SpectatorLeaderboardService.cs`

Service dùng `EliteRacingLeagueContext`.

Cần method hỗ trợ:

1. Lấy active season.
2. Lấy `myRank` của spectator trong active season.
3. Lấy leaderboard predictors trong active season.
4. Lấy leaderboard horses trong active season.
5. Tính reward summary nếu dùng chung cho Dashboard/Rewards.

Rank predictor theo thứ tự:

1. `points DESC`
2. `correctPredictions DESC`
3. `accuracy DESC`
4. `totalPredictions DESC`
5. `spectatorId ASC`

Điểm predictor tính từ:

```txt
RacePredictions.PointsAwarded
```

Accuracy:

```txt
correctPredictions / totalPredictions * 100
```

Nếu `totalPredictions = 0` thì accuracy = 0.

Yêu cầu:

* Không trả entity trực tiếp.
* Query read-only phải dùng `AsNoTracking`.
* Không dùng `PredictionStatuses`.
* Dùng `RacePredictionStatuses`.
* Không hard-code status nếu source đã có constants phù hợp.
* Không dùng raw SQL nếu LINQ làm được ổn định.

Giai đoạn 5: Thêm PredictionEvaluationService

Nếu chưa có thì tạo:

`Services/PredictionEvaluationService.cs`

Service dùng:

* `EliteRacingLeagueContext`

Method chính:

```csharp
Task EvaluateRacePredictionsAsync(int raceId)
```

Logic:

1. Tìm winner của race:

   * `RaceResult.RaceId == raceId`
   * `FinishPosition == 1`
   * Status thuộc result approved/published theo constants thật trong source, ví dụ `RaceResultStatuses.AdminApproved` hoặc `RaceResultStatuses.Published` nếu có.
2. Nếu chưa có winner thì không evaluate.
3. Lấy points:

   * Ưu tiên `Season.PointsPerCorrectPrediction` của season thuộc tournament/race đó.
   * Nếu không lấy được thì default `100`.
4. Lấy các `RacePrediction` của race:

   * chưa `Evaluated`
   * chưa `Cancelled`
5. Với mỗi prediction:

   * `ActualWinnerRegistrationId = winnerRegistrationId`
   * `IsCorrect = prediction.PredictedRegistrationId == winnerRegistrationId`
   * `PointsAwarded = pointsPerCorrectPrediction` nếu đúng, ngược lại `0`
   * `Status = RacePredictionStatuses.Evaluated`
   * `RewardStatus = PredictionRewardStatuses.Pending` nếu đúng, ngược lại `PredictionRewardStatuses.None`
   * `EvaluatedAt = now`
   * cập nhật `UpdatedAt` nếu entity có field này
6. Nếu spectator đoán đúng thì tạo notification:

   * `Title = "Prediction Correct"`
   * `Message = "You predicted the winner correctly."`
   * `IsRead = false`
   * `CreatedAt = now`
   * Nếu Notification entity có `Type`, `RelatedId`, `RelatedType` thì map theo field thật.
   * Không tự thêm field DB mới.

Yêu cầu:

* Không tạo reward table mới.
* Không tạo migration.
* Không evaluate prediction `Cancelled`.
* Không evaluate trùng prediction đã `Evaluated`.
* Không phá PrizeAward logic hiện có.

Giai đoạn 6: Sửa AdminRaceResultsController gọi evaluate

File:

`Controllers/Admin/AdminRaceResultsController.cs`

Inject thêm:

```csharp
private readonly PredictionEvaluationService _predictionEvaluationService;
```

Constructor thêm service theo style hiện tại.

Trong action approve result:

* Giữ nguyên logic approve result hiện có.
* Không đổi route/response nếu không cần.
* Không phá `PrizeAward` logic nếu có.
* Chỉ gọi evaluate sau khi approve thành công.
* Sau khi result được approve và SaveChanges hợp lý, gọi:

```csharp
await _predictionEvaluationService.EvaluateRacePredictionsAsync(result.RaceId);
```

Yêu cầu:

* Không gọi evaluate nếu approve thất bại.
* Không đổi điều kiện admin approve hiện tại.
* Không đổi status flow hiện tại.

Giai đoạn 7: Thêm SpectatorSeasonController

Nếu chưa có thì tạo:

`Controllers/Spectator/SpectatorSeasonController.cs`

Route:

```csharp
[Route("api/spectator/season")]
```

API:

```http
GET /api/spectator/season/current
```

Logic:

* Lấy Season có `Status = SeasonStatuses.Active`.
* Nếu nhiều Active thì lấy mới nhất theo `StartDate DESC`, sau đó `SeasonId DESC`.
* Nếu không có Active thì trả 404 với message rõ ràng.
* Tính:

  * `daysLeft = max(0, endDate.Date - today.Date)`
  * `totalDays = endDate.Date - startDate.Date + 1`
  * `totalPredictors = count distinct spectator đã prediction trong season`
  * `totalPredictions = count prediction trong season`
* Không fake data.

Response cần có:

```json
{
  "seasonId": 1,
  "seasonName": "Season 2026",
  "startDate": "2026-01-01T00:00:00Z",
  "endDate": "2026-12-31T23:59:59Z",
  "daysLeft": 45,
  "totalDays": 365,
  "totalPredictors": 128,
  "totalPredictions": 340
}
```

Giai đoạn 8: Sửa SpectatorPredictionsController

File:

`Controllers/Spectator/SpectatorPredictionsController.cs`

Sửa:

```http
POST /api/spectator/predictions
```

Payload mới:

```json
{
  "tournamentId": 3,
  "predictedHorseId": 12
}
```

Logic:

1. Lấy `spectatorId` từ token theo helper hiện có.
2. Lấy tournament theo `TournamentId`.
3. Lấy race thuộc tournament.
4. Check tournament tồn tại.
5. Check race tồn tại.
6. Check tournament không `Cancelled`, không `Completed`.
7. Không dùng `Scheduled` làm tournament status.
8. Check race chưa đóng prediction:

   * Nếu có helper `RaceStatuses.IsClosedForPrediction(race.Status)` thì dùng helper.
   * Nếu không có helper thì dùng constants race status hiện có để chặn `Ongoing`, `Finished`, `ResultPending`, `Published`, `Cancelled`.
9. Check deadline:

   * Nếu `race.PredictionDeadline` có giá trị thì `now <= PredictionDeadline`.
   * Nếu project có date time provider thì dùng provider hiện có.
   * Nếu không có thì dùng `DateTime.UtcNow`.
10. Check spectator chưa predict tournament này:

* Không dựa vào raceId từ FE.
* Query:

  * `p.SpectatorId == spectatorId`
  * `p.Status != RacePredictionStatuses.Cancelled`
  * `p.Race.TournamentId == request.TournamentId`

11. Nếu đã predict thì trả `409 Conflict`:

```json
{
  "error": "You have already predicted for this tournament.",
  "message": "You have already predicted for this tournament."
}
```

12. Tìm registration:

* `RaceId == race.RaceId`
* `HorseId == request.PredictedHorseId`
* status nằm trong predictable registration statuses.

13. Predictable registration statuses chỉ gồm constants thật đang có, ưu tiên:

* `Approved`
* `JockeyInvited`
* `ReadyToRace`

14. Nếu horse không thuộc tournament/race thì trả `400 BadRequest`:

```json
{
  "error": "Horse is not registered in this tournament.",
  "message": "Horse is not registered in this tournament."
}
```

15. Tạo `RacePrediction`:

* `RaceId = race.RaceId`
* `SpectatorId = spectatorId`
* `PredictedRegistrationId = registration.RegistrationId`
* `Status = RacePredictionStatuses.Pending`
* `IsCorrect = null`
* `PointsAwarded = 0`
* `RewardStatus = PredictionRewardStatuses.None`
* `PredictedAt = now`
* `CreatedAt = now` nếu entity có field này

Sửa:

```http
GET /api/spectator/predictions/my
```

Response mỗi item cần có thêm:

```json
{
  "predictionId": 1,
  "tournamentId": 3,
  "tournamentName": "Dubai Sprint Cup",
  "tournamentStatus": "Ongoing",
  "raceId": 5,
  "raceName": "Dubai Sprint Race",
  "predictedHorseId": 5,
  "predictedHorseName": "Thunder",
  "actualWinnerHorseName": null,
  "isCorrect": null,
  "pointsAwarded": 0,
  "status": "Pending"
}
```

Yêu cầu:

* Giữ field cũ nếu FE cũ đang dùng.
* Không trả prediction của spectator khác.
* Không dùng `PredictionStatuses`.

Giai đoạn 9: Sửa SpectatorTournamentsController

File:

`Controllers/Spectator/SpectatorTournamentsController.cs`

Sửa:

```http
GET /api/spectator/tournaments
```

Mỗi tournament trả thêm:

```json
{
  "hasPredicted": true,
  "myPrediction": {
    "predictedHorseId": 5,
    "predictedHorseName": "Thunder",
    "isCorrect": null,
    "pointsAwarded": 0
  }
}
```

Yêu cầu:

* Dựa trên spectatorId hiện tại.
* Nếu chưa predict thì:

  * `hasPredicted = false`
  * `myPrediction = null`
* Không trả prediction của spectator khác.
* Không làm mất các field cũ FE đang dùng.

Thêm API:

```http
GET /api/spectator/tournaments/{id}/horses
```

Logic:

* Lấy tournament theo id.
* Lấy race thuộc tournament.
* Lấy registration của race có status predict được.
* Join horse, owner, jockey nếu có.
* Không lỗi null khi chưa có jockey.

Response mỗi item:

```json
{
  "registrationId": 10,
  "horseId": 5,
  "horseName": "Thunder",
  "imageUrl": null,
  "breedName": null,
  "age": 4,
  "healthStatus": "Healthy",
  "registrationStatus": "ReadyToRace",
  "ownerName": "John Smith",
  "jockeyName": "Ahmed Al-Rashid"
}
```

Yêu cầu:

* Chỉ lấy horse đã đăng ký trong race/tournament.
* Không trả horse ngoài tournament.
* Không bắt FE gửi registrationId khi predict.

Giai đoạn 10: Thêm SpectatorLeaderboardController

Nếu chưa có thì tạo:

`Controllers/Spectator/SpectatorLeaderboardController.cs`

Route:

```csharp
[Route("api/spectator/leaderboard")]
```

API:

```http
GET /api/spectator/leaderboard/horses
GET /api/spectator/leaderboard/predictors
```

Dùng `SpectatorLeaderboardService`.

Yêu cầu:

* Không trả entity trực tiếp.
* Trả top 50 mặc định nếu project chưa có convention phân trang.
* Ranking ổn định theo tie-breaker đã nêu.
* Chỉ tính trong active season nếu có active season.
* Nếu không có active season thì trả list rỗng hoặc 404 theo convention hiện tại, ưu tiên không làm FE crash.

Predictors response item:

```json
{
  "rank": 1,
  "spectatorId": 10,
  "spectatorName": "Nguyen Van A",
  "points": 450,
  "correctPredictions": 6,
  "accuracy": 85,
  "totalPredictions": 7
}
```

Horses response item:

```json
{
  "rank": 1,
  "horseId": 5,
  "horseName": "Thunder",
  "ownerName": "John Smith",
  "wins": 3,
  "totalRaces": 4,
  "winRate": 75
}
```

Giai đoạn 11: Sửa SpectatorDashboardController

File:

`Controllers/Spectator/SpectatorDashboardController.cs`

Sửa:

```http
GET /api/spectator/dashboard
```

Response cần có:

```json
{
  "upcomingTournaments": 5,
  "predictionsSubmitted": 12,
  "rewardPoints": 1250,
  "myRank": 3,
  "featuredTournament": {
    "tournamentId": 1,
    "tournamentName": "Dubai Sprint Cup",
    "status": "OpenRegistration",
    "location": "Dubai Meydan",
    "prizePool": 2000000,
    "race": {
      "raceDate": "2025-08-01T10:00:00Z",
      "distanceMeters": 2400
    }
  }
}
```

Yêu cầu:

* `myRank` lấy từ `SpectatorLeaderboardService`.
* `predictionsSubmitted` count theo spectator hiện tại.
* `rewardPoints` sum `PointsAwarded` theo spectator hiện tại.
* `featuredTournament` chọn tournament mở đăng ký/sắp tới theo logic hiện có.
* Không làm vỡ field cũ FE đang dùng.

Giai đoạn 12: Sửa SpectatorRewardsController

File:

`Controllers/Spectator/SpectatorRewardsController.cs`

Sửa:

```http
GET /api/spectator/rewards
```

Response cần có:

```json
{
  "rewardPoints": 1250,
  "correctPredictions": 8,
  "predictionAccuracy": 67,
  "myRank": 3,
  "totalDays": 92,
  "pointHistory": [
    {
      "tournamentId": 1,
      "tournamentName": "Dubai Sprint Cup",
      "points": 100,
      "awardedAt": "2025-08-10T12:00:00Z"
    }
  ]
}
```

Yêu cầu:

* `myRank` lấy từ `SpectatorLeaderboardService`.
* `totalDays` lấy từ active season nếu có; nếu không có season thì `0`.
* `pointHistory` lấy từ prediction của spectator hiện tại có `PointsAwarded > 0`.
* `awardedAt` ưu tiên `EvaluatedAt`, fallback `UpdatedAt`/`CreatedAt` nếu entity có.
* Không trả reward của spectator khác.
* Dùng `tournamentName`, không chỉ dùng `raceName`.

Giai đoạn 13: Program.cs đăng ký service

File:

`Program.cs`

Thêm nếu chưa có:

```csharp
builder.Services.AddScoped<SpectatorLeaderboardService>();
builder.Services.AddScoped<PredictionEvaluationService>();
```

Yêu cầu:

* Không đăng ký trùng.
* Thêm using namespace Services nếu cần.
* Giữ các service hiện có.

Giai đoạn 14: Build và search kiểm tra

Chạy:

```bash
dotnet build
```

Chạy search:

```bash
rg -n "Season" Models Constants Data Controllers Services DTOs
rg -n "SpectatorLeaderboardService|PredictionEvaluationService" .
rg -n "PredictionStatuses" Controllers/Spectator Controllers/Admin Services
rg -n "RacePredictionStatuses" Controllers/Spectator Controllers/Admin Services
rg -n "predictedHorseId|TournamentId|PredictedHorseId" Controllers DTOs
```

Yêu cầu:

* Không còn Spectator/Admin prediction flow dùng `PredictionStatuses`.
* POST spectator predictions dùng `TournamentId + PredictedHorseId`.
* Admin approve result gọi `PredictionEvaluationService`.
* `Program.cs` đã đăng ký service.
* `dotnet build` pass hoặc báo rõ lỗi còn lại.

Giai đoạn 15: Test API cần đạt

Test API mới:

1. `GET /api/spectator/season/current`
2. `GET /api/spectator/tournaments/{id}/horses`
3. `GET /api/spectator/leaderboard/horses`
4. `GET /api/spectator/leaderboard/predictors`

Test API đã sửa:

1. `GET /api/spectator/dashboard` có `myRank`.
2. `GET /api/spectator/tournaments` có `hasPredicted`, `myPrediction`.
3. `POST /api/spectator/predictions` nhận `tournamentId + predictedHorseId`.
4. POST prediction lần 2 cùng tournament trả `409 Conflict`.
5. POST prediction horse không thuộc tournament trả `400 BadRequest`.
6. `GET /api/spectator/predictions/my` trả đúng prediction của spectator hiện tại.
7. `GET /api/spectator/rewards` có `rewardPoints`, `correctPredictions`, `predictionAccuracy`, `myRank`, `totalDays`, `pointHistory`.
8. Admin approve result có winner thì prediction được `Evaluated`.
9. Spectator đoán đúng được cộng `PointsAwarded` và `RewardStatus = Pending`.
10. Spectator đoán sai `PointsAwarded = 0` và `RewardStatus = None`.

Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. File tạo mới
3. File đã sửa
4. Season model/SeasonStatuses/DbContext đã thêm chưa
5. POST spectator predictions đã đổi sang tournamentId + predictedHorseId chưa
6. Tournament horses API đã thêm chưa
7. PredictionEvaluationService đã hoạt động và được Admin approve gọi chưa
8. Leaderboard service/controller đã thêm chưa
9. Dashboard myRank và tournaments hasPredicted/myPrediction đã có chưa
10. Rewards response mới đã có chưa
11. Có sửa DB/migration/frontend không
12. dotnet build kết quả/lỗi còn lại
