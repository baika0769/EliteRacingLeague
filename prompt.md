Tôi đồng ý cho sửa.

Giai đoạn BE: chỉnh cơ chế System Time Override để dùng BusinessNow/EffectiveNow cho nghiệp vụ deadline/status.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* Nếu có nhiều file trùng tên hoặc nhiều phiên bản, ưu tiên file trong source hiện tại.
* File nào đã đúng thì giữ nguyên.
* Không sửa database.
* Không migration.
* Không tạo migration.
* Không sửa frontend.
* Không refactor lớn toàn project.
* Không đổi status flow hiện tại.
* Không chuẩn hóa toàn bộ DB sang UTC trong giai đoạn này.
* Không sửa dữ liệu cũ.
* Không xử lý timezone phức tạp quá mức.
* Không thay DateTime.UtcNow trong JWT/token/security logic.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:
Bổ sung hoặc chỉnh `IDateTimeProvider` để có thời gian nghiệp vụ rõ nghĩa:

BusinessNow hoặc EffectiveNow

Ý nghĩa:

* BusinessNow/EffectiveNow = thời gian hệ thống dùng để xử lý nghiệp vụ.
* Nếu không override: BusinessNow dùng giờ local/business hiện tại theo cách project đang lưu RaceDate/Deadline.
* Nếu có override: BusinessNow chính là nowLocal Admin nhập vào.
* RaceDate / Deadline đang lưu theo kiểu local/business time thì so sánh bằng BusinessNow, không so bằng UtcNow.

Giai đoạn 1: Kiểm tra service thời gian hiện có

Kiểm tra các file nếu đã tồn tại:

* Services/SystemTime/IDateTimeProvider.cs
* Services/SystemTime/SystemDateTimeProvider.cs
* Services/SystemTime/RaceTimeStatusService.cs
* Controllers/Admin/AdminSystemController.cs
* Program.cs

Nếu chưa có thì tạo theo prompt trước đó.
Nếu đã có thì chỉ chỉnh nhẹ, không tạo trùng.

Giai đoạn 2: Bổ sung BusinessNow hoặc EffectiveNow vào IDateTimeProvider

Trong IDateTimeProvider, thêm property:

DateTime BusinessNow { get; }

hoặc nếu project đang dùng tên EffectiveNow thì dùng:

DateTime EffectiveNow { get; }

Ưu tiên dùng `BusinessNow` nếu chưa có tên nào.

Interface nên có tối thiểu:

* DateTime UtcNow { get; }
* DateTime RealUtcNow { get; }
* DateTime BusinessNow { get; }
* bool IsOverridden { get; }
* string TimeZoneId { get; }

Ý nghĩa:

* UtcNow: giờ UTC hiệu lực, dùng khi thật sự cần UTC.
* RealUtcNow: giờ UTC thật của server.
* BusinessNow: giờ nghiệp vụ dùng để so sánh RaceDate/Deadline.
* Nếu override bằng nowLocal thì BusinessNow = nowLocal admin nhập.
* Nếu không override thì BusinessNow = giờ local/business hiện tại theo timezone cấu hình hoặc local server.

Không dùng BusinessNow cho:

* JWT expiration
* token validation
* refresh token
* password reset token
* email verification token
* security logic

Giai đoạn 3: Chỉnh SystemDateTimeProvider

Trong SystemDateTimeProvider:

* Lưu override theo cả UTC và local/business time nếu cần.
* Khi Admin gọi override với nowLocal:

  * BusinessNow phải trả đúng nowLocal đó.
  * UtcNow có thể là giá trị convert từ nowLocal sang UTC.
* Khi không override:

  * BusinessNow trả giờ local/business hiện tại.
  * UtcNow trả DateTime.UtcNow.

Ví dụ:
Admin gửi:
{
"nowLocal": "2026-06-28T10:01:00",
"timezone": "Asia/Ho_Chi_Minh"
}

Thì:

* BusinessNow = 2026-06-28 10:01
* UtcNow = thời điểm UTC tương ứng nếu cần hiển thị API time.
* Sync nghiệp vụ phải dùng BusinessNow.

Giai đoạn 4: Chỉnh RaceTimeStatusService dùng BusinessNow

File:
Services/SystemTime/RaceTimeStatusService.cs

Trong SyncAsync, không dùng:

var now = _dateTimeProvider.UtcNow;

Đổi thành:

var now = _dateTimeProvider.BusinessNow;

hoặc nếu tên đã chọn là EffectiveNow:

var now = _dateTimeProvider.EffectiveNow;

Tất cả so sánh nghiệp vụ sau phải dùng BusinessNow:

* race.RaceDate <= now
* race.JockeySelectionDeadline <= now
* race.PredictionDeadline <= now nếu có xử lý
* tournament.StartDate <= now nếu có xử lý

Lý do:
Dữ liệu RaceDate/Deadline hiện tại đang được tạo theo local/business time. Nếu dùng UtcNow sẽ lệch giờ khi test/demo.

Giai đoạn 5: Chỉnh các API có check deadline

Chỉ chỉnh các API thật sự có check deadline/status theo thời gian.

Các file cần kiểm tra:

* Controllers/Owner/OwnerJockeyAssignmentController.cs
* Controllers/Jockey/JockeyInvitationsController.cs
* Controllers/Spectator/SpectatorPredictionsController.cs
* Controllers/Owner/OwnerRegistrationsController.cs nếu có check ngày giờ race/deadline
* Các service liên quan deadline nếu có

Nếu có đoạn:

DateTime.UtcNow

dùng để so sánh:

* JockeySelectionDeadline
* PredictionDeadline
* RaceDate
* TournamentStartDate

thì đổi sang:

_dateTimeProvider.BusinessNow

Ví dụ:

if (race.JockeySelectionDeadline != null &&
race.JockeySelectionDeadline <= _dateTimeProvider.BusinessNow)
{
return BadRequest(new { message = "Đã quá hạn chọn Jockey." });
}

Yêu cầu:

* Inject IDateTimeProvider vào controller/service nếu cần.
* Không thay DateTime.UtcNow ở CreatedAt/UpdatedAt thông thường nếu không cần cho test deadline.
* Không thay DateTime.UtcNow trong JWT/token/security.
* Không refactor toàn bộ project.

Giai đoạn 6: Chỉnh AdminSystemController response time

GET /api/admin/system/time nên trả rõ:

* realUtcNow
* effectiveUtcNow
* businessNow hoặc effectiveBusinessNow
* effectiveLocalNow
* timezone
* isOverridden
* allowTimeOverride

Ví dụ response:
{
"realUtcNow": "2026-06-27T03:00:00Z",
"effectiveUtcNow": "2026-06-28T03:01:00Z",
"businessNow": "2026-06-28T10:01:00",
"effectiveLocalNow": "2026-06-28T10:01:00",
"timezone": "Asia/Ho_Chi_Minh",
"isOverridden": true,
"allowTimeOverride": true
}

Yêu cầu:

* Response API trả raw JSON string/date, không markdown.
* Không đổi route cũ.

Giai đoạn 7: Timezone xử lý an toàn

Khi xử lý timezone:

* Thử TimeZoneInfo.FindSystemTimeZoneById(timezone) trước.
* Nếu timezone là "Asia/Ho_Chi_Minh" nhưng môi trường Windows không nhận, fallback sang "SE Asia Standard Time".
* Nếu timezone vẫn không hợp lệ thì trả BadRequest rõ ràng:
  "Invalid timezone. Use Asia/Ho_Chi_Minh or SE Asia Standard Time."

Không hardcode cộng/trừ giờ thủ công nếu TimeZoneInfo dùng được.

Giai đoạn 8: Không tự tạo status/field mới

Khi dùng:

* RaceStatuses
* TournamentStatuses
* InvitationStatuses

Phải kiểm tra constants thật trong source hiện tại.

Nếu status như:

* AssignedReferee
* RefereeReady
* ClosedRegistration
* Expired

không tồn tại trong source hiện tại thì không tự thêm status mới trong task này.

Chỉ dùng status đã có thật.
Nếu thiếu status cần thiết thì bỏ qua phần tương ứng và báo rõ.

Nếu model không có:

* UpdatedAt
* RespondedAt
* StartDate
* EndDate

thì không thêm field mới.
Chỉ bỏ qua mapping/cập nhật field đó và báo rõ.

Giai đoạn 9: Test luồng chính

Test hết hạn chọn Jockey:

Bước 1:
Tạo race:
JockeySelectionDeadline = 2026-06-28 10:00

Bước 2:
Tạo invitation:
Status = Pending

Bước 3:
Admin override time:

POST /api/admin/system/time/override

Body:
{
"nowLocal": "2026-06-28T10:01:00",
"timezone": "Asia/Ho_Chi_Minh",
"autoSync": true
}

Kết quả mong muốn:

* BusinessNow = 2026-06-28 10:01.
* Sync dùng BusinessNow.
* Deadline = 2026-06-28 10:00.
* Invitation Pending chuyển Expired.

Test API accept invitation:

* Nếu BusinessNow > JockeySelectionDeadline thì Jockey accept phải bị chặn.

Test prediction:

* Nếu BusinessNow > PredictionDeadline thì Spectator tạo prediction phải bị chặn nếu API có logic deadline.

Test race:

* Nếu BusinessNow >= RaceDate thì sync có thể chuyển Race sang Ongoing theo status hiện có.

Giai đoạn 10: Search và build

Chạy:

dotnet build

Search:
rg -n "BusinessNow|EffectiveNow" .
rg -n "DateTime.UtcNow" Controllers Services
rg -n "IDateTimeProvider" Controllers Services Program.cs

Yêu cầu:

* Build pass.
* RaceTimeStatusService dùng BusinessNow/EffectiveNow.
* Deadline API cần test đã dùng BusinessNow/EffectiveNow.
* DateTime.UtcNow còn lại là chấp nhận được nếu không liên quan deadline/status hoặc thuộc security/token.
* Không có migration mới.
* Không sửa DB.
* Không sửa FE.


Bổ sung an toàn:

* Trong SystemDateTimeProvider, việc set/clear/advance override phải thread-safe, dùng lock để tránh BackgroundService và Admin API truy cập cùng lúc.
* Nếu interface hiện tại đang có OverrideUtcNow(DateTime utcNow, string timeZoneId), cần thêm hoặc đổi sang OverrideBusinessNow(DateTime businessNow, string timeZoneId). Không để AdminSystemController chỉ set UTC mà quên lưu BusinessNow.
* Advance phải tăng từ BusinessNow hiện tại, không tăng từ RealUtcNow nếu đang override.
* SyncTimeStatusesResponse nên trả thêm BusinessNow hoặc EffectiveBusinessNow để biết service đã sync theo giờ nghiệp vụ nào.
* AdminSystemController khi autoSync=true phải gọi sync sau khi override/advance đã được set xong.

Sau khi sửa xong chỉ trả lời tối đa 12 dòng:

1. File đã tạo/sửa
2. IDateTimeProvider đã có BusinessNow/EffectiveNow chưa
3. SystemDateTimeProvider override nowLocal có giữ BusinessNow đúng local không
4. RaceTimeStatusService đã dùng BusinessNow chưa
5. Jockey deadline API đã dùng BusinessNow chưa
6. Prediction deadline API đã dùng BusinessNow chưa
7. Owner/Jockey assignment deadline đã dùng BusinessNow chưa
8. Admin system time response đã trả businessNow chưa
9. Có thay DateTime.UtcNow trong JWT/token/security không
10. Có sửa DB/migration/frontend không
11. Search DateTime.UtcNow còn lại thuộc nhóm nào
12. dotnet build kết quả/lỗi còn lại
