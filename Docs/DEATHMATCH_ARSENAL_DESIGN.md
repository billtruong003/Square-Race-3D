# CubeSim — ANIME FIGHT ARENA (deathmatch arsenal): game design (2026-09-21)

## 0. Ý tưởng một câu

Sân trống, vòng an toàn co dần, vũ khí rơi liên tục **bên trong vòng**, mỗi loại một kiểu chết. Map không cần phức tạp: kịch tính đến từ *vũ khí nào rơi xuống gần ai* và *cảnh chết nhìn đã*.

Tiền đề vẫn như cũ: racer không có AI (chạy thẳng, nảy), sim deterministic, không đồng hồ. Vũ khí "tự bắn" khi có mục tiêu trong tầm và cung, đúng như dao hiện tại.

---

## 1. Vòng đấu

| Thứ | Giá trị | Vì sao |
|---|---|---|
| Sân | 68×38 m (long) / 38×68 m (short), 0–4 trụ tròn nhỏ, **không** tường chia | tường chia đã gây treo (SD01); trụ chỉ để đạn có chỗ nổ |
| Racer | 12 solo, 3 tim | 3 tim để vũ khí yếu/mạnh có nghĩa (dao 1 tim, súng bắn tỉa 2, nổ 1 diện rộng) |
| Vòng | Collapse: chờ 10 s, 0,45 m/s, siết tới hộp 6×2 m như luật mới | pressure là đồng hồ |
| Vũ khí trên sân | tối đa **4 cùng lúc**, rơi mới mỗi **6 s** nếu dưới 4 | luôn có đồ, không bao giờ ngập |
| Vị trí rơi | ngẫu nhiên (seed) **trong vòng an toàn hiện tại**, cách mép vòng ≥ 3 m, cách racer ≥ 2 m, cách vũ khí khác ≥ 4 m | vòng co thì đồ dồn vào giữa → bầy dồn theo |
| Cách rơi | "airdrop": bóng mờ tròn hiện 1,2 s trên sàn, rồi vũ khí rơi cắm xuống + bụi + tiếng thịch | người xem đoán được ai sẽ nhặt |
| Cầm | 1 vũ khí/racer, thời gian cầm theo loại (bảng dưới) hoặc hết đạn thì rơi; **chết thì văng ra** (mục 3) | vòng đời đồ ngắn → xoay tua |
| Kết thúc | sống cuối; kẻ giết nhiều nhất ghi trên card kèm vũ khí kết liễu | |

---

## 2. Kho vũ khí (8 loại, mỗi loại một "cảnh chết")

Xác suất rơi theo trọng số; mọi loại dùng model **POLYGON weapons** (đã có) trừ dao/búa dùng model hiện tại + placeholder.

| # | Vũ khí | Loại | Sát thương | Tầm / cung | Nhịp | Cầm | Trọng số | Cảnh chết đặc trưng |
|---|---|---|---|---|---|---|---|---|
| 1 | **Cleaver** (dao bếp, có sẵn) | cận | 1 tim | 1,9 m / 100° | 0,55 s | 8 s | 20 | vệt chém + máu tia (đang có) |
| 2 | **Hammer** (búa) | cận, knockback | 1 tim + **hất 7 m** | 2,2 m / 120° | 1,0 s | 8 s | 12 | nạn nhân bay xoáy, đập tường mất thêm 1 tim, rơi ra ngoài vòng = chết ép |
| 3 | **Pistol9** | xa, đạn | 1 tim | 12 m / 25° | 0,6 s | 6 viên | 16 | đạn vàng, tia lửa, xác giật lùi |
| 4 | **Shotgun3** | xa, 5 đạn rẻ quạt | 1 tim/viên, tối đa 3 | 6 m / 40° | 1,2 s | 3 phát | 12 | 1 phát gần = chết ngay, xác văng 4 m, khói trắng |
| 5 | **Russian_sniper** | xa, hitscan xuyên | **2 tim**, xuyên tối đa 2 con | 30 m / 8° | 1,8 s | 3 phát | 8 | vệt laser đỏ xuyên sân 0,15 s, mọi con trên đường mất tim, "double kill" popup |
| 6 | **M32 Grenade launcher** | xa, đạn cầu vồng | nổ bán kính 4 m: 1 tim, tâm 2 m: 2 tim | 14 m / 30° | 1,5 s | 3 quả | 10 | cầu vồng lựu đạn → ExplosionFireballFire + Nova + hố cháy 4 s trên sàn, xác văng ly tâm |
| 7 | **Buzzsub** (SMG) | xa, bắn dải | 0,5 tim/viên | 9 m / 20° | 0,12 s (băng 12) | 12 viên | 12 | mưa tia, máu tóe liên tục, nạn nhân giật cục |
| 8 | **Blast_set** = **Bomb vest** | tự sát | khi cầm 4 s hoặc chạm đối thủ: nổ 5 m, 2 tim, người cầm chết | 0 m | tới nổ | 5 | đếm 4-3-2-1 to trên đầu, nổ trắng màn hình 3 khung, khói hình nấm nhỏ; "KAMIKAZE" popup |
| 9 | **Nuke** (hiếm, 2m cube đỏ) | tự sát diện rộng | rơi 1 lần/vòng lúc còn ≤ 5 con: 8 s đếm, nổ 12 m, 3 tim | | | 0 (kịch bản) | màn hình trắng, camera rung mạnh, mọi thứ trong vòng chết, người cầm cũng chết → còn ai ngoài vòng nổ thắng |

**Luật đạn (Ranged)** dùng `ProjectilePool` hiện có: đạn thẳng; Shotgun = 5 projectile lệch góc; Grenade = projectile có trọng lực (parabol, nổ khi chạm đất/racer); Sniper = hitscan có sẵn + xuyên (đếm hit). SMG = attackCooldown 0,12 với ammo 12.

**Cân bằng đầu vào**: dao/pistol/SMG phổ biến (48 %), búa/shotgun (24 %), nặng (sniper/M32) 18 %, bom 5 %, nuke kịch bản. Vòng còn ≤ 5 con mà chưa có nuke → nuke rơi giữa vòng.

---

## 3. Combat juice ("bùm chéo", "tung tóe")

Toàn bộ là presentation trừ 2 mục đánh dấu **[sim]**, những mục đó vẫn deterministic vì tính theo tick.

| Hiệu ứng | Cách làm | Khi nào |
|---|---|---|
| **Hit-stop** | sim dừng 4 tick (kill) / 2 tick (trúng) — `Bootstrap.Paused` theo đếm tick, không theo giây **[sim]** | mọi cú trúng |
| **Knockback** **[sim]** | nạn nhân nhận vận tốc đẩy theo hướng đòn: dao 2 m, shotgun 4 m, búa 7 m, nổ ly tâm 5 m; đang bay thì đập tường mất 1 tim (búa/nổ) | mọi cú trúng |
| **Camera shake** | rung 0,15 s biên 0,25 m (trúng), 0,35 s / 0,8 m (nổ), 0,8 s / 2 m (nuke); portrait rung theo cả 2 trục | trúng/nổ |
| **Screen flash** | overlay trắng 2–3 khung alpha 0,6 (nổ), đỏ 0,3 (kill) | nổ/kill |
| **Slow-mo kết liễu** | 2 con cuối: đòn kết liễu → sim chạy 0,25× trong 1,2 s rồi card thắng (đổi fixedDeltaTime? không — chạy 1 tick mỗi 4 khung) **[sim-safe]** | kill cuối |
| **Xác văng** | khi chết: cube bay lên 1,5 m xoay 2 vòng theo hướng đòn rồi rơi + nảy 2 lần, để lại vũng máu (đang có BloodPool) | kill cận/súng |
| **Vũ khí văng** | vũ khí người chết **bay parabol 3–6 m, xoay**, cắm xuống sàn thành pickup mới (vị trí = điểm rơi, snap vào trong vòng) **[sim]** | mọi cái chết |
| **Popup chữ** | "SLASH!", "HEADSHOT", "DOUBLE KILL", "BOOM", "KAMIKAZE", "+1 KO" bay lên từ điểm chết, font dày viền đen (DamagePopupSystem có sẵn, thêm kiểu chữ to) | theo loại |
| **Vệt đòn** | dao: slash trắng cong (có sẵn); búa: sóng xung tròn trên sàn; sniper: laser đỏ; nổ: ExplosionFireball + Nova + Decal cháy 4 s | theo loại |
| **Kill-cam** | card winner đổi thành: 1,5 s zoom camera vào điểm kill cuối (lerp fov/vị trí) rồi mới hiện chữ | kết thúc |
| **Âm** | mỗi vũ khí 1 tiếng bắn + 1 tiếng trúng; nổ = Boom + rung; nuke = còi 8 s + nổ trầm; đều one-shot Kenney/ETFX, vào OfflineAudioTrack như hiện tại | |
| **HUD** | dải luật "DEATHMATCH · shrinking zone, weapons drop inside"; bộ đếm "ALIVE 7 · NEXT DROP 3s · ZONE 60%"; leaderboard: icon vũ khí đang cầm cạnh tên | |

---

## 4. Những gì phải code (ước lượng)

| Việc | Dòng | Ghi chú |
|---|---|---|
| `WeaponDefinition` mở rộng: knockback, pellets, gravity, pierce, blastRadius, blastDamageCenter, fuseSeconds, kind (Melee/Ranged/Suicide) | 60 | JSON round-trip sẵn |
| `CombatSystem`: knockback vào racer (Velocity thêm vào mover 1 bước), shotgun spread, pierce hitscan, projectile parabol + nổ AoE, bomb vest countdown, nuke script | 350 | |
| `ArsenalSpawner` (thay SpawnWeapons): airdrop theo lịch, vị trí trong vòng an toàn, tối đa 4, vũ khí văng khi chết | 200 | dùng `FindValidDropPosition` sẵn |
| Juice: HitStop (tick pause), CameraShake, ScreenFlash, ragdoll-cube (visual only), weapon fling visual, popup lớn, kill-cam | 400 | presentation |
| Model/VFX wiring: 7 POLYGON prefab vào `WeaponVisualLibrary`, 5 ETFX nổ vào `VfxLibrary`, 16 SFX | data | |
| ModeLab spec + map `DM01–DM06` (sân trống + trụ) | 100 | |
| **Tổng** | ~1100 dòng, 3–4 ngày | |

---

## 5. Cửa khả thi (ModeLab, 20 seed × 6 map)

1. Trận 45–120 s ở ≥ 80 % seed; 0 seed chạm 300 s.
2. ≥ 70 % cái chết là do vũ khí (không phải ép vòng). Nếu dưới: tăng nhịp rơi lên 4 s.
3. Không vũ khí nào chiếm > 45 % số kill (nếu sniper/M32 áp đảo → giảm trọng số hoặc tầm).
4. Winner rải ≥ 8 màu / 20 seed.
5. Trung bình ≥ 6 lượt nhặt mỗi trận (đồ có xoay tua).
6. Không cái chết nào do knockback đẩy xuyên tường (kiểm tra bằng đếm `Crushed` trong khi bay).

---

## 6. Video

- **Long**: "12 CUBES, 8 WEAPONS, 1 SURVIVOR" — 6 vòng, mỗi vòng highlight vũ khí kết liễu trên card; podium cuối theo số kill.
- **Short**: 1 vòng, nuke rơi lúc còn 4 con là hook thumbnail ("NUKE DROP").
- Biến thể rẻ sau khi có kho: **Team Deathmatch** (2 đội), **Gun Game** (mỗi kill đổi lên vũ khí kế tiếp, ai qua hết 8 loại thắng), **One-Weapon Rounds** (mỗi vòng chỉ rơi 1 loại: "SNIPER ONLY", "HAMMER ONLY").

---

## 7. Thứ tự làm

1. Kho vũ khí + knockback + airdrop (chơi được, chưa juice) → ModeLab qua cửa.
2. Juice tầng 1: hit-stop, knockback, shake, flash, popup, vũ khí văng.
3. Juice tầng 2: xác văng, kill-cam, slow-mo cuối, nuke.
4. 6 map DM + short/long scene → quay thử 1 clip cho bạn duyệt trước khi quay loạt.


---

## 8. Chốt 2026-09-21: HP thay tim, lớp anime 2D

**HP.** 100 HP/racer (Sudden Death cũ = 35 HP). Thanh máu trên đầu cube (world-space, luôn quay về camera) + cột HP trên leaderboard. Xanh → vàng → đỏ theo %; khi trúng thanh giật, phần mất hiện vệt trắng trễ 0,4 s rồi tụt; số sát thương bay lên; đòn ≥ 50 = "CRITICAL".

| Nguồn | HP |
|---|---|
| Cleaver 35 · Hammer 40 (+hất 7 m) · Pistol 25 · SMG 12/viên · Shotgun 30/viên (max 3) · Sniper 70 xuyên · Grenade 60 tâm / 30 rìa · Bomb vest 80 · Nuke 999 |
| Gai 15 · Cưa 25 · Potion +40 · Ép vòng = chết |

**3D + 2D.** Thế giới, cube, vũ khí (POLYGON), sân giữ 3D. Lớp anime là 2D đè lên: sprite billboard (ETFX 2D), speed lines viền màn hình khi kill, impact frame đen-trắng 2 khung khi đòn ≥ 50, chữ manga (ドン / ゴゴゴ / MUDA, font Bangers), aura quanh cube. Không model 3D mới.

**Phong cách (không dùng nhân vật/nhạc/tên thật):** Dragon Ball làm xương sống (aura theo màu, "POWER" = HP hiện to, còn dưới 25 HP → aura vàng "Super", tốc độ +15 %; nuke = Spirit Bomb đếm 8 s), Demon Slayer cho cận chiến (vệt chém nước/lửa cong), JoJo cho kết liễu (impact frame, pose 0,5 s, ゴゴゴ).

**Thứ tự làm (đã duyệt):** 1 HP + thanh máu + kho vũ khí + airdrop → ModeLab; 2 juice tầng 1; 3 lớp anime + nuke; 4 map DM + quay thử.
