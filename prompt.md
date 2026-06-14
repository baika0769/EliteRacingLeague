Tôi đồng ý cho sửa.

Giai đoạn 2: tách validate Active Jockey dùng chung.

Mục tiêu:

* Tạo service dùng chung để validate Jockey Active.
* Giảm lặp code kiểm tra Jockey Active ở nhiều controller.
* Không đổi nghiệp vụ hiện có.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

File cần thêm:

* Services/JockeyAccessService.cs
* DTOs/Jockey/JockeyAccessResult.cs

Service cần có các hàm:

1. GetCurrentUserId(ClaimsPrincipal user)

* Lấy userId từ token theo pattern hiện có.

2. ValidateActiveJockeyAsync(ClaimsPrincipal user)

* Kiểm tra user hiện tại có phải Jockey active không.
* Trả JockeyAccessResult gồm trạng thái hợp lệ, user, jockey, status code, message, nextStep nếu cần.

3. GetCurrentJockeyAsync(...)

* Trả về User + Jockey nếu hợp lệ.
* Dùng lại logic validate active Jockey.

Logic validate chuẩn:

* Token không có userId => 401 Unauthorized.
* Không tìm thấy user => 401 Unauthorized.
* Role không phải Jockey => 403 Forbidden.
* Email chưa verify => 403, nextStep = VerifyEmail.
* User bị Banned => 403, nextStep = AccountBlocked.
* User chưa Active => 403, nextStep = WaitForActivation.
* Không có hồ sơ Jockey => 403, nextStep = CompleteJockeyProfile.
* Jockey.IsActive = false => 403, nextStep = WaitForActivation.
* Nếu hợp lệ: user.Status = Active và jockey.IsActive = true.

DTO JockeyAccessResult cần có tối thiểu:

* bool Succeeded
* int StatusCode
* string? Message
* string? NextStep
* User? User
* Jockey? Jockey

Sau khi có service, sửa các controller sau để dùng service validate:

* JockeyDashboardController.cs
* JockeyInvitationsController.cs
* JockeyNotificationsController.cs
* JockeyCalendarController.cs
* JockeyAvailabilitiesController.cs

Nếu controller nào chưa tồn tại thì bỏ qua, không tạo mới ở giai đoạn này trừ khi đã có nhu cầu rõ.

Yêu cầu code:

* Đăng ký JockeyAccessService vào DI nếu project cần.
* Dùng style DI hiện có.
* Không đổi route hiện có.
* Không đổi response contract nếu không cần.
* Không ảnh hưởng Owner/Admin/Staff.
* Không sửa AuthController nếu không bắt buộc.
* Không in toàn bộ code ra terminal.

Test cần đạt:

* Jockey Active gọi Dashboard/Calendar/Notifications/Invitations/Availabilities vẫn thành công.
* Jockey Pending bị chặn.
* Owner/Admin/Staff gọi Jockey API bị 403.
* Token sai hoặc thiếu userId bị 401.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Service đã tạo
3. DTO đã tạo
4. Controller đã áp dụng service
5. Logic 401/403 đã xử lý
6. Jockey Active còn dùng được không
7. Role khác có bị ảnh hưởng không
8. Build command
9. Lỗi còn lại nếu có
10. Bước tiếp theo
