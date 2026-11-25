# Racing Game 3D (Blender + Unity)

Game balapan 3D: mobil dibuat dengan script Python di Blender, lalu dimainkan di Unity (balapan 3 lap lawan 3 mobil AI).

Target: **Unity 2022.3 LTS**, Built-in Render Pipeline (template "3D"). Juga jalan di Unity 6 dan URP (material otomatis menyesuaikan).

## Cara menjalankan (5 langkah)

1. Unity Hub -> **Add -> Add project from disk** -> pilih folder `RacingGame` ini. Pilih Unity 2022.3.x yang terpasang.
2. Tunggu import selesai. Akan muncul dialog **"Jalankan setup otomatis?"** -> klik **Ya**.
   Kalau dialog tidak muncul: menu **Racing -> Setup Everything (1 klik)**.
3. Setup akan membuat material, prefab mobil, dan scene `Assets/Scenes/Race.unity`.
4. Kalau muncul pesan "Input diubah ke Both" -> restart Unity Editor sekali.
5. Buka `Assets/Scenes/Race.unity` -> tekan **Play**.

## Kontrol

| Tombol | Fungsi |
|---|---|
| W / S (atau panah) | Gas / rem + mundur |
| A / D (atau panah) | Belok |
| Spasi | Rem tangan (drift) |
| R | Reset ke checkpoint terakhir |
| C | Ganti kamera (dekat / jauh / kap mesin) |
| Esc | Pause |
| Enter | Balapan lagi (setelah finis) |

## Isi project

```
BlenderScripts/
  build_assets.py     Script Blender: membuat Car, Cone, Barrier, Tree, StartGate -> FBX
  make_textures.py    Script Python: membuat tekstur aspal, rumput, curb, garis finis
Assets/
  Models/             FBX hasil Blender (+ Car_preview.png)
  Textures/           PNG tekstur
  Scripts/            Script game (C#)
  Editor/RacingSetup.cs   Setup 1 klik
```

### Script C# (Assets/Scripts)

| Script | Fungsi |
|---|---|
| `CarController` | Fisika mobil: WheelCollider, torsi, rem, kemudi sesuai kecepatan, anti-roll, downforce, grip rumput, rem tangan, gigi & RPM |
| `AICarDriver` | AI mengikuti garis lintasan, mengerem sebelum tikungan, reset otomatis jika tersangkut |
| `TrackBuilder` | Membuat lintasan dari spline (jalan, curb, rumput, garis finis, checkpoint, pembatas, kerucut, pohon) |
| `LapManager` | Hitung mundur, checkpoint berurutan, hitung lap, ranking, rekor lap (PlayerPrefs) |
| `RacerProgress`, `Checkpoint` | Data progres balapan dan trigger checkpoint |
| `CameraFollow` | Kamera mengikuti mobil, FOV melebar saat ngebut |
| `EngineSound` | Suara mesin sintetis (tanpa file audio) |
| `RaceHUD` | Tampilan kecepatan, gigi, RPM, lap, posisi, waktu, klasemen, hasil akhir |

## Mengubah / membuat ulang aset di Blender

1. Buka Blender (3.6 LTS atau 4.x) -> tab **Scripting** -> Open `BlenderScripts/build_assets.py` -> **Run Script**.
2. FBX baru otomatis tertimpa di `Assets/Models/`. Lewat terminal: `blender -b -P build_assets.py -- --out ../Assets/Models`.
3. Di Unity jalankan lagi **Racing -> Setup Everything**.

Bagian yang gampang dimodifikasi di `build_assets.py` (fungsi `build_car`):
- Warna cat: `make_material("Car_Paint", (r, g, b), ...)`
- Ukuran mobil: `L, W`, `wheel_r`, `wheelbase`, `track`
- Spoiler, bumper, lampu: blok kode berlabel komentar masing-masing

Catatan: depan mobil menghadap **-Y** di Blender (= +Z di Unity). Nama roda `Wheel_FL/FR/RL/RR` jangan diubah karena dipakai script setup.

## Mengubah lintasan dan pengaturan balapan

- Bentuk lintasan: pilih objek **Track** di Hierarchy -> ubah `Control Points` (x, z dalam meter) -> klik kanan komponen **Track Builder** -> **Build Track**. Jaga jarak antar titik >= 50 m dan hindari tikungan terlalu tajam (radius < 20 m).
- Jumlah lap: objek **RaceManager** -> `Total Laps`.
- Tingkat kesulitan AI: pilih prefab/instans mobil AI -> `AI Car Driver -> Skill` (0.6 mudah ... 1.05 sulit).
- Performa mobil: `Car Controller` (torsi, top speed, grip, dll.).

## Troubleshooting

- **Keyboard tidak merespons**: Project Settings -> Player -> Active Input Handling = *Both* (atau *Input Manager (Old)*), lalu restart editor.
- **Material pink**: render pipeline bukan Built-in/URP. Jalankan ulang Setup Everything.
- **Mobil menghadap terbalik / roda tidak sinkron**: jalankan ulang Setup Everything (orientasi dideteksi otomatis dari lampu depan).
- **Mobil terlalu licin / terlalu kaku**: atur `Stiffness` di WheelCollider (Forward/Sideways Friction) pada prefab `PlayerCar`.
