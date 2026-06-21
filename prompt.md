Tôi đồng ý cho sửa.

Giai đoạn BE: tạo API Horse Owner Notifications cho trang Notifications.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại trong workspace.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* Code sạch, dễ tái sử dụng, dễ bảo trì.
* Không sửa database.
* Không migration.
* Không tạo bảng notifications mới nếu chưa có.
* Không thêm field DB mới vào Notification.
* Không sửa frontend.
* Không refactor lớn.
* Không tạo trùng controller/DTO/service.
* Không in code dài ra terminal.
* Tuân theo constants hiện tại của dự án.

Mục tiêu:
Tạo đầy đủ API cho HorseOwner dùng trang Notifications gồm:

1. Summary 3 card: Unread, Invitations, Upcoming Races.
2. List notifications có filter tab: All, Registrations, Jockeys, Tournaments.
3. Detail notification.
4. Mark one notification as read.
5. Mark all notifications as read.
6. Bổ sung tạo notification cho Owner ở các nghiệp vụ quan trọng:

   * Admin approve registration.
   * Admin reject registration.
   * Jockey accept invitation.
   * Jockey reject invitation.

Giai đoạn 0: Kiểm tra build nền

Chạy:

dotnet build

Nếu build lỗi:

* Sửa lỗi build trực tiếp trước.
* Không thêm API mới khi project đang build lỗi.

Giai đoạn 1: Kiểm tra Notification infrastructure

Kiểm tra:

* Models/Notification.cs
* Data/EliteRacingLeagueContext.cs
* Controllers/Jockey/JockeyNotificationsController.cs
* Controllers/Referee/RefereeNotificationsController.cs
* Controllers/Spectator/SpectatorNotificationsController.cs
* Controllers/Owner/OwnerBaseController.cs

Yêu cầu:

* Nếu Models/Notification.cs và DbSet<Notification> đã có thì dùng model hiện có.
* Nếu Notification chỉ có UserId, Title, Message, IsRead, CreatedAt thì vẫn làm API được.
* Không thêm Category/Type/RelatedId/RelatedType vào DB.
* Category/StatusLabel trong DTO được suy ra từ Title/Message.
* Nếu project hoàn toàn chưa có Notification model hoặc DbSet thì dừng và báo rõ, không tự tạo bảng.

Giai đoạn 2: Tạo DTO cho Owner Notifications

Tạo thư mục nếu chưa có:

DTOs/Owner/Notifications

Tạo các DTO nếu chưa có:

1. OwnerNotificationSummaryResponse.cs
   Fields:

* int Unread
* int Invitations
* int UpcomingRaces

2. OwnerNotificationResponse.cs
   Fields:

* int NotificationId
* string Title
* string Message
* string Category
* string? StatusLabel
* bool IsRead
* DateTime CreatedAt
* string DisplayTime
* string? RelatedType
* int? RelatedId

3. OwnerNotificationDetailResponse.cs
   Fields:

* int NotificationId
* string Title
* string Message
* string Category
* string? StatusLabel
* bool IsRead
* DateTime CreatedAt
* string DisplayTime
* string? RelatedType
* int? RelatedId

4. OwnerNotificationMarkReadResponse.cs
   Fields:

* string Message
* int NotificationId
* bool IsRead

5. OwnerNotificationMarkAllReadResponse.cs
   Fields:

* string Message
* int UpdatedCount

6. OwnerNotificationListResponse.cs
   Fields:

* int Page
* int PageSize
* int TotalItems
* int TotalPages
* List<OwnerNotificationResponse> Items

Yêu cầu:

* Không trả thẳng entity Notification ra FE.
* Không dùng ké OwnerHorseListResponse cho notification.
* DTO phải nằm đúng namespace theo convention hiện tại.

Giai đoạn 3: Tạo OwnerNotificationsController

Tạo file nếu chưa có:

Controllers/Owner/OwnerNotificationsController.cs

Route:

api/owner/notifications

Yêu cầu:

* [ApiController]
* [Route("api/owner/notifications")]
* [Authorize(Roles = UserRoles.HorseOwner)]
* Nếu project có OwnerBaseController thì kế thừa OwnerBaseController.
* Dùng GetCurrentUserId().
* Dùng ValidateOwnerProfileAsync(ownerId).
* Chỉ Owner Active mới được gọi.
* Owner chỉ xem notification của chính mình.
* Không để Owner A xem notification của Owner B.

Giai đoạn 4: API summary 3 card

Thêm route:

GET /api/owner/notifications/summary

Logic:

1. Lấy ownerId từ token.
2. Validate Owner profile.
3. Unread = count Notifications where UserId = ownerId and IsRead = false.
4. Invitations = count notification liên quan Jockey invitation / jockey response.
5. UpcomingRaces = count race sắp tới của Owner.

Invitations:

* Vì Notification model có thể không có Category/Type, hãy suy ra từ Title/Message.
* Tính invitations nếu Title/Message chứa các nhóm:

  * Invitation Accepted
  * Invitation Rejected
  * Official Jockey
  * Jockey
  * lời mời

UpcomingRaces:

* Tính trực tiếp từ RaceRegistrations + Races.
* RaceRegistration.OwnerId = ownerId.
* Race.RaceDate >= DateTime.UtcNow.
* Registration status thuộc:

  * RaceRegistrationStatuses.Approved
  * RaceRegistrationStatuses.JockeyInvited
  * RaceRegistrationStatuses.ReadyToRace
* Race chưa đóng/hủy theo constants hiện tại:

  * Ưu tiên dùng !RaceStatuses.IsClosedForJockeyAssignment(r.Race.Status)
  * Không dùng RaceStatuses.Open.
  * Không dùng RaceStatuses.Completed.
  * Không dùng hardcode "Open".

Response:
{
"unread": 12,
"invitations": 3,
"upcomingRaces": 5
}

Giai đoạn 5: API list notifications

Thêm route:

GET /api/owner/notifications?category=All&page=1&pageSize=10

Category hỗ trợ:

* All
* Registrations
* Jockeys
* Tournaments

Logic:

1. Lấy ownerId từ token.
2. Validate Owner profile.
3. Query Notifications.AsNoTracking().
4. Chỉ lấy notification.UserId = ownerId.
5. Filter category nếu category khác All.
6. Sort CreatedAt DESC.
7. page mặc định 1.
8. pageSize mặc định 10, clamp 1–50.
9. Trả OwnerNotificationListResponse.

Category mapping:

* Nếu Notification có Category/Type thì ưu tiên dùng field hiện có.
* Nếu không có thì dùng helper private ResolveCategory(title, message).

ResolveCategory:

* Registrations nếu Title/Message chứa:
  Registration, đăng ký, Approved, Rejected, Returned.
* Jockeys nếu Title/Message chứa:
  Invitation, Jockey, lời mời, Official Jockey.
* Tournaments nếu Title/Message chứa:
  Tournament, Race, lịch đua, giải đấu, Upcoming.

ResolveStatusLabel:

* Approved nếu Title/Message chứa Approved.
* Confirmed nếu Title/Message chứa Accepted, Confirmed hoặc Official.
* Returned nếu Title/Message chứa Returned.
* Rejected nếu Title/Message chứa Rejected.
* Pending nếu Title/Message chứa Pending.
* null nếu không xác định.

DisplayTime:

* Tạo helper BuildDisplayTime(DateTime createdAt).
* Có thể trả dạng đơn giản như "2 minutes ago", "1 hour ago", "3 days ago".
* Không cần dùng package ngoài.

Giai đoạn 6: API detail notification

Thêm route:

GET /api/owner/notifications/{notificationId:int}

Logic:

1. Lấy ownerId.
2. Validate Owner profile.
3. Tìm notification theo notificationId và UserId = ownerId.
4. Nếu không có thì trả 404.
5. Trả OwnerNotificationDetailResponse.
6. Giai đoạn đầu không tự mark read khi mở detail; mark read dùng API riêng.

Giai đoạn 7: API mark read

Thêm route:

PUT /api/owner/notifications/{notificationId:int}/read

Logic:

1. Lấy ownerId.
2. Validate Owner profile.
3. Tìm notification theo notificationId và UserId = ownerId.
4. Nếu không có thì trả 404.
5. Set IsRead = true.
6. SaveChanges.

Response:
{
"message": "Notification marked as read.",
"notificationId": 1,
"isRead": true
}

Giai đoạn 8: API mark all read

Thêm route:

PUT /api/owner/notifications/read-all

Lưu ý route:

* Route read-all phải không bị nhầm với {notificationId:int}.
* Các route có id phải dùng constraint int:
  GET "{notificationId:int}"
  PUT "{notificationId:int}/read"

Logic:

1. Lấy ownerId.
2. Validate Owner profile.
3. Lấy tất cả notification chưa đọc của Owner.
4. Set IsRead = true.
5. SaveChanges.
6. Trả updatedCount.

Response:
{
"message": "All notifications marked as read.",
"updatedCount": 12
}

Giai đoạn 9: Bổ sung tạo notification cho Owner khi Admin approve/reject registration

File cần kiểm tra:

Controllers/Admin/AdminRaceRegistrationsController.cs

Khi Admin approve registration:

* Chỉ tạo notification nếu status thật sự chuyển sang RaceRegistrationStatuses.Approved.
* Không tạo trùng notification nếu approve lại mà status không đổi.
* Tạo notification cho OwnerId của registration.

Title:
Registration Approved

Message:
[HorseName] registered for [TournamentName] has been approved.

Nếu chưa include được Horse/Tournament thì include nhẹ:

* RaceRegistration -> Horse
* RaceRegistration -> Race -> Tournament

Nếu thiếu dữ liệu tên thì message fallback:
Your registration has been approved.

Khi Admin reject registration:

* Chỉ tạo notification nếu status thật sự chuyển sang RaceRegistrationStatuses.Rejected.
* Tạo notification cho OwnerId.

Title:
Registration Rejected

Message:
Your registration for [HorseName] has been rejected.

Không sửa database.
Không đổi luồng approve/reject hiện có ngoài việc thêm notification.

Giai đoạn 10: Bổ sung tạo notification cho Owner khi Jockey accept/reject invitation

File cần kiểm tra:

Controllers/Jockey/JockeyInvitationsController.cs

Khi Jockey accept invitation:

* Chỉ tạo notification cho Owner nếu status thật sự chuyển sang InvitationStatuses.Accepted.
* Không gửi notification cho Jockey trong event này.
* OwnerId lấy từ invitation.Registration.OwnerId hoặc InvitedByOwnerId tùy model hiện có.
* Không làm Jockey Accept auto assign RaceRegistration.JockeyId.

Title:
Invitation Accepted

Message:
[JockeyName] accepted invitation for [HorseName].

Khi Jockey reject invitation:

* Chỉ tạo notification cho Owner nếu status thật sự chuyển sang InvitationStatuses.Rejected.

Title:
Invitation Rejected

Message:
[JockeyName] rejected invitation for [HorseName].

Nếu thiếu JockeyName/HorseName thì dùng fallback message đơn giản.

Giai đoạn 11: Không tạo upcoming race notification cố định

Không tạo background job.
Không tạo notification định kỳ.
Không thêm service scheduler.

UpcomingRaces trong summary chỉ tính trực tiếp từ RaceRegistrations + Races.

Giai đoạn 12: Tuân thủ constants hiện tại

Phải bám constants hiện tại:

* UserRoles.HorseOwner
* UserStatuses.Active nếu cần check user.
* RaceRegistrationStatuses.Approved
* RaceRegistrationStatuses.JockeyInvited
* RaceRegistrationStatuses.ReadyToRace
* RaceRegistrationStatuses.Rejected
* InvitationStatuses.Accepted
* InvitationStatuses.Rejected
* RaceStatuses.IsClosedForJockeyAssignment(...)
* Không dùng RaceStatuses.Open.
* Không dùng RaceStatuses.Closed.
* Không dùng RaceStatuses.Completed.
* Không dùng PredictionStatuses trong controller mới.

Theo quy ước hiện tại, RaceStatuses chỉ gồm Scheduled, AssignedReferee, RefereeReady, Ongoing, Finished, ResultPending, Published, Cancelled; Open/Closed/Completed không còn dùng cho Race. RaceRegistration dùng Pending, Approved, JockeyInvited, ReadyToRace, Rejected, Cancelled, Completed. Invitation dùng Pending, Accepted, Rejected, Cancelled, Expired. RacePrediction dùng RacePredictionStatuses, không dùng PredictionStatuses trong controller.
Giai đoạn 13: Rà soát constants cũ

Chạy:

rg 'RaceStatuses.(Open|Closed|Completed)' Controllers
rg 'Status == "Open"|r.Status == "Open"' Controllers
rg '\bPredictionStatuses\b' Controllers

Yêu cầu:

* Không còn RaceStatuses.Open/Closed/Completed trong Controllers.
* Không còn hardcode "Open" cho Race.
* Không còn PredictionStatuses trong Controllers.

Giai đoạn 14: Build

Chạy:

dotnet build

Yêu cầu:

* 0 error.
* Nếu có warning cũ không liên quan thì ghi rõ, không sửa lan man.
* Nếu lỗi do file vừa thêm/sửa thì sửa trực tiếp, không refactor lớn.

Giai đoạn 15: Test BE bằng Swagger/Postman

Test routes:

1. GET /api/owner/notifications/summary
2. GET /api/owner/notifications?category=All&page=1&pageSize=10
3. GET /api/owner/notifications?category=Registrations&page=1&pageSize=10
4. GET /api/owner/notifications?category=Jockeys&page=1&pageSize=10
5. GET /api/owner/notifications?category=Tournaments&page=1&pageSize=10
6. GET /api/owner/notifications/{notificationId}
7. PUT /api/owner/notifications/{notificationId}/read
8. PUT /api/owner/notifications/read-all

Test nghiệp vụ:

1. Owner A không xem notification Owner B.
2. Mark read xong IsRead = true.
3. Mark all read trả updatedCount đúng.
4. Admin approve registration thì Owner nhận Registration Approved.
5. Admin reject registration thì Owner nhận Registration Rejected.
6. Jockey accept invitation thì Owner nhận Invitation Accepted.
7. Jockey reject invitation thì Owner nhận Invitation Rejected.
8. Summary UpcomingRaces tính đúng registration Approved/JockeyInvited/ReadyToRace và race chưa đóng.

Giai đoạn 16: Commit nếu có thay đổi

Chạy:

git status --short
git diff --stat

Nếu có thay đổi hợp lệ:

git add .
git commit -m "add owner notifications api"

Không merge main.
Không push main nếu chưa được yêu cầu.

Giai đoạn 17: Báo cáo những gì đã làm

Sau khi xong, chỉ trả lời tối đa 15 dòng:

1. Build nền ban đầu có lỗi không
2. Notification model/DbSet có sẵn không
3. File đã tạo mới
4. File đã sửa
5. DTO đã tạo ở đâu
6. OwnerNotificationsController routes đã thêm
7. Summary tính Unread/Invitations/UpcomingRaces như thế nào
8. List notifications filter category như thế nào
9. Detail/mark read/mark all read đã làm chưa
10. Đã tạo notification cho Admin approve/reject registration chưa
11. Đã tạo notification cho Jockey accept/reject invitation chưa
12. UpcomingRaces dùng constants/helper nào
13. Kết quả rg constants cũ
14. dotnet build kết quả
15. Lỗi còn lại nếu có
