@echo off
setlocal DisableDelayedExpansion
rem ExplorerAudioTools: per-user native COM Explorer verbs (single + multi-selection).
rem Keep this installer in the repository root, next to src\.
set "EAT_PUBLISH=%~dp0src\ExplorerAudioTools\bin\Release\net10.0-windows\win-x64\publish"
set "EAT_EXE=%EAT_PUBLISH%\ExplorerAudioTools.exe"
set "EAT_BRIDGE=%EAT_PUBLISH%\ExplorerAudioTools.ShellBridge.dll"
set "EAT_CLSID_RENAME={C78FFB2A-2CEB-475C-92D9-F86A59C10674}"
set "EAT_CLSID_TAGS={B8A64E28-7451-47C8-86DA-8E94E761D0C1}"
set "EAT_MENU=HKCU\Software\Classes\SystemFileAssociations\.mp3\shell\ExplorerAudioTools"

if not exist "%EAT_EXE%" (
    echo ERROR: C# executable missing. Run dotnet publish first.
    pause
    exit /b 1
)
if not exist "%EAT_BRIDGE%" (
    echo ERROR: Native COM bridge DLL missing. Run Build-ShellBridge.cmd first.
    pause
    exit /b 1
)

rem Both CLSIDs are implemented by the same unmanaged ShellBridge DLL.
reg add "HKCU\Software\Classes\CLSID\%EAT_CLSID_RENAME%\InprocServer32" /ve /t REG_SZ /d "%EAT_BRIDGE%" /f >nul || goto :fail
reg add "HKCU\Software\Classes\CLSID\%EAT_CLSID_RENAME%\InprocServer32" /v ThreadingModel /t REG_SZ /d "Apartment" /f >nul || goto :fail
reg add "HKCU\Software\Classes\CLSID\%EAT_CLSID_TAGS%\InprocServer32" /ve /t REG_SZ /d "%EAT_BRIDGE%" /f >nul || goto :fail
reg add "HKCU\Software\Classes\CLSID\%EAT_CLSID_TAGS%\InprocServer32" /v ThreadingModel /t REG_SZ /d "Apartment" /f >nul || goto :fail

rem Recreate ONLY our own context-menu subtree.
reg delete "%EAT_MENU%" /f >nul 2>nul
reg add "%EAT_MENU%" /v MUIVerb /t REG_SZ /d "ExplorerAudioTools" /f >nul || goto :fail
reg add "%EAT_MENU%" /v SubCommands /t REG_SZ /d "" /f >nul || goto :fail
reg add "%EAT_MENU%" /v MultiSelectModel /t REG_SZ /d "Player" /f >nul || goto :fail

set "EAT_RENAME=%EAT_MENU%\shell\RenameFromTags"
reg add "%EAT_RENAME%" /v MUIVerb /t REG_SZ /d "Dateiname aus ID3v2-Tags" /f >nul || goto :fail
reg add "%EAT_RENAME%" /v MultiSelectModel /t REG_SZ /d "Player" /f >nul || goto :fail
reg add "%EAT_RENAME%\command" /v DelegateExecute /t REG_SZ /d "%EAT_CLSID_RENAME%" /f >nul || goto :fail

set "EAT_TAGS=%EAT_MENU%\shell\TagsFromFilename"
reg add "%EAT_TAGS%" /v MUIVerb /t REG_SZ /d "Tags aus Dateiname" /f >nul || goto :fail
reg add "%EAT_TAGS%" /v MultiSelectModel /t REG_SZ /d "Player" /f >nul || goto :fail
reg add "%EAT_TAGS%\command" /v DelegateExecute /t REG_SZ /d "%EAT_CLSID_TAGS%" /f >nul || goto :fail

echo ExplorerAudioTools installed: two native context-menu commands.
echo Windows 11: select MP3 files ^> Show more options ^> ExplorerAudioTools.
pause
exit /b 0

:fail
echo ERROR: Could not register ExplorerAudioTools. Installation may be incomplete.
pause
exit /b 1
