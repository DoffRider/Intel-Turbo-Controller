# TurboToggle

**TurboToggle** adalah aplikasi Windows untuk mengontrol Intel Turbo Boost secara otomatis dan manual, dengan monitoring sensor CPU/GPU real-time, alert suhu, log CSV, dan fitur hemat listrik.

Dibuat dengan C# (.NET Framework 4.7.2) — ringan, tanpa installer, cukup extract dan jalankan.

![Status](https://img.shields.io/badge/status-stable-brightgreen)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET%20Framework-4.7.2-purple)

---

## ✨ Fitur Utama

### 🌡️ Monitoring Sensor Real-time
- **CPU**: Temp, Freq, Load, Power, Fan, Voltage
- **GPU**: Temp, Freq, Load, Hotspot, Power, VRAM
- **System**: RAM, MB Temp, Network, Disk Activity, Uptime
- Sensor dibaca dari **LibreHardwareMonitor** (akurat, core-level)

### 🎨 Overlay Fleksibel
- Melayang di atas taskbar
- Mode **horizontal** atau **vertical**
- Rata **kiri / tengah / kanan**
- Warna terpisah CPU / GPU / System
- Opacity custom (10-100%)
- Drag untuk posisi

### 📊 Tray Icon Informatif
- Setiap sensor punya ikon tray sendiri
- Format: `65°`, `1.2G`, `45%`, dll.
- Kustomisasi: **font, style, ukuran, margin, outline, gradient**
- Warna CPU / GPU / System terpisah

### ⚡ Kontrol Turbo Boost
- Manual ON/OFF via GUI atau tray menu
- **Auto-Switch**: Turbo OFF saat game/app di list berjalan
- **Auto-Disable Idle**: Turbo OFF saat CPU idle (hemat listrik)
- Prioritas: Auto-Switch > Auto-Disable
- Hotkey global (default `Ctrl+Alt+T`)
- **Temporary Boost**: nyalakan Turbo untuk durasi tertentu

### 🔔 Alert Suhu
- Notifikasi saat suhu CPU/GPU/Hotspot lewat batas
- Custom sound per source (WAV)
- Cooldown mencegah spam

### 📝 Log Sensor CSV
- Rekam semua sensor ke file CSV
- Auto-rotate harian
- Interval custom (0.5-60 detik)
- Buka file langsung dari app

### 🎯 Fitur Tambahan
- Auto-start saat Windows boot
- Auto-minimize ke tray
- Notifikasi toast in-game (borderless fullscreen)
- Profil pengaturan tersimpan otomatis

---

## 📁 Struktur Project

```
Intel-Turbo-Controller/
├── README.md                       # File ini
├── LICENSE                         # Lisensi
├── build.bat                       # Script compile
├── Program.cs                      # Entry point
│
├── Core/                           # Logika inti
│   ├── Config.cs                   # Load/save pengaturan
│   ├── HotkeyManager.cs            # Global hotkey
│   ├── Turbo.cs                    # Wrapper powercfg
│   └── WindowEnumerator.cs         # Enumerate visible windows
│
├── Models/                         # Data model
│   ├── SensorSample.cs             # Container nilai sensor
│   └── WindowInfo.cs               # Info window untuk picker
│
├── Sensors/                        # Pembacaan sensor
│   └── SensorReader.cs             # Baca CPU/GPU/RAM/Network
│
├── UI/                             # Semua form & control
│   ├── MainForm.cs                 # Form utama + helper
│   ├── MainForm.AutoSwitchTab.cs   # Tab Auto-Switch
│   ├── MainForm.OverlayTab.cs      # Tab Overlay
│   ├── MainForm.TrayTab.cs         # Tab Tray Icon
│   ├── MainForm.HotkeyTab.cs       # Tab Hotkey
│   ├── MainForm.BehaviorTab.cs     # Tab Behavior
│   ├── MainForm.AlertTab.cs        # Tab Alert
│   ├── MainForm.LogTab.cs          # Tab Log
│   ├── MainForm.IdleTab.cs         # Tab Idle
│   ├── MainForm.BoostTab.cs        # Tab Boost
│   ├── TrayContext.cs              # ApplicationContext + tray
│   ├── TaskbarOverlay.cs           # Overlay melayang
│   ├── ToastForm.cs                # Notifikasi custom
│   ├── HotkeyCaptureForm.cs        # Dialog set hotkey
│   ├── WindowPickerForm.cs         # Dialog pilih window
│   └── SensorDiagForm.cs           # Dialog diagnostik
│
├── LibreHardwareMonitorLib.dll     # Sensor library
├── System.Memory.dll               # Dependency
├── System.Buffers.dll              # Dependency
├── System.Runtime.CompilerServices.Unsafe.dll
├── HidSharp.dll                    # Dependency
└── System.IO.Ports.dll             # Dependency (opsional)
```

---

## 🚀 Cara Pakai

### Prasyarat
- Windows 10 / 11 (64-bit)
- .NET Framework 4.7.2 (sudah built-in di Windows 10+)
- **Administrator privileges** (untuk kontrol Turbo Boost)

### Cara Build

1. **Clone repo**
   ```cmd
   clone reponya
   ```

2. **Jalankan build.bat**
   ```
   jalankan build.bat
   ```

3. **Output**: `TurboToggle.exe` (~60 KB)

4. **Jalankan**
   - Dobel-klik `TurboToggle.exe`
   - Klik **Yes** pada prompt UAC
   - Ikon akan muncul di system tray

### Cara Pakai Cepat

| Ingin | Cara |
|---|---|
| **Toggle Turbo manual** | Klik kanan tray icon → Turbo ON/OFF |
| **Buka control panel** | Klik kiri tray icon |
| **Boost 30 menit** | Tray → Temporary Boost → 30 menit |
| **Hotkey toggle** | Tekan `Ctrl+Alt+T` |
| **Cek suhu real-time** | Lihat tray icon (angka suhu) |

---

## 🔧 Konfigurasi

Semua pengaturan tersimpan otomatis di:

```
%APPDATA%\TurboToggle\config.ini
```

Kalau ada masalah, hapus file ini untuk reset ke default.

---

## ⚠️ Catatan Penting

- **Antivirus False Positive**: Beberapa antivirus menandai TurboToggle sebagai trojan karena akses low-level hardware. Ini **false positive** — kode tersedia lengkap untuk diverifikasi. Tambahkan folder ke exclusion jika perlu.
- **Exclusive Fullscreen Game**: Toast tidak akan muncul di mode ini (batasan Windows). Gunakan **Borderless Windowed** kalau ingin toast terlihat.
- **Multi-monitor**: Posisi overlay disimpan relatif ke sudut monitor primary.

---

## 🐛 Known Issues

- Windows 11 kadang reset posisi tray icon setelah update
- CPU temp perlu driver kernel (WinRing0) — bisa diblokir antivirus
- Sensor MB Temp tergantung motherboard (beberapa tidak support)
- Beberapa motherboard tidak expose suhu CPU via WMI

---

## 📜 Lisensi

MIT License — bebas dipakai, dimodifikasi, dan didistribusikan.

---

## 🙏 Credits

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — sensor library
- Icons by [Microsoft Segoe UI](https://docs.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font)

---

## 🤝 Kontribusi

Pull request dan issue sangat diterima. Untuk perubahan besar, buka issue dulu untuk diskusi.

## Disclaimer
- *AI Generated* Mohon berhati-hati, selalu periksa kembali
- *AI Generated* Please becareful, always double check