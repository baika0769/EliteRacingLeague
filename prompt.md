Tôi đồng ý cho sửa.

Nhiệm vụ: sửa lại luồng Jockey profile để không tự Active và phân biệt rõ API xác minh với API cập nhật thông tin thường.

Phạm vi được sửa:

* Controllers/Jockey/JockeyProfileController.cs
* Controllers/AuthController.cs nếu cần chỉnh nextStep
* Controllers/Admin/AdminVerificationsController.cs nếu cần đồng bộ approve/reject
* DTO liên quan nếu thiếu response/request
* Không sửa database
* Không migration
* Không sửa frontend
* Không refactor lớn
* Không in code dài ra terminal

Logic bắt buộc:

1. API PUT /api/jockey/profile/verification
   Dùng cho hồ sơ xác minh Jockey.

Khi Jockey save hồ sơ xác minh:

* Update thông tin verification/document/experience.
* Nếu hồ sơ đủ:

  * user.Status = Pending
  * jockey.IsActive = false
  * nextStep = WaitForActivation
* Không được set user.Status = Active.
* Không được set jockey.IsActive = true.
* Không được trả nextStep = GoToDashboard.
* Response:
  message = "Đã gửi hồ sơ. Vui lòng chờ admin duyệt."
  status = "Pending"
  isActive = false
  nextStep = "WaitForActivation"

2. API PUT /api/jockey/profile/me
   Dùng cho Jockey đã Active sửa thông tin cá nhân thường.

Chỉ cho sửa thông tin thường nếu field có trong DB, ví dụ:

* phone
* profileImageUrl/avatar nếu project xem đây là thông tin thường
* bio nếu DB có
* các thông tin không phải giấy tờ xác minh

API này không được đổi:

* user.Status
* jockey.IsActive

Nếu Jockey sửa giấy tờ xác minh như:

* idCardFrontUrl
* idCardBackUrl
* certificateFileUrl
* healthCertificateUrl
  thì phải dùng API /api/jockey/profile/verification, không xử lý ở /profile/me.

3. Auth nextStep:

* EmailVerified = false => VerifyEmail
* Jockey Pending + chưa đủ hồ sơ => CompleteJockeyProfile
* Jockey Pending + đã đủ hồ sơ => WaitForActivation
* Jockey Active + jockey.IsActive = true => GoToDashboard
* Inactive => ContactSupport
* Banned => AccountBlocked
* Logic đặc biệt này chỉ áp dụng cho role Jockey.
* Role khác giữ nguyên luồng cũ.

4. Admin approve:
   Chỉ Admin approve mới được:

* user.Status = Active
* jockey.IsActive = true

5. Admin reject:

* user.Status = Inactive hoặc trạng thái reject hiện có
* jockey.IsActive = false
* nextStep sau login/me là ContactSupport hoặc Rejected nếu project có

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã sửa
2. API verification đã không tự Active chưa
3. API profile/me có giữ nguyên Status/IsActive chưa
4. Auth nextStep đã đúng chưa
5. Admin approve có set Active đúng chưa
6. Admin reject có sync IsActive false chưa
7. Role khác có bị ảnh hưởng không
8. Build command
9. Lỗi còn lại nếu có
