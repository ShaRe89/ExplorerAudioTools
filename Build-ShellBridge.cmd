@echo off
setlocal DisableDelayedExpansion
rem Requires Visual Studio with Desktop development with C++ (MSVC x64).
set "EAT_ROOT=%~dp0"
set "EAT_SOURCE=%EAT_ROOT%src\ExplorerAudioTools.ShellBridge\ShellBridge.cpp"
set "EAT_PUBLISH=%EAT_ROOT%src\ExplorerAudioTools\bin\Release\net10.0-windows\win-x64\publish"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%EAT_PUBLISH%\ExplorerAudioTools.exe" (
    echo ERROR: Publish the C# project with dotnet publish first.
    exit /b 1
)
if not exist "%VSWHERE%" (
    echo ERROR: Visual Studio Installer / vswhere.exe missing.
    exit /b 1
)
set "EAT_VS="
for /f "usebackq delims=" %%V in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "EAT_VS=%%V"
if not defined EAT_VS (
    echo ERROR: MSVC x64 tools not installed.
    exit /b 1
)
call "%EAT_VS%\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
cl /nologo /std:c++17 /EHsc /W4 /O2 /MT /LD /DUNICODE /D_UNICODE /Fo:"%TEMP%\ExplorerAudioTools_ShellBridge.obj" "%EAT_SOURCE%" /link /DLL /OUT:"%EAT_PUBLISH%\ExplorerAudioTools.ShellBridge.dll" /IMPLIB:"%TEMP%\ExplorerAudioTools_ShellBridge.lib" ole32.lib shell32.lib uuid.lib
if errorlevel 1 (
    echo ERROR: Native COM bridge build failed.
    exit /b 1
)
echo Shell bridge built: %EAT_PUBLISH%\ExplorerAudioTools.ShellBridge.dll
exit /b 0
