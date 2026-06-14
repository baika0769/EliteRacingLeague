Tôi đồng ý cho sửa.

Giai đoạn 2: tạo helper kiểm tra hồ sơ Jockey đã hoàn thiện chưa.

Mục tiêu:

* Tạo logic dùng chung để kiểm tra Jockey đã điền đủ hồ sơ chưa.
* Logic này sẽ phục vụ Auth nextStep và API Jockey Settings.
* Không sửa flow login/me lớn ở giai đoạn này nếu chưa cần.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

Phạm vi được làm:

* Kiểm tra entity/model Jockey.
* Kiểm tra entity/model JockeyDistanceExperience.
* Kiểm tra constants health status, distance, distance skill nếu đã có.
* Tạo helper hoặc service theo convention hiện có:

  * Ưu tiên Helpers/JockeyProfileHelper.cs nếu project có folder Helpers.
  * Nếu project dùng service pattern rõ ràng thì tạo Services/JockeyProfileService.cs.
* Nếu AuthController hiện đã có helper private IsJockeyProfileCompleted thì có thể tách ra helper dùng chung, nhưng không làm thay đổi logic role khác.

Helper cần có:

* IsJockeyProfileCompleted(Jockey jockey)

Logic kiểm tra hồ sơ hoàn thiện:

* ProfileImageUrl có giá trị.
* IdCardFrontUrl có giá trị.
* IdCardBackUrl có giá trị.
* CertificateNo có giá trị.
* CertificateFileUrl có giá trị.
* HealthCertificateUrl có giá trị.
* WeightKg > 0.
* YearsOfExperience >= 0.
* HealthStatus hợp lệ theo constants/enum hiện có.
* Distance experience bắt buộc có đủ 3 cự ly: 1000, 1500, 2400.
* Mỗi distance experience phải có skill hợp lệ: NoExperience, Basic, Good, Expert.
* Breed experience không bắt buộc.

Yêu cầu bảo vệ role khác:

* Helper này chỉ kiểm tra object Jockey, không tự xử lý Owner/Admin/Staff.
* Không áp dụng CompleteJockeyProfile hoặc WaitForActivation cho role khác ở giai đoạn này.
* Không đổi response contract của login/me.

Kết quả mong muốn sau giai đoạn này:

* Có helper/service dùng chung để xác định:

  * Hồ sơ chưa đủ => CompleteJockeyProfile
  * Hồ sơ đủ nhưng chưa duyệt => WaitForActivation
  * Đã duyệt => GoToDashboard
* Nhưng giai đoạn này chỉ tạo helper, chưa cần sửa toàn bộ Auth flow nếu chưa bắt buộc.

Sau khi sửa xong chỉ trả lời tối đa 8 dòng:

1. File đã tạo/sửa
2. Helper/service đã tạo
3. Field bắt buộc đã kiểm tra
4. Distance experience có bắt đủ 3 dòng không
5. Breed experience có bắt buộc không
6. Role khác có bị ảnh hưởng không
7. Build command
8. Lỗi còn lại nếu có
