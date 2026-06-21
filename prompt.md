Tôi đồng ý cho sửa.

Giai đoạn kiểm tra và hoàn tất chuẩn hóa constants/status BE hiện tại.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã đúng thì giữ nguyên, không sửa lại.
* Chỉ sửa lỗi còn sót nếu phát hiện lệch constants/status.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi nghiệp vụ ngoài phạm vi constants/status.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:
Kiểm tra và hoàn tất các thay đổi chuẩn hóa constants/status sau:

1. RaceStatuses không còn Open, Closed, Completed.
2. Owner đăng ký race dùng RaceStatuses.CanRegister().
3. Owner/Jockey assignment không dùng RaceStatuses.Completed.
4. Jockey Dashboard dùng helper IsCompletedForDashboard().
5. Spectator Prediction dùng helper IsClosedForPrediction().
6. Admin pending race result dùng RaceResultStatuses.RefereeConfirmed.
7. Admin approve result cập nhật đúng RaceResult, Race, RaceRegistration.
8. Admin/Spectator Prediction dùng RacePredictionStatuses.
9. Admin assign referee dùng RefereeAssignmentStatuses.Assigned.
10. Owner không còn hardcode status nghiệp vụ.
11. Search toàn project không còn constant cũ trong Controllers/Services.

Giai đoạn 0: Kiểm tra build nền

Chạy:
dotnet build

Nếu build đang lỗi:

* Chỉ sửa lỗi build liên quan trực tiếp constants/status.
* Không refactor rộng.
* Không thêm chức năng mới.

Giai đoạn 1: Kiểm tra Constants/RaceStatuses.cs

File:

* Constants/RaceStatuses.cs

Yêu cầu đúng:
RaceStatuses chỉ còn:

* Scheduled
* AssignedReferee
* RefereeReady
* Ongoing
* Finished
* ResultPending
* Published
* Cancelled

Không còn:

* Open
* Closed
* Completed

All phải chứa đúng 8 status trên.

Phải có helper:

* CanRegister(string? status) => status == Scheduled
* IsClosedForPrediction(string? status) => Ongoing/Finshed/ResultPending/Published/Cancelled
* IsClosedForJockeyAssignment(string? status) => Ongoing/Finished/ResultPending/Published/Cancelled
* IsCompletedForDashboard(string? status) => Finished/ResultPending/Published

Nếu đã đúng thì không sửa.

Giai đoạn 2: Kiểm tra Owner xem tournament/race và đăng ký race

File:

* Controllers/Owner/OwnerTournamentsController.cs

Kiểm tra:

* Không còn t.Race.Status == RaceStatuses.Open.
* Phải dùng RaceStatuses.CanRegister(t.Race.Status).
* Vẫn giữ điều kiện TournamentStatuses.OpenRegistration.

File:

* Controllers/Owner/OwnerRegistrationsController.cs

Kiểm tra:

* Không còn race.Status != RaceStatuses.Open.
* Không còn tự check Scheduled rời rạc nếu đã có helper.
* Đăng ký race phải dùng:
  RaceStatuses.CanRegister(...)
  hoặc !RaceStatuses.CanRegister(...)

File:

* Controllers/Owner/OwnerRacesController.cs

Kiểm tra:

* Không còn r.Status == "Open".
* Phải dùng RaceStatuses.CanRegister(r.Status).
* Vẫn giữ điều kiện Owner xem được race nếu đã có RaceRegistration:
  RaceStatuses.CanRegister(r.Status) ||
  r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)

Kiểm tra thêm:

* Không còn registration.Race.Status == RaceStatuses.Completed.
* Nếu kiểm tra race đóng thì dùng:
  RaceStatuses.IsClosedForJockeyAssignment(registration.Race.Status)

Giai đoạn 3: Kiểm tra Owner/Jockey Assignment

File:

* Controllers/Owner/OwnerJockeyAssignmentController.cs

Kiểm tra:

* Không còn RaceStatuses.Completed.
* Không còn logic:
  RaceStatus == RaceStatuses.Completed
  RaceStatus != RaceStatuses.Completed

Phải dùng:

* RaceStatuses.IsClosedForJockeyAssignment(...)
* hoặc !RaceStatuses.IsClosedForJockeyAssignment(...)

Áp dụng cho:

* data.RaceStatus
* registration.Race.Status
* registration.RaceStatus nếu có

Luồng đúng:
Đóng luồng mời/chọn Jockey khi Race là:

* Ongoing
* Finished
* ResultPending
* Published
* Cancelled

Giai đoạn 4: Kiểm tra Jockey Dashboard

File:

* Controllers/Jockey/JockeyDashboardController.cs

Kiểm tra:

* Không còn RaceStatuses.Completed.
* Race đã xong phải dùng:
  RaceStatuses.IsCompletedForDashboard(r.Race.Status)

Race được xem là đã xong nếu:

* Finished
* ResultPending
* Published

Giai đoạn 5: Kiểm tra Spectator Prediction

File:

* Controllers/Spectator/SpectatorPredictionsController.cs

Kiểm tra:

* Không còn check thủ công:
  RaceStatuses.Cancelled ||
  RaceStatuses.Ongoing ||
  RaceStatuses.Completed ||
  RaceStatuses.ResultPending ||
  RaceStatuses.Published

Phải dùng:
RaceStatuses.IsClosedForPrediction(race.Status)

Spectator không prediction được khi Race là:

* Ongoing
* Finished
* ResultPending
* Published
* Cancelled

Giai đoạn 6: Kiểm tra Admin pending race result

File:

* Controllers/Admin/AdminRaceResultsController.cs

Kiểm tra pending result:

* Không dùng RaceResultStatuses.Draft.
* Phải dùng RaceResultStatuses.RefereeConfirmed.

File:

* Controllers/Admin/AdminDashboardController.cs

Kiểm tra count pending result:

* Không count Draft.
* Phải count RefereeConfirmed.

Luồng đúng:
Referee nhập result -> Draft
Referee confirm -> RefereeConfirmed
Admin pending result -> lấy RefereeConfirmed

Giai đoạn 7: Kiểm tra Admin approve result

File:

* Controllers/Admin/AdminRaceResultsController.cs

Khi Admin approve result, cần đúng:

* Chỉ approve nếu result.Status == RaceResultStatuses.RefereeConfirmed.
* Nếu không đúng thì trả BadRequest rõ ràng.
* Set result.Status = RaceResultStatuses.AdminApproved.
* Set result.PublishedAt = DateTime.UtcNow.
* Set result.UpdatedAt = DateTime.UtcNow nếu field có.
* Set result.Registration.Status = RaceRegistrationStatuses.Completed.

Kiểm tra logic publish race:

* Nếu race chỉ có một result hoặc tất cả result khác đã AdminApproved thì:
  result.Race.Status = RaceStatuses.Published
  result.Race.UpdatedAt = DateTime.UtcNow

Không phá logic PrizeAward hiện có.
Không tạo PrizeAward nếu không có PrizeRule, trừ khi code hiện tại đã có rule khác.

Giai đoạn 8: Kiểm tra Admin Prediction dùng đúng constants

File:

* Controllers/Admin/AdminPredictionsController.cs
* Controllers/Spectator/SpectatorPredictionsController.cs

Yêu cầu:

* Không dùng PredictionStatuses trong Controllers/Services.
* Phải dùng RacePredictionStatuses:

  * RacePredictionStatuses.Pending
  * RacePredictionStatuses.All
  * RacePredictionStatuses.Locked
  * RacePredictionStatuses.Evaluated
  * RacePredictionStatuses.Cancelled nếu có

PredictionStatuses.cs có thể giữ lại để tránh refactor lớn, nhưng Controllers/Services không dùng nữa.

Giai đoạn 9: Kiểm tra Admin assign referee

File:

* Controllers/Admin/AdminTournamentsController.cs

Yêu cầu:

* Không còn Status = "Assigned".
* Phải dùng:
  RefereeAssignmentStatuses.Assigned

Khi existing assignment != null:

* Ngoài cập nhật RefereeId và AssignedAt, phải set:
  existing.Status = RefereeAssignmentStatuses.Assigned

Mục tiêu:
Nếu assignment cũ từng Cancelled, Admin gán lại Referee thì status quay về Assigned.

Giai đoạn 10: Kiểm tra hardcode status trong Owner

File:

* Controllers/Owner/OwnerBaseController.cs

Yêu cầu:

* Không còn data.UserStatus == "Pending".
* Không còn data.UserStatus != "Pending".
* Phải dùng UserStatuses.Pending.

File:

* Controllers/Owner/OwnerRegistrationsController.cs

Journey key phải dùng constants:

* RaceRegistrationStatuses.Approved
* RaceRegistrationStatuses.JockeyInvited
* RaceRegistrationStatuses.ReadyToRace

Không hardcode:

* "Approved"
* "JockeyInvited"
* "ReadyToRace"

File:

* Constants/HorseActivityStatuses.cs
* Controllers/Owner/OwnerHorsesController.cs

Nếu đã có HorseActivityStatuses thì kiểm tra:

* HorseActivityStatuses.Active
* HorseActivityStatuses.Inactive

OwnerHorsesController không nên hardcode "Active"/"Inactive" cho trạng thái ngựa.

Lý do:

* UserStatuses.Active là trạng thái tài khoản.
* HorseActivityStatuses.Active là trạng thái bật/tắt ngựa.

Giai đoạn 11: Search kiểm tra toàn project

Chạy các lệnh:

rg -n "RaceStatuses.(Open|Closed|Completed)|r.Status == "Open"|"Open"" Controllers Services Constants -S

Kết quả mong muốn:

* Không còn lỗi trong Controllers/Services.
* Không còn RaceStatuses.Open/Closed/Completed.

Chạy:

rg -n 'Status = "Assigned"|"Assigned"' Controllers Services -S

Kết quả mong muốn:

* Không còn Status = "Assigned" trong Controllers/Services.
* Nếu còn chuỗi "Assigned" là message/label không phải status thì ghi rõ.

Chạy:

rg -n "RaceResultStatuses.Draft" Controllers/Admin -S

Kết quả mong muốn:

* Không còn trong Admin pending/approve flow.
* RaceResultStatuses.Draft còn ở RefereeRacesController.cs là đúng vì Referee nhập result ban đầu là Draft.

Chạy:

rg -n "\bPredictionStatuses\b" Controllers Services -S

Kết quả mong muốn:

* Không còn dùng PredictionStatuses trong Controllers/Services.

Chạy:

rg -n '"Pending"|"Active"|"Inactive"|"Approved"|"JockeyInvited"|"ReadyToRace"|"Completed"|"Cancelled"' Controllers Services -S

Yêu cầu:

* Nếu là hardcode status nghiệp vụ thì đổi sang constants.
* Không sửa chuỗi message tiếng Việt/tiếng Anh hiển thị.
* Không sửa label không phải status.
* Nếu còn chuỗi hợp lệ không phải status thì báo rõ.

Giai đoạn 12: Test nhanh nghiệp vụ

Test build:

* dotnet build phải pass.

Test Owner:

1. Tournament OpenRegistration + Race Scheduled.
2. Owner thấy race/tournament để đăng ký.
3. Owner đăng ký được race Scheduled.
4. Owner không đăng ký được race Ongoing/Finished/ResultPending/Published/Cancelled.

Test Admin Result:

1. Referee tạo Draft.
2. Referee confirm thành RefereeConfirmed.
3. Admin pending result thấy RefereeConfirmed.
4. Admin approve result.
5. RaceResult = AdminApproved.
6. RaceRegistration = Completed.
7. Race = Published khi đủ điều kiện.

Test Prediction:

1. Controller dùng RacePredictionStatuses.
2. Spectator không prediction được race đã đóng theo IsClosedForPrediction.

Test Jockey:

1. Jockey dashboard không dùng RaceStatuses.Completed.
2. Completed dashboard tính bằng IsCompletedForDashboard.

Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. File đã sửa
3. RaceStatuses hiện còn Open/Closed/Completed không
4. Owner register race đã dùng CanRegister chưa
5. Owner/Jockey assignment đã dùng IsClosedForJockeyAssignment chưa
6. JockeyDashboard đã dùng IsCompletedForDashboard chưa
7. SpectatorPredictions đã dùng IsClosedForPrediction chưa
8. Admin pending result đã dùng RefereeConfirmed chưa
9. Admin approve result đã cập nhật Race/Registration đúng chưa
10. PredictionStatuses còn dùng trong Controllers/Services không
11. Hardcode status nghiệp vụ còn sót không
12. Build command/lỗi còn lại
