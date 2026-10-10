@echo off
setlocal

REM Paksa working directory = folder build.bat berada
cd /d "%~dp0"

echo Current dir: %CD%
echo.

if not exist "TurboToggle.cs" (
    echo [ERROR] File TurboToggle.cs tidak ditemukan di folder ini.
    echo         Pastikan kedua file ada di folder yang sama.
    echo.
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
"%CSC%" /nologo /target:winexe /optimize+ ^
    /out:TurboToggle.exe ^
    /r:System.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Drawing.dll ^
    /r:LibreHardwareMonitorLib.dll ^
	/r:System.Memory.dll ^
    /r:System.Buffers.dll ^
    /r:System.Runtime.CompilerServices.Unsafe.dll ^
    /r:HidSharp.dll ^
    TurboToggle.cs

if errorlevel 1 (
    echo.
    echo GAGAL compile.
    pause
    exit /b 1
)

echo.
echo Selesai: %CD%\TurboToggle.exe
dir /b TurboToggle.exe
pause