# CubeSim — Game design các chế độ mới (2026-09-04)

## 0. Tiền đề bắt buộc (mọi thiết kế phải qua cửa này)

1. **Racer không có AI.** Nó chỉ chạy thẳng tốc độ cố định và nảy khi va chạm. Mọi luật đòi racer *muốn* làm gì (đứng yên trong vùng, né ô sàn, đuổi theo ai) đều vô nghĩa. Luật chỉ được xây trên **va chạm, vị trí, thời gian và vật phẩm** — thứ tự tự nhiên xảy ra khi 12 khối nảy trong sân.
2. **Deterministic.** Mọi cơ chế chạy trong `SimulationRunner.Step` theo seed; không dùng `Random.value`, không dùng physics của Unity ngoài query tường.
3. **Không đồng hồ** (trừ chế độ mà đồng hồ *là* luật). Trận phải tự kết thúc bằng cơ chế + pressure, và phải có đường thoát chống treo.
4. **Đọc được trong 2 giây từ trên xuống**: dải luật + bộ đếm phải diễn tả được luật.

Loại ngay vì vi phạm tiền đề 1: **Floor is Lava** (không né được ô mất), **King of the Hill** (không ở lại vùng được).

Còn lại 9 chế độ, chia 3 nhóm theo cửa khả thi:

| Nhóm | Chế độ | Vì sao qua cửa |
|---|---|---|
| A — chỉ cần va chạm | Infection, Hot Potato, Lucky Block Battle, Bumper Sumo | luật kích hoạt bằng chạm |
| B — chỉ cần vị trí/thứ tự | Elimination Race, Grand Prix, Team Race, Paint War | dùng đích/ô sàn đã có |
| C — cần thực thể mới | Boss Fight, Eat & Grow | cần chứng minh bằng test map trước |

---

## 1. INFECTION (Zombie) — nhóm A

**Concept.** 12 khối, 1 con bị nhiễm lúc 3 s. Chạm là lây. Con sạch cuối cùng thắng.

**Luật.**
- Bắt đầu: tất cả sạch. Tại t = 3 s, seed chọn 1 con → nhiễm (đổi màu xanh độc, outline tím, tiếng rít).
- Chạm (overlap hộp, như melee hiện tại) giữa nhiễm và sạch → sạch thành nhiễm ngay lập tức, không cooldown. Nhiễm không bao giờ hết nhiễm.
- Nhiễm chạy nhanh hơn 8 % (để không treo khi con sạch cuối cùng chạy trước mãi).
- Kết thúc: còn **1 con sạch** → con đó thắng ("SURVIVOR"). Trường hợp 2 con sạch cùng bị lây trong một bước → xử theo thứ tự index (deterministic), con sau là winner.
- Không tim, không dao. Hazard trên map vẫn giết bình thường (chết là hết, không tính sạch/nhiễm).

**Chống treo.** Collapse siết về hộp 10×8 m từ 15 s. Trong hộp đó 12 khối chạm nhau vài lần mỗi giây → không thể không lây. Dự kiến trận 25–60 s.

**Cần code.**
- `InfectionSystem` (mới, ~150 dòng): trạng thái nhiễm per racer, kiểm tra cặp chạm qua `RacerContactGrid` (đã có), sự kiện `OnInfected(victim, source)`.
- `WinCondition.LastClean` (1 case trong runner).
- Visual: đổi tint + outline (dùng lại `ArmedOutline` với màu khác), VFX `BloodSplat` màu xanh, SFX 1 one-shot.
- UI: dải luật "INFECTION · last clean cube survives" + bộ đếm "CLEAN 7 · INFECTED 5"; leaderboard: con nhiễm có icon ☣ và xếp xuống dưới.

**Map.** Dùng SD/TW hiện có (không dao). Tốt nhất là map thoáng ít hành lang cụt để lây lan đều.

**Test khả thi (pass/fail).** 20 seed trên 3 map: trận dài 20–90 s (pass ≥ 18/20); không seed nào chạm 300 s; winner rải ≥ 6 màu khác nhau (không lệch màu do index).

**Video.** Short 1 map; long 6 vòng, mỗi vòng "patient zero" khác màu.

---

## 2. HOT POTATO (Bom chuyền) — nhóm A

**Concept.** Một quả bom trên đầu một con, 8 s nổ. Chạm ai thì bom sang con đó. Nổ là chết. Sống cuối thắng.

**Luật.**
- t = 2 s: bom xuất hiện trên con do seed chọn. Đồng hồ bom 8 s hiện to trên đầu, nhịp tick tăng dần.
- Chạm giữa người cầm và con khác → bom chuyển sang con kia; **1,0 s không được chuyền ngược lại cho người vừa đưa** (chống ping-pong vô hạn giữa hai con kẹt góc). Sau 1 s thì chuyền ngược được.
- Nổ: người cầm chết; mọi con trong bán kính 3 m mất 1 tim (2 tim/con). VFX nổ + rung camera nhẹ.
- 2 s sau nổ, bom mới sinh trên con còn sống do seed chọn. Lặp đến khi còn 1.
- Không dao. Hazard vẫn hoạt động.

**Chống treo.** Mỗi chu kỳ ≤ 10 s giết ≥ 1 con → tối đa ~110 s cho 12 con. Không cần collapse mạnh; dùng Collapse nhẹ 0,3 m/s để gom bầy cho chuyền nhiều.

**Cần code.**
- `BombSystem` (~120 dòng): holder, fuse, lịch sử chuyền, nổ = damage theo bán kính qua delegate damage hiện có.
- Visual: bom = sphere đen + ngòi sáng, đặt ở `WeaponAnchor` (đã có), số đếm bằng TextMesh như counter đá.
- UI: "HOT POTATO · holder explodes at 0" + "BOMB ON: YELLOW  3.2s".

**Test khả thi.** 20 seed: trận 40–130 s; số lần chuyền trung bình ≥ 4 mỗi chu kỳ (nếu < 2, bầy quá thưa → tăng collapse); không có chu kỳ nào bom ở một con quá 8 s mà không nổ (bug).

**Video.** Short và long đều hợp; long 6 vòng.

---

## 3. LUCKY BLOCK BATTLE — nhóm A

**Concept.** Sudden Death nhưng không có dao lúc đầu. Sân rải thùng `?`. Đập thùng ra đồ ngẫu nhiên (theo seed). Sống cuối thắng.

**Luật.**
- Thùng: breakable 1 cú đập, 12–16 thùng/map, mọc lại sau 12 s ở chỗ cũ (như coin).
- Bảng rơi (seed, xác suất): dao 25 % · potion +1 tim 20 % · giáp (chặn 1 đòn) 15 % · boost tốc độ 5 s 15 % · bom nổ tại chỗ (mất 1 tim mọi con trong 3 m, kể cả người đập) 15 % · bẫy gai mọc tại chỗ thùng 10 %.
- Đồ rơi nằm tại chỗ thùng, nhặt bằng chạm (dùng pickup logic hiện có). Bom và bẫy kích hoạt ngay.
- 2 tim. Dao rơi theo giờ cầm như hiện tại.

**Chống treo.** Collapse chuẩn của Sudden. Dao xuất hiện xác suất 25 %/thùng, 14 thùng → gần chắc có dao trong 20 s đầu.

**Cần code.**
- Marker `?` trong ASCII + builder (thùng Kenney crate).
- `LootTable` seeded + 2 vật phẩm mới: giáp (flag chặn 1 damage), boost (dùng `BoostUntil` đã có).
- Bom/bẫy tại chỗ: dùng lại SpikeTrap runtime, damage theo bán kính.
- UI: "LUCKY BLOCKS · smash for loot" + "ALIVE 9 · KNIVES OUT 2".

**Test khả thi.** 20 seed: trận 40–120 s; ≥ 70 % trận có ≥ 1 kill bằng dao (nếu ít → tăng tỉ lệ dao); không trận nào kết thúc chỉ bằng collapse trước 30 s.

**Video.** Cả hai; thumbnail "?" rất dễ bán.

---

## 4. BUMPER SUMO — nhóm A

**Concept.** Võ đài tròn giữa sân, không tường viền, rơi khỏi đài là chết. Bumper rải trên đài đẩy tung. Đài thu nhỏ dần. Sống cuối thắng.

**Luật.**
- Playable = hình tròn bán kính 14 m (ASCII: ô ngoài đài là `~` = vực). Racer ra ngoài mép → rơi (animation rơi + co nhỏ 0,5 s) → chết.
- 5–7 bumper trên đài (đã có), boost ×2 trong 0,8 s làm khối bay xa.
- Đài thu: bán kính giảm 0,25 m/s từ 10 s (Collapse dạng tròn → cần `CircularPressure`), mép đài đỏ nhấp nháy.
- Không dao, không tim. Va chạm khối–khối đẩy nhau như hiện tại (contact solver).

**Chống treo.** Đài thu về bán kính 3 m ở ~55 s; 2 khối cuối trong 3 m với bumper chắc chắn văng ra.

**Cần code.**
- Marker `~` (vực, không tường, không sàn) + luật "ngoài playable là rơi" (đã có với crusher, tổng quát hóa 20 dòng).
- `CircularPressure` (mới, ~80 dòng, cùng interface `PressureField`).
- Visual: đài = đĩa, vực = nền tối + hiệu ứng rơi.
- UI: "SUMO · fall off and you're out" + "ALIVE 6 · RING 9 m".

**Test khả thi.** 20 seed: trận 30–90 s; ≥ 50 % cái chết là do bumper hất (đo: chết trong 1 s sau bump) — nếu chủ yếu chết do đài thu thì bumper quá yếu.

**Video.** Short rất hợp (sân tròn vừa khung dọc).

---

## 5. ELIMINATION RACE — nhóm B

**Concept.** Đua nhiều vòng; mỗi vòng con về đích cuối cùng bị loại. Đến khi còn 1.

**Luật.**
- 12 racer. Lịch loại: vòng 1–2 loại 3 con/vòng → 6; vòng 3–4 loại 1 → 4; vòng 5 loại 1 → 3; vòng 6 loại 1 → 2; vòng 7 chung kết 1v1. Tổng 7 vòng.
- Vòng kết thúc khi số con **chưa về** = số bị loại của vòng đó (không đợi con cuối lết). Con chết vì hazard/pressure = bị loại (ưu tiên loại con chết trước, còn thiếu thì loại con về cuối).
- Pressure ChaseDown 1,5 m/s như Race để vòng gọn.
- Kết thúc video: bảng loại theo vòng + winner.

**Chống treo.** Pressure đuổi giết con lết; đích 3 suất không cần.

**Cần code.**
- `EpisodeDirector`: chế độ `Elimination` (danh sách loại mỗi vòng, racerCount giảm dần, racer bị loại không spawn vòng sau; giữ màu/tên).
- Runner: `WinCondition.ReachGoal` với `requiredFinishers = alive − eliminateThisRound` (tính lúc bắt đầu vòng).
- UI: dải "ELIMINATION · last 3 home are OUT" + "HOME 4/9 · OUT ZONE 3"; card giữa vòng "ELIMINATED: RED, PINK, LIME".
- Leaderboard: con bị loại gạch chéo, xếp cuối, giữ nguyên.

**Test khả thi.** Chỉ cần 1 seed chạy hết 7 vòng đúng lịch; kiểm tra racer bị loại không xuất hiện lại.

**Video.** Long (7 vòng ≈ 5–6 phút). Short = 1 vòng đầu "3 con bị loại là ai?" cũng dùng được.

---

## 6. GRAND PRIX — nhóm B

**Concept.** 8 vòng đua, tích điểm theo thứ tự về đích, tổng điểm cao nhất vô địch.

**Luật.**
- Điểm: 1st 10 · 2nd 8 · 3rd 6 · 4th 5 · 5th 4 · 6th 3 · 7th 2 · 8th 1 · còn lại 0. Vòng kết thúc khi 8 con về hoặc pressure giết hết phần còn lại.
- Bảng tổng cập nhật sau mỗi vòng (card 3 s: top 5 + điểm). Vô địch = tổng cao nhất; hòa → số lần nhất.
- Map thay đổi mỗi vòng (8 map RC khác nhau).

**Cần code.** Điểm cộng dồn trong `EpisodeDirector` (~60 dòng), card bảng điểm, leaderboard hiện "27 pts". Runner đã có thứ tự finisher.

**Test.** 1 seed toàn video; kiểm tra điểm khớp log.

**Video.** Long. Đây là format "series" bán được nhiều tập.

---

## 7. TEAM RACE — nhóm B

**Concept.** 2 đội × 6. Đội có **3 con về đích trước** thắng vòng. Best-of-5.

**Luật.** Như Race, thêm bộ đếm theo đội. Đội thua vòng bị trừ 1 con vòng sau? — không, giữ đơn giản: 5 vòng, đội thắng 3 vòng thắng video.

**Cần code.** `WinCondition.TeamFinishers` (đếm finisher theo `racer.Team`, ~40 dòng); UI "RED 2/3 · BLUE 1/3".

**Video.** Long 5 vòng.

---

## 8. PAINT WAR (Chiếm đất) — nhóm B

**Concept.** Sân là lưới ô 1,4 m. Racer chạy tới đâu tô màu ô đó (đè màu cũ). Hết 60 s, ai nhiều ô nhất thắng. Đây là chế độ **có đồng hồ** như Coin Rush.

**Luật.**
- Mỗi ô ghi màu của racer cuối cùng đứng lên (tâm racer trong ô). Ô tường không tính.
- 12 màu; hết giờ đếm ô. Có bumper/băng chuyền để quãng đường đa dạng. Không dao; có "potion sơn" (ô 3×3 quanh chỗ nhặt đổi màu người nhặt).
- Team variant: 2 đội, đếm theo đội.
- Đồng hồ 60 s (short) / 90 s (long). 5 s cuối đỏ nhấp nháy.

**Chống treo.** Không cần (có đồng hồ).

**Cần code.**
- `PaintGrid`: mảng ô + mesh sàn một khối với màu theo ô (đổi màu vertex, không tạo object mỗi ô). ~200 dòng.
- `WinCondition.MostTiles`; leaderboard cột "ô" thay tim; bộ đếm "TIME 34s · LEAD RED 143".
- Map: dùng CR (thoáng) hoặc vẽ mới.

**Test khả thi.** Hiệu năng: 48×26 = 1 248 ô, cập nhật màu mesh mỗi bước — tầm thường. Cân bằng: 20 seed, chênh lệch top1–top2 trung bình 5–15 % (nếu quá sát thì hay, quá xa thì map lệch).

**Video.** Cả hai. Hình ảnh mạnh nhất trong danh sách.

---

## 9. BOSS FIGHT — nhóm C (phải chứng minh trước)

**Concept.** 12 racer vs 1 khối khổng lồ 6×6 m có 60 HP. Racer cầm dao chạm boss thì boss mất máu. Boss húc là mất tim. Giết boss trước khi bị diệt sạch.

**Luật.**
- Boss: 6×6 m, tốc độ 3,5 m/s, chạy-nảy như racer nhưng **không nảy khi va racer** (đẩy racer văng, gây 1 tim, knockback 4 m, mỗi racer 1 s mới bị lại). Boss là tường di động với logic kẹp như máy ép: racer bị kẹp giữa boss và tường → chết.
- Racer: 3 tim, không tự hồi; 3 dao trên sân, mọc lại 6 s sau khi rơi. Dao chạm boss: 1 HP/đòn, cooldown đòn 0,5 s. Con không dao chạm boss chỉ bị đau.
- Pha: 100–60 % HP đi thẳng; 60–30 % tốc độ 4,5 m/s, mỗi 6 s "dậm" (spike vòng bán kính 5 m, báo trước 0,6 s); < 30 % tốc 5,5 m/s + 2 lưỡi cưa gắn quanh thân xoay.
- Kết thúc: boss 0 HP → **racer gây nhiều damage nhất** thắng ("SLAYER"); racer chết hết → boss thắng (video vẫn hợp lệ, nhãn "BOSS WINS").
- Potion 2 chai trên sân, mọc lại 15 s.

**Chống treo.** Boss luôn di chuyển và luôn tìm ra racer vì sân kín; collapse không cần. Nếu sau 120 s boss > 50 % HP thì boss tự tăng tốc theo bậc mỗi 20 s (đảm bảo kết thúc bằng cách này hay cách khác).

**Cần code.**
- `BossEntity` chạy trong runner như device: vị trí, hướng, HP, pha, va chạm với tường (dùng `PlanarMover` với half-extent 3 m), va chạm với racer (grid hiện có). ~300 dòng.
- Damage boss từ melee: mở rộng `TryMelee` để boss là mục tiêu.
- Visual: cube lớn có mắt (eye cube scale 3, tint đỏ đen), thanh HP trên đầu boss + trên HUD; dậm/cưa dùng SpikeTrap + SawBlade sẵn có gắn theo boss.
- UI: "BOSS FIGHT · kill it before it kills you" + "BOSS 42/60 · ALIVE 9".

**Chứng minh khả thi — bắt buộc trước khi vẽ map thật.**
Map `DEV_BOSS` (sân trống 40×30, 3 dao, 2 potion). Chạy 30 seed. Pass khi:
1. Boss chết trong **45–120 s** ở ≥ 60 % seed; boss thắng ≤ 30 %; không seed nào chạm 300 s.
2. Damage rải: top-1 gây < 50 % tổng damage ở ≥ 70 % seed (nếu không thì "SLAYER" là ngẫu nhiên vô nghĩa).
3. Không quá 3 racer chết trong 10 s đầu (boss không được là máy xay).
4. Kẹp-tường giết ≤ 30 % số cái chết (nếu hơn: đây là máy ép, không phải boss → giảm tốc/bán kính).
Nếu 1–4 không đạt sau 3 lần chỉnh (tốc độ, HP, số dao), bỏ chế độ.

**Video.** Long: 3 boss liên tiếp HP tăng dần; short: 1 boss 40 HP.

---

## 10. EAT & GROW — nhóm C

**Concept.** Ăn đồ ăn để lớn. Con lớn hơn ≥ 2 bậc chạm con nhỏ thì nuốt. Con to nhất khi hết thức ăn và hết đối thủ thắng.

**Luật.**
- 10 bậc size: cạnh 2 m → 4 m, mỗi bậc +0,2 m, tốc độ −4 %/bậc. Thức ăn 40 miếng, mọc lại 10 s, +1 bậc/miếng.
- Nuốt: chênh ≥ 2 bậc, chạm → con nhỏ chết, con lớn +1 bậc. Chênh < 2 bậc → đẩy nhau bình thường.
- Kết thúc: còn 1 con (sống cuối). Collapse nhẹ từ 40 s.

**Rủi ro thật.** Racer to là mục tiêu to, va tường nhiều, chậm → có thể bị collapse giết trước; hoặc ngược lại con to nhất thắng chắc từ giây 20 (không kịch tính). Cần test.

**Cần code.** `HalfExtent` động + scale visual + rebuild contact grid theo size lớn nhất (~120 dòng); luật nuốt trong contact step; food đã có.

**Chứng minh khả thi.** `DEV_GROW`, 30 seed: pass khi thứ tự size ở giây 20 **không** trùng winner ở ≥ 40 % seed (còn lật kèo), trận 40–120 s.

---

## 11. Bảng chốt và thứ tự làm

| Ưu tiên | Chế độ | Nhóm | Effort | Cửa chứng minh |
|---|---|---|---|---|
| 1 | Infection | A | 1 ngày | test 20 seed |
| 2 | Hot Potato | A | 1 ngày | test 20 seed |
| 3 | Elimination Race | B | 1 ngày | 1 seed full |
| 4 | Lucky Block Battle | A | 2 ngày | test 20 seed |
| 5 | Paint War | B | 2–3 ngày | cân bằng 20 seed |
| 6 | Boss Fight | C | 3–4 ngày | DEV_BOSS 30 seed, 4 tiêu chí |
| 7 | Bumper Sumo | A | 2 ngày | test 20 seed |
| 8 | Grand Prix | B | 0,5 ngày | 1 seed |
| 9 | Team Race | B | 0,5 ngày | 1 seed |
| 10 | Eat & Grow | C | 2 ngày | DEV_GROW 30 seed |

Quy trình chung cho mọi chế độ: **code luật → map DEV → chạy batch seed đo pass/fail → mới vẽ map thật (10 map/chế độ) → quay.** Không vẽ map trước khi qua cửa.


## 12. Kết quả cửa khả thi (ModeLab, 2026-09-04, 20 seed/chế độ, map DEV_OPEN trừ khi ghi khác)

| Chế độ | Kết quả | Chốt |
|---|---|---|
| Infection | 44–65 s, winner rải 10 màu, 0 treo. Cần 2 cú cắn mới lây (tim = thanh nhiễm), ủ 3 s, nghỉ 3 s | **PASS** |
| Hot Potato | 1 tim: 64–88 s, 24–34 lần chuyền, 9–11 vụ nổ, 0 hòa | **PASS** (1 tim) |
| Lucky Block (DEV_LOOT) | 42–71 s, 14 thùng, 1–5 dao rơi, 11/11 mạng bằng dao | **PASS** |
| Paint War | 60 s đúng đồng hồ, top1–top2 chênh 1–20 ô, 1090/1296 ô được tô | **PASS** |
| Team Race (RC02) | 7–32 s, RED 11 – BLUE 9 | **PASS** (nhanh vì map trống) |
| Bumper Sumo (DEV_SUMO) | 2–6 s: cả bầy chạy thẳng xuống vực, bumper không kịp có vai trò | **FAIL → bỏ** (đúng như tiêu chề 1: khối không tự né mép) |
| Elimination Race (M_Elimination) | 12→9→6→4→3→2→1 đúng lịch, mỗi vòng 21–61 s, racer giữ màu/tên qua vòng, card ELIMINATED giữa vòng. Cần map ít chết (sàng 26 map RC: chọn RC19/24/22/18/25/26/15) + slab đuổi chỉ đến muộn (30 s, 1,2 m/s) | **PASS** |
| Grand Prix (M_GrandPrix) | điểm 10-8-6-5-4-3-2-1 dồn qua vòng, hiện trên leaderboard, card STANDINGS sau mỗi vòng | play-test lần 2 đang chạy |

Bài học: ModeLab phải reset `config.mode`/đội/màu mỗi lần vì template là bản clone của lần chạy trước; SD/RC maps có vùng X (1 tim/s) nên đo cân bằng phải dùng map sạch.

Luật runner bổ sung cho đua: vòng kết thúc khi số con còn chạy không thể lấp đủ suất (trước đây chạy tới 300 s); knockout dùng `endRules.eliminateCount`: kết thúc khi số con chưa về đích = suất loại còn lại (con chết đã tính vào suất).
