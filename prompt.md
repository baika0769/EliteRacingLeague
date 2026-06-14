Tôi đồng ý cho sửa.

Giai đoạn 3: hoàn chỉnh Dashboard và Pending Invitations cho Jockey.

Mục tiêu:

* Kiểm tra lại API Dashboard và Pending Invitations đã có.
* Chỉ sửa lỗi hoặc bổ sung phần còn thiếu.
* Không làm lại controller từ đầu nếu đã có.
* Bổ sung notification cho Jockey khi Owner gửi invitation nếu hệ thống đã có bảng/entity Notification.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

API đã có cần kiểm tra:

* GET /api/jockey/dashboard
* GET /api/jockey/invitations/pending
* PUT /api/jockey/invitations/{id}/accept
* PUT /api/jockey/invitations/{id}/reject

Phần 1: kiểm tra GET /api/jockey/dashboard

Dashboard cần trả đúng:

* pendingInvitations: số lời mời đang chờ của Jockey hiện tại.
* acceptedRaces: số race đã nhận.
* upcomingRaces: số race sắp tới.
* completedRaces: số race đã hoàn thành.
* profileStatus: users.Status.
* healthStatus: jockeys.HealthStatus.

Yêu cầu:

* Chỉ Jockey Active mới gọi được dashboard.
* Nếu user.Role không phải Jockey => 403.
* Nếu user.Status != Active hoặc jockey.IsActive != true => 403.
* API chỉ đọc dữ liệu, không ghi database.
* Không ảnh hưởng Owner/Admin/Staff.

Phần 2: kiểm tra Pending Invitations

GET /api/jockey/invitations/pending:

* Chỉ Jockey Active gọi được.
* Chỉ trả invitation của Jockey hiện tại.
* Chỉ trả invitation Status = Pending hoặc trạng thái pending tương ứng hiện có.
* Response cần đủ dữ liệu để frontend hiển thị: invitationId, raceId, raceName/title nếu có, raceDate nếu có, location nếu có, ownerName nếu có, status, createdAt/sentAt nếu có.

PUT /api/jockey/invitations/{id}/accept:

* Chỉ Jockey Active gọi được.
* Invitation phải thuộc Jockey hiện tại.
* Invitation phải đang Pending.
* Update jockey_invitations:

  * Status = Accepted
  * RespondedAt = now
* Update race_registrations liên quan:

  * JockeyId = currentJockeyId
  * Status = ReadyToRace hoặc trạng thái tương ứng hiện có.
* Không accept lại invitation đã accept/reject.
* Nếu có transaction pattern thì dùng transaction.

PUT /api/jockey/invitations/{id}/reject:

* Chỉ Jockey Active gọi được.
* Invitation phải thuộc Jockey hiện tại.
* Invitation phải đang Pending.
* Update jockey_invitations:

  * Status = Rejected
  * RespondedAt = now
* Không gắn Jockey vào race_registrations.
* Sau reject, Owner phải có thể mời Jockey khác theo logic hiện có.

Phần 3: bổ sung Notification khi Owner gửi invitation

Nhiệm vụ:

* Tìm API/controller/service nơi Owner gửi invitation cho Jockey.
* Khi Owner tạo JockeyInvitation thành công, nếu project đã có Notification entity/table/service thì tạo thêm notification cho Jockey.
* Không tạo migration mới.
* Không tự bịa bảng Notification nếu project chưa có.
* Nếu chưa có Notification entity/table, chỉ ghi rõ “Chưa có Notification infrastructure, cần làm giai đoạn riêng”.

Logic notification mong muốn:
Owner gửi invitation
=> Insert JockeyInvitation
=> Insert Notification cho user của Jockey

Notification nên có nội dung tương đương:

* Receiver/UserId = userId của Jockey
* Title = "Bạn có lời mời tham gia cuộc đua"
* Message = Owner hoặc race đã mời bạn tham gia cuộc đua
* Type = Invitation hoặc giá trị tương ứng hiện có
* IsRead = false
* CreatedAt = now
* Link/ReferenceId = invitationId hoặc raceId nếu DB có field

Yêu cầu bảo vệ role khác:

* Không sửa logic login/me.
* Không sửa Admin approve/reject.
* Không sửa Jockey Settings/Profile.
* Không ảnh hưởng Owner/Admin/Staff ngoài việc Owner gửi invitation có thêm notification.

Test cần đạt:

* Jockey Active gọi dashboard => 200 OK.
* Jockey Pending gọi dashboard => 403.
* Dashboard count pendingInvitations đúng.
* Jockey thấy pending invitation khi Owner đã gửi lời mời.
* Jockey accept => invitation Accepted, race_registration gắn Jockey.
* Jockey reject => invitation Rejected, Owner có thể mời người khác.
* Owner gửi invitation => Jockey có notification nếu project có Notification.
* Build không lỗi.

Sau khi xong chỉ trả lời tối đa 10 dòng:

1. File đã kiểm tra
2. File đã sửa
3. Dashboard đã đúng chưa
4. Pending invitations đã đúng chưa
5. Accept đã update invitation/race registration chưa
6. Reject có cho Owner mời lại chưa
7. Notification đã tạo được chưa
8. Có ảnh hưởng role khác không
9. Build command
10. Lỗi còn lại nếu có
