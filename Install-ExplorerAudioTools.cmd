@echo off
setlocal DisableDelayedExpansion
rem ExplorerAudioTools native COM multiselect installer; HKCU, no admin rights.
rem Installs the native bridge alongside the C# executable.
set "EAT_PUBLISH=%~dp0src\ExplorerAudioTools\bin\Release\net10.0-windows\win-x64\publish"
set "EAT_EXE=%EAT_PUBLISH%\ExplorerAudioTools.exe"
set "EAT_BRIDGE=%EAT_PUBLISH%\ExplorerAudioTools.ShellBridge.dll"
set "EAT_CLSID={C78FFB2A-2CEB-475C-92D9-F86A59C10674}"
set "EAT_CLASS=HKCU\Software\Classes\CLSID\%EAT_CLSID%\InprocServer32"
set "EAT_MENU=HKCU\Software\Classes\SystemFileAssociations\.mp3\shell\ExplorerAudioTools"
set "EAT_VERB=%EAT_MENU%\shell\RenameFromTags"

if not exist "%EAT_EXE%" (
    echo ERROR: Missing C# executable. Run dotnet publish first.
    pause
    exit /b 1
)
if not exist "%EAT_BRIDGE%" (
    echo ERROR: Missing ShellBridge DLL. Run Build-ShellBridge.cmd first.
    pause
    exit /b 1
)

rem Register lightweight unmanaged COM adapter for the current user.
reg add "%EAT_CLASS%" /ve /t REG_SZ /d "%EAT_BRIDGE%" /f >nul || goto :fail
reg add "%EAT_CLASS%" /v ThreadingModel /t REG_SZ /d "Apartment" /f >nul || goto :fail

rem Replace only our own previous single-file verb.
reg delete "%EAT_MENU%" /f >nul 2>nul
reg add "%EAT_MENU%" /v MUIVerb /t REG_SZ /d "ExplorerAudioTools" /f >nul || goto :fail
reg add "%EAT_MENU%" /v SubCommands /t REG_SZ /d "" /f >nul || goto :fail
reg add "%EAT_MENU%" /v MultiSelectModel /t REG_SZ /d "Player" /f >nul || goto :fail
reg add "%EAT_VERB%" /v MUIVerb /t REG_SZ /d "Dateiname aus ID3v2-Tags" /f >nul || goto :fail
reg add "%EAT_VERB%" /v MultiSelectModel /t REG_SZ /d "Player" /f >nul || goto :fail
reg add "%EAT_VERB%\command" /v DelegateExecute /t REG_SZ /d "%EAT_CLSID%" /f >nul || goto :fail

echo ExplorerAudioTools native multi-selection menu registered for current user.
echo Windows 11: Right-click selected MP3 files ^> Show more options ^> ExplorerAudioTools.
pause
exit /b 0

:fail
echo ERROR: Registry registration failed. Prior menu may require reinstalling.
pause
exit /b 1
