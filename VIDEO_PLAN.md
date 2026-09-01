# CubeSim — Pet Survival: kế hoạch 8 video đầu

Mỗi video ≈ 10 phút: 6 vòng × (~90–130s + card chuyển vòng). Mở scene tương ứng rồi chạy
`CubeSim → Record Episode (Play + Capture)` — video tự chạy, tự dừng sau podium, file MP4
1080p60 nằm trong `Recordings/`. Cùng scene = cùng video từng frame (seed cố định).

Nhạc: mỗi lần play bốc ngẫu nhiên 1 bài trong songbook (8 bản public domain: Mountain King,
Für Elise, Turkish March, Gran Vals/Nokia, Korobeiniki/Tetris, Ode to Joy, Carmen, William Tell)
— va chạm chơi nốt kế tiếp, kill = hợp âm thứ, goal = hợp âm trưởng, trần nhịp 7 nốt/giây.
Nếu muốn cố định bài cho một video: sửa `MelodySongs.Pick` seed trong `SimAudioSystem`.

| Video | Scene | Vòng (map — thể thức) |
|---|---|---|
| 1 | CubeSimulation_Video01 | Comb01 sinh tồn · Chamber01 đua goal · Garden01 sinh tồn · Rainbow01 đua · Mega01 đua · Open01 sinh tồn |
| 2 | CubeSimulation_Video02 | Comb02 · Rooms01 · Garden02 · Rainbow02 · Chamber02 · Gauntlet01 |
| 3 | CubeSimulation_Video03 | Open02 · Chamber03 · Rainbow03 · Garden03 · Mega02 · Comb03 |
| 4 | CubeSimulation_Video04 | Rooms02 · Gauntlet02 · Garden04 · Chamber04 · Rainbow04 · Open03 |
| 5 | CubeSimulation_Video05 | Comb04 · Mega03 · Rooms03 · Garden05 · Chamber05 · Gauntlet03 |
| 6 | CubeSimulation_Video06 | Open04 · Rainbow05 · Rooms04 · Mega04 · Comb05 · Chamber06 |
| 7 | CubeSimulation_Video07 | Gauntlet04 · Garden01 · Rainbow06 · Rooms05 · Mega05 · Open05 |
| 8 | CubeSimulation_Video08 | BlockBreak · Arena5v5 · Gauntlet05 · Chamber01 · Comb01 · Mega01 (tập "remix") |

Bảy họ map (44 arena, tất cả sinh từ ASCII template trong `Assets/CubeSim/Arenas/Templates/`,
được flood-fill + silhouette check tự động khi build):

- **Comb** — lược chữ cái của video 5v5 gốc, các nhịp khe khác nhau.
- **Chamber** — buồng goal kín, cửa breakable có đếm ngược.
- **Mega** — bức tường HP 400 chắn goal, cả bầy mài (format 4.1M views).
- **Rainbow** — cổng nhiều lớp màu, gặm từng lớp.
- **Rooms** — chuỗi vách ngăn breakable zigzag.
- **Garden** — vườn thức ăn cho Pet Survival (điểm ăn hiện trên leaderboard).
- **Open/Gauntlet** — đấu trường mở & mê cung răng cưa.

Racer: 10 pet Kenney (heo, mèo, chó, thỏ, cánh cụt, gấu trúc, hổ, sư tử, gà con, bò, koala,
cáo) — chia round-robin, tint theo màu đội, speed 10.

Nhớ dán credit trong description mọi video còn dùng track nền:
"Wholesome" Kevin MacLeod (incompetech.com) — CC BY 4.0 (xem Assets/CubeSim/Audio/Music/CREDITS.txt).
