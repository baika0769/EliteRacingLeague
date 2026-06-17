Tôi đồng ý cho sửa.

Giai đoạn sửa lỗi: Jockey health_status đang dùng sai constants của Horse.

Mục tiêu:

1. Tạo constants riêng cho Jockey health status.
2. Sửa JockeyProfileController validate bằng constants mới.
3. Sửa JockeyLookupsController trả health status đúng cho Jockey.
4. Sửa các controller Owner đang kiểm tra Jockey có thể đua hay không.
5. Kiểm tra AuthController khi tạo Jockey mặc định healthStatus phải là Unknown.

Không được làm:

* Không sửa database.
* Không migration.
* Không sửa constraint DB.
* Không refactor lớn.
* Không sửa HorseHealthStatuses.
* Không sửa frontend.
* Không in code dài ra terminal.

File cần tạo:

* Constants/JockeyHealthStatuses.cs

Nội dung logic constants:

* Fit
* Injured
* Suspended
* Unknown

Constants cần có:

* All
* IsValid(string? status)
* Normalize(string? status)
* CanRace(string? status)

Yêu cầu:

* Normalize cho phép FE gửi fit, FIT, Fit nhưng backend lưu đúng chuẩn DB là Fit.
* CanRace chỉ trả true khi healthStatus = Fit.

File cần sửa 1:
Controllers/Jockey/JockeyProfileController.cs

Trong hàm UpdateVerification:

* Không được dùng HorseHealthStatuses để validate request.HealthStatus.
* Phải dùng JockeyHealthStatuses.Normalize(request.HealthStatus).
* Nếu Normalize trả null thì BadRequest:
  message = "Tình trạng sức khỏe Jockey không hợp lệ."
  allowedValues = JockeyHealthStatuses.All
  nextStep = AuthNextSteps.CompleteJockeyProfile
* Khi gán DB:
  jockey.HealthStatus = normalizedHealthStatus;
* Không lưu trực tiếp request.HealthStatus chưa normalize.

File cần sửa 2:
Controllers/Jockey/JockeyLookupsController.cs

Yêu cầu:

* Nếu đang trả healthStatuses = HorseHealthStatuses.All thì đổi sang JockeyHealthStatuses.All.
* Jockey Settings lookup phải trả:
  Fit
  Injured
  Suspended
  Unknown
* Không trả health status của Horse như Healthy, NeedsCheck, Sick, Recovering, UnfitToRace.

File cần sửa 3:
Các controller Owner liên quan chọn/mời Jockey:

* Controllers/Owner/OwnerJockeyAssignmentController.cs
* Controllers/Owner/OwnerRacesController.cs nếu có liên quan

Yêu cầu:

* Tìm các chỗ đang dùng HorseHealthStatuses.CanRace(jockey.HealthStatus) hoặc candidate.HealthStatus để kiểm tra Jockey.
* Nếu logic đó dùng cho Jockey thì đổi sang JockeyHealthStatuses.CanRace(...).
* Jockey chỉ được đua khi health_status = Fit.
* Những chỗ dùng HorseHealthStatuses cho Horse thì giữ nguyên.

File cần kiểm tra 4:
Controllers/AuthController.cs

Yêu cầu:

* Khi register role Jockey và tạo profile Jockey, HealthStatus mặc định phải là:
  JockeyHealthStatuses.Unknown
* Nếu chưa import constants thì thêm using đúng.
* Không được để Jockey HealthStatus = HorseHealthStatuses.Healthy.
* Không được để giá trị mặc định làm lỗi CK_jockeys_health_status.

Search toàn project:

* Search HorseHealthStatuses.
* Nếu dòng nào dùng cho Horse thì giữ nguyên.
* Nếu dòng nào dùng cho jockey.HealthStatus, candidate.HealthStatus, request.HealthStatus trong JockeyProfileController thì đổi sang JockeyHealthStatuses.

Test cần đạt:

1. Register Jockey mới:

* Không lỗi CK_jockeys_health_status.
* jockeys.health_status = Unknown.

2. Update hồ sơ Jockey với healthStatus = "Fit":

* Pass validate.
* DB lưu Fit.

3. Update hồ sơ Jockey với healthStatus = "fit" hoặc "FIT":

* Pass validate.
* DB lưu Fit.

4. Update hồ sơ Jockey với healthStatus = "Healthy":

* Bị reject 400 vì Healthy là status của Horse, không phải Jockey.

5. Owner chọn Jockey:

* Jockey Fit => được chọn/mời nếu các điều kiện khác hợp lệ.
* Jockey Unknown/Injured/Suspended => không được chọn/mời.

Sau khi sửa xong chỉ trả lời tối đa 10 dòng:

1. File đã tạo/sửa
2. Constants JockeyHealthStatuses đã tạo chưa
3. JockeyProfileController đã dùng Normalize chưa
4. JockeyLookupsController đã trả đúng status Jockey chưa
5. Owner controller đã dùng JockeyHealthStatuses.CanRace chưa
6. AuthController register Jockey mặc định Unknown chưa
7. Có giữ nguyên HorseHealthStatuses không
8. Search HorseHealthStatuses còn sót chỗ Jockey nào không
9. Build command
10. Lỗi còn lại nếu có
