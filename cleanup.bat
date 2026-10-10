@echo off
setlocal enabledelayedexpansion

REM Pindah ke folder script
cd /d "%~dp0"

echo ============================================================
echo  TurboToggle Cleanup Script
echo ============================================================
echo.

REM Pastikan folder _TRASH ada
if not exist "_TRASH" (
    mkdir "_TRASH"
    echo [OK] Folder _TRASH dibuat.
) else (
    echo [OK] Folder _TRASH sudah ada.
)
echo.

REM ============================================================
REM FILE YANG AKAN DIPINDAH
REM ============================================================
set FILES_TO_MOVE=Aga.Controls.dll Aga.Controls.pdb BlackSharp.Core.dll DiskInfoToolkit.dll LibreHardwareMonitor.exe LibreHardwareMonitor.exe.config LibreHardwareMonitorLib.pdb LibreHardwareMonitorLib.xml Microsoft.Bcl.AsyncInterfaces.dll Microsoft.Bcl.HashCode.dll Microsoft.Win32.TaskScheduler.dll OxyPlot.dll OxyPlot.WindowsForms.dll RAMSPDToolkit-NDD.dll System.CodeDom.dll System.Collections.Immutable.dll System.Formats.Nrbf.dll System.IO.Pipelines.dll System.Numerics.Vectors.dll System.Reflection.Metadata.dll System.Resources.Extensions.dll System.Security.AccessControl.dll System.Security.Principal.Windows.dll System.Text.Encodings.Web.dll System.Text.Json.dll System.Threading.AccessControl.dll System.Threading.Tasks.Extensions.dll

set MOVED_COUNT=0
set NOT_FOUND_COUNT=0

echo ------------------------------------------------------------
echo  Memindahkan file tidak wajib ke _TRASH\
echo ------------------------------------------------------------

for %%F in (%FILES_TO_MOVE%) do (
    if exist "%%F" (
        move /Y "%%F" "_TRASH\" >nul
        if !errorlevel! equ 0 (
            echo   [MOVED] %%F
            set /a MOVED_COUNT+=1
        ) else (
            echo   [FAIL ] %%F - gagal dipindah
        )
    ) else (
        set /a NOT_FOUND_COUNT+=1
    )
)

echo.
echo ------------------------------------------------------------
echo  Selesai.
echo   - File dipindah : !MOVED_COUNT!
echo   - File tidak ada: !NOT_FOUND_COUNT!
echo ------------------------------------------------------------
echo.
echo File yang DIPERTAHANKAN di root:
echo   - build.bat
echo   - Program.cs
echo   - README.md
echo   - LibreHardwareMonitorLib.dll
echo   - HidSharp.dll
echo   - System.Memory.dll
echo   - System.Buffers.dll
echo   - System.Runtime.CompilerServices.Unsafe.dll
echo.
echo Langkah selanjutnya:
echo   1. Jalankan build.bat untuk test compile
echo   2. Test TurboToggle.exe - cek semua sensor terbaca
echo   3. Kalau error, cek log untuk tahu file mana yang perlu dikembalikan
echo      File backup ada di folder _TRASH\, tinggal 'move' balik ke root.
echo.

pause