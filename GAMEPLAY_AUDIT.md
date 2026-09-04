# Audit gameplay CubeSim — 2026-09-03

Chốt lại: chỉ có **3 gameplay** (Race/ReachGoal, Solo Battle/LastAlive, Team Battle/LastTeamAlive).
Mọi thứ khác là *object* gắn vào map: kính màu `C` / kính trắng `N`, đá `B` / mega `M`, rotor `O`,
cửa nhịp `D`, sàn độc `X`, thức ăn `F`, dao `W`, bo ép (Chase / Collapse).

## 1. Cơ chế — đã sửa hôm nay
| Vấn đề | Sửa |
|---|---|
| Dao quá nhỏ | Cleaver 3.0m → **6.0m** (gấp đôi) |
| Va đập đá có "delay" 0.35s → húc liên tục không tính | Cooldown còn **0.1s**: ép sát đá là mài liên tục (~10 hit/s), vẫn chặn 1 bước vật lý tính 2 lần |
| Không có máu | EpicToon Red blood: **BloodSplatDirectional** mỗi nhát trúng, **BloodExplosion** khi chết, **BloodPoolGrowing** vũng máu ở lại sàn 40s (chết vì bo thì bụi, không máu) |
| Format "Saw" dùng rotor thay cho cưa | Rotor giữ đúng là **chong chóng đẩy**. Cưa là object riêng **`S` Saw blade** (Assets/SawBlade/model_0.prefab, 0.69m gốc, chưa code) — xem GAMEPLAY_PLAN.md. Code chém 1 tim đã sẵn trong RotorObstacle (`damagePerHit`, mặc định 0) để tái dùng cho saw blade |
| Food `F` nằm trong `O` (SW02, SW06) sinh ra 4 rotor con vô nghĩa | Chuyển food ra lane trên/dưới |
| Đá: cột 2×24 ô là 1 khối 4 hit, stretch xấu, hở khe | Tảng 2×2 ô, 3 hit, phủ kín; mega 250→60; RM03/04/05 thành mỏ đặc |
| Bo ép một kiểu cho mọi map | Profile theo ý đồ: Chase (đua) / Collapse (chém) |

## 2. Audit từng gameplay từ map + hình

### Race (RC01–12) — vấn đề lớn nhất là "12 map một khuôn"
- Tất cả là **cột chắn dọc so le** (đi qua khe). Nhìn 12 map như 1 map. Không có điểm quyết định, không có bẫy, không có nhịp.
- **Goal là dải cao hết chiều sân** ở mép phải → cứ trôi sang phải là về đích, layout gần như không quan trọng.
- RC08/09/12 có rotor + cửa, giờ rotor chém nên có nguy hiểm thật.

Ý tưởng (dùng đúng object đang có):
1. **Goal nhỏ 4×4 ô, đặt lệch** (góc trên / góc dưới / giữa) — mỗi map một vị trí, layout mới có nghĩa.
2. **Đường tắt tử thần**: lối ngắn đi qua sàn độc `X` (mất 0.5 tim/s) vs lối vòng an toàn — racer đi tắt có thể chết trước đích.
3. **Cổng nhịp** `D` ở chốt chặn: cả đàn dồn chờ cửa mở → tension, rồi bung ra.
4. **Cửa cuối bằng kính trắng `N` 10 hit** ngay trước goal: đàn tới trước phải đập chung, kẻ đến sau hưởng — drama đảo ngược.
5. **Rotor blade chắn cổng hẹp**: qua được mới về đích, đi sai nhịp là mất tim.
6. **Domino**: cột `B` 1–2 hit mở nhánh mới khi có con húc — map "mở dần".

### Rainbow (RB01–09)
- Chỉ là 2–3 **vách kính thẳng đứng** full-lane; đàn bật qua bật lại, ai đúng màu tình cờ chạm là mở.
- 7 màu kính vs 10 racer → 3 con không bao giờ có vai trò (chấp nhận được, nhưng nên có ít nhất 1 lớp `N` mỗi map để ai cũng đập được).
- RB07–09 (tường ngang chia đôi + kính) là dạng tốt nhất — nên nhân bản kiểu đó.

Ý tưởng: kính **so le nửa lane** (trên/dưới xen kẽ, màu khác nhau) để đường đi zigzag; kết hợp kính + đá (đập đá mở lối vòng qua kính); 1 lớp `N` cuối.

### Rock Mine (RM01–09) — đã sửa hôm nay
- Còn có thể thêm: **mạch quặng** (band đá có 1 hàng tảng 1 hit = đường yếu để "khoan"), mega plug độc đạo + 2 mỏ vòng.

### Solo Battle 1 tim — "Sudden" (SD01–08)
- Sân **gần như trống**, 2 pad dao, vài cột. 1 tim + trống = ai gặp dao trước thắng, thuần may rủi, nhìn chán.
- Ý tưởng: dao **4→6 pad** rải đều; **rotor blade ở tâm** (giờ chém thật) làm sân giữa nguy hiểm; **sàn độc `X`** vành ngoài để Collapse + độc ép vào tâm; 2–3 cột `#` tạo góc phục kích.

### Solo Battle rotor — "Saw" (SW01–08)
- Format này dùng rotor (chong chóng đẩy) trong khi tên là Saw → cần object cưa thật (`S`), rotor chỉ là vật cản xoay.
- Dao chỉ **1** cây cho 10 con → gần như không có combat; nên 3.
- Ý tưởng: rotor **kích thước lẫn lộn** (6 ô và 10 ô), rotor **ở chốt giữa 2 tường** (bắt buộc đi qua), 2 rotor quay ngược chiều sát nhau.

### Team Battle (TW01–08)
- Cấu trúc ổn (chuồng + cửa 60 hit + trận giữa). Vấn đề: **4 dao cho 20 con** → đa số đánh tay không; nên 8. Food 2 cụm ổn.
- Ý tưởng: tường giữa bằng `B` (đục để mở đường tấn công), rotor blade ở tâm sân.

## 3. Thiếu asset / cơ chế (chưa có, cần quyết)
- **Lưỡi cưa thật** (mesh tròn răng cưa) — hiện dùng cube đỏ.
- **Máy ép** (tường di chuyển qua lại) — có thể ghép từ Door + Pressure, chưa có object riêng.
- **Bumper / lò xo** đẩy văng, **speed pad**, **teleporter** — chưa có gì.
- **Spike floor** — dùng tạm `X` (đỏ + băng cảnh báo).

## 4. Đề xuất thứ tự làm
1. (xong) cơ chế: dao ×2, mài đá liên tục, máu, rotor chém, material blade.
2. Số lượng dao: Sudden 2→6 pad, Saw 1→3, TeamWar 4→8. *(sửa format, 5 phút)*
3. Race: goal nhỏ lệch góc + 3 mẫu map mới (đường tắt độc / cổng nhịp / kính cuối). *(1–2 giờ)*
4. Sudden: rotor tâm + sàn độc vành. Saw: rotor lẫn cỡ + chốt. *(1 giờ)*
5. Asset mới (lưỡi cưa, máy ép) — sau khi có kết quả niche.
