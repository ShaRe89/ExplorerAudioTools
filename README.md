# ExplorerAudioTools

Kleine, erweiterbare Werkzeuge für die Verwaltung von Audiodateien direkt im Windows-Explorer.

**Aktueller Stand:** Die erste Funktion ist als C#-Anwendung umgesetzt und unter Windows 11 getestet. Die frühere PowerShell-Implementierung wird nicht mehr benötigt.

## Funktionen

### MP3-Datei aus ID3v2-Tags umbenennen

Benennt eine MP3-Datei nach dem Schema **`Künstler - Titel.mp3`** um.

**Beispiel:**

`1.mp3` → `Cascada - Because the Night.mp3`

- Verwendet ausschließlich **ID3v2** (Artist und Title). **ID3v1 wird niemals als Ersatz herangezogen.**
- Prüft die erforderlichen Tags und den neuen Dateinamen **vor** der Umbenennung.
- Bricht bei fehlenden Angaben, ungültigen Dateinamen oder vorhandenen Zieldateien ab, ohne die Datei umzubenennen.
- Führt die Umbenennung über die Windows-Shell (`IFileOperation`) aus und fordert einen **Explorer-Undo-Eintrag** (`FOFX_ADDUNDORECORD`) an. Rückgängig wurde unter Windows 11 erfolgreich getestet.
- Läuft als C#-Windows-Anwendung **ohne PowerShell- oder Konsolenfenster**. Fehler werden per Dialog angezeigt; erfolgreiche Umbenennungen laufen ohne Meldung ab.

**Aktuelle Einschränkung:** Das Explorer-Kontextmenü unterstützt pro Aufruf **eine einzelne MP3-Datei**. Die Mehrfachauswahl ist noch nicht freigegeben; eine atomare Mehrdatei-Umbenennung wird nicht zugesichert.

## Voraussetzungen

- Windows 11 (bisher dort getestet)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) zum Kompilieren
- .NET 10 Desktop Runtime zum Ausführen der mit `--self-contained false` erstellten Anwendung

Das Projekt nutzt **TagLibSharp 2.3.0** zum Auslesen der Metadaten (NuGet-Abhängigkeit).

## Kompilieren

Im Repository-Hauptverzeichnis:

```powershell
dotnet publish .\src\ExplorerAudioTools\ExplorerAudioTools.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Die ausführbare Datei liegt anschließend unter:

```text
src\ExplorerAudioTools\bin\Release\net10.0-windows\win-x64\publish\ExplorerAudioTools.exe
```

## Integration in den Windows-Explorer

Die Datei **`Install-ExplorerAudioTools-CSharp.cmd`** im Repository-Hauptverzeichnis per Doppelklick starten, nachdem die EXE erstellt wurde.

Der Installer:

- Ermittelt den EXE-Pfad relativ zu seinem eigenen Speicherort – **kein fest codierter Benutzerpfad**.
- Registriert das Kontextmenü nur für den aktuellen Benutzer (`HKCU`); Administratorrechte sind nicht nötig.
- Ersetzt einen eventuell vorhandenen alten `ExplorerAudioTools`-Menüeintrag.

Danach im Explorer:

**Rechtsklick auf eine MP3 → Weitere Optionen anzeigen → ExplorerAudioTools → Dateiname aus ID3v2-Tags**

**Hinweis:** Das Kontextmenü verweist auf die EXE im Build-Verzeichnis. Wird das Repository verschoben oder die EXE gelöscht, muss die Anwendung neu gebaut und der Installer erneut ausgeführt werden.

## Projektstruktur

```text
ExplorerAudioTools/
├── src/
│   └── ExplorerAudioTools/
│       ├── ExplorerAudioTools.csproj
│       ├── Program.cs
│       ├── RenameFromTags.cs
│       └── ShellRename.cs
├── Install-ExplorerAudioTools-CSharp.cmd
├── README.md
└── LICENSE
```

## Geplante Erweiterungen

- Sichere Mehrfachauswahl mit vollständiger Vorabprüfung
- Tags aus Dateinamen übernehmen
- Metadaten prüfen und Dateinamen bereinigen
- Duplikate erkennen
- Weitere Audioformate, z. B. FLAC

## Lizenz

MIT – siehe [LICENSE](LICENSE).

## Native Explorer Multi-Selection

ExplorerAudioTools supports renaming multiple selected MP3 files in a single operation.

### Architecture
- C# (.NET 10): ID3v2 parsing, validation and renaming.
- Native C++ COM bridge: IExecuteCommand and IObjectWithSelection.
- All selected files are passed to the C# application in one invocation.
- Windows IFileOperation provides native Explorer Undo support.

### Build and Installation
1. Publish the C# project for win-x64.
2. Run Build-ShellBridge.cmd (Visual Studio C++ Build Tools required).
3. Run Install-ExplorerAudioTools.cmd (no administrator privileges required).

.NET 10 Desktop Runtime is required.

### Verified
- Single-file renaming
- Batch renaming
- Duplicate target detection
- Native Explorer Undo for multiple files
- No PowerShell dependency

### Limitations
- Windows file operations are not fully transactional.
- Rollback is best-effort.
- Installed DLLs may require an Explorer restart before rebuilding.
- Moving the installation directory requires reinstalling.

### Uninstallation
Run Uninstall-ExplorerAudioTools.cmd.
