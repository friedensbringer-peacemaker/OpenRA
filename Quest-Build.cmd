@echo off
rem xr.openra: Werkzeuge laden, Red-Alert-Paket laden, APK bauen und auf die Quest installieren.
rem Aufruf ohne Argument = kompletter Ablauf; "Quest-Build.cmd --no-install" baut nur.
setlocal
set "BASH="
for %%G in ("%ProgramFiles%\Git\bin\bash.exe" "%ProgramFiles(x86)%\Git\bin\bash.exe" "%LocalAppData%\Programs\Git\bin\bash.exe") do (
    if exist %%G if not defined BASH set "BASH=%%~G"
)
if not defined BASH (
    echo Git fuer Windows fehlt. Bitte von https://git-scm.com/download/win installieren.
    pause
    exit /b 1
)
"%BASH%" "%~dp0quest/all.sh" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" echo. & echo Abgebrochen mit Fehler %RC%. Details stehen oben.
pause
exit /b %RC%
