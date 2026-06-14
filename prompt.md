Tôi đồng ý cho sửa.

Nhiệm vụ: kiểm tra lại Giai đoạn 6 cũ đã làm cho Jockey Settings, sau đó chỉ bổ sung phần còn thiếu. Không làm lại từ đầu nếu code đã có.

Phạm vi:

* Kiểm tra API PUT /api/jockey/profile/verification hiện tại.
* Kiểm tra API GET /api/jockey/profile/me hiện tại.
* Kiểm tra lookup APIs:

  * GET /api/jockey/lookups/settings-options
  * GET /api/jockey/lookups/horse-breeds
* Kiểm tra DTO/constants liên quan.

Quy tắc quan trọng:

* Không refactor lớn.
* Không sửa database.
* Không tạo migration.
* Không sửa frontend.
* Không in code dài ra terminal.
* Không đổi route cũ nếu đã có route hợp lý.
* Chỉ sửa phần thiếu hoặc sai.
* Nếu API/DTO nào đã đúng thì giữ nguyên.

Yêu cầu kiểm tra PUT /api/jockey/profile/verification:

* Lấy userId/jockeyId từ token.
* User phải tồn tại.
* Role phải là Jockey, sai role trả 403.
* Email chưa verify thì trả nextStep = VerifyEmail.
* Validate weightKg > 0.
* Validate yearsOfExperience >= 0.
* Validate healthStatus hợp lệ.
* Validate document bắt buộc nếu DB có field.
* Validate distanceMeters chỉ nhận 1000, 1500, 2400.
* Validate distance skill chỉ nhận NoExperience, Basic, Good, Expert.
* Validate breed experience chỉ nhận Basic, Good, Expert.
* Validate breedId tồn tại và active nếu DB có trạng thái active.
* Update bảng jockeys.
* Replace hoặc upsert jockey_distance_experiences.
* Replace hoặc upsert jockey_breed_experiences.
* Sau khi save: user.Status phải là Pending, jockey.IsActive phải là false.
* Tuyệt đối không set Active.
* Tuyệt đối không trả nextStep = GoToDashboard ở PUT.
* Response sau PUT:
  message = "Đã gửi hồ sơ. Vui lòng chờ admin duyệt."
  status = "Pending"
  isActive = false
  nextStep = "WaitForActivation"

Yêu cầu kiểm tra GET /api/jockey/profile/me:

* Trả đủ dữ liệu để render trang Jockey Settings.
* Có userId, jockeyCode nếu DB có, fullName, email, phone, status, isActive, nextStep.
* Có weightKg, healthStatus, yearsOfExperience, certificateNo, certificateFileUrl, profileImageUrl, idCardFrontUrl, idCardBackUrl, healthCertificateUrl nếu DB có.
* Có distanceExperiences gồm distanceMeters, label, skillLevel.
* Có breedExperiences gồm breedId, breedName, experienceLevel.
* Logic nextStep:

  * Chưa verify email: VerifyEmail
  * Hồ sơ chưa đủ: CompleteJockeyProfile
  * Hồ sơ đủ nhưng Pending hoặc isActive false: WaitForActivation
  * Chỉ khi user.Status Active và jockey.IsActive true: GoToDashboard

Yêu cầu lookup:

* GET /api/jockey/lookups/settings-options trả:

  * healthStatuses: Healthy, NeedsCheck, Sick, Injured, Recovering, UnfitToRace
  * distanceOptions: 1000/1000m Sprint, 1500/1500m Mid, 2400/2400m Endurance
  * distanceSkillLevels: NoExperience, Basic, Good, Expert
  * breedExperienceLevels: Basic, Good, Expert
* GET /api/jockey/lookups/horse-breeds trả breedId, breedName.
* Nếu horse_breeds có isActive/status thì chỉ lấy active, nếu không có thì lấy toàn bộ.

Sau khi xong chỉ trả lời tối đa 12 dòng:

1. Phần đã có sẵn
2. Phần đã sửa/bổ sung
3. File đã sửa
4. Route GET profile
5. Route PUT verification
6. Route lookup options
7. Route lookup horse breeds
8. Đã đảm bảo PUT không Active hay chưa
9. Build command
10. Lỗi còn lại nếu có
11. Bước tiếp theo
