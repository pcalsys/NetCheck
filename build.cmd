@echo off
setlocal

set "NETCHECK_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%NETCHECK_POWERSHELL%" (
    echo ERROR: Windows PowerShell was not found.
    exit /b 1
)

"%NETCHECK_POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Configuration Release %*
set "NETCHECK_EXIT_CODE=%ERRORLEVEL%"

if not "%NETCHECK_EXIT_CODE%"=="0" (
    echo.
    echo NetCheck build failed with exit code %NETCHECK_EXIT_CODE%.
    exit /b %NETCHECK_EXIT_CODE%
)

echo.
echo NetCheck build completed successfully.
exit /b 0
