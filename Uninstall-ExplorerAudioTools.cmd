@echo off
setlocal
rem Removes only ExplorerAudioTools registry entries, not application files.
reg delete "HKCU\Software\Classes\SystemFileAssociations\.mp3\shell\ExplorerAudioTools" /f >nul 2>nul
reg delete "HKCU\Software\Classes\CLSID\{C78FFB2A-2CEB-475C-92D9-F86A59C10674}" /f >nul 2>nul
echo ExplorerAudioTools context menu unregistered for this user.
pause
