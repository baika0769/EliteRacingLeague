Tôi đồng ý cho sửa.

Giai đoạn ưu tiên: sửa lỗi nghiêm trọng Jockey không được tự Active.

Nhiệm vụ:

* Kiểm tra và sửa luồng Jockey update hồ sơ, Auth nextStep, Admin approve/reject.
* Chỉ sửa file thật sự cần thiết.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

File cần kiểm tra/sửa nếu liên quan:

* Controllers/Jockey/JockeyProfileController.cs
* Controllers/AuthController.cs
* Controllers/Admin/AdminVerificationsController.cs
* DTO/constants liên quan nếu cần, nhưng không sửa lan man.

Logic sai cần loại bỏ:

* Jockey update hồ sơ xong mà backend set user.Status = Active.
* Jockey update hồ sơ xong mà backend set jockey.IsActive = true.
* Jockey update hồ sơ xong mà response trả nextStep = GoToDashboard.

Logic đúng bắt buộc:

1. Khi Jockey PUT /api/jockey/profile/verification hoặc API save hồ sơ:

* Update thông tin hồ sơ Jockey.
* Update/replace distance experiences.
* Update/replace breed experiences nếu có.
* Sau khi hồ sơ đủ:

  * user.Status = Pending
  * jockey.IsActive = false
  * nextStep = WaitForActivation
* Không được set Active ở API này.
* Không được trả GoToDashboard ở API này.
* Response đúng:
  message = "Đã gửi hồ sơ. Vui lòng chờ admin duyệt."
  status = "Pending"
  isActive = false
  nextStep = "WaitForActivation"

2. Auth login/me nextStep:

* EmailVerified = false => VerifyEmail.
* User/Jockey Banned => AccountBlocked.
* User/Jockey Inactive => ContactSupport hoặc Rejected nếu project đã có nextStep Rejected.
* Role Jockey + Pending + chưa đủ hồ sơ => CompleteJockeyProfile.
* Role Jockey + Pending + đã đủ hồ sơ => WaitForActivation.
* Role Jockey + Active + jockey.IsActive = true => GoToDashboard.
* Role khác Jockey giữ logic cũ, không áp dụng CompleteJockeyProfile/WaitForActivation.

3. Admin approve:

* Chỉ Admin approve mới được:

  * user.Status = Active
  * jockey.IsActive = true
  * nextStep login/me sau đó mới là GoToDashboard.

4. Admin reject:

* Khi Admin reject thì đồng bộ trạng thái với bảng Jockey.
* user.Status = Inactive hoặc status reject hiện có của project.
* jockey.IsActive = false.
* nextStep sau login/me là ContactSupport hoặc Rejected theo constants hiện có.

Helper cần kiểm tra/tạo:
private static bool IsJockeyProfileCompleted(Jockey jockey)

Điều kiện hồ sơ Jockey đủ:

* ProfileImageUrl có giá trị.
* IdCardFrontUrl có giá trị.
* IdCardBackUrl có giá trị.
* CertificateFileUrl có giá trị.
* HealthCertificateUrl có giá trị.
* WeightKg > 0.
* YearsOfExperience >= 0.
* HealthStatus hợp lệ.
* Distance experience có dữ liệu hợp lệ, tốt nhất đủ 3 cự ly 1000, 1500, 2400.
* Breed experience không bắt buộc.

Yêu cầu bảo vệ role khác:

* Logic đặc biệt chỉ áp dụng cho user.Role == Jockey.
* Owner/Admin/Staff giữ nguyên luồng login/me hiện tại.
* Không thay đổi response contract ngoài giá trị nextStep/message nếu cần.

Sau khi sửa:

* Chạy build nếu được phép.
* Chỉ trả lời tối đa 10 dòng:

  1. File đã sửa
  2. Đã bỏ tự Active ở PUT chưa
  3. PUT trả WaitForActivation chưa
  4. Auth nextStep Jockey Pending chưa đủ hồ sơ
  5. Auth nextStep Jockey Pending đủ hồ sơ
  6. Admin approve set Active đúng chưa
  7. Admin reject sync đúng chưa
  8. Role khác có giữ nguyên không
  9. Build command
  10. Lỗi còn lại nếu có
