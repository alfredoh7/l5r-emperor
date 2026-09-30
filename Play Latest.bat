@echo off
setlocal DisableDelayedExpansion
rem Launch the checked-out development version via Git Bash's tools/run.sh,
rem including unmerged local UI work. Never fetches or switches branches.
set "L5R_ROOT=%~dp0"
set "L5R_BASH="
if exist "%ProgramFiles%\Git\bin\bash.exe" set "L5R_BASH=%ProgramFiles%\Git\bin\bash.exe"
if not defined L5R_BASH if exist "%ProgramFiles%\Git\usr\bin\bash.exe" set "L5R_BASH=%ProgramFiles%\Git\usr\bin\bash.exe"
if not defined L5R_BASH if exist "%ProgramFiles(x86)%\Git\bin\bash.exe" set "L5R_BASH=%ProgramFiles(x86)%\Git\bin\bash.exe"
if not defined L5R_BASH if exist "%LocalAppData%\Programs\Git\bin\bash.exe" set "L5R_BASH=%LocalAppData%\Programs\Git\bin\bash.exe"
if not defined L5R_BASH goto missing_bash

"%L5R_BASH%" "%L5R_ROOT%Play Latest.command"
set "L5R_EXIT=%ERRORLEVEL%"
if not "%L5R_EXIT%"=="0" goto failed
exit /b 0

:missing_bash
echo Git Bash is required but was not found. Install Git for Windows:
echo https://git-scm.com/download/win
pause
exit /b 1

:failed
echo.
echo L5R Emperor exited with an error ^(code %L5R_EXIT%^). See "artifacts\checks\latest-launch.log".
pause
exit /b %L5R_EXIT%
