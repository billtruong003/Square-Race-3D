# CubeSim — LUẬT THỂ LOẠI (Format Rulebook)

Nguyên tắc tối thượng: **mỗi thể loại là một bộ luật đóng**. Map thuộc thể loại nào phải tuân
đúng luật thể loại đó — không bao giờ có chuyện map "last man standing" lại mọc ra đích đến,
hay map đua lại có bo siết giết người từ sau lưng. Mỗi thể loại làm **10 map riêng**, thiết kế
theo đúng khung luật của nó, đặt tên theo prefix của thể loại.

Ký hiệu template: `#` tường, `B` breakable, `M` mega block, `C` rainbow gate, `D` cửa trượt,
`O` rotor, `G` đích, `X` hazard, `W` khu vũ khí, `F` khu food, `L/R` spawn.

---

## Quy tắc chung cho MỌI thể loại

| Quy tắc | Nội dung |
|---|---|
| Nhất quán luật | Thể loại sinh tồn: **cấm G**. Thể loại đua: **bắt buộc G**. |
| **Bo là công cụ có ý đồ, mỗi map một profile** | `Chase` (map đua): **một tường đẩy từ sau lưng spawn về phía goal**, dừng cách vạch đích ~8m (inset 54) — lùa cả đàn về đích, ai lết lại thì bị nghiền, không còn cảnh lảng vảng tới hết giờ. `Collapse` (map chém): **cả 4 phía ép về hố giữa** (X 22 / Z 12 → hố 24×14m) — bắt buộc phải đánh nhau. `Corridor`: 2 bên trái-phải (kiểu cũ). `Park`: đứng yên. Không bao giờ dùng siết đối xứng trên map có goal ở mép (inset 22 → tường tới x=12, cán qua goal). |
| **Đua thì cấm dao** | Mọi format ReachGoal (Race, Rainbow, RockMine) có **0 vũ khí**. Lý do: giết bớt racer = mất người đập cổng kính / đá, càng ít racer thì chướng ngại càng không mở được, round chết cứng. Đua là đua, không phải chém. |
| Racer mặc định | 10 con solo (trừ thể loại team), 3 tim, speed 11.5, eye cube tint màu. |
| Vũ khí | Chỉ dao bếp (Cleaver 3m, pulse). **1 dao/map** (Team War & Four-Way: 2 dao, mỗi phe một hướng). Con đang cầm dao + con dao được viền **outline đỏ (silhouette qua stencil)**, thả dao là mất viền. Dao rơi trên sân render nổi trên racer/tường (bay ở 3.1 m). |
| Âm thanh | BGM pool + 4 one-shot + tiếng kính vỡ cho breakable. |
| Visual breakable/mega | Model đá `Assets/KenneyDungeon/rocks.fbx` (không còn khối hộp trơn). |
| Visual rainbow gate | Kính stylized (StylizedGlass + toon lighting nhúng thẳng), mỗi lớp một màu, phổ đủ 7 sắc. |
| Visual goal | Làm lại: pad phẳng màu đặc (giữ màu hẳn), viền phát sáng nhẹ, KHÔNG lem texture. |
| Visual hazard | Làm lại: sàn màu đặc đỏ trầm + viền kẻ sọc cảnh báo, không glow lòe loẹt. |
| Seed | Mỗi video seed riêng; cùng map khác seed = video khác. |

---


## Luật chung cho SHORTS và LONG (chốt 2026-09-03)

- **Không có đồng hồ, cả short lẫn long.** Đua chạy tới khi **3 con về đích** (winner = con đầu); PvP chạy tới khi **còn 1 con**; Team War tới khi còn 1 đội. Chỉ Coin Rush là format đồng hồ (40 s). Mốc 300 s chỉ là chốt an toàn chống treo, không phải luật.
- Pressure lo nhịp độ: đua = slab đuổi từ trên xuống 1,5 m/s (map đích ở đáy) hoặc Park (map đích giữa sân như RC13/RC14); PvP = collapse siết về hộp 8×10 m ở 0,5 m/s.
- **12 racer** solo (4 hàng × 3 cột dải leaderboard), Team War 20.
- Teleporter: pad `1` và `2` vẽ mỗi loại một lần = **một cặp hai chiều**; vẽ hai pad cùng số = cặp riêng. Không đặt pad cho phép nhảy qua toàn bộ chướng ngại tới sát đích (RB11 đã sửa: pad 2 rơi vào túi gai giữa hai lớp kính, chỉ nhảy qua lớp 1).
- **Dải luật** dưới leaderboard: dòng luật theo thể loại (RuleText.Describe) + bộ đếm sống (về đích x/3, còn sống, đội, đồng hồ Coin Rush đỏ 5 s cuối); dòng luật cũng in trên card ROUND.
- Cổng khóa mở = **chìm hẳn xuống sàn** (không dẹt tại chỗ, camera trên xuống nhìn không khác đóng).

## F01 — TEAM KNIFE WAR (Sinh tồn dao — luật user chốt)

> 10 vs 10, hai đầu map bị **tường breakable chặn**, phá tường xong hai đàn tràn vào giữa
> chém nhau. Không đích, không hazard. Team cuối còn người = thắng.

| Luật | Giá trị |
|---|---|
| Đội hình | **10 RED vs 10 BLUE** (20 racer), tint theo team |
| Win | LastAlive theo team — team còn racer cuối cùng thắng |
| CẤM | `G` (đích), `X` (hazard), `O` (rotor) — chỉ chém nhau thuần |
| Bắt buộc | 2 khu spawn L/R ở 2 đầu, mỗi đầu bịt bằng **tường B** (60 hit — một cửa gộp bị 10 con húc ~2.5–5 hit/s, giữ chuồng 12–24s) ngăn với khu giữa |
| Map | Khu giữa THOÁNG (≥ 60% diện tích mở) để combat đọc rõ; chỉ vài khối che chắn lớn |
| Vũ khí | 2 dao, đặt ở khu giữa (2 khu W trung tâm) |
| Bo | `Collapse` 4 phía về hố giữa 24×14m — delay 20s, speed 0.2 — dồn hai đàn vào một chỗ ở trận cuối |
| Thời lượng | 150s/round |
| 10 map biến thể | Giữa trống hoàn toàn / 1 khối trung tâm / 2 trụ / hành lang rộng / chữ thập / vòng khuyên / 4 khối góc / 2 phòng thông nhau / trụ so le / đấu trường bậc thang |

## F02 — SOLO KNIFE FFA (Sinh tồn solo)

| Luật | Giá trị |
|---|---|
| Đội hình | 10 solo, mỗi con một màu |
| Win | LastAlive |
| CẤM | `G`, team |
| Map | Thoáng như F01 nhưng spawn rải 2 bên không cần tường chặn |
| Vũ khí | 1 dao |
| Bo siết | CÓ (chuẩn 0.25) |
| 10 map | Như F01 nhưng bố cục khác + được phép 1 khu `X` nhỏ làm điểm né (tối đa 1) |

## F03 — RACE (Đua về đích)

| Luật | Giá trị |
|---|---|
| Win | ReachGoal — về nhất thắng round |
| Bắt buộc | `G` một đầu, spawn `L` đầu kia; đường đi có ≥ 2 nhánh để kèo lật |
| Bo | `Chase` speed 0.5, delay 10s, dừng cách goal 8m |
| CẤM | Team |
| Vũ khí | **0 dao** (luật đua-cấm-dao) |
| Chướng ngại được phép | `D` cửa trượt, `O` rotor nhỏ giữa đường, `X` làn phạt chậm... tất cả đều NÉ ĐƯỢC — không có ngõ cụt bắt buộc ăn dmg |
| Độ dài đường đua | Map PHẢI bắt racer đi vòng: đo được ~25–30s/round. Cấm map trống trơn cho đi thẳng một mạch (RC04 cũ về đích trong 5.4s — hỏng). |
| Số round/video | ~18 round để video đạt ~9 phút (round đua rất ngắn) |
| 10 map | Maze đặc / serpentine / lanes song song / zigzag cửa trượt / rotor giữa ngã tư / làn hazard / cầu hẹp / phòng nối phòng / đường vòng cung / tổ hợp |

## F04 — SUDDEN DEATH (1 tim)

| Luật | Giá trị |
|---|---|
| Tim | **1** — chạm dao là chết; bẫy/cưa chỉ cắn 0.5 tim nên map không tự giết sạch trước khi có dao |
| Win | LastAlive |
| CẤM | `G`; `X` cũng cấm (dính tí lửa chết luôn thì ức chế, không đọc được kèo) |
| Map | Thoáng vừa, ít vật cản để né dao bằng di chuyển |
| Vũ khí | 1 dao |
| Bo | `Collapse` nhanh — delay 8s, speed 0.3 — round phải ngắn, căng |
| Thời lượng | 120s |

## F05 — SQUEEZE (Bo siết là nhân vật chính)

| Luật | Giá trị |
|---|---|
| Win | LastAlive |
| CẤM | `G` |
| Bắt buộc | Bo siết NHANH (speed 0.35+, delay 5s) — map chết dần theo phút |
| Map | Phòng lồng phòng, cửa `D`/`B` để thoát vào trong; ai kẹt ngoài bị nghiền |
| Vũ khí | 1 dao |
| 10 map | Hộp lồng nhau / xoắn ốc / phòng 4 lớp / nút cổ chai / mê cung co / ... |

## F06 — ROCK MINE (Đập đá xuyên map)

| Luật | Giá trị |
|---|---|
| Win | ReachGoal |
| Bắt buộc | `G` sau **tường đá** — field đá `B` được **cắt thành từng tảng 2×2 ô (2.8m), mỗi tảng 3 hit, có collider riêng** nên đàn phải ĐÀO HẦM xuyên qua (mỗi con một lối), không phải "húc 4 phát cả cột biến mất"; và/hoặc `M` mega rock (counter, 60 hit — 10 con đục được trong ~20s) chắn độc đạo. Tảng đá phủ kín ô (overscale 8%, mirror xen kẽ) nên không hở khe. Round 130s. |
| Bo | `Chase` speed 0.35, delay 15s — ép phải đào, không được lảng vảng; chậm hơn Race vì phải chừa thời gian đục |
| CẤM | **Dao** — cần đủ racer để đục hết đá |
| Vũ khí | **0 dao** |
| 9 map | RM01–09: band mỏng/dày, mỏ 10 ô, mega plug độc đạo, band + cửa nhịp; goal và food xáo vị trí để không map nào lặp |

## F07 — RAINBOW RUSH (Cổng cầu vồng)

| Luật | Giá trị |
|---|---|
| Win | ReachGoal |
| Hai loại kính | `C` **cổng màu**: mỗi lớp một màu lấy đúng từ palette racer — **chỉ con cube đúng màu đó mới đập được**, đổi lại chỉ tốn **2 hit**. `N` **pane trắng**: ai đập cũng được nhưng dai, **10 hit**. Map phải có cả hai để tạo trade-off. |
| Bắt buộc | Chuỗi cổng trên đường tới `G`; cổng màu tối đa 3–4 lớp (mỗi lớp khoá 1 màu, nhiều quá thì tắc) |
| Bo | `Chase` speed 0.4, delay 12s |
| CẤM | **Dao** — phải còn đủ racer để đập hết các lớp kính |
| Map | Đường chính rõ ràng xuyên các cổng; nhánh phụ vòng xa hơn nhưng ít cổng hơn (trade-off) |
| 10 map | Cổng thẳng hàng / cổng so le / 2 đường 2 phổ màu / cổng + cửa trượt / vòng cung cầu vồng / ... |

## F08 — SAW GAUNTLET (Né lưỡi xoay)

| Luật | Giá trị |
|---|---|
| Win | LastAlive |
| CẤM | `G` |
| Bắt buộc | ≥ 2 rotor `O` LỚN (sweep ≥ 8 ô) là nguồn chết chính; dao chỉ 1 con |
| Bo | `Collapse` chậm (delay 15s, speed 0.2, hố 20×10) — ép dần racer vào vùng rotor |
| 10 map | 1 rotor khổng lồ giữa / 2 rotor ngược chiều / 4 rotor góc / rotor + trụ / hành lang rotor / ... |

## F09 — HAZARD FLOOR (Sàn tử thần)

| Luật | Giá trị |
|---|---|
| Win | LastAlive |
| CẤM | `G` |
| Bắt buộc | Vùng `X` chiếm 25–40% sàn theo pattern đọc được (kẻ ô, vành đai, xương cá) — visual mới màu đặc + sọc cảnh báo |
| Luật dmg | Giữ 0.5/s (2 giây trong vùng = mất 1 tim) — đủ để né kịp |
| Vũ khí | 1 dao |
| 10 map | Bàn cờ / vành đai lửa / xương cá / hồ trung tâm / hành lang hẹp lửa 2 bên / ... |

## F10 — WEAPON FRENZY (Loạn đao)

| Luật | Giá trị |
|---|---|
| Win | LastAlive |
| CẤM | `G` |
| Bắt buộc | **2 dao** (mỗi phe một hướng) — dao là hàng hiếm, ai cầm dao là mối nguy có viền đỏ |
| Map | Thoáng + vài khối né; food nhiều (kèo hồi điểm số) |
| Bo siết | CÓ chuẩn |
| 10 map | Như F02 nhưng mật độ W dày gấp 3 |

---

## Việc engine cần làm để phục vụ luật (theo thứ tự)

1. **Team win condition**: LastAlive hiện tính theo CON cuối — cần thêm `LastTeamAlive`
   (round kết thúc khi chỉ còn 1 team có racer sống) cho F01.
2. **Breakable chặn spawn** (F01): builder đặt tường B ngăn khu spawn với khu giữa — đã có
   sẵn cơ chế B, chỉ là quy ước template.
3. **Rock visual** cho B/M: thay khối hộp bằng `Assets/KenneyDungeon/rocks.fbx`.
4. **Glass shader** cho C: `Assets/Arm/StylizedGlass.shader` + nhúng thẳng toon lighting
   (không include), đủ phổ 7 màu.
5. **Goal/Hazard visual mới**: pad màu đặc + viền; hazard sọc cảnh báo.
6. **100 template map** (10 thể loại × 10) theo đúng khung luật trên.
7. Format plan v2: 10 thể loại × N video từ đúng pool map của thể loại đó.


## Sàn độc `X` — luật chung (bổ sung 2026-09-03)
| Luật | Giá trị |
|---|---|
| Sát thương | 0.5 tim/giây khi đứng trên; chạy thẳng qua dải 2 ô mất ~0.13 tim |
| Dùng để | đánh thuế đường ngắn (RC02, RC11), ép rẽ (RC06), phạt đứng chờ trước cửa (RC09), vành ép về tâm (SD02, SD06, SD08) |
| Visual | sàn đỏ đậm + băng vàng/đen |

## Goal nhỏ (bổ sung 2026-09-03)
Map đua dùng goal 4×6 ô đặt lệch (trên / giữa / dưới), không còn dải cao hết sân: vị trí goal quyết định lane nào lợi.
Bộ kiểm tra map (web editor + Python) bắt buộc: có đường tới goal, không lách được đá/kính nếu map có đá/kính, hành lang ≥ 2 ô, không đối xứng trên/dưới.


## Bộ device mới (code 2026-09-03) — marker, luật, asset
| Marker | Device | Luật | Asset |
|---|---|---|---|
| `S` | **Saw blade** | ô vuông = cưa quay tại chỗ; dải dài = ray, cưa chạy qua lại 5 m/s; chạm lưỡi mất **0.5 tim** (riêng format Saw Gauntlet: 1 tim), 0.8s mới bị chém lại; không chặn đường | Assets/SawBlade/model_0 + ray thép đỏ |
| `P` | **Máy ép** | dải P là quãng trượt; khối dài bằng nửa dải trượt hết dải rồi về, chu kỳ 3.6s, lùi chậm đập nhanh; kẹp racer vào tường = chết (crushed) | khối thép đỏ |
| `T` | **Bẫy gai** | chu kỳ nghỉ 2.4s → báo 0.6s (gai nhú) → gai lên 1.4s; đứng trên khi gai lên mất **0.5 tim** (racer 1 tim sống sót 1 lần), 1s mới dính lại | Kenney trap.fbx, mỗi ô một tấm |
| `U` | **Bumper** | chạm thùng là bật văng theo hướng ra xa, tốc độ ×2 trong 0.8s | Kenney barrel.fbx |
| `>` `<` `^` `v` | **Băng chuyền** | đứng trên bị kéo 6 m/s theo mũi tên (cộng vào chuyển động) | texture tileableConveyor (FBX format 2) cuộn theo chiều kéo |
| `K` / `k` | **Cổng khóa / chìa** | bất kỳ ai chạm chìa → mọi cổng hạ xuống sàn, các chìa khác biến mất | FBX format 2 gate-lasers.fbx (mỗi lá 2 ô) / key.fbx |
| `$` | **Coin** | mỗi ô một coin, ăn +1, mọc lại sau 8s; win `MostCoins` = hết giờ ai nhiều coin nhất (chết vẫn giữ coin nhưng ưu tiên người sống) | Kenney coin.fbx |
| `+` | **Potion** | hồi 1 tim, chỉ ai thiếu máu ăn được, mọc lại sau 20s | Kenney potion.fbx |
| `1` `2` | **Teleporter** | bước vào pad 1 hiện ra ở pad 2 (và ngược lại), 1.5s mới dịch lại | shader `CubeSim/Portal` (xoáy tím tự chạy) + vòng đế |
| `A` `Z` | **Chuồng trên / dưới** | thêm 2 chuồng cho map 4 góc / 4 phe (RoundRobin: racer i → chuồng i%4, đội i%4) | — |

Tất cả chạy trong `ArenaDeviceSystem` theo thời gian mô phỏng, cùng seed = cùng kết quả. Sát thương/giết đi qua đúng đường `ApplyDamage`/`Kill` của runner nên máu, popup, leaderboard, thống kê đều đúng.
Đá/boulder (`B`, `M`): tiếng **cuốc đá** riêng (RockHit, Kenney impactMining) mỗi cú đập + tiếng đá vỡ trầm (RockBreak) khi sập, không dùng chung tiếng kính; khối đá **rung 0.26 s** mỗi cú đập (chỉ rung mesh, collider đứng yên nên không ảnh hưởng determinism).

Âm thanh device: 12 one-shot CC0 (Kenney) trong Audio/SFX/Devices — cưa chém, máy ép đập, gai đâm, gai báo, bumper, coin, cổng mở, chìa, potion, teleport. **Không có SFX loop** (tiếng cưa rít đã bỏ theo yêu cầu: chỉ one-shot).


## F07 — COIN RUSH (Ai nhiều coin nhất)
| Luật | Giá trị |
|---|---|
| Win | `MostCoins` — hết 90s, ai nhiều coin nhất thắng (ưu tiên người sống; hòa thì xét KO) |
| Bắt buộc | field coin `$` (mỗi ô 1 coin, mọc lại 8s), không goal |
| Vũ khí | 0 (CR09 có pad dao nhưng format tắt dao) |
| Bo | Park |
| 10 map | CR01–10 |
| Round | 5 round/video |

## F08 — FOUR-WAY WAR (4 phe)
| Luật | Giá trị |
|---|---|
| Win | `LastTeamAlive`, 4 đội × 5 racer (RoundRobin: racer i → chuồng i%4, đội i%4) |
| Bắt buộc | 4 chuồng `L R A Z` bịt bằng đá 60 hit, hố giữa có dao |
| Vũ khí | 2 dao |
| Bo | Collapse delay 25s, 0.2 |
| Map | TW11–13 |


Cửa nhịp `D` dùng model FBX format 2 `gate-door.fbx` ghép vào khối cửa (trượt xuống sàn khi mở).
