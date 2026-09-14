@echo off
setlocal enabledelayedexpansion

echo ====================================================
echo       freewall Windows Setup Build Script
echo ====================================================
echo.

echo [1/4] Stopping running freewall / spoofdpi processes...
taskkill /F /IM freewall-win.exe >nul 2>&1
taskkill /F /IM spoofdpi.exe >nul 2>&1
taskkill /F /IM goodbyedpi.exe >nul 2>&1

echo [2/4] Publishing .NET 10.0 Self-Contained Release...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ".\publish"
if errorlevel 1 (
    echo [ERROR] dotnet publish failed!
    pause
    exit /b 1
)

if not exist ".\publish\spoofdpi\spoofdpi.exe" (
    echo [INFO] Copying spoofdpi.exe to publish folder...
    if not exist ".\publish\spoofdpi" mkdir ".\publish\spoofdpi"
    copy /y ".\spoofdpi\spoofdpi.exe" ".\publish\spoofdpi\" >nul
)

echo [3/4] Searching for Inno Setup compiler (ISCC.exe)...
set "ISCC_PATH="

where iscc >nul 2>&1
if not errorlevel 1 (
    set "ISCC_PATH=iscc"
) else if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
) else if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles%\Inno Setup 6\ISCC.exe"
) else if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" (
    echo.
    echo [NOTICE] Inno Setup 6 is not installed on this system.
    echo You can download and install Inno Setup 6 from: https://jrsoftware.org/isdl.php
    echo After installing, run this script again to generate the setup.exe.
    echo.
    echo Note: The standalone application has already been built in the 'publish' folder!
    echo.
    pause
    exit /b 0
)

echo [4/4] Compiling installer with freewall.iss...
"%ISCC_PATH%" "freewall.iss"
if errorlevel 1 (
    echo [ERROR] Inno Setup compilation failed!
    pause
    exit /b 1
)

echo.
echo ====================================================
echo   Successfully created installer!
echo   Location: installer_output\
echo ====================================================
echo.
pause
