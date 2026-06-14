Tôi đồng ý cho sửa.

Giai đoạn 8: làm Jockey Dashboard.

Chỉ làm API dashboard cho Jockey. Không sửa Settings, không sửa Admin approve/reject, không sửa Auth nếu không bắt buộc, không sửa database, không migration, không sửa frontend, không refactor lớn, không in code dài ra terminal.

API cần tạo:
GET /api/jockey/dashboard

Mục tiêu:

* Chỉ Jockey đã được Admin approve mới gọi dashboard thành công.
* Jockey Pending hoặc chưa Active phải bị chặn.
* API chỉ đọc dữ liệu, không ghi database.

Trước khi sửa:

* Kiểm tra controller Jockey hiện có.
* Kiểm tra auth pattern lấy userId từ token.
* Kiểm tra entity/model/table liên quan:

  * users
  * jockeys
  * jockey_invitations
  * race_registrations
  * races
* Kiểm tra status constants hiện có cho invitation, race registration, race.
* Nếu tên bảng/entity/status khác mô tả, dùng đúng tên hiện có trong project.

Quyền truy cập:

* Token thiếu/sai: 401 theo pattern hiện có.
* User không phải Jockey: 403 Forbidden.
* Jockey chưa được approve:

  * user.Status != Active hoặc jockey.IsActive != true
  * trả 403 Forbidden hoặc response lỗi theo pattern hiện có.
* Jockey Active:

  * user.Status = Active
  * jockey.IsActive = true
  * được xem dashboard.

Response gợi ý:

{
"pendingInvitations": 3,
"acceptedRaces": 2,
"upcomingRaces": 1,
"completedRaces": 5,
"profileStatus": "Active",
"healthStatus": "Healthy"
}

Dữ liệu cần tính:

1. pendingInvitations

* Đếm từ jockey_invitations.
* Chỉ đếm lời mời của Jockey hiện tại.
* Chỉ đếm status Pending hoặc status tương đương hiện có.

2. acceptedRaces

* Đếm từ race_registrations.
* Chỉ đếm registration của Jockey hiện tại.
* Chỉ đếm status Accepted/Approved/Confirmed hoặc status tương đương hiện có.

3. upcomingRaces

* Đếm races sắp tới liên quan đến Jockey qua race_registrations.
* Race sắp tới là race date/time > now nếu DB có field thời gian.
* Nếu project có status race thì không đếm race Cancelled/Completed.

4. completedRaces

* Đếm race đã hoàn thành của Jockey qua race_registrations + races.
* Dựa vào race status Completed nếu có.
* Nếu không có status Completed thì dùng race date/time < now theo field hiện có.

5. profileStatus

* Lấy từ users.Status.

6. healthStatus

* Lấy từ jockeys.HealthStatus.

DTO cần tạo nếu chưa có:

* DTOs/Jockey/JockeyDashboardResponse.cs

Controller:

* Có thể thêm action vào JockeyProfileController nếu project đang gom Jockey APIs ở đó.
* Hoặc tạo Controllers/Jockey/JockeyDashboardController.cs nếu convention tách controller rõ hơn.
* Route phải là GET /api/jockey/dashboard.

Yêu cầu bảo vệ role khác:

* API này chỉ áp dụng cho role Jockey.
* Không sửa login/me của Owner/Admin/Staff.
* Không query dashboard cho role khác.
* Không thay đổi flow Admin/Owner.

Test cần đạt:

* Jockey Active gọi GET /api/jockey/dashboard => 200 OK.
* Jockey Pending gọi dashboard => bị chặn.
* Owner/Admin gọi dashboard => 403 Forbidden.
* pendingInvitations đếm đúng lời mời Pending.
* acceptedRaces đếm đúng race đã nhận.
* upcomingRaces đếm đúng race sắp tới.
* completedRaces đếm đúng race đã hoàn thành.
* Build không lỗi.

Nếu thiếu entity/table/status để tính chính xác, không tự bịa database. Hãy dùng field hiện có và ghi rõ giả định.

Sau khi sửa xong chỉ trả lời tối đa 9 dòng:

1. File đã tạo/sửa
2. Route dashboard
3. DTO đã tạo/sửa
4. Điều kiện chặn Pending
5. Cách đếm invitation
6. Cách đếm races
7. Có ảnh hưởng role khác không
8. Build command
9. Lỗi còn lại nếu có
