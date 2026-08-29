@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

git.exe rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo This file must be run inside a Git repository.
    goto :end
)

:menu
cls
echo.
echo ==========================================
echo          BuildFit Git Helper
echo ==========================================
echo.
echo  [1] Upload my changes  (commit + pull + push)
echo  [2] Download updates   (pull only)
echo  [3] Check status
echo  [0] Exit
echo.
set "ACTION="
set /p "ACTION=Type 1, 2, 3, or 0 then press Enter: "
if "%ACTION%"=="0" goto :exit
if "%ACTION%"=="1" goto :upload
if "%ACTION%"=="2" goto :pull_only
if "%ACTION%"=="3" goto :status
echo Invalid selection. Please try again.
timeout /t 2 >nul
goto :menu

:upload
cls
echo.
echo --- Changes to upload ---
git.exe status --short
set "HAS_CHANGES="
for /f "delims=" %%A in ('git.exe status --porcelain') do set "HAS_CHANGES=1"

if not defined HAS_CHANGES goto :sync_existing

echo.
set "COMMIT_MESSAGE="
set /p "COMMIT_MESSAGE=What did you change? "
if not defined COMMIT_MESSAGE set "COMMIT_MESSAGE=Update project files"
echo.
echo Commit message: %COMMIT_MESSAGE%
set "CONFIRM="
set /p "CONFIRM=Continue with commit, pull, and push? (Y/N then Enter): "
if /i "%CONFIRM%"=="N" goto :menu
if /i not "%CONFIRM%"=="Y" (
    echo Please enter Y or N.
    goto :upload
)

git.exe add -A
git.exe commit -m "%COMMIT_MESSAGE%"
if errorlevel 1 (
    echo.
    echo Commit failed. Nothing was pushed.
    goto :end
)

:sync_existing
echo.
echo Downloading the latest changes...
git.exe pull --rebase
if errorlevel 1 (
    echo.
    echo Pull failed. Resolve the conflict, then run this file again.
    goto :end
)

echo.
echo Uploading to remote...
git.exe push
if errorlevel 1 (
    echo.
    echo Push failed. Nothing else will be changed.
    goto :end
)

echo.
echo Done. Your branch is synced.
goto :end

:pull_only
cls
echo.
echo Downloading the latest changes...
git.exe pull --rebase
if errorlevel 1 (
    echo.
    echo Pull failed. If you have local changes, use option 1 instead.
) else (
    echo.
    echo Done. Latest changes downloaded.
)
goto :end

:status
cls
echo.
echo --- Current Git status ---
git.exe status --short --branch
echo.
pause
goto :menu

:exit
endlocal
exit /b

:end
echo.
pause
endlocal
