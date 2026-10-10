@echo off
setlocal
cd /d "%~dp0"

echo Current dir: %CD%
echo.

if not exist "Program.cs" (
    echo [ERROR] Program.cs tidak ditemukan.
    pause
    exit /b 1
)

if not exist "LibreHardwareMonitorLib.dll" (
    echo [ERROR] LibreHardwareMonitorLib.dll tidak ditemukan.
    pause
    exit /b 1
)

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] csc.exe tidak ditemukan. .NET Framework 4.x mungkin belum aktif.
    pause
    exit /b 1
)

echo Compiling TurboToggle.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
    /out:TurboToggle.exe ^
    /r:System.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    /r:System.Management.dll ^
    /r:System.Net.NetworkInformation.dll ^
    /r:LibreHardwareMonitorLib.dll ^
    /r:System.Memory.dll ^
    /r:System.Buffers.dll ^
    /r:System.Runtime.CompilerServices.Unsafe.dll ^
    /r:HidSharp.dll ^
    Program.cs ^
    Core\HotkeyManager.cs ^
    Core\Turbo.cs ^
    Core\Config.cs ^
    Core\WindowEnumerator.cs ^
    Models\WindowInfo.cs ^
    Models\SensorSample.cs ^
    Sensors\SensorReader.cs ^
    UI\TaskbarOverlay.cs ^
    UI\HotkeyCaptureForm.cs ^
    UI\ToastForm.cs ^
    UI\WindowPickerForm.cs ^
    UI\SensorDiagForm.cs ^
    UI\TrayContext.cs ^
    UI\MainForm.cs ^
    UI\MainForm.AutoSwitchTab.cs ^
    UI\MainForm.OverlayTab.cs ^
    UI\MainForm.TrayTab.cs ^
    UI\MainForm.HotkeyTab.cs ^
    UI\MainForm.AlertTab.cs ^
    UI\MainForm.LogTab.cs ^
    UI\MainForm.IdleTab.cs ^
    UI\MainForm.BoostTab.cs ^
    UI\MainForm.BehaviorTab.cs

if errorlevel 1 (
    echo.
    echo GAGAL compile.
    pause
    exit /b 1
)

echo.
echo Selesai: %CD%\TurboToggle.exe
dir /b TurboToggle.exe
echo.
echo NOTE: TurboToggle.exe butuh semua DLL di folder yang sama saat dijalankan.
pause