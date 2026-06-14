Tôi đồng ý cho sửa.

Giai đoạn 3: hoàn chỉnh API GET cho trang Jockey Settings.

Mục tiêu:

* Frontend chỉ cần 1 API GET để load toàn bộ trang Jockey Settings.
* API cần tạo/hoàn chỉnh: GET /api/jockey/profile/me
* Chỉ làm API GET profile/me và DTO response liên quan.
* Không làm PUT.
* Không làm Admin approve/reject.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

Trước khi sửa:

* Kiểm tra controller/service Jockey hiện có.
* Kiểm tra auth pattern lấy userId từ token.
* Kiểm tra entity/model:

  * User
  * Jockey
  * JockeyDistanceExperience
  * JockeyBreedExperience
  * HorseBreed
* Kiểm tra helper IsJockeyProfileCompleted đã tạo ở Giai đoạn 2.
* Kiểm tra constants distance/skill/health nếu đã có.
* Chỉ đọc file liên quan trực tiếp.

API cần có:
GET /api/jockey/profile/me

Quyền truy cập:

* Token thiếu/sai: trả 401 theo pattern hiện có.
* User không tồn tại: trả 401 hoặc 404 theo pattern hiện có.
* User không phải role Jockey: trả 403 Forbidden.
* Token Jockey hợp lệ: trả hồ sơ của chính Jockey đang đăng nhập.

Dữ liệu cần đọc:

* users: userId, fullName, email, phone, status, emailVerified, role
* jockeys: jockeyCode nếu có, weightKg, healthStatus, yearsOfExperience, certificateNo, certificateFileUrl, profileImageUrl, idCardFrontUrl, idCardBackUrl, healthCertificateUrl, isActive
* jockey_distance_experiences: distanceMeters, skillLevel
* jockey_breed_experiences + horse_breeds: breedId, breedName, experienceLevel

Response cần có dạng tương đương:

{
"userId": 12,
"jockeyCode": "J-12",
"fullName": "Sebastian Reid",
"email": "[s.reid@org.com](mailto:s.reid@org.com)",
"phone": "+1 (555) 234-8890",
"status": "Pending",
"isActive": false,
"nextStep": "WaitForActivation",
"weightKg": 54,
"healthStatus": "Healthy",
"yearsOfExperience": 8,
"certificateNo": "CERT-2024-001",
"certificateFileUrl": "/uploads/certificate_2024.pdf",
"profileImageUrl": "/uploads/profile.jpg",
"idCardFrontUrl": "/uploads/id_front.jpg",
"idCardBackUrl": "/uploads/id_back.jpg",
"healthCertificateUrl": "/uploads/health.pdf",
"distanceExperiences": [],
"breedExperiences": []
}

DTO cần có nếu chưa tồn tại:

* DTOs/Jockey/JockeyProfileResponse.cs
* DTOs/Jockey/JockeyDistanceExperienceResponse.cs
* DTOs/Jockey/JockeyBreedExperienceResponse.cs

Yêu cầu mapping distanceExperiences:

* Nếu Jockey đã có distance experience thì trả dữ liệu thật.
* Mỗi item gồm:

  * distanceMeters
  * label
  * skillLevel
* label map theo constants:

  * 1000 => 1000m Sprint
  * 1500 => 1500m Mid
  * 2400 => 2400m Endurance
* Nếu Jockey chưa có distance experience:

  * Có thể trả list rỗng nếu project đang dùng list rỗng.
  * Tốt hơn cho frontend là trả default 3 cự ly với skillLevel = NoExperience nếu convention cho phép.
  * Chọn cách ít phá code hiện tại nhất và ghi rõ đã chọn cách nào.

Yêu cầu mapping breedExperiences:

* Nếu Jockey có breed experience thì trả:

  * breedId
  * breedName
  * experienceLevel
* Nếu không có thì trả list rỗng.
* Breed experience không bắt buộc.

Logic nextStep:

* EmailVerified = false => VerifyEmail.
* Jockey Pending + hồ sơ chưa đủ theo IsJockeyProfileCompleted => CompleteJockeyProfile.
* Jockey Pending + hồ sơ đủ => WaitForActivation.
* Jockey Active + jockey.IsActive = true => GoToDashboard.
* Inactive => ContactSupport.
* Banned => AccountBlocked.
* Không tự set Active trong API GET.
* API GET chỉ đọc dữ liệu, không thay đổi database.

Yêu cầu bảo vệ role khác:

* Logic API này chỉ cho role Jockey.
* Owner/Admin/Staff gọi API này phải trả 403.
* Không sửa logic login/me của role khác trong giai đoạn này.

Yêu cầu code:

* Giữ style code hiện tại.
* Đặt DTO đúng folder convention.
* Dùng DbContext/repository/service theo pattern hiện có.
* Không đổi tên class/namespace/route cũ nếu không cần.
* Nếu field nào response cần nhưng DB không có, map null hoặc bỏ theo DTO hiện có và ghi rõ.
* Không in toàn bộ code ra terminal.

Test cần đạt:

* Token Jockey gọi GET /api/jockey/profile/me => trả hồ sơ của chính Jockey.
* Token Owner gọi API => 403 Forbidden.
* Jockey chưa có distance => trả list rỗng hoặc default 3 cự ly.
* Jockey có breed => trả breedName + experienceLevel.
* Build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Route GET đã hoàn chỉnh
3. DTO đã tạo/sửa
4. Distance trả list rỗng hay default 3 cự ly
5. Breed mapping đã có breedName chưa
6. nextStep đã dùng helper chưa
7. Owner/Admin/Staff có bị chặn 403 không
8. Build command
9. Lỗi còn lại nếu có
10. Bước tiếp theo
