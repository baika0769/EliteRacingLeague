Tôi đồng ý cho sửa.

Giai đoạn 6: sửa API PUT Jockey Verification / Jockey Settings save.

Chỉ sửa API PUT verification/settings cho Jockey. Không làm GET, không làm frontend, không sửa database, không migration, không refactor lớn.

Mục tiêu:

* API PUT dùng UpdateJockeyVerificationRequest đã tạo.
* Jockey cập nhật hồ sơ xong thì chờ Admin duyệt.
* Tuyệt đối không set Active trong API này.

Logic bắt buộc:

1. Lấy jockeyId/userId từ token theo pattern auth hiện có.
2. Kiểm tra user tồn tại.
3. Kiểm tra user role là Jockey, nếu không đúng trả 403 Forbidden.
4. Kiểm tra email đã verify, nếu chưa verify trả nextStep = VerifyEmail.
5. Validate WeightKg > 0.
6. Validate YearsOfExperience >= 0.
7. Validate HealthStatus hợp lệ theo constants/enum hiện có.
8. Validate đủ document bắt buộc: avatar/profile image, national id front, national id back, racing certificate, health certificate nếu DB có các field này.
9. Update bảng jockeys.
10. Replace hoặc upsert jockey_distance_experiences theo request.
11. Replace hoặc upsert jockey_breed_experiences theo request.
12. Nếu hồ sơ đủ: giữ user.Status = Pending, set jockey.IsActive = false.
13. Trả response có nextStep = WaitForActivation.

Điểm cực kỳ quan trọng:

* Không set user.Status = Active trong API này.
* Không set jockey.IsActive = true trong API này.
* Chỉ Admin approve mới được active Jockey.

Yêu cầu code:

* Kiểm tra controller/service PUT hiện có trước khi sửa.
* Giữ route và style code hiện tại nếu đã có API PUT.
* Dùng DTO/constants đã tạo ở giai đoạn trước.
* Không in code ra terminal.
* Không giải thích dài.
* Nếu thiếu field trong DB để validate document, ghi rõ field thiếu và dùng field hiện có.

Sau khi xong chỉ trả lời tối đa 8 dòng:

1. File đã sửa
2. Route PUT đã sửa
3. Validate đã thêm
4. Logic status/isActive đã xử lý
5. Build command
6. Còn lỗi hay không
7. Bước tiếp theo
