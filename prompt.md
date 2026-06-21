Tôi đồng ý cho sửa.

Giai đoạn BE: sửa lỗi dùng RaceStatuses.CanRegister() trong EF IQueryable và gom logic registerable race status.

Lưu ý quan trọng:

* Chỉ đọc và sửa source code hiện tại tôi vừa gửi.
* Không dùng file cũ hoặc suy luận từ phiên bản cũ.
* File nào đã đúng thì giữ nguyên.
* Không sửa database.
* Không migration.
* Không sửa frontend.
* Không refactor lớn.
* Không đổi nghiệp vụ.
* Không sửa lan man file khác.
* Không in code dài ra terminal.
* Code sạch, dễ tái sử dụng, dễ bảo trì.

Mục tiêu:

1. Thêm danh sách RaceStatuses.RegisterableStatuses dùng chung.
2. OwnerRegistrationsController.GetOpenTournaments dùng RegisterableStatuses thay vì array local.
3. OwnerRacesController.GetRaceDetail không gọi RaceStatuses.CanRegister() bên trong IQueryable.
4. Không sửa GetEligibleHorses và CreateRegistration nếu chúng đang gọi CanRegister() sau khi đã load race về memory.

File cần sửa:

1. Constants/RaceStatuses.cs
2. Controllers/Owner/OwnerRegistrationsController.cs
3. Controllers/Owner/OwnerRacesController.cs

Phần 1: Sửa Constants/RaceStatuses.cs

Hiện CanRegister đang đúng nghiệp vụ:

public static bool CanRegister(string? status)
{
return status is Scheduled or AssignedReferee or RefereeReady;
}

Nhưng cần thêm mảng dùng chung để EF Core có thể dịch thành SQL IN.

Thêm:

public static readonly string[] RegisterableStatuses =
{
Scheduled,
AssignedReferee,
RefereeReady
};

Sau đó sửa CanRegister thành:

public static bool CanRegister(string? status)
{
return status != null && RegisterableStatuses.Contains(status);
}

Yêu cầu:

* Không đổi nghiệp vụ.
* Race có thể đăng ký khi status là:

  * Scheduled
  * AssignedReferee
  * RefereeReady

Phần 2: Sửa OwnerRegistrationsController.GetOpenTournaments

File:
Controllers/Owner/OwnerRegistrationsController.cs

Hiện đang có array local:

var registerableRaceStatuses = new[]
{
RaceStatuses.Scheduled,
RaceStatuses.AssignedReferee,
RaceStatuses.RefereeReady
};

và query:

registerableRaceStatuses.Contains(t.Race.Status)

Yêu cầu sửa:

* Xóa array local nếu không còn cần.
* Dùng chung:

RaceStatuses.RegisterableStatuses.Contains(t.Race.Status)

Lý do:

* Tránh lặp logic.
* EF Core dịch được Contains trên array thành SQL IN.

Không sửa:

* Không sửa logic khác của GetOpenTournaments.
* Không đổi route/response.

Phần 3: Sửa OwnerRacesController.GetRaceDetail

File:
Controllers/Owner/OwnerRacesController.cs

Hiện còn lỗi:

RaceStatuses.CanRegister(r.Status) ||

nằm bên trong:

.Where(r => ...)

Vấn đề:

* Đây là IQueryable.
* EF Core không dịch được method tự viết RaceStatuses.CanRegister() ra SQL.

Yêu cầu sửa:
Đổi:

RaceStatuses.CanRegister(r.Status) ||

thành:

RaceStatuses.RegisterableStatuses.Contains(r.Status) ||

Lý do:

* RegisterableStatuses.Contains(r.Status) dịch được thành SQL IN.
* Giữ nguyên điều kiện cho phép Owner xem race nếu đã có RaceRegistration.

Logic quyền xem vẫn phải giữ:

RaceStatuses.RegisterableStatuses.Contains(r.Status) ||
r.RaceRegistrations.Any(rr => rr.OwnerId == ownerId.Value)

Không sửa:

* Không sửa GetEligibleHorses.
* Không sửa CreateRegistration.
* Vì 2 chỗ này gọi RaceStatuses.CanRegister(race.Status) sau khi đã load race về memory, không nằm trong IQueryable nên an toàn.
* Không đổi route/response.
* Không đổi message.
* Không đổi logic Owner xem race đã đăng ký.

Sau khi sửa:

* Chạy dotnet build.
* Chạy search kiểm tra:

rg -n "RaceStatuses\.CanRegister\(" Controllers/Owner/OwnerRacesController.cs
rg -n "registerableRaceStatuses" Controllers/Owner/OwnerRegistrationsController.cs
rg -n "RegisterableStatuses" Constants/RaceStatuses.cs Controllers/Owner/OwnerRegistrationsController.cs Controllers/Owner/OwnerRacesController.cs

Kết quả mong muốn:

* OwnerRacesController.GetRaceDetail không còn gọi RaceStatuses.CanRegister() trong IQueryable.
* OwnerRegistrationsController.GetOpenTournaments không còn array local registerableRaceStatuses.
* RaceStatuses.cs có RegisterableStatuses.
* dotnet build pass.


Bổ sung:
- Sau khi sửa, kiểm tra toàn bộ Controllers xem còn chỗ nào gọi RaceStatuses.CanRegister(...) bên trong IQueryable như Where/Any/Select chưa.
- Nếu còn trong IQueryable thì đổi sang RaceStatuses.RegisterableStatuses.Contains(...).
- Nếu CanRegister được gọi sau khi đã FirstOrDefaultAsync/ToListAsync/load entity về memory thì giữ nguyên.

Sau khi xong chỉ trả lời tối đa 7 dòng:

1. File đã sửa
2. RaceStatuses.RegisterableStatuses đã thêm chưa
3. CanRegister đã dùng RegisterableStatuses chưa
4. GetOpenTournaments đã bỏ array local chưa
5. GetRaceDetail đã bỏ CanRegister trong IQueryable chưa
6. Có sửa DB/migration/frontend không
7. Build command/kết quả build
