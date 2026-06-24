Tôi đồng ý cho sửa.

Giai đoạn BE: bổ sung HealthCertificateImageUrl cho Horse và các API Owner quản lý ngựa.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã đúng thì giữ nguyên.
* Không sửa database.
* Không migration.
* Không tạo migration.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi nghiệp vụ cũ.
* Không tạo DTO mới nếu DTO hiện tại đã đủ chỗ mở rộng.
* Không sửa lan man controller khác.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:
Bổ sung field:

HealthCertificateImageUrl

để Owner có thể:

1. Gửi URL ảnh giấy sức khỏe khi tạo ngựa.
2. Cập nhật URL ảnh giấy sức khỏe khi sửa ngựa.
3. Xem URL ảnh giấy sức khỏe ở danh sách My Horses.
4. Xem URL ảnh giấy sức khỏe ở detail ngựa.
5. Xem URL ảnh giấy sức khỏe trong API performance nếu endpoint đã tồn tại.

Lưu ý DB:

* SQL đã có cột:
  health_certificate_image_url
* Chỉ map property vào cột này.
* Không tạo migration.
* Không sửa schema DB.

File cần sửa:

1. Models/Horse.cs
2. Data/EliteRacingLeagueContext.cs
3. DTOs/Owner/CreateOwnerHorseRequest.cs
4. DTOs/Owner/UpdateOwnerHorseRequest.cs
5. DTOs/Owner/OwnerHorseResponse.cs
6. Controllers/Owner/OwnerHorsesController.cs
7. DTOs/Owner/Results/OwnerHorsePerformanceInfoResponse.cs nếu endpoint performance đã tồn tại

Giai đoạn 1: Map field vào model

File:
Models/Horse.cs

Hiện có:

public string? ImageUrl { get; set; }

Thêm gần ImageUrl:

public string? HealthCertificateImageUrl { get; set; }

Yêu cầu:

* Không đổi tên ImageUrl.
* Không xóa field cũ.
* Không thêm validation phức tạp.

Giai đoạn 2: Map cột DB trong DbContext

File:
Data/EliteRacingLeagueContext.cs

Trong cấu hình entity Horse, tìm mapping ImageUrl:

entity.Property(e => e.ImageUrl)
.HasMaxLength(500)
.HasColumnName("image_url");

Thêm mapping:

entity.Property(e => e.HealthCertificateImageUrl)
.HasMaxLength(500)
.HasColumnName("health_certificate_image_url");

Yêu cầu:

* Không tạo migration.
* Không sửa schema.
* Không đổi mapping ImageUrl cũ.

Giai đoạn 3: Cho phép Owner gửi URL khi tạo ngựa

File:
DTOs/Owner/CreateOwnerHorseRequest.cs

Thêm:

public string? HealthCertificateImageUrl { get; set; }

File:
Controllers/Owner/OwnerHorsesController.cs

Trong API:

POST /api/owner/horses

Khi tạo Horse, map thêm:

HealthCertificateImageUrl = request.HealthCertificateImageUrl

Luồng đúng:
Owner upload ảnh giấy sức khỏe bằng API upload hiện có
→ nhận URL
→ gửi HealthCertificateImageUrl khi tạo ngựa
→ BE lưu vào horses.health_certificate_image_url.

Yêu cầu:

* Không upload file trực tiếp trong API tạo ngựa.
* API tạo ngựa chỉ nhận URL.
* Không đổi validation cũ nếu không cần.

Giai đoạn 4: Cho phép Owner cập nhật URL ảnh giấy sức khỏe

File:
DTOs/Owner/UpdateOwnerHorseRequest.cs

Thêm:

public string? HealthCertificateImageUrl { get; set; }

File:
Controllers/Owner/OwnerHorsesController.cs

Trong API:

PUT /api/owner/horses/{horseId}

Map thêm:

horse.HealthCertificateImageUrl = request.HealthCertificateImageUrl;

Lưu ý:

* Nếu update hiện tại chỉ cập nhật khi field khác null thì giữ đúng pattern đó.
* Nếu update hiện tại cho phép ghi đè null thì giữ đúng pattern đó.
* Không đổi nghiệp vụ các field khác.

Giai đoạn 5: Trả ảnh giấy sức khỏe ở danh sách My Horses

File:
DTOs/Owner/OwnerHorseResponse.cs

Thêm:

public string? HealthCertificateImageUrl { get; set; }

File:
Controllers/Owner/OwnerHorsesController.cs

Trong API:

GET /api/owner/horses

Map thêm:

HealthCertificateImageUrl = h.HealthCertificateImageUrl

Mục tiêu:
Danh sách My Horses trả được field:

healthCertificateImageUrl

để FE biết ngựa nào đã có hoặc chưa có giấy sức khỏe.

Giai đoạn 6: Trả ảnh giấy sức khỏe ở detail ngựa

Vẫn dùng:

* DTOs/Owner/OwnerHorseResponse.cs
* Controllers/Owner/OwnerHorsesController.cs

Trong API:

GET /api/owner/horses/{horseId}

Map thêm:

HealthCertificateImageUrl = h.HealthCertificateImageUrl

Mục tiêu:
Trang detail ngựa có URL ảnh giấy sức khỏe.

Giai đoạn 7: Trả ảnh giấy sức khỏe ở API performance nếu đã có

Kiểm tra endpoint:

GET /api/owner/horses/{horseId}/performance

Nếu endpoint này đã tồn tại trong OwnerHorsesController.cs thì sửa thêm.

File:
DTOs/Owner/Results/OwnerHorsePerformanceInfoResponse.cs

Thêm:

public string? HealthCertificateImageUrl { get; set; }

Trong OwnerHorsesController.cs, phần select horseInfo thêm:

h.HealthCertificateImageUrl

Rồi map vào response Horse:

HealthCertificateImageUrl = horseInfo.HealthCertificateImageUrl

Yêu cầu:

* Nếu endpoint performance chưa có trong source hiện tại thì không tạo mới trong task này.
* Nếu endpoint performance đã có thì bắt buộc bổ sung field này.
* Không sửa logic raceHistory/performance nếu không liên quan.
* Không sửa DTO khác.

Giai đoạn 8: Kiểm tra response JSON

Response JSON mong muốn có field camelCase:

healthCertificateImageUrl

Áp dụng cho:

* GET /api/owner/horses
* GET /api/owner/horses/{horseId}
* GET /api/owner/horses/{horseId}/performance nếu endpoint tồn tại

Giai đoạn 9: Test cần đạt

Test build:
dotnet build

Test tạo ngựa:
POST /api/owner/horses

Request có:
{
"horseName": "Horse A",
"imageUrl": "/uploads/horses/a.png",
"healthCertificateImageUrl": "/uploads/horses/health-a.png"
}

Kết quả:

* Horse.HealthCertificateImageUrl được lưu.

Test cập nhật ngựa:
PUT /api/owner/horses/{horseId}

Request có:
{
"healthCertificateImageUrl": "/uploads/horses/health-new.png"
}

Kết quả:

* health_certificate_image_url được cập nhật theo pattern update hiện tại.

Test danh sách:
GET /api/owner/horses

Response item có:
healthCertificateImageUrl

Test detail:
GET /api/owner/horses/{horseId}

Response có:
healthCertificateImageUrl

Test performance nếu endpoint tồn tại:
GET /api/owner/horses/{horseId}/performance

Response horse có:
healthCertificateImageUrl

Giai đoạn 10: Search kiểm tra

Sau khi sửa, chạy:

rg -n "HealthCertificateImageUrl" Models Data DTOs Controllers
rg -n "health_certificate_image_url" Data

Bổ sung vào phần “Không sửa”:

* Không sửa OwnerRegistrationsController.
* Không sửa AdminRaceRegistrationsController.
* Không sửa Jockey controller trong giai đoạn này.
* Không sửa Referee controller trong giai đoạn này.
* Không sửa logic đăng ký race.
* Không sửa logic duyệt registration.

Bổ sung vào phần yêu cầu cuối:

* Không dùng HealthCertificateImageUrl để kiểm tra điều kiện đăng ký race.
* Không dùng HealthCertificateImageUrl để kiểm tra điều kiện Admin duyệt/reject registration.
* HealthCertificateImageUrl chỉ là URL ảnh giấy sức khỏe để Owner lưu và FE hiển thị.
* Không biến HealthCertificateImageUrl thành điều kiện nghiệp vụ trong giai đoạn này.




Kết quả mong muốn:

* Models/Horse.cs có property.
* Data/EliteRacingLeagueContext.cs có mapping.
* CreateOwnerHorseRequest có field.
* UpdateOwnerHorseRequest có field.
* OwnerHorseResponse có field.
* OwnerHorsesController map field ở create/update/list/detail.
* OwnerHorsePerformanceInfoResponse và performance endpoint có field nếu endpoint tồn tại.

Sau khi sửa xong chỉ trả lời tối đa 9 dòng:

1. File đã sửa
2. Horse model đã có HealthCertificateImageUrl chưa
3. DbContext đã map health_certificate_image_url chưa
4. CreateOwnerHorseRequest đã nhận field chưa
5. UpdateOwnerHorseRequest đã nhận field chưa
6. OwnerHorseResponse list/detail đã trả field chưa
7. Performance response đã trả field chưa hoặc endpoint chưa tồn tại
8. Có sửa DB/migration/frontend không
9. Build command/kết quả build
