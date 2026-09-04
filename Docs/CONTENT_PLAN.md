# CubeSim — Kiểm kê nội dung (2026-09-04)

## 1. Chế độ chơi đang có (8)

| # | Chế độ | Luật kết thúc | Racer | Dao | Pressure | Map | Vòng/long |
|---|---|---|---|---|---|---|---|
| F01 | Team Knife War | đội cuối cùng | 20 (10v10) | 2 | Collapse | TW01–10, TW14, TW15 (12) | 6 |
| F02 | Sudden Death | sống cuối, 1 tim | 12 | 1 | Collapse | SD01–20 (20) | 6 |
| F03 | Saw Gauntlet | sống cuối, 2 tim, cưa 1 tim | 12 | 1 | Collapse | SW01–15 (15) | 6 |
| F04 | Race | 3 con về đích | 12 | 0 | Chase / Park | RC01–30 (30) | 12 |
| F05 | Rainbow Glass | 3 con về đích, kính đúng màu | 12 | 0 | Chase | RB01–16 (16) | 8 |
| F06 | Rock Mine | 3 con về đích, đục đá | 12 | 0 | Chase | RM01–16 (16) | 8 |
| F07 | Coin Rush | nhiều coin nhất, 40 s (short) / 90 s (long) | 12 | 0 | Park | CR01–10 (10) | 5 |
| F08 | Four-Way War | đội cuối, 4 đội × 5 | 20 | 2 | Collapse | TW11–13, TW16–18 (6) | 6 |

Tổng **133 map** (114 gốc + wave 4: LB01–08 Lucky Block, RB13–16, RM13–16, TW16–18) (mỗi map có bản ngang 48×26 cho long và bản dọc 26×48 cho short).

## 2. Thiết bị / cơ chế đã code (dùng được trong mọi chế độ)

Dao (1–2/map, outline đỏ khi cầm) · Lưỡi cưa cố định & chạy ray · Máy ép · Bẫy gai (0.5 tim) · Bumper (đẩy + boost) · Băng chuyền · Cổng khóa + chìa · Coin · Potion hồi tim · Teleporter (cặp 1↔2) · Rotor đẩy · Đá đục (B, boulder 2×2) · Boulder khổng lồ (M) · Kính màu (C) · Kính trắng (N) · Pressure (Chase / ChaseDown / Collapse / Park) · Cửa nhịp (D) · Đồ ăn (F).

## 3. Có thể làm bao nhiêu video

### Shorts (1 map = 1 video, dọc)
- Tổng 114, **đã quay 20** (ledger `Recordings/recorded_shorts.txt`), **còn 94**.
- Còn lại theo chế độ: TW 10 · SD 16 · SW 13 · RC 27 · RB 9 · RM 9 · CR 8 · FourWay 2.

### Long (nhiều vòng, ngang, không đồng hồ)
Số video **không lặp map** = số map ÷ số vòng:

| Chế độ | Map | Vòng | Video không lặp map | Ghi chú |
|---|---|---|---|---|
| Team War | 12 | 6 | 2 | |
| Sudden Death | 20 | 6 | 3 | |
| Saw | 15 | 6 | 2 | |
| Race | 30 | 12 | 2 | |
| Rainbow | 12 | 8 | 1 | video 2 lặp 4 map |
| Rock Mine | 12 | 8 | 1 | video 2 lặp 4 map |
| Coin Rush | 10 | 5 | 2 | |
| Four-Way | 3 | 6 | 0 | mỗi video lặp map 2 lần |

→ **13 video long không lặp map**; Wave 1 hiện cấu hình 16 video (2/chế độ), map trộn thứ tự + seed khác nhau nên 2 video cùng chế độ vẫn khác diễn biến. Muốn thêm video long không lặp map thì cần map mới cho Rainbow, Rock Mine, Four-Way trước.

### Dạng video mới làm được chỉ bằng cấu hình (không cần code)
| Dạng | Cấu hình | Cần |
|---|---|---|
| Team Sudden Death | LastTeamAlive + 1 tim + 2 dao | dùng map TW |
| Team Coin Rush | MostCoins tính theo đội | 1 sửa nhỏ: cộng coin theo đội |
| Team Race | ReachGoal, đội có 3 con về trước thắng | 1 sửa nhỏ: đếm finisher theo đội |
| Key Race | RC18, RM12 và map có K/k | có sẵn, chỉ là nhãn thể loại |
| Boss Boulder | RM06, RM07 (M) một khối khổng lồ giữa | có sẵn |
| Teleport Chaos | RB11, CR08, SD17, RC20 | có sẵn |
| Marathon | 1 map, 20 racer, đua 10 suất | cấu hình |
| 1v1 Duel series | 2 racer, best-of-5 trên 5 map | cấu hình (racerCount 2) |

## 4. Đề xuất ưu tiên
1. Quay 16 video long Wave 1 (đã build lại theo luật mới).
2. Vẽ thêm map cho Rainbow (+4), Rock Mine (+4), Four-Way (+3) để mỗi chế độ có ≥ 2 video long không lặp.
3. Thử 2 dạng mới rẻ nhất: Team Sudden Death, 1v1 Duel.

## 5. Ý tưởng chế độ mới (theo kênh cùng thể loại)

| Chế độ | Luật | Cần thêm |
|---|---|---|
| Infection / Zombie | 1 con nhiễm, chạm là lây, con sạch cuối thắng | code nhỏ: chạm → đổi đội/màu |
| Elimination Race | mỗi vòng con về cuối bị loại | code nhỏ: luật loại |
| Floor is Lava | ô sàn biến mất dần | code vừa: sàn ô + lịch xóa |
| King of the Hill | tích 30 s trong vùng giữa | code nhỏ: timer vùng |
| Hot Potato | bom chuyền tay, hết giờ nổ | code nhỏ |
| Sumo | võ đài + bumper, rơi ra là chết | map mới |
| Lucky Block | thùng đập ra đồ ngẫu nhiên | code nhỏ |
| Eat & Grow | ăn để to, nuốt nhỏ hơn | code vừa |
| Team Race / Relay | đội 3 con về trước | code nhỏ |
| Gun Fight | súng bắn đạn | config + model |
| Grand Prix | tích điểm nhiều vòng | code nhỏ |
| Paint War | tô màu sàn, nhiều ô thắng | code lớn |
| Boss Fight | 12 vs 1 boss HP | code lớn |

Ưu tiên đề xuất: Infection, Elimination Race, Floor is Lava.

## 6. Wave 4 (2026-09-04): 19 map mới
- Lucky Block LB01–08 (thùng `?`), Rainbow RB13–16, Rock Mine RM13–16, Four-Way TW16–18. Nguồn: `scratchpad/mapgen/designs4.py`, validator 0 lỗi, bản dọc V_ đã sinh.
- Long không lặp map sau wave 4: Rainbow 2, Rock Mine 2, Four-Way 1 (6 map / 6 vòng).
- 7 chế độ mới (tier A+B) có scene `Scenes/Modes/M_*`; Sumo bỏ.
