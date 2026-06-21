Tôi đồng ý cho sửa.

Giai đoạn tích hợp: đưa code mới từ nhánh `khaii` sang nền constants chuẩn của nhánh `test`.

Bối cảnh:

* `test` = nhánh chuẩn constants/status hiện tại.
* `khaii` = nhánh có code mới nhưng có thể lệch constants.
* `main` = nhánh cuối cùng cần sạch, đúng luồng.
* Không merge thẳng `khaii` vào `main`.
* Không merge toàn bộ nhánh `khaii`.
* Làm trên nhánh tích hợp được tạo từ `test`.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại trong workspace.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* Code phải sạch, dễ tái sử dụng, dễ bảo trì.
* Không lấy bừa file constants từ `khaii` nếu `test` đã đúng.
* Không sửa database.
* Không migration.
* Không đổi schema.
* Không tự thêm field vào entity.
* Không sửa connection string.
* Không sửa appsettings nếu không liên quan trực tiếp.
* Không sửa frontend nếu không được yêu cầu.
* Không refactor lớn.
* Không in code dài ra terminal.
* Không push `main`.
* Không merge vào `main` trong giai đoạn này.
* Chỉ tạo nhánh tích hợp, sửa constants, build/test, commit, rồi báo cáo để tôi kiểm tra trước khi merge main.

Mục tiêu:

1. Đảm bảo workspace sạch trước khi checkout/chuyển nhánh.
2. Tạo nhánh tích hợp từ `test`.
3. Đưa từng phần code cần giữ từ `khaii` sang.
4. Không lấy file constants từ `khaii` nếu `test` là chuẩn.
5. Sửa toàn bộ code mới để bám constants/status hiện tại của `test`.
6. Build/test pass.
7. Commit trên nhánh tích hợp.
8. Báo cáo rõ đã làm gì.

Giai đoạn 0: Kiểm tra workspace an toàn

Chạy:

git status --short

Nếu workspace có file chưa commit hoặc untracked thì DỪNG và báo rõ, không checkout/chuyển nhánh.

Nếu chỉ có file hỗ trợ như:

* AGENTS.md
* prompt.md

thì KHÔNG tự xóa, KHÔNG tự commit nếu chưa được phép.

Hãy báo:

"Workspace chưa sạch. Có file chưa được theo dõi hoặc chưa commit. Vui lòng commit/stash/xóa trước khi tiếp tục."

Nếu tôi đã xử lý bằng stash/commit/xóa rồi thì mới tiếp tục.

Giai đoạn 1: Kiểm tra branch remote/local

Chạy:

git fetch origin
git branch -a

Đảm bảo có các nhánh:

* test
* khaii
* main

Nếu thiếu nhánh nào thì dừng và báo rõ.

Giai đoạn 2: Tạo nhánh tích hợp từ test

Chạy:

git checkout test
git pull origin test
git checkout -b integrate/khaii-features-on-current-constants

Ý nghĩa:

* Nhánh `integrate/khaii-features-on-current-constants` lấy constants đúng từ `test` làm nền.
* Mọi code mới từ `khaii` phải sửa theo chuẩn constants của `test`.

Không được checkout `main`.
Không được merge `khaii`.

Giai đoạn 3: Kiểm tra file khác nhau giữa test và khaii

Chạy:

git diff --name-status test..khaii

Sau đó phân nhóm file khác nhau.

Chỉ lấy các file thật sự cần giữ từ `khaii`.

Tuyệt đối không checkout nguyên folder nếu chưa chắc đúng path.

Ví dụ nếu cần phần Owner Notifications thì chỉ lấy đúng file thực tế sau khi xem diff, ví dụ:

git checkout khaii -- Controllers/Owner/OwnerNotificationsController.cs
git checkout khaii -- DTOs/Owner/Notifications

Ví dụ nếu cần phần Owner Jockey Assignment:

git checkout khaii -- Controllers/Owner/OwnerJockeyAssignmentController.cs
git checkout khaii -- DTOs/Owner/OwnerJockeyAssignmentDtos.cs

Ví dụ nếu cần phần Jockey Invitations:

git checkout khaii -- Controllers/Jockey/JockeyInvitationsController.cs

Ví dụ nếu cần phần Admin Race Registrations:

git checkout khaii -- Controllers/Admin/AdminRaceRegistrationsController.cs

Yêu cầu:

* Nếu file đã tồn tại ở `test` và đang đúng constants thì không ghi đè bừa.
* Nếu cần lấy code mới từ `khaii` vào file đã tồn tại, hãy mở diff và merge thủ công nội dung cần thiết.
* Không checkout các file constants từ `khaii` nếu `test` đã là chuẩn.
* Không checkout appsettings, .env, migration, database script, file backup.
* Sau mỗi nhóm file, chạy:

git diff --stat
git diff --name-only

Giai đoạn 4: Giữ constants chuẩn từ test

Ưu tiên giữ constants từ `test`:

* Constants/RaceStatuses.cs
* Constants/RaceResultStatuses.cs
* Constants/RaceRegistrationStatuses.cs
* Constants/TournamentStatuses.cs
* Constants/InvitationStatuses.cs
* Constants/RacePredictionStatuses.cs
* Constants/JockeyHealthStatuses.cs
* Constants/UserStatuses.cs
* Constants/UserRoles.cs
* Constants/PrizeAwardStatuses.cs

Quy ước RaceStatuses hiện tại:

* Scheduled
* AssignedReferee
* RefereeReady
* Ongoing
* Finished
* ResultPending
* Published
* Cancelled

Không dùng trong RaceStatuses:

* Open
* Closed
* Completed

Nếu code mới từ `khaii` dùng 3 status cũ này thì phải sửa theo helper/status chuẩn của `test`.

Giai đoạn 5: Rà soát lệch constants/status

Chạy:

rg 'RaceStatuses.(Open|Closed|Completed)' .
rg 'Status == "Open"|r.Status == "Open"' .
rg 'PredictionStatuses' Controllers
rg '"Approved"|"JockeyInvited"|"ReadyToRace"|"Pending"|"Rejected"|"Cancelled"|"Completed"' Controllers

Mục tiêu sửa:

* RaceStatuses.Open → RaceStatuses.CanRegister(...) hoặc RaceStatuses.Scheduled theo ngữ cảnh.
* RaceStatuses.Completed → RaceStatuses.Finished / RaceStatuses.ResultPending / RaceStatuses.Published hoặc helper phù hợp.
* RaceStatuses.Closed → helper đóng race phù hợp.
* PredictionStatuses → RacePredictionStatuses.
* Hardcode status → constants tương ứng nếu đó là status nghiệp vụ.

Không sửa chuỗi message hiển thị cho người dùng.
Không sửa label UI/API response nếu chỉ là text mô tả.
Không sửa text trong documentation nếu không ảnh hưởng compile/runtime.

Giai đoạn 6: Sửa Owner đăng ký race theo constants hiện tại

Nếu thấy:

race.Status == RaceStatuses.Open

hoặc:

race.Status != RaceStatuses.Open

thì sửa thành:

RaceStatuses.CanRegister(race.Status)

hoặc:

!RaceStatuses.CanRegister(race.Status)

Quy ước đúng:

* Tournament.Status = TournamentStatuses.OpenRegistration.
* Race.Status = RaceStatuses.Scheduled.
* Owner chỉ đăng ký khi tournament đang mở đăng ký và race có thể đăng ký.

Các file thường cần kiểm tra:

* Controllers/Owner/OwnerTournamentsController.cs
* Controllers/Owner/OwnerRegistrationsController.cs
* Controllers/Owner/OwnerRacesController.cs

Giai đoạn 7: Sửa Owner Notifications nếu lệch RaceStatuses

Trong:

Controllers/Owner/OwnerNotificationsController.cs

Nếu có:

RaceStatuses.Completed

thì bỏ, vì Race không dùng Completed nữa.

Ưu tiên dùng helper nếu `test` có:

!RaceStatuses.IsClosedForJockeyAssignment(r.Race.Status)

Nếu helper chưa có thì dùng rõ các status đóng:

r.Race.Status != RaceStatuses.Ongoing &&
r.Race.Status != RaceStatuses.Finished &&
r.Race.Status != RaceStatuses.ResultPending &&
r.Race.Status != RaceStatuses.Published &&
r.Race.Status != RaceStatuses.Cancelled

Không dùng RaceStatuses.Completed cho Race.

Giai đoạn 8: Sửa Admin pending results

Trong:

* Controllers/Admin/AdminRaceResultsController.cs
* Controllers/Admin/AdminDashboardController.cs

Nếu pending result đang lấy:

RaceResultStatuses.Draft

thì đổi thành:

RaceResultStatuses.RefereeConfirmed

Quy ước:

* Draft = Referee mới nhập result.
* RefereeConfirmed = Referee đã xác nhận, Admin mới duyệt.

Giai đoạn 9: Sửa Admin approve result đúng luồng

Khi Admin approve result, đảm bảo đúng:

RaceResult.Status = RaceResultStatuses.AdminApproved
Race.Status = RaceStatuses.Published
RaceRegistration.Status = RaceRegistrationStatuses.Completed

Yêu cầu:

* Không phá logic tạo/cập nhật PrizeAward hiện có.
* Nếu đã có PrizeAward logic thì giữ lại.
* Không tạo PrizeAward nếu không có PrizeRule, trừ khi logic hiện tại đã quy định khác.
* Không sửa SpectatorRewardsController.

Giai đoạn 10: Sửa Prediction constants

Trong Controllers/Admin và Controllers/Spectator, nếu còn dùng:

PredictionStatuses.Pending
PredictionStatuses.Locked
PredictionStatuses.Evaluated
PredictionStatuses.Cancelled

thì sửa thành:

RacePredictionStatuses.Pending
RacePredictionStatuses.Locked
RacePredictionStatuses.Evaluated
RacePredictionStatuses.Cancelled

Không bắt buộc xóa PredictionStatuses.cs nếu còn để tránh refactor lớn.

Giai đoạn 11: Giảm hardcode status trong Controllers

Các hardcode status nghiệp vụ như:

* "Approved"
* "JockeyInvited"
* "ReadyToRace"
* "Pending"
* "Rejected"
* "Cancelled"
* "Active"
* "Inactive"
* "Banned"

Nếu thuộc nghiệp vụ status thì thay bằng constants tương ứng:

* RaceRegistrationStatuses
* InvitationStatuses
* UserStatuses
* RaceResultStatuses
* RacePredictionStatuses
* PrizeAwardStatuses

Không sửa message tiếng Việt.
Không sửa label hiển thị.
Không sửa các text không phải status nghiệp vụ.

Giai đoạn 12: Build và test

Chạy:

dotnet build

Nếu project có test:

dotnet test

Nếu build lỗi:

* Chỉ sửa lỗi liên quan đến file vừa tích hợp.
* Không refactor rộng.
* Không đổi nghiệp vụ ngoài constants/status.
* Không sửa DB/migration.

Test nhanh các luồng chính:

1. Owner thấy tournament OpenRegistration + race Scheduled.
2. Owner đăng ký được race Scheduled.
3. Owner không đăng ký race Ongoing/Finished/ResultPending/Published/Cancelled.
4. Owner Notifications không dùng RaceStatuses.Completed.
5. Owner Jockey Assignment vẫn build.
6. Jockey Accept/Reject Invitation vẫn đúng.
7. Admin approve/reject registration vẫn đúng.
8. Referee nhập result Draft.
9. Referee confirm result thành RefereeConfirmed.
10. Admin pending result thấy RefereeConfirmed.
11. Admin approve result cập nhật RaceResult/Race/RaceRegistration đúng.
12. Spectator prediction dùng RacePredictionStatuses.
13. Jockey dashboard không phụ thuộc RaceStatuses.Completed.

Giai đoạn 13: Kiểm tra trước khi commit

Chạy lại:

rg 'RaceStatuses.(Open|Closed|Completed)' Controllers
rg 'PredictionStatuses' Controllers
rg 'Status == "Open"|r.Status == "Open"' Controllers

Kết quả mong muốn:

* Không còn RaceStatuses.Open trong Controllers.
* Không còn RaceStatuses.Completed trong Controllers.
* Không còn RaceStatuses.Closed trong Controllers.
* Không còn PredictionStatuses trong Controllers.
* Không còn hardcode "Open" cho Race trong Controllers.

Nếu còn:

* Sửa theo constants/helper chuẩn của `test`.
* Chạy lại dotnet build.

Giai đoạn 14: Commit nhánh tích hợp

Sau khi build pass và rà soát pass:

git status --short
git diff --stat
git add .
git commit -m "integrate khaii features with current status constants"

Không merge vào main.
Không push main.

Giai đoạn 15: Báo cáo kết quả

Sau khi làm xong, chỉ trả lời tối đa 15 dòng:

1. Workspace ban đầu có sạch không
2. Đã tạo nhánh tích hợp từ test chưa
3. File/nhóm chức năng đã lấy từ khaii
4. Có lấy file constants từ khaii không
5. Constants nào được giữ từ test
6. Đã sửa RaceStatuses.Open ở đâu
7. Đã sửa RaceStatuses.Completed/Closed ở đâu
8. Đã sửa hardcode "Open" ở đâu
9. Admin pending result đã dùng RefereeConfirmed chưa
10. Admin approve result đã cập nhật Race/Registration chưa
11. Prediction đã dùng RacePredictionStatuses chưa
12. Kết quả rg kiểm tra constants cũ
13. dotnet build kết quả
14. dotnet test kết quả nếu có
15. Lỗi còn lại nếu có
