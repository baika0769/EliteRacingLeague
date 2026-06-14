Tôi đồng ý cho sửa.

Làm lại Giai đoạn 7: sửa Auth nextStep cho Jockey.

Chỉ sửa AuthController hoặc file auth liên quan thật sự cần thiết. Không sửa database, không migration, không frontend, không refactor lớn.

Mục tiêu:

* Login/me phải trả nextStep đúng cho Jockey.
* AuthController.GetNextStep(user) hiện chưa biết Jockey đã điền hồ sơ hay chưa.
* Chỉ áp dụng logic hồ sơ cho user.Role == Jockey.
* Các role khác ngoài Jockey phải giữ nguyên logic login/me hiện tại.

Yêu cầu bảo vệ role khác:

* Không áp dụng CompleteJockeyProfile cho Owner/Admin/Staff.
* Không áp dụng WaitForActivation cho Owner/Admin/Staff.
* LoadJockeyForNextStepAsync chỉ query Jockey khi user.Role == Jockey.
* GetNextStep phải branch rõ:

  * Nếu không phải Jockey: giữ logic cũ.
  * Nếu là Jockey: mới kiểm tra hồ sơ Jockey.
* Không làm thay đổi response contract của login/me ngoài giá trị nextStep.
* Message “Vui lòng hoàn thiện hồ sơ” chỉ dùng cho Jockey Pending chưa đủ hồ sơ.

Logic nextStep:

* EmailVerified = false => VerifyEmail.
* Banned => AccountBlocked.
* Inactive => ContactSupport.
* Jockey Pending và chưa đủ hồ sơ => CompleteJockeyProfile.
* Jockey Pending và đã đủ hồ sơ => WaitForActivation.
* Jockey Active và jockey.IsActive = true => GoToDashboard.
* Role khác Jockey giữ logic cũ.

Tạo/sửa helper:
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

Không in code dài. Sau khi sửa chỉ trả lời tối đa 8 dòng:

1. File đã sửa
2. Method đã sửa
3. Helper đã tạo/sửa
4. Role khác có giữ nguyên không
5. Build command cần chạy
6. Lỗi còn lại nếu có
