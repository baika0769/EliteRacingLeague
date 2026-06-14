Tôi đồng ý cho sửa.

Giai đoạn 4: làm trang Jockey Notifications.

Mục tiêu:

* Tạo API cho màn hình Jockey Notifications.
* Jockey xem danh sách notification, lọc, sắp xếp, phân trang.
* Jockey xem summary: total alerts, unread, invitations.
* Jockey xem detail notification.
* Jockey đánh dấu đã đọc một notification hoặc tất cả.
* Có thể xóa notification nếu DB/controller pattern hiện có hỗ trợ.
* Nếu notification liên quan invitation thì detail trả thêm raceDetail để frontend render card bên phải.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API cần tạo trong:
Controllers/Jockey/JockeyNotificationsController.cs

Routes cần có:

1. GET /api/jockey/notifications
2. GET /api/jockey/notifications/summary
3. GET /api/jockey/notifications/{id}
4. PUT /api/jockey/notifications/{id}/read
5. PUT /api/jockey/notifications/read-all
6. DELETE /api/jockey/notifications/{id} nếu project cho phép xóa notification

Trước khi sửa:

* Kiểm tra project đã có Notification entity/table chưa.
* Kiểm tra DbContext có DbSet Notifications chưa.
* Kiểm tra notification fields hiện có: NotificationId, UserId/ReceiverId, Title, Message, IsRead, CreatedAt, Type, ReferenceId, RaceId, InvitationId nếu có.
* Kiểm tra JockeyInvitation entity.
* Kiểm tra Race, RaceRegistration, Horse, Owner/User entity để map raceDetail nếu có.
* Kiểm tra auth pattern lấy userId/currentJockeyId từ token.
* Kiểm tra route/controller convention trong Controllers/Jockey.
* Chỉ đọc file liên quan trực tiếp.

Nếu project chưa có Notification entity/table:

* Không tự tạo migration.
* Không tự bịa table mới.
* Chỉ tạo được phần dựa trên existing entity nếu có.
* Nếu thiếu infrastructure, dừng và báo rõ: “Chưa có Notification entity/table, cần giai đoạn DB riêng.”

Quyền truy cập chung:

* Token thiếu/sai: 401 theo pattern hiện có.
* User không phải Jockey: 403 Forbidden.
* Jockey chưa Active:

  * user.Status != Active hoặc jockey.IsActive != true
  * trả 403 Forbidden.
* Jockey chỉ được thao tác notification của chính mình.
* Không ảnh hưởng Owner/Admin/Staff.

API 1: GET /api/jockey/notifications

Query params:

* status: All, Unread, Read
* date: lọc theo CreatedAt theo ngày, ví dụ 2024-07-14
* sort: Newest, Oldest
* page
* pageSize

Logic:

* Lấy notifications theo userId của Jockey đang login.
* status = All hoặc null: lấy tất cả.
* status = Unread: IsRead = false.
* status = Read: IsRead = true.
* date có giá trị: lọc CreatedAt theo ngày đó.
* sort = Newest hoặc null: CreatedAt giảm dần.
* sort = Oldest: CreatedAt tăng dần.
* page mặc định 1.
* pageSize mặc định 10, có giới hạn hợp lý nếu project có convention.
* Trả phân trang.

Response dạng:

{
"items": [
{
"notificationId": 1,
"title": "New Race Invitation: Grand Ascot Cup",
"message": "Trainer Arthur Pendleton has invited you to ride Thunderbolt.",
"isRead": false,
"createdAt": "2024-07-14T16:30:00Z",
"displayTime": "2 mins ago"
}
],
"totalItems": 128,
"page": 1,
"pageSize": 10
}

DTO cần tạo nếu chưa có:

* DTOs/Jockey/Notifications/JockeyNotificationListQuery.cs hoặc tên theo convention
* DTOs/Jockey/Notifications/JockeyNotificationListResponse.cs
* DTOs/Jockey/Notifications/JockeyNotificationItemResponse.cs

API 2: GET /api/jockey/notifications/summary

Logic:

* totalAlerts = Count notifications theo userId của Jockey.
* unread = Count notifications where userId = current user và IsRead = false.
* invitations = Count JockeyInvitations của currentJockeyId where Status = Pending.

Response:

{
"totalAlerts": 128,
"unread": 12,
"invitations": 7
}

DTO cần tạo nếu chưa có:

* DTOs/Jockey/Notifications/JockeyNotificationSummaryResponse.cs

API 3: GET /api/jockey/notifications/{id}

Logic:

* Chỉ lấy notification thuộc userId của Jockey hiện tại.
* Nếu không tồn tại hoặc không thuộc user hiện tại: 404 hoặc 403 theo pattern hiện có.
* Trả detail notification.
* Nếu notification liên quan JockeyInvitation thì include raceDetail nếu có đủ dữ liệu.
* Không bắt buộc auto mark read khi xem detail, trừ khi project đã có convention như vậy.

Response detail dạng:

{
"notificationId": 1,
"title": "New Race Invitation: Grand Ascot Cup",
"message": "Owner has invited you to join this race.",
"isRead": false,
"createdAt": "2024-07-14T16:30:00Z",
"raceDetail": {
"raceId": 5,
"raceName": "Grand Ascot Cup - G1 Stakes",
"raceDate": "2024-07-14T16:30:00Z",
"location": "Ascot Racecourse",
"horseId": 10,
"horseName": "Thunderbolt",
"horseAge": 4,
"ownerName": "Arthur Pendleton",
"ownerMessage": "Thunderbolt is at peak performance."
}
}

DTO cần tạo nếu chưa có:

* DTOs/Jockey/Notifications/JockeyNotificationDetailResponse.cs
* DTOs/Jockey/Notifications/JockeyNotificationRaceDetailResponse.cs

Mapping raceDetail:

* Nếu Notification có InvitationId/ReferenceId trỏ tới JockeyInvitation thì join qua JockeyInvitation.
* Từ JockeyInvitation lấy Race/RaceRegistration/Horse/Owner tùy model hiện có.
* raceId lấy từ Race.
* raceName lấy từ Race.Name/Title nếu có.
* raceDate lấy từ Race.Date/StartTime nếu có.
* location lấy từ Race.Location nếu có.
* horseId/horseName/horseAge lấy từ Horse nếu có.
* ownerName lấy từ Owner/User nếu có.
* ownerMessage lấy từ invitation.Message/Note nếu có.
* Nếu field nào không có trong DB thì map null, không tự bịa field.

API 4: PUT /api/jockey/notifications/{id}/read

Logic:

* Chỉ update notification thuộc userId của Jockey hiện tại.
* Set IsRead = true.
* Nếu có ReadAt field thì set ReadAt = now.
* SaveChanges.
* Response message thành công.

Response gợi ý:
{
"message": "Đã đánh dấu notification là đã đọc.",
"notificationId": 1,
"isRead": true
}

API 5: PUT /api/jockey/notifications/read-all

Logic:

* Lấy tất cả notification của Jockey hiện tại có IsRead = false.
* Set IsRead = true.
* Nếu có ReadAt field thì set ReadAt = now.
* SaveChanges.
* Response trả số lượng đã update.

Response gợi ý:
{
"message": "Đã đánh dấu tất cả notification là đã đọc.",
"updatedCount": 12
}

API 6: DELETE /api/jockey/notifications/{id}

Logic:

* Chỉ xóa notification thuộc userId của Jockey hiện tại.
* Nếu project có soft delete thì dùng soft delete.
* Nếu không có soft delete và pattern cho phép hard delete thì remove.
* Nếu project không có pattern xóa notification thì không cần làm DELETE, chỉ báo rõ.

Yêu cầu displayTime:

* Tạo helper private hoặc method nhỏ để convert CreatedAt thành text đơn giản.
* Ví dụ:

  * dưới 1 phút: "Just now"
  * dưới 60 phút: "x mins ago"
  * dưới 24 giờ: "x hours ago"
  * còn lại: "x days ago"
* Không cần localization phức tạp ở giai đoạn này.

Yêu cầu bảo vệ role khác:

* API này chỉ áp dụng cho role Jockey.
* Không sửa AuthController.
* Không sửa Owner invitation flow ở giai đoạn này.
* Không sửa Admin.
* Không ảnh hưởng Owner/Admin/Staff.

Test cần đạt:

* Jockey Active gọi GET /api/jockey/notifications => 200 OK.
* Jockey Pending gọi => 403.
* Owner/Admin gọi => 403.
* status=Unread chỉ trả notification chưa đọc.
* status=Read chỉ trả notification đã đọc.
* date lọc đúng theo CreatedAt.
* sort=Newest trả mới nhất trước.
* sort=Oldest trả cũ nhất trước.
* page/pageSize trả đúng phân trang.
* summary trả đúng totalAlerts, unread, invitations.
* detail notification thuộc Jockey trả đúng.
* detail notification của user khác không được xem.
* mark read set IsRead = true.
* read-all cập nhật tất cả notification chưa đọc của Jockey.
* delete chỉ xóa notification của chính Jockey nếu có làm DELETE.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route list notifications
3. Route summary
4. Route detail
5. Route mark read/read-all
6. Route delete có làm không
7. raceDetail có map được không
8. Có ảnh hưởng role khác không
9. Build command
10. Lỗi còn lại nếu có
