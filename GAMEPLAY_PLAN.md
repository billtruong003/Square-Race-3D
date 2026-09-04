# Kế hoạch gameplay & bộ map — 2026-09-03

Mục tiêu: có **một bộ map "hẳn hoi"** — mỗi map là một câu chuyện có kịch tính, không phải cột chắn xếp đều —
và biết trước gameplay nào có thể mở rộng với resource đang có. Plan trước, vẽ layout trên web, rồi mới sửa scene.

---

## A. Nguyên tắc thiết kế map
1. **3 hồi**: mở màn (spawn / chuồng) → giữa (chướng ngại, điểm quyết định) → kết (chốt cuối / đích / hố tử thần).
2. Mỗi map có **≥ 1 "drama device"** — thứ làm người xem đoán sai: cửa nhịp, kính đập chung, đường tắt độc, lưỡi cưa chạy ray, cổng khóa cần chìa.
3. **Không có lối vô nghĩa**: mọi nhánh phải rẻ hơn hoặc nguy hiểm hơn nhánh kia (đánh đổi), không có nhánh "giống hệt".
4. Round tự kết thúc trong 60–130s nhờ bo (Chase/Collapse) + device, không bao giờ trôi tới hết giờ.
5. Rotor là **rotor** (chong chóng đẩy, xoay tại chỗ). **Saw blade** là object khác: cưa tròn chém 1 tim, quay tại chỗ hoặc chạy theo ray.

---

## B. Kho object

| Object | Marker | Trạng thái | Asset |
|---|---|---|---|
| Tường | `#` | có | cube toon |
| Đá đục được / mega | `B` `M` | có (tảng 2×2, 3 hit / 60 hit) | KenneyDungeon/rocks.fbx |
| Kính màu / kính trắng | `C` `N` | có (2 hit đúng màu / 10 hit) | StylizedGlass |
| Rotor (đẩy) | `O` | có | cube xám |
| Cửa nhịp | `D` | có | cube trượt xuống sàn |
| Sàn độc | `X` | có (0.5 tim/s) | decal đỏ + băng |
| Thức ăn | `F` | có (12 model) | KenneyDungeon/FBX format 1 |
| Dao | `W` | có (6m, chỉ dao bếp) | cooking-knife |
| Bo ép | — | có (Chase / Collapse / Corridor) | slab vàng |
| Goal / spawn | `G` `L` `R` | có | pad xanh |
| **Saw blade** | `S` | **XONG 03/09** — ô vuông quay tại chỗ, dải dài = ray 5 m/s, chém 1 tim | Assets/SawBlade/model_0.prefab |
| **Bẫy gai nhịp** | `T` | **XONG** — nghỉ 2.4s / báo 0.6s / lên 1.4s, 1 tim | KenneyDungeon/trap.fbx |
| **Cổng khóa + chìa** | `K` `k` | **XONG** — ai chạm chìa, mọi cổng hạ | KenneyDungeon/gate.fbx + key.fbx |
| **Máy ép** | `P` | **XONG** — khối trượt hết dải P, kẹp vào tường = chết | khối thép đỏ |
| **Bumper** | `U` | **XONG** — văng ×2 tốc độ 0.8s | KenneyDungeon/barrel.fbx |
| **Coin** | `$` | **XONG** — win MostCoins, cột $ trên leaderboard | KenneyDungeon/coin.fbx |
| **Potion** | `+` | **XONG** — hồi 1 tim, mọc lại 20s | KenneyDungeon/potion.fbx |

**VFX có sẵn chưa dùng (Epic Toon FX)**: Explosions (mega vỡ), Fire (sàn độc cháy), Lightning (sét đánh ngẫu nhiên — device mới rẻ tiền), Giblets (chết vì cưa), Decals (vết máu bền), Confetti (đích).

**Bộ đồ ăn Kenney (200 model)**: dùng được ngay làm (1) food pickup — hiện 12, có thể lên 30–40 theo chủ đề map (map "bếp": dao, chảo, thớt; map "chợ": rau củ); (2) trang trí theme cho từng bộ map (cutting-board làm sàn khu vực, pan/pot làm cột trang trí không collider). Không cần mua thêm model.

**Không dùng**: POLYGON weapons (súng — đã chốt chỉ dao), Monsters pack (để dành cho "boss" sau).

---

Thêm (xong 03/09): băng chuyền `> < ^ v`, teleporter `1 2`, chuồng trên/dưới `A Z` (4 phe).

## C. Gameplay có thể mở rộng (từ 3 lõi × object)

| # | Gameplay | Lõi | Object cần | Code thêm | Độ hấp dẫn |
|---|---|---|---|---|---|
| 1 | **Gauntlet** — chạy về đích qua cưa chạy ray, bẫy gai, máy ép | Race | S, T, P | S/T/P object | cao (Simulation Central kiểu "obstacle course") |
| 2 | **Key Race** — ăn chìa mở cổng, cổng cuối cần 2 chìa | Race | K, k | K/k | cao (đảo ngược liên tục) |
| 3 | **Coin Rush** — 90s, ai ăn nhiều coin nhất; coin mọc lại theo nhịp | mới: MostScore | $ | win condition + score UI | cao, rất "channel" |
| 4 | **Blood Pit** — Solo, sàn độc vành + cưa tâm + Collapse | Solo | S, X | S | trung bình–cao |
| 5 | **Siege** — Team, tường giữa bằng đá, đục để tấn công, potion hồi máu | Team | B, + | + | trung bình |
| 6 | **Last Cube on Platform** — Collapse nhanh + bumper văng | Solo | U | U | trung bình |
| 7 | **King of the Hill** — giữ vùng tâm 20s | mới | region | timer + UI | trung bình, code nhiều |
| 8 | Boss (monsters) | — | — | nhiều | để sau |

**Đề xuất làm trước**: 1 (Gauntlet) và 3 (Coin Rush) — 2 gameplay mới hẳn với ít code nhất, tận dụng SawBlade + Kenney coin. Sau đó 2 (Key Race).

---

## D. Bộ map "hẳn hoi" — khung 10 map/gameplay (tên + device + 3 hồi)

### Race (đích)
| Map | Hồi 1 | Hồi 2 (device) | Hồi 3 |
|---|---|---|---|
| R1 Cửa Nhịp | chuồng | 2 cửa nhịp lệch pha — đàn dồn chờ | goal nhỏ góc trên |
| R2 Đường Tắt Độc | chuồng | lối ngắn qua sàn độc vs lối vòng | goal giữa |
| R3 Kính Chung | chuồng | 2 lớp kính màu so le nửa lane | kính trắng 10 hit ngay trước goal |
| R4 Mỏ Vàng | chuồng | mỏ đá 6 ô + mạch quặng 1 hit | goal góc dưới |
| R5 Cưa Ray | chuồng | 3 cưa chạy ray ngang qua hành lang | goal giữa |
| R6 Chong Chóng | chuồng | 2 rotor lớn ở 2 chốt hẹp | goal góc |
| R7 Chìa Khóa | chuồng | 2 chìa 2 góc, cổng giữa | goal sau cổng |
| R8 Máy Ép | chuồng | hành lang 3 máy ép lệch pha | goal |
| R9 Domino | chuồng | cột đá 1 hit mở nhánh dần | goal |
| R10 Hỗn Hợp | chuồng | kính + đá + cưa tâm | goal nhỏ |

### Solo Battle (1 sống sót)
| Map | Device |
|---|---|
| S1 Hố Máu | sàn độc vành + Collapse |
| S2 Cưa Tâm | cưa quay tại chỗ giữa sân, dao 4 góc |
| S3 Chong Chóng Đôi | 2 rotor ngược chiều, khe hẹp giữa |
| S4 Bốn Phòng | 4 phòng nối bằng cửa nhịp, Collapse |
| S5 Máy Ép Hành Lang | 2 hành lang ép, sân giữa nhỏ |
| S6 Gai Nhịp | sàn gai bật/tắt theo ô cờ |
| S7 Bumper Pit | bumper vành, Collapse nhanh |
| S8 Sudden 1 tim + dao 6 | sân mở, cột phục kích |
| S9 Kính Vỡ | vách kính chia sân, phải đập mới gặp nhau |
| S10 Đá Đục | khối đá tâm giấu dao |

### Team Battle (10v10)
| Map | Device |
|---|---|
| T1 Chuồng Cửa | (hiện tại) |
| T2 Tường Đá Giữa | đục tường để tấn công |
| T3 Cưa Biên | cưa chạy ray dọc 2 biên |
| T4 Potion | 2 potion hồi máu ở tâm |
| T5 Ba Cửa | 3 cửa nhịp mở lệch → đánh từng đợt |
| T6 Sàn Độc Tâm | tâm độc, phải đánh ở biên |
| T7 Chìa Khóa | chìa mở cửa chuồng đối phương |
| T8 Rotor Tâm | rotor lớn giữa sân |

---

## E. Quy trình
1. **Chốt plan này** (gameplay nào làm trước, object nào code trước).
2. **Web layout editor** (tôi build, 1 artifact HTML): lưới 48×26, palette marker (kể cả S/T/K/P/U/$/+), vẽ bằng chuột, kiểm tra flood-fill + hành lang ≥ 2 ô, xem silhouette, export ASCII → dán vào `Assets/CubeSim/Arenas/Templates`. Vẽ đủ bộ map trên web trước.
3. **Code object mới** theo thứ tự: Saw blade (S) → Trap (T) → Coin/MostScore ($) → Key/Gate (K) → Crusher (P) → Bumper (U) → Potion (+). Mỗi object: builder marker + runtime system + VFX/âm thanh + dòng rulebook.
4. **Build + play test theo checklist**: round 60–130s, không round nào hết giờ, tối thiểu 1 pha device xảy ra mỗi round, không racer kẹt.
5. **Quay** batch + monitor như hiện tại.

## F. Resource cần thêm
- Model: **không cần mua** (SawBlade, Kenney trap/gate/key/coin/chest/potion/barrel có sẵn).
- Âm thanh cần tìm: saw buzz loop, trap clank, coin ding, gate open, crusher thud, bumper boing (6 file ngắn, free).
- Font/UI: score counter cho Coin Rush (dùng leaderboard hiện có, thêm cột).
