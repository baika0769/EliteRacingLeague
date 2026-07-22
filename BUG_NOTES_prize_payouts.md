# Bug: Owner/Jockey Reward Center bị lỗi 500 (Internal Server Error)

## Triệu chứng
- `GET /api/owner/rewards/summary` → 500
- `GET /api/owner/rewards/available` → 500
- Nhiều khả năng `GET /api/jockey/rewards/summary` và `GET /api/jockey/rewards` cũng lỗi tương tự (dùng chung bảng `prize_payouts`).

## Nguyên nhân gốc #1 — Thiếu migration cho bảng `prize_payouts`
Log lỗi thật (lấy từ terminal backend):

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Invalid object name 'prize_payouts'.
   at ... OwnerRewardsController.GetAvailableRewards ...
```

- Entity `PrizePayout` đã được khai báo đầy đủ trong `Data/EliteRacingLeagueContext.cs` (`DbSet<PrizePayout> PrizePayouts`, có cấu hình `ToTable("prize_payouts", ...)` kèm 3 check constraint: `CK_prize_payouts_recipient_type`, `CK_prize_payouts_status`, `CK_prize_payouts_amount`).
- Nhưng **không có migration nào tạo bảng `prize_payouts` trong DB** — grep toàn bộ thư mục `Migrations/` không ra kết quả nào cho `prize_payouts`. `ModelSnapshot.cs` cũng không có entity này.
- Kết luận: ai đó đã thêm entity `PrizePayout` vào model nhưng quên chạy `dotnet ef migrations add` để sinh migration tương ứng. Bảng `prize_awards` (liên quan) thì có migration đầy đủ, chỉ riêng `prize_payouts` bị thiếu.

### Cách sửa
```
dotnet ef migrations add AddPrizePayoutsTable
dotnet ef database update
```

## Nguyên nhân gốc #2 — `EliteRacingLeagueContextModelSnapshot.cs` bị lỗi cú pháp, chặn `dotnet ef migrations add`
Khi chạy lệnh migrate ở trên lần đầu, gặp lỗi:

```
System.InvalidOperationException: The property 'IX_referee_reports_reviewed_by_admin_id' cannot be added to the type
'Eliteracingleague.API.Models.RefereeReport' because no property type was specified...
```

Nguyên nhân: file `Migrations/EliteRacingLeagueContextModelSnapshot.cs` có 3 chỗ dùng sai overload của `HasIndex`, truyền tên index như một **property thứ 2** thay vì gọi `.HasDatabaseName(...)`:

```csharp
// SAI — EF hiểu "IX_referee_reports_reviewed_by_admin_id" là 1 property khác, không tồn tại → crash
b.HasIndex("ReviewedByAdminId", "IX_referee_reports_reviewed_by_admin_id");
b.HasIndex("Status", "IX_referee_reports_status");
b.HasIndex("IdempotencyKey", "UX_point_transactions_idempotency_key").IsUnique();
```

Đã sửa lại đúng cú pháp thành:

```csharp
b.HasIndex("ReviewedByAdminId").HasDatabaseName("IX_referee_reports_reviewed_by_admin_id");
b.HasIndex("Status").HasDatabaseName("IX_referee_reports_status");
b.HasIndex("IdempotencyKey").IsUnique().HasDatabaseName("UX_point_transactions_idempotency_key");
```

Lưu ý: file migration gốc `20260719190000_AddRefereeReportReviewWorkflow.cs` (dùng `migrationBuilder.CreateIndex(...)`) **hoàn toàn đúng** — bug chỉ nằm ở `ModelSnapshot.cs` (file này được EF tự sinh, nghi là có ai đó chỉnh tay sai, hoặc lỗi khi merge/generate lại).

### Trạng thái hiện tại
Đã sửa cả 2 nguyên nhân trên tạm thời để test, nhưng sau đó đã `git reset --hard` về commit trước khi sửa (theo yêu cầu revert). Tức là **cả 2 bug này vẫn còn nguyên trong code hiện tại**, cần dev backend áp dụng lại 2 phần sửa ở trên rồi chạy `dotnet ef migrations add AddPrizePayoutsTable` + `dotnet ef database update`.
