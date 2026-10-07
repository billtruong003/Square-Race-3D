# WEAPON ARENA — kế hoạch v2 sau khi nghiên cứu kênh (2026-09-21)

## 0. Đã xem gì

| Kênh / video | View | Thấy gì |
|---|---|---|
| MrBouncerson — *Bouncing Ball Arena: RGB vs Large Yellow Square* | 76K (top kênh 57K sub) | 2D tối giản, sân tròn, lưới nền, mỗi thực thể có **dãy tim** trên góc, vũ khí (súng, kiếm) nằm trên sàn để nhặt, **boss khối vàng khổng lồ** nhiều tim, 3 đội màu |
| MrBouncerson — *Red Inferno vs Blue Blizzard*, *Golden Colossi vs RGB* | 27–36K | đội có **nguyên tố** (lửa/băng), đội vs khối khổng lồ |
| Simulation Central — *Cube Battle: Survive the Weapon Arena* | **276K** | 20 cube, **bảng đấu loại** (bracket, vương miện) trên đầu màn hình, mỗi vòng 4 con trên một sân nhỏ có **bẫy** (máy ép vàng, cưa xoay, tường xanh quét), cube có mặt, **vũ khí cầm tay thấy rõ** (kiếm, súng) |
| Square League — *Squares Battle Royale: Only 1 Survives* | **1,9M** | 2D pixel, cube mặt cảm xúc, súng lục nằm trên bệ, sân bậc thang; *Squares vs Fire Boss* 311K, *Shotgun Arena 2* 118K |
| Block and Beat — *Squares vs Sniper Rifle Square* | 132K | một con cầm sniper vs bầy |
| SQRAGE — *Squares vs ALL BOSS* | 246K | boss liên hoàn |

**Kết luận về "cái gì ăn view":**
1. **Vũ khí phải nhìn thấy đang hoạt động trên người cube**: kiếm xoay quanh thân, súng bắn ra đạn bay/nảy tường. Không phải "chạm là mất máu".
2. **Sân nhỏ, ít con mỗi lượt (4), có bẫy** (máy ép, cưa, tường quét) — đấu tay đôi đọc được, không phải 12 con loạn xạ.
3. **Cấu trúc giải đấu** (bracket 20 → 5 → 1) giữ người xem tới cuối; crown.
4. **Boss** (bầy vs khối khổng lồ) và **đội nguyên tố** là hai format phụ có số.
5. Tim/thanh máu luôn hiện; cube có mặt/cảm xúc. Cả 2D lẫn 3D đều được, không cần anime hóa; nhận diện của mình là 3D toon + eye cube + VFX, đó là lợi thế.

Vì vậy v2 bỏ hướng "12 con, airdrop 10 súng" và làm **Weapon Arena** theo công thức đã chứng minh, bằng asset thật.

---

## 1. Format: TOURNAMENT (long) và DUEL (short)

**Long — "20 Cubes, 1 Crown"** (~10–12 phút)
- 20 cube → 5 bảng × 4 con, mỗi bảng đấu trên **một sân khác nhau** (5 sân), sống cuối thắng bảng.
- 5 người thắng → chung kết 5 con trên sân thứ 6. Tổng 6 vòng.
- **Bracket UI** trên đầu màn hình suốt video: ô "?" đổi thành mặt cube khi thắng, vương miện ở đỉnh. Cube thua bị gạch đỏ.
- Mỗi vòng có **card giới thiệu sân + vũ khí có trên sân** (2 s).

**Short — "Duel"** (30–60 s): 4 cube, 1 sân, 1 vòng, sống cuối. 1 map = 1 short như hiện tại.

Không đích, không đồng hồ; sân nhỏ + bẫy + tường quét là thứ kết thúc trận.

---

## 2. Cơ chế vũ khí: **gắn trên người, tự hoạt động**

Cube vẫn chạy thẳng và nảy (không AI). Vũ khí nhặt được gắn lên cube và **tự chạy theo nhịp riêng**; sát thương xảy ra khi phần "lưỡi/đạn" chạm đối thủ. Người xem thấy vũ khí làm việc liên tục, không cần telegraph phức tạp.

| Vũ khí | Model (có sẵn) | Hoạt động khi cầm | Sát thương | Đặc trưng nhìn thấy |
|---|---|---|---|---|
| **Sword** | POLYGON `knife` scale to / ETFX `SwordSlashThin` trail | **quay quanh thân 1 vòng/0,8 s**, hitbox = lưỡi | 15/lần chạm, 0,3 s cooldown/nạn nhân | vệt tròn sáng, đúng kiểu kênh 2D nhưng 3D |
| **Axe** | `fireaxe` | quay chậm 1 vòng/1,4 s, lưỡi dài 2,4 m | 35 + hất 5 m + choáng 0,5 s | to, chậm, đáng sợ |
| **Chainsaw** | `SawBlade/model_0` (cưa) gắn trước mũi | cưa quay tại chỗ trước mũi | 8/0,15 s khi kề sát (xé máu liên tục) | máu phun liên tục, tiếng cưa (one-shot lặp có ngắt) |
| **Boxing glove** | primitive + ETFX `PowerboxBoxingGlove` icon | đấm thẳng 0,5 s/lần về hướng chạy | 20 + **hất 8 m** | pinball, kết hợp bumper/cưa/máy ép |
| **Revolver** | `magnum` | bắn theo hướng chạy mỗi 0,7 s, đạn **nảy tường 2 lần** | 25/viên | đạn nảy quanh sân, tự bắn trúng mình cũng được (drama) |
| **Shotgun** | `pumpgun_a` | 5 viên rẻ quạt mỗi 1,2 s, tầm 6 m | 12/viên | gần = chết ngay, giật lùi 2 m |
| **SMG** | `Buzzsub` | 3 viên/0,3 s liên tục, tản 15° | 8/viên | mưa đạn |
| **Sniper** | `Russian_sniper` | mỗi 2 s: **laser ngắm 0,8 s** về con gần nhất (bỏ qua hướng chạy) rồi bắn | 60, xuyên 2 | "một phát chết", laser đỏ |
| **Rocket** | `rocket_launcher` + `rocket_launcher_rocket` | 1 quả/2,5 s theo hướng chạy, nổ chạm tường/racer | 70 tâm / 35 rìa, hất | ETFX RocketMissile + ExplosionFireball |
| **Grenade** | `M32_40mm_grenade` | lob 1 quả/2 s về con gần nhất, **vòng đỏ điểm rơi** | 50 / 25 | bay qua tường |
| **Shield** | ETFX `ShieldSoft` bao quanh | chặn 3 đòn rồi vỡ | 0 | bong bóng xanh, "BLOCK" |
| **Bomb vest** | `Blast_set` | đếm 4 s hoặc chạm là nổ | 80 / 40, tự chết | KAMIKAZE |
| **Laser sword** | `laser_a` + emissive blade | như Sword nhưng lưỡi 3 m, quay nhanh 1 vòng/0,5 s | 20 | lightsaber hiếm (weight thấp) |

Luật chung: 1 vũ khí/cube; cầm theo giờ (8 s) hoặc đạn; chết → văng ra 4 m thành đồ nhặt. Vũ khí mọc trên **bệ cố định trong sân** (không airdrop), bệ tái sinh sau 6 s — đúng cách các kênh làm, người xem biết đồ ở đâu.

HP 100, thanh máu trên đầu + cột leaderboard; đòn ≥ 40 HP → "CRITICAL" + choáng.

---

## 3. Sáu sân (mỗi sân 1 bảng đấu, 4 con). Kích thước nhỏ: **34×20 ô = 48×28 m**, camera gần hơn, bẫy chiếm 30 % sân.

Bẫy dùng thiết bị đã có: máy ép `P`, cưa `S`, gai `T`, bumper `U`, băng chuyền `> <`, cửa nhịp `D`, tường quét = pressure Chase từ trái. Bệ vũ khí `W` (2×2) đặt 2–3 bệ/sân, mỗi bệ gắn 1 loại cố định (sân "Shotgun Alley" chỉ có shotgun + sword…): mỗi sân là một *thực đơn vũ khí*, tên sân nói luôn.

| # | Sân | Bẫy | Bệ vũ khí | Kết thúc bằng |
|---|---|---|---|---|
| A1 | **Piston Hall** | 3 máy ép trên trần, cưa xoay giữa | sword, revolver | ép + đấu |
| A2 | **Saw Corridor** | hành lang cưa ray dọc giữa, gai 2 góc | chainsaw, boxing glove | hất vào cưa |
| A3 | **Bumper Pit** | 8 bumper vòng ngoài, hố giữa | boxing glove, shotgun | pinball |
| A4 | **Sniper Alley** | 2 tường có khe, băng chuyền kéo vào giữa | sniper, SMG | băng chuyền dồn vào tầm sniper |
| A5 | **Bomb Yard** | cửa nhịp chia 2 phòng, gai giữa | grenade, bomb vest, shield | nổ qua tường |
| FINAL | **Colosseum** | 4 trụ, **tường quét** từ trái 0,3 m/s từ 20 s | tất cả 6 bệ | tường quét ép chung kết |

ASCII từng sân sẽ vẽ trong forge sau khi bạn duyệt danh sách này (30 phút/sân). Sân dựng bằng Kenney room/floor/wall + banner, bệ vũ khí = Kenney `template-floor-layer-raised` + spotlight.

---

## 4. Asset thật (tất cả đã có trong project, không placeholder)

- **Model vũ khí**: POLYGON `knife, fireaxe, magnum, pumpgun_a, Buzzsub, Russian_sniper, rocket_launcher(+rocket), M32_40mm_grenade, Blast_set, laser_a`; `SawBlade/model_0` (chainsaw); glove = ETFX `PowerboxBoxingGlove` billboard trên cube primitive.
- **VFX Epic Toon FX**: `SwordSlashThin/Thick`, `SwordWave`, `SwordHitRedCritical`, `ToonPunchHeavy/Extreme`, `ToonRadialPunchHeavy` (sóng hất), `GunFire`, `StandardMuzzle`, `BulletSmallFire` (đạn), `LaserMissile`/`LaserRedOBJ`, `RocketMissile`, `GrenadeMissile`, `GrenadeExplosion`, `ExplosionFireballFire`, `ExplosionNovaFire`, `FlashExplosionRadial`, `ShieldSoft`, `WarningExplosive` (điểm rơi), `StunnedCirclingStars`, `BloodSplatCritical`, `BloodShowerLoop` (chainsaw), `SoulExplosion` (chết), chữ comic `KaPow/Pow/Bang/Blam/Smack/Critical/Miss/Wow`, `Windlines` (tốc), `ItemSparkleBurst` (bệ tái sinh), `ConfettiBlastRainbow` (crown).
- **SFX**: Deadly Kombat (blade_hit, body_hit_*, whoosh, block_*, bone_breaking, finisher), Kenney impact, ETFX gunshot/explosion.
- **Sân**: Kenney `room-*`, `template-*`, `banner`, `cables`; bẫy đã có model.
- **Cube**: eye cube + mặt cảm xúc bằng ETFX `Emoji*` (EmojiShocked khi bị trúng nặng, EmojiDead khi chết, EmojiCool khi thắng) — kênh 2D dùng mặt cảm xúc, mình có sẵn 40 emoji.
- **Thiếu**: font `Bangers` (miễn phí) cho bracket/popup. Không mua gì.

---

## 5. Lớp trình bày

Bracket UI (top strip, 20 ô), thanh máu + ghost, hit-stop 3/6 tick, rung camera, số máu + chữ comic, slow-mo kết liễu + kill-cam, cube chết bay xoáy + vũng máu, vũ khí văng, card sân/vũ khí, emoji trên cube. Không cần "anime" riêng; toon 3D + comic text là đủ và khác biệt so với 2D.

---

## 6. Phụ (sau khi Arena chạy)

- **Boss Arena**: 12 cube vs 1 khối khổng lồ 3 000 HP có cưa/máy ép gắn thân (tier C cũ, giờ có số: 76K–311K).
- **Element Teams**: Inferno (đòn thêm cháy 5 HP/s × 3 s, để lại vệt lửa) vs Blizzard (đòn làm chậm 40 % × 2 s) — 10v10 trên Colosseum.

---

## 7. Lịch và cửa duyệt

**A (2 ngày)**: 13 vũ khí gắn-người + bệ vũ khí + 6 sân nhỏ + bracket director (5 bảng + chung kết) + ModeLab: bảng 4 con 25–70 s, ≥ 80 % chết do vũ khí/bẫy, không vũ khí > 35 % kill, 0 hòa/treo.
**B (1 ngày)**: VFX/SFX/emoji/bracket UI/kill-cam, quay **1 clip thử** (1 bảng + chung kết rút gọn).
**C**: bạn duyệt clip → quay long + 20 short duel.

Dừng ở đây chờ bạn duyệt: danh sách vũ khí (mục 2), sáu sân (mục 3), format tournament (mục 1).

---

## Trạng thái 2026-09-21 — Step A xong (chờ duyệt clip thử ở Step B)

- 14 model vũ khí build qua `CubeSim/Build Weapon Assets` (POLYGON + SawBlade + BoxingGloves/Glove.prefab mới), tự flip/scale theo trục dài nhất, thấy rõ từ trên.
- 6 sân `Arenas/Arena/A1..A5, FINAL` (48×28 m) dựng từ template ASCII, mỗi bệ `W` gắn 1 loại (`ArenaTournamentBuilder.Arenas`), bệ = đĩa vàng phát sáng, vũ khí tái sinh 6 s; đồ hết giờ/hết đạn biến mất, đồ của cube chết nằm 8 s rồi biến mất.
- Scene `Scenes/Modes/M_Arena.unity`: 20 cube → 5 bảng × 4 → chung kết 5 (`RoundSpec.finalOfWinners`), tốc độ 4.5 m/s (`RoundSpec.racerSpeed`), HP 100, Collapse tới hố 8×8 (delay 20 s, 0.35 m/s), FINAL Chase từ trái.
- Lab gate `CubeSim/Arena/Lab Gate` (Docs/ModeLab/arena_gate.txt, 120 vòng): 86 % vòng 25–70 s, 80 % chết do vũ khí/bẫy, Sniper cao nhất 25 %, 0 hòa/timeout.
- Còn Step B: bracket UI (Bangers), VFX/SFX theo vũ khí, emoji, kill-cam/hit-stop/shake, rồi quay 1 clip thử.
