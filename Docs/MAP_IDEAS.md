# Ý tưởng layout & cơ chế map — để duyệt (2026-09-03)

Nguồn tham khảo: nguyên tắc chokepoint / cấu trúc map multiplayer (World of Level Design, Level Design Book, MY.GAMES top-down
shooter), marble race (funnel, spiral, gate, split path), obstacle course (crusher, conveyor, bumper, spinner).
Mỗi ý tưởng ghi rõ: cần object gì (có sẵn / cần code), hợp gameplay nào, độ "kịch tính" kỳ vọng.

## A. Cấu trúc map (topology) — dùng được ngay với object hiện có
| # | Ý tưởng | Mô tả | Object | Gameplay | Kịch tính |
|---|---|---|---|---|---|
| A1 | **Spiral (xoắn ốc)** | đường xoắn từ mép vào tâm (hoặc tâm ra mép); goal ở tâm; bo ép từ ngoài | `#` + cửa nhịp/đá ở mỗi vòng | Race | cao: cả đàn cùng chạy một dòng, ai lách được ai |
| A2 | **Double spiral** | 2 xoắn ốc lồng nhau, 2 cửa vào; hai nửa đàn gặp nhau ở tâm | `#` `D` | Race, Team | cao |
| A3 | **4 corner + combat zone** | 4 chuồng ở 4 góc, mỗi góc 1 lối; tâm là hố chiến (dao, food) có vành sàn độc hoặc rotor; bo Collapse | `L/R` ×4 (cần 4 team spawn) `W` `X` `O` | Solo, Team 4 phe | rất cao: 4 hướng đổ về một hố |
| A4 | **Hub & spoke** | tâm là hub, 4–6 phòng xung quanh nối bằng cửa nhịp; dao chỉ ở hub | `#` `D` `W` | Solo | cao: ra hub là chết, không ra thì đói |
| A5 | **Ring / donut** | vành tròn chạy quanh khối tâm; goal ở phía đối diện; 2 chiều đi | `#` | Race | trung bình; hay khi thêm cưa chạy vành |
| A6 | **Gauntlet hành lang** | 1 hành lang dài uốn 2–3 lần, mỗi đoạn một loại bẫy khác nhau | `D` `O` `B` `N` `X` | Race | cao (obstacle course) |
| A7 | **Funnel (phễu)** | sân rộng thu hẹp dần về một khe 2 ô rồi mở ra lại | `#` | Race, Solo | cao: dồn cục ở cổ phễu |
| A8 | **Chia sân 3 lane khác luật** | lane nhanh-nguy / lane trung bình / lane chậm-an toàn (đã có RC02, RC11) | `X` `B` `N` | Race | cao |
| A9 | **Islands** | 3–4 đảo nối bằng cầu 2 ô; cầu có cửa nhịp; bo nuốt đảo ngoài trước | `#` `D` | Solo, Team | cao |
| A10 | **Mê cung đối xứng gương trái/phải** (không phải trên/dưới) | 2 đội xuất phát 2 bên, mê cung như nhau, gặp nhau ở tâm | `#` `B` | Team | trung bình–cao |
| A11 | **Bàn cờ cột** | lưới cột 3×3 ô cách nhau 2 ô; dao ở giữa | `#` `W` | Solo | trung bình (cover, phục kích) |
| A12 | **Cầu thang lệch** | các bậc ngang lệch dần lên/xuống, lối đi hẹp ở đầu mỗi bậc | `#` | Race | trung bình |

## B. Cơ chế / device mới — cần code (theo GAMEPLAY_PLAN.md)
| # | Device | Marker | Asset | Hợp | Kịch tính | Code |
|---|---|---|---|---|---|---|
| B1 | **Saw blade** quay tại chỗ / chạy ray | `S` (ray = dải S) | Assets/SawBlade | Race, Solo | rất cao | trung bình (đã có logic chém, cần object + ray) |
| B2 | **Crusher / máy ép** trượt ngang | `P` | cube | Race, Solo | rất cao | trung bình (tái dùng Door + kẹp = chết) |
| B3 | **Bẫy gai nhịp** | `T` | Kenney trap | Solo, Race | cao | thấp (HazardArea bật/tắt theo chu kỳ) |
| B4 | **Bumper** | `U` | Kenney barrel | Solo | trung bình | thấp (đẩy văng khi chạm) |
| B5 | **Conveyor / băng chuyền** | `>` `<` `^` `v` | decal mũi tên | Race | cao | trung bình (thêm vận tốc theo hướng khi đứng trên) |
| B6 | **Cổng khóa + chìa** | `K` `k` | Kenney gate+key | Race, Team | cao | trung bình |
| B7 | **Coin / điểm** | `$` | Kenney coin | gameplay mới Coin Rush | cao | trung bình (win MostScore) |
| B8 | **Potion** hồi máu/tốc | `+` | Kenney potion | Team, Solo | trung bình | thấp |
| B9 | **Sét ngẫu nhiên** | (global) | EpicToon Lightning | Solo | trung bình | thấp (mỗi 8s đánh 1 ô 3×3, cảnh báo 1s) |
| B10 | **4 team spawn** (cho A3) | `L R T B`? | — | Team 4 phe | cao | thấp (RoundRobin 4 chuồng) |
| B11 | **Cửa một chiều** | `>` trên tường | — | Race | trung bình | thấp (collider chỉ chặn 1 hướng) |
| B12 | **Teleporter cặp** | `1 2` | EpicToon portal | Race | cao (đảo ngược thứ hạng) | thấp–trung bình |

## C. Đề xuất bộ map đợt 3 (nếu duyệt A + B1–B3)
- Race: A1 Spiral, A2 Double spiral, A6 Gauntlet (cưa + ép + gai), A7 Funnel, A9 Islands-race.
- Solo: A3 4-corner combat pit, A4 Hub & spoke, A9 Islands, A11 Bàn cờ.
- Team: A3 (4 đội), A10 mê cung gương, Siege (tường đá + potion).

Thứ tự tôi đề xuất: **A1, A3, A4, A7** vẽ ngay (không cần code) → B1 saw blade → B2 crusher → B3 gai → A6 Gauntlet.
