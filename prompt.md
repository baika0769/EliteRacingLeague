Tôi đồng ý cho sửa.

Giai đoạn BE: chuẩn hóa constants trạng thái Race / RaceResult / Prediction để tránh xung đột Owner, Admin, Referee, Spectator.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã đúng thì giữ nguyên, không sửa lại.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi nghiệp vụ ngoài phạm vi constants/status.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu chính:

1. Thống nhất Owner đăng ký race theo RaceStatuses.Scheduled, không dùng RaceStatuses.Open nữa.
2. Admin pending race result phải lấy RaceResultStatuses.RefereeConfirmed, không lấy Draft.
3. RaceStatuses.cs phải chuẩn hóa theo bộ status mới và có helper dùng chung.
4. Admin approve result phải cập nhật Race.Status và RaceRegistration.Status đúng luồng.
5. Prediction chỉ dùng một bộ constants: RacePredictionStatuses.
6. Giảm hardcode chuỗi status trong controller.

Giai đoạn 0: Kiểm tra build nền

Việc đầu tiên:

* Chạy dotnet build.
* Nếu có lỗi build cũ thì sửa lỗi build trực tiếp trước.
* Không thêm thay đổi lớn khi project đang build lỗi.

File cần kiểm tra:

* Constants/RaceStatuses.cs
* Constants/RaceResultStatuses.cs
* Constants/RaceRegistrationStatuses.cs
* Constants/TournamentStatuses.cs
* Constants/RacePredictionStatuses.cs
* Constants/PredictionStatuses.cs
* Controllers/Owner/OwnerTournamentsController.cs
* Controllers/Owner/OwnerRegistrationsController.cs
* Controllers/Owner/OwnerRacesController.cs
* Controllers/Owner/OwnerJockeyAssignmentController.cs
* Controllers/Admin/AdminRaceResultsController.cs
* Controllers/Admin/AdminDashboardController.cs
* Controllers/Admin/AdminPredictionsController.cs
* Controllers/Spectator/SpectatorPredictionsController.cs
* Controllers/Jockey/JockeyDashboardController.cs

Giai đoạn 1: Chốt quy ước constants đúng

Quy ước nghiệp vụ cần dùng:

TournamentStatuses:

* Draft
* OpenRegistration
* ClosedRegistration
* Ongoing
* Completed
* Cancelled

RaceStatuses:

* Scheduled
* AssignedReferee
* RefereeReady
* Ongoing
* Finished
* ResultPending
* Published
* Cancelled

Không dùng RaceStatuses.Open, RaceStatuses.Closed, RaceStatuses.Completed cho Race nữa.

RaceRegistrationStatuses:

* Pending
* Approved
* JockeyInvited
* ReadyToRace
* Rejected
* Cancelled
* Completed

RaceResultStatuses:

* Draft
* RefereeConfirmed
* AdminApproved
* Published
* Returned

RacePredictionStatuses:

* Pending
* Locked
* Evaluated
* Cancelled

Không dùng PredictionStatuses nữa trong controller nếu RacePredictionStatuses đã có cùng nghiệp vụ.

Giai đoạn 2: Sửa Constants/RaceStatuses.cs

Sửa RaceStatuses.cs theo hướng chuẩn:

public static class RaceStatuses
{
public const string Scheduled = "Scheduled";
public const string AssignedReferee = "AssignedReferee";
public const string RefereeReady = "RefereeReady";
public const string Ongoing = "Ongoing";
public const string Finished = "Finished";
public const string ResultPending = "ResultPending";
public const string Published = "Published";
public const string Cancelled = "Cancelled";

```
public static readonly string[] All =
{
    Scheduled,
    AssignedReferee,
    RefereeReady,
    Ongoing,
    Finished,
    ResultPending,
    Published,
    Cancelled
};

public static bool IsValid(string? status)
{
    return !string.IsNullOrWhiteSpace(status)
        && All.Contains(status);
}

public static bool CanRegister(string? status)
{
    return status == Scheduled;
}

public static bool IsClosedForPrediction(string? status)
{
    return status is Ongoing or Finished or ResultPending or Published or Cancelled;
}

public static bool IsClosedForJockeyAssignment(string? status)
{
    return status is Ongoing or Finished or ResultPending or Published or Cancelled;
}

public static bool IsCompletedForDashboard(string? status)
{
    return status is Finished or ResultPending or Published;
}
```

}

Yêu cầu:

* Nếu xóa Open/Closed/Completed làm lộ lỗi compile thì sửa các chỗ đang dùng sang helper hoặc status chuẩn.
* Không thêm Completed vào All.
* Race đã chạy xong dùng Finished.
* Race chờ admin duyệt kết quả dùng ResultPending.
* Race đã công bố kết quả dùng Published.

Giai đoạn 3: Sửa Owner không kiểm tra RaceStatuses.Open nữa

File:

* Controllers/Owner/OwnerTournamentsController.cs

Đổi logic:
t.Race.Status == RaceStatuses.Open

thành:
RaceStatuses.CanRegister(t.Race.Status)

Vẫn giữ điều kiện:
t.Status == TournamentStatuses.OpenRegistration

Ý nghĩa:

* Tournament.Status = OpenRegistration dùng để mở đăng ký.
* Race.Status = Scheduled dùng để race đã lên lịch và có thể đăng ký.

File:

* Controllers/Owner/OwnerRegistrationsController.cs

Tìm các đoạn:
race.Status != RaceStatuses.Open

Đổi thành:
!RaceStatuses.CanRegister(race.Status)

Message có thể giữ:
"Race hiện không mở đăng ký."

File:

* Controllers/Owner/OwnerRacesController.cs

Tìm hardcode:
r.Status == "Open"

Đổi theo chuẩn:
RaceStatuses.CanRegister(r.Status)

Hoặc nếu logic cho phép Owner xem race đã đăng ký rồi thì giữ phần:
r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)

Nhưng phần mở đăng ký phải dùng Scheduled, không dùng Open.

Giai đoạn 4: Sửa Admin pending result dùng RefereeConfirmed

File:

* Controllers/Admin/AdminRaceResultsController.cs

Trong GET pending results, đổi:
.Where(r => r.Status == RaceResultStatuses.Draft)

thành:
.Where(r => r.Status == RaceResultStatuses.RefereeConfirmed)

Lý do:

* Draft là kết quả Referee mới nhập, chưa xác nhận.
* Admin chỉ duyệt result đã được Referee confirm.

File:

* Controllers/Admin/AdminDashboardController.cs

Nếu pendingResults đang count Draft thì đổi thành:
RaceResultStatuses.RefereeConfirmed

Giai đoạn 5: Sửa Admin approve result cập nhật Race và Registration

File:

* Controllers/Admin/AdminRaceResultsController.cs

Sau khi:
result.Status = RaceResultStatuses.AdminApproved;
result.PublishedAt = DateTime.UtcNow;

Bổ sung cập nhật Race:
var race = await _context.Races.FirstOrDefaultAsync(r => r.RaceId == result.RaceId);
if (race != null)
{
race.Status = RaceStatuses.Published;
race.UpdatedAt = DateTime.UtcNow;
}

Bổ sung cập nhật Registration:
var registration = await _context.RaceRegistrations
.FirstOrDefaultAsync(r => r.RegistrationId == result.RegistrationId);

if (registration != null)
{
registration.Status = RaceRegistrationStatuses.Completed;
}

Lưu ý:

* Nếu trong method đã có biến registration để tạo PrizeAward thì dùng lại, không query trùng nếu không cần.
* Không làm hỏng logic tạo PrizeAward hiện có.
* Không tạo PrizeAward nếu không có PrizeRule như logic hiện tại.
* Không sửa SpectatorRewardsController.

Giai đoạn 6: Sửa các chỗ còn dùng RaceStatuses.Completed

Tìm toàn project:
RaceStatuses.Completed

Đổi theo ngữ cảnh:

1. Nếu đang kiểm tra race đã đóng, đã xong, không cho mời Jockey:
   Dùng:
   RaceStatuses.IsClosedForJockeyAssignment(race.Status)

Ví dụ trong:

* Controllers/Owner/OwnerJockeyAssignmentController.cs
* Controllers/Owner/OwnerRacesController.cs

2. Nếu đang kiểm tra prediction đóng:
   Dùng:
   RaceStatuses.IsClosedForPrediction(race.Status)

Ví dụ trong:

* Controllers/Spectator/SpectatorPredictionsController.cs

3. Nếu dashboard Jockey đang tính completed races:
   Dùng:
   RaceStatuses.IsCompletedForDashboard(r.Race.Status)
   hoặc ưu tiên RaceRegistrationStatuses.Completed nếu query đang dựa trên registration.

Không thay bằng RaceStatuses.Finished một cách máy móc nếu logic thực tế là Published/ResultPending cũng được xem là đã xong.

Giai đoạn 7: Chuẩn hóa Prediction constants

Hiện project có 2 file:

* Constants/PredictionStatuses.cs
* Constants/RacePredictionStatuses.cs

Yêu cầu:

* Controller Admin/Spectator liên quan RacePrediction phải dùng RacePredictionStatuses.
* Không dùng PredictionStatuses trong controller nữa nếu không cần.
* Không bắt buộc xóa PredictionStatuses.cs ở giai đoạn này để tránh rủi ro, nhưng không để controller mới/cũ dùng nhầm.

File cần sửa:

* Controllers/Admin/AdminPredictionsController.cs

Đổi:
PredictionStatuses.Pending
PredictionStatuses.Locked
PredictionStatuses.Evaluated
PredictionStatuses.All

thành:
RacePredictionStatuses.Pending
RacePredictionStatuses.Locked
RacePredictionStatuses.Evaluated
RacePredictionStatuses.All

File:

* Controllers/Spectator/SpectatorPredictionsController.cs

Đảm bảo đã dùng RacePredictionStatuses.

Giai đoạn 8: Giảm hardcode status trong controller

Search trong Controllers:

* "Open"
* "Pending"
* "Active"
* "Inactive"
* "Approved"
* "JockeyInvited"
* "ReadyToRace"

Chỉ sửa các hardcode đang thuộc nghiệp vụ status rõ ràng.

Ví dụ:

* data.UserStatus == "Pending" đổi thành UserStatuses.Pending.
* data.UserStatus != "Pending" đổi thành UserStatuses.Pending.
* Key = "Approved" đổi thành RaceRegistrationStatuses.Approved nếu đó là registration status key.
* Key = "JockeyInvited" đổi thành RaceRegistrationStatuses.JockeyInvited.
* Key = "ReadyToRace" đổi thành RaceRegistrationStatuses.ReadyToRace.
* r.Status == "Open" đổi thành RaceStatuses.CanRegister(r.Status).

Không sửa chuỗi message tiếng Việt.
Không sửa label UI như "Đã gửi đơn", "Chờ Admin duyệt".
Không sửa nếu chuỗi không phải status nghiệp vụ.

Giai đoạn 9: Test cần đạt

Test build:

* dotnet build phải pass.

Test Owner:

1. Admin tạo tournament OpenRegistration và race Scheduled.
2. Owner gọi open tournaments/new tournaments vẫn thấy race.
3. Owner đăng ký race Scheduled không bị lỗi "Race hiện không mở đăng ký."
4. Owner không đăng ký được race Ongoing/Finished/ResultPending/Published/Cancelled.

Test Referee/Admin result:

1. Referee nhập result Draft.
2. Referee confirm result thành RefereeConfirmed.
3. Admin pending results phải thấy result RefereeConfirmed.
4. Admin approve result.
5. RaceResult.Status = AdminApproved.
6. Race.Status = Published.
7. RaceRegistration.Status = Completed.
8. PrizeAward logic cũ vẫn chạy nếu có PrizeRule.

Test Spectator prediction:

1. Prediction dùng RacePredictionStatuses.
2. Không prediction được khi race Ongoing/Finished/ResultPending/Published/Cancelled.
3. Prediction được ở race Scheduled nếu các điều kiện khác hợp lệ.

Test không ảnh hưởng:

* Owner Jockey Assignment vẫn build.
* Jockey Dashboard không còn phụ thuộc RaceStatuses.Completed.
* Admin Dashboard pending results count đúng RefereeConfirmed.
* Không còn chỗ controller dùng RaceStatuses.Open.
* Không còn chỗ controller dùng PredictionStatuses nếu RacePredictionStatuses là chuẩn.


Bổ sung bắt buộc trước khi sửa:

1. Kiểm tra thêm các file:

* Controllers/Owner/OwnerBaseController.cs
* Controllers/Owner/OwnerHorsesController.cs
* Controllers/Referee/RefereeDashboardController.cs

2. Trong AdminRaceResultsController.ApproveResult:

* Chỉ cho approve khi result.Status == RaceResultStatuses.RefereeConfirmed.
* Nếu result đang Draft, Returned, AdminApproved hoặc Published thì trả BadRequest, không approve lại.

3. Khi cập nhật Race.Status = RaceStatuses.Published:

* Nếu race chỉ có một result đại diện thì set Published ngay sau AdminApproved.
* Nếu race có nhiều RaceResult, chỉ set Published khi tất cả RaceResult của race đã AdminApproved.
* Không làm mất logic tạo PrizeAward hiện có.

4. Khi dọn hardcode:

* OwnerBaseController: đổi "Pending" thành UserStatuses.Pending.
* OwnerRacesController: đổi r.Status == "Open" thành RaceStatuses.CanRegister(r.Status).
* OwnerRegistrationsController: các Key trong journey như "Approved", "JockeyInvited", "ReadyToRace" chỉ đổi sang constants nếu chúng thật sự đại diện cho status nghiệp vụ; nếu chỉ là key UI thì có thể giữ nguyên.
* OwnerHorsesController: "Active"/"Inactive" chỉ đổi nếu xác định là domain status; nếu chỉ là filter cho bool IsActive thì không bắt buộc sửa.

5. Sau khi sửa phải kiểm tra bằng search:

* Không còn RaceStatuses.Open trong Controllers.
* Không còn RaceStatuses.Closed trong Controllers.
* Không còn RaceStatuses.Completed trong Controllers.
* Không còn PredictionStatuses trong Controllers.
* Không còn r.Status == "Open".
* dotnet build phải pass.





Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. File đã sửa
3. RaceStatuses đã chuẩn hóa helper chưa
4. Owner đã bỏ RaceStatuses.Open chưa
5. Admin pending result đã dùng RefereeConfirmed chưa
6. Admin approve result đã cập nhật Race/Registration chưa
7. RaceStatuses.Completed còn được dùng trong controller không
8. Admin/Spectator prediction đã dùng RacePredictionStatuses chưa
9. Hardcode status nào đã thay bằng constants
10. Có sửa DB/migration không
11. Build command
12. Lỗi còn lại nếu có
