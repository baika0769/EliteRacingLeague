Tôi đồng ý cho sửa.

Giai đoạn 6: sửa JockeyCode đang null trong response Jockey Profile.

File cần sửa:

* Controllers/Jockey/JockeyProfileController.cs

Vấn đề:

* Hiện API Jockey Profile đang trả JockeyCode = null.
* Không đổi database.
* Không migration.
* Không thêm field mới.

Yêu cầu sửa:

* Khi map response Jockey Profile, nếu DB không có JockeyCode hoặc JockeyCode đang null thì trả mã tự sinh từ JockeyId.
* Format cần dùng:
  JockeyCode = $"J-{jockey.JockeyId:D5}"

Ví dụ:

* JockeyId = 12
* JockeyCode = "J-00012"

Yêu cầu:

* Chỉ sửa mapping response.
* Không sửa entity.
* Không sửa database.
* Không sửa DTO nếu DTO đã có JockeyCode.
* Không sửa frontend.
* Không refactor lớn.
* Không ảnh hưởng role khác.
* Không in code dài ra terminal.

Figma:

* Frontend/Figma nên dùng JockeyCode backend trả về.
* Không hard-code mã kiểu J-8829-XR.

Sau khi sửa xong chỉ trả lời tối đa 6 dòng:

1. File đã sửa
2. Field đã sửa
3. Format JockeyCode
4. Có đổi DB không
5. Build command
6. Lỗi còn lại nếu có
