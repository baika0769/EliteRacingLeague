Tôi đồng ý cho sửa.

Giai đoạn hoàn thiện nhánh tích hợp: cleanup constants và DTO trước khi push nhánh integrate.

Bối cảnh hiện tại:

* Tôi đang đứng ở nhánh: integrate/khaii-features-on-current-constants
* Nhánh này đã được tạo từ test.
* Code mới từ khaii đã được đưa sang.
* dotnet build trước đó đã pass.
* Không merge main trong giai đoạn này.
* Không push main trong giai đoạn này.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại trong workspace.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Không sửa database.
* Không migration.
* Không đổi schema.
* Không tự thêm field vào entity.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.
* Không merge vào main.
* Không push main.
* Chỉ cleanup, build/test, commit và báo cáo.

Giai đoạn 0: Kiểm tra nhánh và workspace

Chạy:

git branch --show-current
git status --short

Yêu cầu:

* Nhánh hiện tại phải là integrate/khaii-features-on-current-constants.
* Nếu đang không đúng nhánh thì dừng và báo rõ.
* Nếu workspace có thay đổi không liên quan thì dừng và báo rõ.
* Nếu workspace sạch hoặc chỉ có thay đổi do chính giai đoạn này tạo ra thì tiếp tục.

Giai đoạn 1: Cleanup OwnerRegistrationsController dùng helper CanRegister

Kiểm tra file:

Controllers/Owner/OwnerRegistrationsController.cs

Nếu còn kiểm tra trực tiếp:

race.Status == RaceStatuses.Scheduled
race.Status != RaceStatuses.Scheduled
t.Race.Status == RaceStatuses.Scheduled

thì đổi sang helper:

RaceStatuses.CanRegister(race.Status)
!RaceStatuses.CanRegister(race.Status)
RaceStatuses.CanRegister(t.Race.Status)

Mục tiêu:

* Bám đúng convention hiện tại.
* Owner chỉ đăng ký khi Tournament.Status = OpenRegistration và RaceStatuses.CanRegister(race.Status) = true.
* Không đổi nghiệp vụ ngoài việc dùng helper.

Giai đoạn 2: Cleanup OwnerNotificationsController không dùng ké OwnerHorseListResponse

Kiểm tra file:

Controllers/Owner/OwnerNotificationsController.cs

Nếu API list notification đang dùng OwnerHorseListResponse chỉ để trả Page/PageSize/TotalItems/TotalPages/Items thì sửa.

Tạo DTO riêng nếu chưa có:

DTOs/Owner/Notifications/OwnerNotificationListResponse.cs

Fields:

* int Page
* int PageSize
* int TotalItems
* int TotalPages
* List<OwnerNotificationResponse> Items

Sau đó sửa OwnerNotificationsController dùng OwnerNotificationListResponse.

Yêu cầu:

* Không trả entity Notification trực tiếp.
* Không dùng DTO của Horse cho Notifications.
* Không phá route hiện có:
  GET /api/owner/notifications?category=All&page=1&pageSize=10

Giai đoạn 3: Rà constants cũ trong Controllers

Chạy:

rg 'RaceStatuses.(Open|Closed|Completed)' Controllers
rg 'Status == "Open"|r.Status == "Open"' Controllers
rg '\bPredictionStatuses\b' Controllers

Yêu cầu:

* Không còn RaceStatuses.Open trong Controllers.
* Không còn RaceStatuses.Closed trong Controllers.
* Không còn RaceStatuses.Completed trong Controllers.
* Không còn hardcode "Open" cho Race trong Controllers.
* Không còn PredictionStatuses trong Controllers.

Nếu còn thì sửa theo constants/helper hiện tại:

* RaceStatuses.Open → RaceStatuses.CanRegister(...)
* RaceStatuses.Completed/Closed → helper đóng race hoặc Finished/ResultPending/Published theo ngữ cảnh
* PredictionStatuses → RacePredictionStatuses

Không sửa message tiếng Việt.
Không sửa label hiển thị.
Không sửa text không phải status nghiệp vụ.

Giai đoạn 4: Build

Chạy:

dotnet build

Yêu cầu:

* 0 error.
* Nếu có warning cũ không liên quan thì ghi rõ, không sửa lan man.
* Nếu build lỗi do thay đổi vừa làm thì sửa trực tiếp, không refactor lớn.

Giai đoạn 5: Test nếu có

Nếu project có test project thì chạy:

dotnet test

Nếu không có test project riêng thì ghi rõ không chạy dotnet test.

Giai đoạn 6: Commit cleanup nếu có thay đổi

Chạy:

git status --short
git diff --stat

Nếu có thay đổi hợp lệ thì commit:

git add .
git commit -m "cleanup owner notifications and registration status helpers"

Nếu không có thay đổi thì không commit mới, chỉ báo rõ.

Giai đoạn 7: Không merge main

Không được chạy:

* git checkout main
* git merge
* git push origin main

Chỉ báo cáo để tôi kiểm tra trước.

Giai đoạn 8: Báo cáo kết quả

Sau khi làm xong, chỉ trả lời tối đa 12 dòng:

1. Đang đứng trên nhánh nào
2. Workspace ban đầu sạch không
3. OwnerRegistrationsController đã dùng RaceStatuses.CanRegister chưa
4. OwnerNotificationsController còn dùng OwnerHorseListResponse không
5. DTO OwnerNotificationListResponse đã tạo chưa
6. Kết quả rg RaceStatuses.Open/Closed/Completed
7. Kết quả rg hardcode "Open"
8. Kết quả rg PredictionStatuses
9. dotnet build kết quả
10. dotnet test kết quả nếu có
11. Commit mới tạo là gì, hoặc không có thay đổi để commit
12. Lỗi còn lại nếu có
