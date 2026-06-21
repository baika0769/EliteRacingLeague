Tôi đồng ý cho sửa.

Giai đoạn BE: hoàn thiện Owner Notifications.

Lưu ý quan trọng:

* Chỉ đọc và phân tích source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã có và đúng thì giữ nguyên.
* Không tạo trùng controller/DTO/service.
* Không sửa database.
* Không migration.
* Không tạo bảng notifications mới nếu chưa có.
* Không thêm field DB mới vào Notification.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:

1. Kiểm tra bảng/model Notification hiện có.
2. Tạo DTO cho Owner Notifications.
3. Tạo OwnerNotificationsController.
4. Làm API summary 3 card.
5. Làm API list notifications có filter category + phân trang.
6. Làm API detail notification.
7. Làm API mark read.
8. Làm API mark all read.
9. Bổ sung tạo notification cho Owner ở các nghiệp vụ quan trọng.

Giai đoạn 0: Kiểm tra build nền

Việc đầu tiên:

* Chạy dotnet build.
* Nếu có lỗi build cũ thì sửa lỗi build trực tiếp trước.
* Không thêm logic mới khi project đang build lỗi.

Giai đoạn 1: Kiểm tra Notification infrastructure

Kiểm tra các file:

* Models/Notification.cs
* Data/EliteRacingLeagueContext.cs
* Controllers/Jockey/JockeyNotificationsController.cs nếu có
* Controllers/Referee/RefereeNotificationsController.cs nếu có
* Controllers/Spectator/SpectatorNotificationsController.cs nếu có

Cần xác định Notification hiện có các field nào:

* NotificationId
* UserId
* Title
* Message
* IsRead
* CreatedAt
* Type hoặc Category nếu có
* RelatedId nếu có
* RelatedType nếu có
* NotificationType nếu có

Yêu cầu:

* Nếu Models/Notification.cs và DbSet<Notification> đã tồn tại thì dùng model hiện có.
* Nếu Notification model chỉ có UserId, Title, Message, IsRead, CreatedAt thì vẫn làm API được.
* Nếu thiếu Category/Type/RelatedId/RelatedType thì DTO vẫn trả được Category/StatusLabel bằng cách suy ra từ Title/Message; RelatedId/RelatedType trả null.
* Không thêm field vào DB.
* Không migration.
* Nếu project hoàn toàn chưa có Notification model hoặc DbSet thì dừng phần tạo API và báo rõ thiếu Notification infrastructure, không tự tạo bảng.

Giai đoạn 2: Tạo DTO cho Owner Notifications

Tạo thư mục nếu chưa có:

* DTOs/Owner/Notifications

Hoặc nếu project đang gom DTO Owner ở nơi khác thì thêm đúng convention hiện tại.

Tạo DTO nếu chưa có:

1. OwnerNotificationSummaryResponse

Fields:

* int Unread
* int Invitations
* int UpcomingRaces

2. OwnerNotificationResponse

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

3. OwnerNotificationDetailResponse

Fields:

* int NotificationId
* string Title
* string Message
* string Category
* string? StatusLabel
* bool IsRead
* DateTime CreatedAt
* string? RelatedType
* int? RelatedId

4. OwnerNotificationMarkReadResponse

Fields:

* string Message
* int NotificationId
* bool IsRead

5. OwnerNotificationMarkAllReadResponse nếu chưa có

Fields:

* string Message
* int UpdatedCount

Yêu cầu:

* Không trả thẳng entity Notification ra FE.
* DTO Category/StatusLabel có thể suy ra từ Title/Message nếu DB không có field category.

Giai đoạn 3: Tạo OwnerNotificationsController

Tạo file nếu chưa có:

* Controllers/Owner/OwnerNotificationsController.cs

Route:

* /api/owner/notifications

Yêu cầu:

* [ApiController]
* [Route("api/owner/notifications")]
* [Authorize(Roles = UserRoles.HorseOwner)]
* Nếu project có OwnerBaseController thì kế thừa OwnerBaseController.
* Dùng pattern hiện có:

  * GetCurrentUserId()
  * ValidateOwnerProfileAsync(ownerId)
  * InvalidToken()
* Chỉ Owner Active mới được gọi.
* Owner chỉ xem notification của chính mình.

Giai đoạn 4: API summary 3 card

Thêm API:
GET /api/owner/notifications/summary

Logic:

1. Lấy ownerId từ token.
2. Validate Owner profile.
3. Unread = count Notifications where UserId = ownerId and IsRead = false.
4. Invitations = count notification liên quan Jockey invitation / jockey response của Owner.
5. UpcomingRaces = count race sắp tới của Owner.

Invitations:

* Nếu Notification có Category/Type thì lọc category Jockeys hoặc Invitations.
* Nếu Notification không có Category/Type thì suy ra bằng Title/Message chứa:

  * Invitation Accepted
  * Invitation Rejected
  * Official Jockey
  * Jockey
  * lời mời
* Không làm lỗi nếu không có field Category.

UpcomingRaces tính trực tiếp từ RaceRegistrations + Races:

* RaceRegistration.OwnerId = ownerId
* Race.RaceDate >= DateTime.UtcNow
* Registration status thuộc:

  * Approved
  * JockeyInvited
  * ReadyToRace
* Race chưa đóng/hủy:

  * Không lấy Cancelled.
  * Nếu RaceStatuses helper có IsClosedForJockeyAssignment hoặc status Published/Finished/ResultPending thì loại theo chuẩn hiện có.

Response:
{
"unread": 12,
"invitations": 3,
"upcomingRaces": 5
}

Giai đoạn 5: API danh sách notifications

Thêm API:
GET /api/owner/notifications?category=All&page=1&pageSize=10

Category hỗ trợ:

* All
* Registrations
* Jockeys
* Tournaments

Logic:

1. Lấy ownerId từ token.
2. Validate Owner profile.
3. Chỉ lấy notification.UserId = ownerId.
4. Không trả notification của user khác.
5. Filter category nếu category khác All.
6. Sort CreatedAt DESC.
7. Có phân trang.
8. page mặc định 1.
9. pageSize mặc định 10, clamp 1–50.

Mapping category:

Registrations:

* Registration Approved
* Registration Rejected
* Registration Returned
* Hoặc title/message chứa Registration, đăng ký, approved, rejected, returned.

Jockeys:

* Invitation Accepted
* Invitation Rejected
* Official Jockey selected
* Hoặc title/message chứa Invitation, Jockey, lời mời.

Tournaments:

* Tournament update
* Race schedule change
* Upcoming race
* Hoặc title/message chứa Tournament, Race, lịch đua, giải đấu.

Nếu Notification model có Category/Type:

* Ưu tiên dùng field đó.

Nếu không có:

* Suy ra category từ Title/Message bằng private helper trong controller, ví dụ:
  ResolveCategory(notification)
  ResolveStatusLabel(notification)
  BuildDisplayTime(notification.CreatedAt)

Response nên có dạng:
{
"page": 1,
"pageSize": 10,
"totalItems": 25,
"totalPages": 3,
"items": [...]
}

Nếu project đã có PagedResult convention thì dùng convention hiện có.

Giai đoạn 6: API detail notification

Thêm API:
GET /api/owner/notifications/{notificationId}

Logic:

1. Lấy ownerId từ token.
2. Validate Owner profile.
3. Tìm notification theo notificationId.
4. Kiểm tra notification.UserId == ownerId.
5. Nếu không thuộc owner thì trả 404 hoặc 403 theo pattern project.
6. Trả OwnerNotificationDetailResponse.

Yêu cầu:

* Giai đoạn đầu không tự mark read khi mở detail.
* Mark read dùng API riêng ở giai đoạn 7.

Giai đoạn 7: API mark read

Thêm API:
PUT /api/owner/notifications/{notificationId}/read

Logic:

1. Lấy ownerId.
2. Validate Owner profile.
3. Tìm notification theo notificationId.
4. Kiểm tra notification.UserId == ownerId.
5. Nếu không thuộc owner thì trả 404 hoặc 403 theo pattern project.
6. Set IsRead = true.
7. SaveChanges.

Response:
{
"message": "Notification marked as read.",
"notificationId": 1,
"isRead": true
}

Giai đoạn 8: API mark all read

Thêm API:
PUT /api/owner/notifications/read-all

Logic:

1. Lấy ownerId.
2. Validate Owner profile.
3. Lấy tất cả notification chưa đọc của Owner.
4. Set IsRead = true.
5. SaveChanges.
6. Trả số lượng đã update.

Response:
{
"message": "All notifications marked as read.",
"updatedCount": 12
}

Giai đoạn 9: Bổ sung nơi tạo notification cho Owner

Trang Notifications chỉ có dữ liệu thật nếu nghiệp vụ khác tạo notification.

Tạo helper private nếu cần để tránh lặp:
CreateOwnerNotification(int ownerId, string title, string message)

Không tạo service lớn nếu không cần.
Không thêm DB field mới.

Các nơi cần kiểm tra/bổ sung:

1. Admin approve registration

Tìm controller xử lý Admin approve RaceRegistration, ví dụ:

* Controllers/Admin/AdminRaceRegistrationsController.cs
* hoặc controller Admin khác đang approve registration

Khi Admin approve registration:

* Tạo notification cho Owner.

Title:
Registration Approved

Message:
[HorseName] registered for [TournamentName] has been approved.

Nếu chưa include Horse/Tournament thì include nhẹ để lấy tên.
Nếu không lấy được tên thì message đơn giản vẫn được.

2. Admin reject/return registration

Khi Admin reject hoặc return registration:

* Tạo notification cho Owner.

Title:
Registration Rejected
hoặc Registration Returned theo action thực tế.

Message:
Your registration for [HorseName] has been rejected/returned.

3. Jockey accepted invitation

File cần kiểm tra:

* Controllers/Jockey/JockeyInvitationsController.cs

Khi Jockey accept lời mời:

* Tạo notification cho Owner, không gửi cho Jockey.

Title:
Invitation Accepted

Message:
[JockeyName] accepted invitation for [HorseName].

Owner nhận notification:

* UserId = registration.OwnerId hoặc InvitedByOwnerId theo field hiện có.

4. Jockey rejected invitation

Khi Jockey reject lời mời:

* Tạo notification cho Owner.

Title:
Invitation Rejected

Message:
[JockeyName] rejected invitation for [HorseName].

5. Official Jockey selected

File cần kiểm tra:

* Controllers/Owner/OwnerJockeyAssignmentController.cs

Khi Owner bấm Sign official jockey:

* Có thể tạo notification cho chính Owner để ghi nhận, nếu hợp lý.
* Không bắt buộc nếu sợ dư notification.

Title:
Official Jockey Selected

Message:
[JockeyName] has been selected as official jockey for [HorseName].

6. Upcoming race

Giai đoạn này không tạo notification upcoming race cố định.
Không tạo background job.
Summary UpcomingRaces tính trực tiếp từ RaceRegistrations + Races.

Giai đoạn 10: Test BE

Test cần đạt:

1. dotnet build pass.

2. Owner gọi:
   GET /api/owner/notifications/summary
   Kết quả:

* Có unread
* Có invitations
* Có upcomingRaces

3. Owner gọi:
   GET /api/owner/notifications?category=All&page=1&pageSize=10
   Kết quả:

* Chỉ trả notification của Owner đang login.
* Sort CreatedAt DESC.
* Có paging.

4. Filter category:
   GET /api/owner/notifications?category=Registrations
   GET /api/owner/notifications?category=Jockeys
   GET /api/owner/notifications?category=Tournaments

5. Owner gọi detail notification của mình:
   GET /api/owner/notifications/{notificationId}
   => 200 OK.

6. Owner A gọi notification của Owner B:
   => 404 hoặc 403.

7. Mark read:
   PUT /api/owner/notifications/{notificationId}/read
   => IsRead = true.

8. Mark all read:
   PUT /api/owner/notifications/read-all
   => updatedCount đúng.

9. Admin approve registration:
   => Owner nhận notification Registration Approved.

10. Admin reject registration:
    => Owner nhận notification Registration Rejected.

11. Jockey accept invitation:
    => Owner nhận notification Invitation Accepted.

12. Jockey reject invitation:
    => Owner nhận notification Invitation Rejected.

Yêu cầu cuối:
*Khi tạo notification ở Admin approve/reject hoặc Jockey accept/reject, nếu action bị gọi lại nhiều lần thì tránh tạo notification trùng không cần thiết, hoặc chỉ tạo khi status thật sự thay đổi.
* Không ảnh hưởng Jockey/Referee/Spectator notifications.
* Không ảnh hưởng Owner Dashboard/My Horse/My Registrations.
* Không sửa database/migration.
* Không tạo background job.
* dotnet build phải pass.

Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. Build nền ban đầu có lỗi không
2. Notification model/DbSet có sẵn không
3. File đã tạo mới
4. File đã sửa
5. OwnerNotificationsController routes
6. DTO đã tạo ở đâu
7. Summary đã tính unread/invitations/upcomingRaces chưa
8. List/detail/mark read/mark all read đã làm chưa
9. Category dùng field DB hay suy ra từ Title/Message
10. Đã bổ sung notification cho Admin approve/reject registration chưa
11. Đã bổ sung notification cho Jockey accept/reject invitation chưa
12. Build command/lỗi còn lại
