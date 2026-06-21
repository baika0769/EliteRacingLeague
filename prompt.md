Tôi đồng ý cho kiểm tra SQL Server.

Mục tiêu:
Kiểm tra vì sao Admin tạo/approve tournament nhưng phía Owner không thấy giải trong Open Tournaments.

Thông tin database cần kiểm tra:

* SQL Server database: EliteRacingLeague
* Connection string:
  Server=(local);User ID=sa;Password=12345;Database=EliteRacingLeague;TrustServerCertificate=True;

Lưu ý quan trọng:

* Chỉ đọc source code hiện tại trong project đang mở.
* Chỉ kiểm tra database EliteRacingLeague.
* Không sửa database nếu chưa được tôi đồng ý.
* Không chạy UPDATE/DELETE/ALTER/INSERT nếu chưa được tôi đồng ý.
* Chỉ chạy SELECT để kiểm tra.
* Không migration.
* Không sửa frontend.
* Không refactor code.
* Không đổi nghiệp vụ.
* Không in password ra báo cáo cuối.
* Nếu cần tạo script kiểm tra thì tạo file .sql, không tự sửa DB.
* Hiển thị rõ từng giai đoạn đang làm.

Giai đoạn 1: Kiểm tra kết nối database

Việc cần làm:

* Dùng connection string:
  Server=(local);User ID=sa;Password=12345;Database=EliteRacingLeague;TrustServerCertificate=True;
* Kiểm tra có kết nối được SQL Server không.
* Kiểm tra đúng database EliteRacingLeague không.

Chạy SQL:

SELECT DB_NAME() AS CurrentDatabase;

Kết quả mong muốn:

CurrentDatabase = EliteRacingLeague

Nếu không kết nối được, báo lỗi rõ:

* Sai server
* Sai user/password
* SQL Server chưa bật
* Database EliteRacingLeague không tồn tại
* SQL Server Authentication chưa bật

Giai đoạn 2: Kiểm tra schema các bảng liên quan

Chạy các SQL sau:

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'tournaments'
ORDER BY ORDINAL_POSITION;

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'races'
ORDER BY ORDINAL_POSITION;

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'race_registrations'
ORDER BY ORDINAL_POSITION;

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'horse_owners'
ORDER BY ORDINAL_POSITION;

SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'users'
ORDER BY ORDINAL_POSITION;

Mục tiêu:
Xác nhận các bảng có đủ cột BE đang dùng.

Giai đoạn 3: Kiểm tra danh sách tournament/race hiện tại

Chạy SQL:

SELECT
t.tournament_id,
t.tournament_name,
t.status AS tournament_status,
r.race_id,
r.status AS race_status,
r.race_date,
r.max_horses,
COUNT(CASE
WHEN rr.status NOT IN ('Rejected', 'Cancelled') THEN 1
END) AS registered_count
FROM tournaments t
LEFT JOIN races r ON r.tournament_id = t.tournament_id
LEFT JOIN race_registrations rr ON rr.race_id = r.race_id
GROUP BY
t.tournament_id,
t.tournament_name,
t.status,
r.race_id,
r.status,
r.race_date,
r.max_horses
ORDER BY t.tournament_id DESC;

Kết luận rõ từng tournament:

* Có đủ điều kiện hiện ở Owner Open Tournaments không.
* Nếu không hiện thì lý do là gì.

Điều kiện đúng để hiện ở Owner Open Tournaments:

* tournament_status = OpenRegistration
* race_status = Scheduled
* race_date >= hôm nay
* registered_count < max_horses
* Owner hiện tại chưa đăng ký race đó

Giai đoạn 4: Xác định Owner đang test

Chạy SQL:

SELECT
u.user_id,
u.full_name,
u.email,
u.role,
u.status AS user_status,
ho.owner_id,
ho.is_active
FROM users u
JOIN horse_owners ho ON ho.user_id = u.user_id
WHERE u.role = 'HorseOwner'
ORDER BY u.user_id DESC;

Tìm Owner đang test, ví dụ:
Test Owner 01

Ghi lại:
owner_id = ?

Giai đoạn 5: Kiểm tra Owner đó đã đăng ký tournament/race nào

Sau khi biết owner_id, chạy SQL này, thay <OWNER_ID_HERE> bằng owner_id thật:

SELECT
rr.registration_id,
rr.owner_id,
rr.race_id,
rr.status AS registration_status,
t.tournament_id,
t.tournament_name,
t.status AS tournament_status,
r.status AS race_status,
r.race_date
FROM race_registrations rr
JOIN races r ON r.race_id = rr.race_id
JOIN tournaments t ON t.tournament_id = r.tournament_id
WHERE rr.owner_id = <OWNER_ID_HERE>
ORDER BY rr.registration_id DESC;

Kết luận:

* Nếu Owner đã đăng ký race đó rồi thì tournament không hiện ở Open Tournaments là đúng.
* Nếu Owner chưa đăng ký mà vẫn không hiện thì kiểm tra tiếp API/filter BE.

Giai đoạn 6: Kiểm tra riêng các tournament đang OpenRegistration + Scheduled

Chạy SQL, thay <OWNER_ID_HERE> bằng owner_id thật:

SELECT
t.tournament_id,
t.tournament_name,
t.status AS tournament_status,
r.race_id,
r.status AS race_status,
r.race_date,
r.max_horses,
COUNT(CASE
WHEN rr.status NOT IN ('Rejected', 'Cancelled') THEN 1
END) AS registered_count,
CASE
WHEN EXISTS (
SELECT 1
FROM race_registrations rr2
WHERE rr2.race_id = r.race_id
AND rr2.owner_id = <OWNER_ID_HERE>
AND rr2.status NOT IN ('Rejected', 'Cancelled')
)
THEN 1 ELSE 0
END AS owner_already_registered
FROM tournaments t
JOIN races r ON r.tournament_id = t.tournament_id
LEFT JOIN race_registrations rr ON rr.race_id = r.race_id
WHERE t.status = 'OpenRegistration'
AND r.status = 'Scheduled'
GROUP BY
t.tournament_id,
t.tournament_name,
t.status,
r.race_id,
r.status,
r.race_date,
r.max_horses
ORDER BY t.tournament_id DESC;

Kết luận:

* Nếu owner_already_registered = 1 thì Owner không thấy là đúng.
* Nếu owner_already_registered = 0, race_date còn tương lai, chưa full slot mà vẫn không thấy thì có thể lỗi API hoặc FE.

Giai đoạn 7: Đối chiếu source BE

Kiểm tra file:

* Controllers/Owner/OwnerRegistrationsController.cs

Tìm method lấy Open Tournaments.

Báo cáo điều kiện filter hiện tại:

* TournamentStatuses.OpenRegistration
* RaceStatuses.CanRegister hoặc RaceStatuses.Scheduled
* RaceDate >= today
* RegisteredCount < MaxHorses
* OwnerAlreadyRegistered

Kiểm tra file:

* Controllers/Admin/AdminTournamentsController.cs

Báo cáo:

* Admin tạo tournament đang set Tournament.Status gì.
* Admin tạo race đang set Race.Status gì.
* Admin approve tournament đổi Tournament.Status thành gì.
* Admin assign referee có đổi Race.Status từ Scheduled sang AssignedReferee không.
* Nếu assign referee làm race bị ẩn khỏi Owner Open Tournaments thì báo rõ.

Giai đoạn 8: Kết luận cuối

Chỉ báo cáo, chưa sửa.

Báo cáo tối đa 12 dòng:

1. Có kết nối được database EliteRacingLeague không
2. Database thực tế đang dùng là gì
3. Owner đang test là ai, owner_id bao nhiêu
4. Tournament nào đủ điều kiện hiện ở Open Tournaments
5. Tournament nào không hiện và lý do
6. Owner đã đăng ký tournament/race nào
7. Có tournament bị ẩn vì OwnerAlreadyRegistered không
8. Có tournament bị ẩn vì RaceDate đã qua không
9. Có tournament bị ẩn vì Race.Status không phải Scheduled không
10. Có tournament bị ẩn vì full slot không
11. API BE đang filter theo điều kiện nào
12. Có cần sửa code không; nếu cần thì đề xuất file và logic, chưa tự sửa
