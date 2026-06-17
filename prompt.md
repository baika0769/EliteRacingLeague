Tôi đồng ý cho sửa.

Giai đoạn BE: tạo API upload file cho ảnh ngựa và hồ sơ Jockey.

Mục tiêu:

* Tạo API upload file dùng chung cho FE.
* FE upload file lên BE, BE lưu file vào wwwroot/uploads/{category}.
* BE trả về URL để FE lưu vào database thông qua các API hiện có.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không in code dài ra terminal.

File cần tạo/sửa:

1. Controllers/UploadsController.cs
2. Program.cs
3. .gitignore
4. Tạo folder wwwroot/uploads
5. Tạo file rỗng wwwroot/uploads/.gitkeep nếu cần

API cần tạo:
POST /api/uploads

Authorize:

* API cần có [Authorize].
* Token hợp lệ mới được upload.

Request:
Content-Type: multipart/form-data

Form-data:

* file: File
* category: Text

Category hợp lệ:

* horses
* jockeys

Rule upload:

* category = horses: chỉ cho ảnh

  * .jpg
  * .jpeg
  * .png
  * .webp
* category = jockeys: cho ảnh hoặc pdf

  * .jpg
  * .jpeg
  * .png
  * .webp
  * .pdf
* Dung lượng tối đa: 10MB.
* Nếu file null hoặc rỗng: 400 BadRequest.
* Nếu category sai: 400 BadRequest.
* Nếu extension sai: 400 BadRequest.
* Tên file lưu bằng Guid để tránh trùng.
* Lưu file vào:

  * wwwroot/uploads/horses
  * wwwroot/uploads/jockeys
* Trả relative URL dạng:

  * /uploads/horses/{fileName}
  * /uploads/jockeys/{fileName}
* Trả thêm absoluteUrl.

Code controller cần theo style project hiện có, nhưng logic chính tương đương:

* Inject IWebHostEnvironment.
* Nếu _env.WebRootPath null thì fallback về Directory.GetCurrentDirectory()/wwwroot.
* Tạo folder nếu chưa tồn tại.
* Lưu file bằng FileStream.
* Response Ok gồm:

  * message
  * url
  * absoluteUrl
  * fileName
  * contentType
  * size

Response thành công ví dụ:
{
"message": "Upload file thành công.",
"url": "/uploads/horses/abc123.jpg",
"absoluteUrl": "http://localhost:5146/uploads/horses/abc123.jpg",
"fileName": "abc123.jpg",
"contentType": "image/jpeg",
"size": 12345
}

Sửa Program.cs:

* Tìm pipeline hiện có.
* Đảm bảo có app.UseStaticFiles(); trước app.UseAuthentication().
* Thứ tự mong muốn:
  app.UseHttpsRedirection();
  app.UseCors("AllowFrontend");
  app.UseStaticFiles();
  app.UseAuthentication();
  app.UseAuthorization();
  app.MapControllers();

Sửa .gitignore:
Thêm:
wwwroot/uploads/*
!wwwroot/uploads/.gitkeep

Tạo:

* wwwroot/uploads/.gitkeep

Yêu cầu bảo vệ:

* Không sửa API horse hiện có.
* Không sửa API jockey hiện có.
* Không sửa entity/model.
* Không sửa database.
* Không sửa appsettings.json hoặc connection string.
* Không ảnh hưởng Owner/Jockey/Admin flow khác.

Test cần đạt:

1. Upload horse image:
   POST /api/uploads
   Authorization: Bearer <token owner>
   form-data:

* file = horse.jpg
* category = horses
  => 200 OK, trả url /uploads/horses/...

2. Upload jockey pdf:
   POST /api/uploads
   Authorization: Bearer <token jockey>
   form-data:

* file = certificate.pdf
* category = jockeys
  => 200 OK, trả url /uploads/jockeys/...

3. Upload horses với pdf:
   => 400 BadRequest.

4. Upload category sai:
   => 400 BadRequest.

5. Upload file trên 10MB:
   => 400 BadRequest.

6. Sau upload, mở absoluteUrl trên browser phải xem được file.

7. dotnet build không lỗi.

Sau khi sửa xong chỉ trả lời tối đa 8 dòng:

1. File đã tạo/sửa
2. Route upload
3. Category hỗ trợ
4. Static files đã bật chưa
5. .gitignore đã thêm chưa
6. Test Postman cần chạy
7. Build command
8. Lỗi còn lại nếu có
