# Audit lớn Square Race (CubeSim): gameplay, đồ họa, quay, ý tưởng, pipeline edit, nhạc (2026-10-07)

Dữ liệu dùng cho audit:
- **Video thật:** 43 Short trong `Recordings/Shorts`, 29 giải Arena với 174 round, đọc từ telemetry.
- **Code:** đọc trực tiếp, có dẫn file:dòng.
- **Bản đồ:** 160 template ASCII.
- **Báo cáo ModeLab:** 25 file.
- **Pipeline tham khảo:** `C:\Projects\unity-clip-kit`.
- **Research ngách:** marble race, Algodoo, weapon-ball, bouncing-ball, cùng chính sách YouTube 2025–26.

Tôi chỉ đọc, chưa sửa gì trong project.

---

## 0. Tóm tắt: 8 lỗi CRITICAL chặn đường tới video chất lượng cao

| # | Lỗi | Bằng chứng | Hậu quả |
|---|---|---|---|
| C1 | **Mọi Short kết bằng ~7.5 s màn hình "WINS" gần như đứng yên** | Đo độ sáng 6/6 Short: tail 7.4–7.6 s (RB09 dài 29 s thì 7.5 s là card). TW14 có 31 s gần như đứng hình | Mất 15–25% thời lượng Short vào đoạn chết. Người xem vuốt đi ngay trước khi video kịp loop. Đây là lỗi retention số 1 |
| C2 | **Frame đầu lộ tên map nội bộ "V_CR03"** + overlay tối "ROUND 1" | `cr03_first.png` | Frame đầu quyết định viewed/swiped. Hiện nó tối, lộ debug, không có câu hỏi hook |
| C3 | **Không có gì tạo kịch tính.** Racer là bi-a thuần: tốc độ cố định, đi thẳng rồi phản xạ, không có chút steering nào | `PlanarMover.cs:51-118`, `PlanarMath.Reflect`. Seed bốc mù `ShortsPlanBuilder.cs:166` (`draw.Next`) | Kết quả ngẫu nhiên, không chọn lọc. Arena: 28% số round không có lần đổi người dẫn nào. 28 round một con giết hết |
| C4 | **Leaderboard Race xếp sai thứ.** Con đang chạy được xếp theo `DistanceTravelled`, mà tốc độ ai cũng bằng nhau | `LeaderboardOverlay.cs:259-264` | Ngôi sao ★ "người dẫn đầu" gần như là racer số 0 hoặc nhiễu. Đổi hạng trên đường đua không hiện được và cũng không đo được |
| C5 | **Camera tĩnh toàn map.** Không follow, không zoom, không finish-cam. Juice (hit-stop, shake, kill-cam) chỉ có ở Arena | `SimulationCamera.cs:14-85`; `SimulationBootstrap.cs:642` ghi rõ "older formats never see any of it" | Trên Short 1080 px mỗi ô chỉ ~22 px. Cube và mắt quá nhỏ trên điện thoại, nửa dưới khung hình trống |
| C6 | **Chung kết Arena hụt hơi** | 22/29 chung kết người thắng còn ≥40 HP. Final thường chỉ 15–20 s. 18/29 giải có round 3 chạm timeout 61 s (Bumper Pit) | Cao trào cuối video, chỗ quan trọng nhất cho retention và loop, lại yếu nhất |
| C7 | **Không có timeline sự kiện theo thời gian video.** Telemetry chỉ có ở Arena, `t` là sim-time | `ArenaTelemetry.cs` chỉ tạo cho `ModeKind.Arena` (`EpisodeDirector.cs:278-282`) | Không làm được pipeline edit tự động (cắt Short, chapter, zoom, nhạc theo nhịp) |
| C8 | **Thời lượng card không ổn định.** Card chờ bằng `WaitForSecondsRealtime`, trong khi Recorder khóa frame | `EpisodeDirector.cs:300-362` | Độ dài card trong video phụ thuộc tốc độ render. Hai lần quay cùng seed có thể cho hai video khác nhau |

Thêm một rủi ro **về kênh** (không phải lỗi code): chính sách "inauthentic content" (15/07/2025) nêu đích danh *"videos where characters are put in the same situation over and over again"*. 133 Short cùng khung 48×26, spawn trái, đích phải chính là kiểu dễ bị coi là mass-produced. Xem mục 5.

---

## 1. Gameplay và kịch tính

### 1.1 Movement là random walk (C3)
- Racer không biết đích ở đâu. "Đua" thực chất là đi ngẫu nhiên, bị bo Chase đẩy dần về đích (`EpisodeDirector.ApplyPressureProfile`).
- Kết quả hoàn toàn do góc xuất phát theo seed (`RacerFactory.cs:232`).
- Thiên lệch theo slot spawn (A1, 20 seed): YELLOW thắng 8, RED 6, GREEN và BLUE mỗi màu 3.

**Fix, xếp theo hiệu quả trên chi phí:**
1. **Seed scouting có điểm drama.** Rẻ nhất, lời nhất, và physics vẫn trung thực.
   - `ModeLab.Run` đã chạy headless nhiều seed. Thêm chấm điểm cho mỗi seed: số lần đổi dẫn, số lần đổi dẫn trong 20% cuối, cách biệt về đích (finish margin), comeback (từ cuối lên nhất), khoảng cách đều giữa các kill, quãng chết dài nhất, độ dài màn 1v1 cuối.
   - Builder lấy top-N seed thay cho `draw.Next`. Sim là deterministic nên seed đã dò chính là video sẽ quay ra.
2. **Progress field cho Race.**
   - Lúc build map, chạy BFS khoảng cách từ ô `G` trên lưới ASCII, ra `Racer.Progress`.
   - Leaderboard sort theo giá trị này (thay dòng 259-261) và bắn event `OnLeadChange`.
3. **Steering nhẹ (tùy chọn).** Mỗi bước lerp heading 2–5% về gradient progress, để vẫn trông như bi-a nhưng có "ý chí". Rubber-band nhẹ: con bét được +3–5% tốc độ.
4. **Escalation trong round.** Ví dụ thành công: Earclacks *Weapon Ball Battles* (2025, vũ khí lớn dần mỗi hit) và bouncing ball (tăng tốc dần).
   - Battle: bo co, cưa tăng tốc, dao dài ra mỗi kill.
   - Race: cổng nhịp `D` đóng mở nhanh dần.

### 1.2 Nhịp độ (dữ liệu thật)
| Mode | Vấn đề |
|---|---|
| Arena | Round trung bình 37 s. First kill trung vị 12.2 s. **23.7% thời gian round nằm trong các quãng >5 s không có hit nào.** Quãng chết dài nhất 41 s |
| Hot Potato | Cứ đúng ~10 s chết một con như máy đếm nhịp (fuse 6 + respawn 2). Round luôn dài đúng 100 hoặc 110 s. Cần random fuse mỗi lượt chuyền |
| Infection | 0 death, trung vị 52 s |
| Knockout RC06 | 240 s, tức là chạm timeout |
| TeamRace RC02 | Có lần chỉ 7.3 s |
| TeamWar (TW14) | 3v3 kẹt trong hình chữ thập nhỏ ~40 s. Slab vàng chiếm 70% màn hình |

**Quy tắc đề xuất:** lấy sự kiện làm mục tiêu thiết kế. Battle cần ≥1 sự kiện mỗi 3–5 s, Race cần ≥1 lần đổi dẫn mỗi 8–10 s. Seed scouting loại seed vi phạm.

### 1.3 Chung kết (C6)
- Final phải là màn dài và căng nhất: 1v1 HP ngang nhau, có arena riêng cho final.
- Seed scouting với ràng buộc: winnerHp ≤ 30, có ≥2 lần đổi dẫn HP.
- Bumper Pit: sửa dead air (đặt thêm vũ khí, bo nhanh hơn) hoặc bỏ khỏi rotation.

---

## 2. Đồ họa và trình bày

Quan sát từ contact sheet (CR03, RB09, TW14, RC13 portrait; Arena T01 landscape):

1. **Cube quá nhỏ trên điện thoại.** Arena portrait chỉ chiếm khoảng 75% chiều rộng và 60% chiều cao. Trên cùng là bảng 12 dòng chữ nhỏ, dưới đáy trống. Cần:
   - Lưới **portrait-native** (ví dụ 14×24 thay vì 48×26 xoay dọc).
   - Camera zoom theo cụm hành động (mục 3).
2. **Palette tối, độ tương phản thấp** ở các format cũ: sàn xám đen, nền đen. Arena (sàn pastel, viền dày) đẹp và đọc tốt hơn hẳn → nên đưa style Arena về mọi format.
3. **Slab bo vàng phát sáng** chiếm phần lớn màn hình ở cuối TeamWar và Rainbow, chói và che hành động. Nên dùng vùng tối hoặc sọc cảnh báo mờ thay cho khối vàng emissive.
4. **Số HP in trên tường đá** (TW: "38 35 35 36…") làm rối hình. Nên chỉ hiện vết nứt, không hiện số.
5. **Màu trùng nhau.** Một round có cả GREEN, OLIVE, LIME, YELLOW, GOLD gần như không phân biệt được. Nên chọn palette có khoảng cách cảm nhận lớn nhất cho mỗi round.
6. **Card "X WINS!"** chỉ là chữ trắng trên lớp dim, không có ăn mừng. Nên làm:
   - Zoom vào winner.
   - Confetti.
   - Tên và chân dung lớn.
   - Hiện 1.0–1.5 s rồi cắt.
7. **HUD Arena** (rule strip, bracket) chữ quá nhỏ ở 1080p, đọc không nổi trên điện thoại. Cỡ chữ tối thiểu nên khoảng 1/30 chiều cao khung.
8. **Safe zone Shorts 1080×1920.** Chữ nên nằm trong khoảng ~250 px từ trên xuống, tránh ~400–670 px dưới đáy và ~200 px bên phải (cột nút Like/Share). Leaderboard đặt ở dải giữa trên.

> Theo quy ước [layout-first], mọi thay đổi về look phải làm mockup 2–3 hướng cho bạn chốt trước rồi mới implement.

---

## 3. Quay video (recording)

| Vấn đề | Fix |
|---|---|
| Card dùng `WaitForSecondsRealtime`, không theo frame (C8) | Chờ theo số frame (`giây × 60`) khi đang quay |
| Kill-cam chỉ đóng băng sim (`runner.Finished` → `Step` return), chỉ VFX còn chạy | Hoãn `Finished` ~1.5 s sim ở timeScale 0.3 để thế giới vẫn chuyển động chậm |
| Không có camera director | Thêm `ActionCamera`: ease vị trí và kích thước ortho về bounding box của cụm nóng (top 3 progress, trận đánh gần, người cầm bom). Có finish-cam khi leader cách G < X ô. Portrait zoom chặt hơn |
| Juice chỉ có ở Arena | Đưa `CombatJuice` ra khỏi `case Arena` (`SimulationBootstrap.cs:642`) cho mọi mode: kill, vỡ cổng, về đích |
| Xuất 1080p60 | Cân nhắc **1440p**: YouTube cấp bitrate VP9/AV1 cao hơn, art phẳng nhiều cạnh sắc nét hơn hẳn |
| Telemetry ghi đè: tên file không có timestamp, cùng scene quay lại là mất | Thêm timestamp giống mp4 |
| Short không có telemetry | Xem mục 4: timeline chung cho mọi mode |

---

## 4. Pipeline edit video (tham khảo unity-clip-kit)

### 4.1 unity-clip-kit dùng lại được gì
clip-kit được làm cho **video tutorial có thuyết minh**: TTS dẫn nhịp, cắt cảnh theo câu nói, con trỏ chuột, Remotion.

**Không hợp với CubeSim:**
- `ClipDirector`/HTTP, `FrameRecorder` (JPEG theo wall-clock, có thể rớt frame), `player.py`. CubeSim quay deterministic, khóa frame, tốt hơn.
- Remotion hard-code layout 1920×1080. Remotion cũng chỉ miễn phí cho cá nhân hoặc team ≤3 người.

**Dùng lại được:**
- `narrate.py`: chuỗi lọc âm và `loudnorm` 2-pass. Thay mốc −16 LUFS bằng **−14 LUFS** cho YouTube.
- `subs.py` (SRT), nếu sau này có voice hoặc caption.
- `_write_video` trong `edit.py` (concat sang H.264) và cơ chế render chia chunk có retry trong `render.mjs`.
- `Cards.tsx`, `lib.tsx` (ramp/beat, font, theme) nếu chọn Remotion cho card và chữ.
- `ClipScreen.cs`: chiếu world→screen, chép sang để ghi rect của racer vào telemetry.

**Thiếu hoàn toàn, phải tự viết:** chọn highlight tự động, auto-zoom/reframe, speed ramp trong segment, lớp nhạc và SFX, transition, layout dọc.

### 4.2 Kiến trúc đề xuất: "Unity làm đúng, Python làm nhanh"
Overlay quan trọng (leaderboard, HP, card, kill feed) **render ngay trong Unity** vì sim deterministic. Python chỉ cắt, ghép, reframe, nhạc, master.

```
Unity (EpisodeDirector + EpisodeTimeline)
  ├─ <scene>_<stamp>.mp4      1440p60, khóa frame
  ├─ <scene>_<stamp>.wav      game SFX (OfflineAudioTrack)
  └─ <scene>_<stamp>.timeline.json   ← MỚI, mọi mode
        events: {frame, t_video, round, kind, who, by, cause, alive_left, rank_changes, rect[x0,y0,x1,y1]}
        kinds : intro, round_card, round_start, countdown_go, hit, kill, elim, lead_change,
                gate_break, goal/placement, final_n(3,2), killcam, winner_card, podium, bracket
        samples: vị trí và progress mỗi 10 frame (để reframe và vẽ biểu đồ)

Python  tools/edit/  (thư mục mới, viết mới theo khung clip-kit)
  1. score.py      chấm "heat" theo cửa sổ trượt 1 s: kill=5, elim=5, lead_change=4,
                   final_n=6, hit=1, gate_break=2; chuẩn hóa → đường cong drama
  2. long.py       ghép bản dài:
                     - quãng heat≈0 dài >5 s → tua 3–4x, góc màn hình hiện "x4"
                     - chapter = round_start (ffmetadata + text mô tả)
                     - giữa các round có 1–2 s chuyển cảnh yên → chỗ đặt mid-roll tự nhiên (YouTube auto mid-roll từ 05/2025)
                     - mở đầu bằng "cold open": 3–5 s cảnh căng nhất của trận final (teaser), rồi mới vào giải
  3. shorts.py     tự cắt Short từ bản dài hoặc từ lần quay portrait:
                     - chọn cửa sổ 20–45 s có tổng heat cao nhất và kết thúc tại winner frame
                     - reframe 9:16 theo rect của cụm nóng (crop mượt, ease)
                     - frame 0: đã có chuyển động + câu hỏi lớn ("WHICH COLOR SURVIVES?")
                     - kết thúc đúng frame thắng + 0.8 s → loop về frame 0 (không còn tail 7.5 s)
  4. music.py      nhạc theo trạng thái (mục 6), ducking dưới SFX quan trọng, crossfade theo beat
  5. master.py     ghép game WAV + nhạc → loudnorm −14 LUFS / −1 dBTP → mux; kèm thumbnail pick
                   (frame heat cao nhất + winner) và description (chapter + credit nhạc NCS)
  6. qc.py         freezedetect (fail nếu quãng tĩnh >1.5 s), đo độ dài, kiểm tra safe zone, kiểm tra peak
```

**Thứ tự build:**
1. `EpisodeTimeline` trong Unity (thay `ArenaTelemetry`, gắn cho mọi mode, stamp `Time.frameCount - startFrame`; `OfflineAudioTrack` đã có frame counter).
2. `qc.py` + `shorts.py`, vì chúng sửa thẳng C1 và C2.
3. `long.py`.
4. `music.py`.

---

## 5. Ý tưởng và hướng mở rộng nội dung (từ research)

**Benchmark** (số liệu gần đúng từ tracker bên thứ ba):

| Kênh | Số liệu | Mô hình |
|---|---|---|
| Jelle's Marble Runs | ~1.5M sub | League có 32 đội có tên và tính cách, season, thăng hạng, xuống hạng, bình luận viên. Dù vậy doanh thu AdSense chỉ khoảng $82k/năm, phần lớn tiền đến từ sponsor |
| Marble Factory (Algodoo) | "216 countries & 215 eliminations" **15.6M view, 33.9k comment** | Video khác của cùng kênh chỉ ~300k, tức là biên độ cực lớn |
| Carson Jay Marbles | "Italian Brainrot elimination" 237k. Video lệch trend cùng kênh chỉ 1.5–9.5k | Bám trend giúp **gấp ~20 lần** |
| AI Warehouse (Unity + ML-Agents) | 829k sub với **21 video**, ~2.4M view/video | Ít video, concept mạnh, có nhân vật xuyên suốt (Albert) |
| Earclacks Weapon Ball Battles (2025) | Viral, có fandom và fan art | Nhân vật archetype đơn giản + vũ khí scale theo hit + bracket |

**Đề xuất cho CubeSim:**
1. **Dàn nhân vật cố định (cast).** 16–24 eye-cube có tên, tính cách, màu riêng không trùng, có hồ sơ W-L và streak lưu JSON qua các tập. Đây vừa là móc để người xem cổ vũ, vừa là "series following a set of characters", điều chính sách YouTube **cho phép** rõ ràng.
2. **Season / League.** Vòng loại → vòng bảng → knockout → final → bảng tổng → teaser tập sau. Hạ tầng bracket của Arena đã có sẵn.
3. **Số lớn trong tiêu đề.** "100 Cubes, 99 Eliminations". Cần mode đông racer (Elimination 100 con, mỗi vài giây loại một con) vì đây là format ăn view nhất của ngách.
4. **Biến thể theo trend.** Country cubes (cờ trên mặt), meme cast theo mùa. Tách thành series riêng, không trộn vào cast chính.
5. **Escalation mechanic** cho Battle: vũ khí lớn hoặc nhanh dần mỗi kill (Earclacks), bo co tăng tốc.
6. **Map:** 60/62 map RC/RB/RM đều spawn trái, đích phải, và 150/160 map dùng cùng lưới 48×26. Nên:
   - Đổi vị trí goal (góc, tâm), dùng lưới dọc riêng cho Short.
   - Làm các ý tưởng đã có trong `GAMEPLAY_AUDIT.md` §2: đường tắt qua `X`, cổng nhịp `D`, kính `N` cuối.
7. **Kỹ thuật mở rộng.** Thêm mode hiện phải sửa 8 file switch `ModeKind`. Nên có interface `IModeSystem` (Step/OnContact/RuleLine/WinCheck) trước khi làm thêm mode.

**Tuân thủ chính sách (để giữ monetization cả kênh):**
- Mỗi video một map hoặc rule khác nhau thật sự.
- Có lớp kể chuyện: cast, rivalry, caption, recap.
- Tiêu đề và mô tả không theo một template cứng.
- Không đăng dồn hàng loạt Short gần như giống hệt nhau.

---

## 6. Nhạc

**Hiện trạng:**
- Mode BGM Pool: ~83 track trong `Assets/BGM_LOOP`. **Cần xác minh nguồn và license từng track trước khi bật kiếm tiền.**
- Collision-melody: 20 bài cổ điển và dân ca, đều public domain.

**Sự thật về license** (kèm nguồn trong report research):

| Nguồn | Kết luận |
|---|---|
| **YouTube Audio Library** | An toàn nhất, không bị claim |
| **NCS** | Dùng được để kiếm tiền nếu **ghi credit trong description**. Kênh Algodoo lớn dùng nhiều (Max Brhon, Rob Gasser…) |
| **Epidemic / Artlist** | Trả phí, video đăng trong thời gian còn subscription thì clear vĩnh viễn |
| **Pixabay** | Miễn phí nhưng một số tác giả đăng ký Content ID. Phải giữ license để dispute |
| **StreamBeats** | Chỉ dành cho stream, **không** dùng cho video kiếm tiền |
| **Phonk thật** | Gần như chắc bị claim. Tránh |

- **Luật Shorts:** Short dài >60 s mà dính bất kỳ claim Content ID nào sẽ bị **chặn toàn cầu**. Short có nhạc claim cũng bị chia doanh thu (1 bài còn 50%, 2 bài còn 33%).
- **Giai điệu public domain chơi bằng MIDI** vẫn từng bị claim tự động (ví dụ AdRev claim "Flight of the Bumblebee"). Rủi ro thấp nhưng không phải bằng 0.

**Đề xuất theo format:**

| Phần | Nhạc |
|---|---|
| Short | **Một lựa chọn duy nhất**: hoặc collision-melody (giai điệu PD, giọng pluck cao, rất hợp trend "bouncing ball plays a song"), hoặc 1 track Audio Library hoặc NCS. Không trộn cả hai. Giữ Short ≤60 s, hoặc chỉ vượt 60 s khi audio 100% tự làm |
| Long, phần mở | Hype ngắn 3–5 s (riser + hit) trên cold open |
| Long, round thường | Electronic mid-tempo 120–128 BPM, bed nhẹ (Audio Library hoặc NCS), duck −6 dB khi có kill hoặc gate |
| Long, khi ≤3 con hoặc final | Đổi sang track căng (trống trầm, riser lên dần) |
| Khoảnh khắc thắng | **Im lặng 0.4–0.6 s** rồi victory sting. Tương phản tạo cao trào |
| Bracket, standings | Loop nhẹ. Đây cũng là chỗ YouTube đặt mid-roll |

Hệ thống cần thêm: `music.py` chọn track theo state từ timeline (intro / round / final_n / winner / bracket), crossfade đúng beat, và ghi credit vào description.

---

## 7. Shorts: công thức retention

- **Độ dài:** 20–45 s (completion ~70–90%). Hiện tại 21–85 s, đa số 45–68 s, nhiều Short đang quá dài.
- **0–1 s:** đã có chuyển động, cả dàn hiện rõ, chữ lớn hỏi một câu ("WHICH COLOR WINS?"). Không có card tối, không tên map.
- **Mỗi 3–5 s một sự kiện:** loại, đổi dẫn, vỡ cổng, kèm toast "RED OUT · 5 LEFT".
- **Cuối:** slow-mo 1v1 hoặc photo finish → winner 0.8–1.2 s → **cắt** để loop (mỗi lần replay đều tính view từ 31/03/2025).
- **Phễu:** dùng "Related video" trỏ về bản dài cùng giải. Short là kênh khám phá. RPM Short chỉ bằng khoảng 3–14% long-form, nên tiền nằm ở video dài.

## 8. Long-form: cấu trúc 10–20 phút

```
0:00  Cold open: 4 s cảnh căng nhất trận final + "Only one survives." 
0:05  Giới thiệu cast (thẻ tên, record W-L, "fan favourite", "underdog") — ≤20 s
0:25  Vòng 1..N  (mỗi vòng: rule card 1.5 s → round → kill feed / toasts → standings 2 s)
~50%  Twist giữa video (luật đặc biệt, map lạ, hồi sinh 1 con) để giữ đường retention
cuối  Final 1v1 trên arena riêng, nhạc final, slow-mo, im lặng, sting
      Podium + bảng season + "Next episode: …" (end screen 10–20 s)
```
- Chapter theo round, chapter đầu bắt buộc bắt đầu ở 00:00.
- Tiêu đề kiểu: "16 Cubes, 1 Winner — Knife Tournament EP.3" hoặc "100 Cubes Elimination Race #1".

---

## 9. Lộ trình fix đề xuất

| Đợt | Việc | Sửa lỗi |
|---|---|---|
| **P0-a** (1 ngày) | Card chờ theo frame. Bỏ tên map. Card thắng ≤1.5 s. Short cắt đúng frame thắng. `qc.py` freezedetect | C1 C2 C8 |
| **P0-b** (1–2 ngày) | `EpisodeTimeline` cho mọi mode (frame-stamped, có rect) + progress field + leaderboard đúng + event lead_change | C4 C7 |
| **P0-c** (1–2 ngày) | Seed scouting có điểm drama trong ModeLab. Builder chọn top seed. Ràng buộc riêng cho final | C3 C6 |
| **P1** (2–3 ngày) | ActionCamera + juice cho mọi mode + kill-cam slow-mo thật. HUD: 3-2-1-GO, kill feed, "N LEFT", "FINAL TWO" (mockup trước) | C5 |
| **P1** | Pipeline Python: shorts.py → long.py → music.py → master.py | — |
| **P2** | Cast có tên + season JSON. Lưới portrait-native. Đa dạng vị trí goal. Palette khác biệt. Xuất 1440p | identity, map |
| **P2** | Refactor `IModeSystem`. Mode 100-cube Elimination. Escalation vũ khí | mở rộng |

Sau P0 nên quay lại 3–5 Short và chấm bằng `qc.py` + đường heat, rồi mới quay hàng loạt. 133 Short còn lại hiện chưa nên quay, vì chúng sẽ dính toàn bộ C1–C5.
