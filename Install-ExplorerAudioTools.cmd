@echo off
setlocal DisableDelayedExpansion

rem ExplorerAudioTools C# installer (per-user, no administrator rights).
rem Put this .cmd in the repository root, next to src\.
set "EAT_EXE=%~dp0src\ExplorerAudioTools\bin\Release\net10.0-windows\win-x64\publish\ExplorerAudioTools.exe"
set "EAT_KEY=HKCU\Software\Classes\SystemFileAssociations\.mp3\shell\ExplorerAudioTools"
set "EAT_VERB=%EAT_KEY%\shell\RenameFromTags"

if not exist "%EAT_EXE%" (
    echo ERROR: C# executable not found:
    echo "%EAT_EXE%"
    echo Build with dotnet publish first; keep this installer in the repository root.
    pause
    exit /b 1
)

rem Replace our old PowerShell context-menu registration, not any other menu keys.
reg delete "%EAT_KEY%" /f >nul 2>nul

rem Static cascading menu for .mp3 files. One-file mode is intentional.
reg add "%EAT_KEY%" /v MUIVerb /t REG_SZ /d "ExplorerAudioTools" /f >nul || goto :fail
reg add "%EAT_KEY%" /v SubCommands /t REG_SZ /d "" /f >nul || goto :fail
reg add "%EAT_KEY%" /v MultiSelectModel /t REG_SZ /d "Single" /f >nul || goto :fail
reg add "%EAT_VERB%" /v MUIVerb /t REG_SZ /d "Dateiname aus ID3v2-Tags" /f >nul || goto :fail
reg add "%EAT_VERB%" /v MultiSelectModel /t REG_SZ /d "Single" /f >nul || goto :fail

rem %%1 deliberately writes a literal %1 Explorer placeholder into the registry.
rem The WinExe executable starts directly, with no PowerShell or console window.
reg add "%EAT_VERB%\command" /ve /t REG_SZ /d "\"%EAT_EXE%\" \"%%1\"" /f >nul || goto :fail

echo ExplorerAudioTools C# context menu installed for the current user.
echo Windows 11: Right-click MP3 ^> Show more options ^> ExplorerAudioTools.
pause
exit /b 0

:fail
echo ERROR: Could not register ExplorerAudioTools. Check permissions and retry.
pause
exit /b 1
