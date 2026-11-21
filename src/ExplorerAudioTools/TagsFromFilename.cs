using System.IO;

namespace ExplorerAudioTools;

/// <summary>
/// Writes ID3v2 and ID3v1 Artist/Title from "Artist - Title.mp3".
/// Validates all selected paths and backs up all files before writing.
/// Tag changes are not registered in Windows Explorer's rename Undo history.
/// </summary>
internal static class TagsFromFilename
{
    internal readonly record struct TagPlan(string Source, string Artist, string Title, bool NeedsUpdate);

    // ID3v1 stores only 30 bytes per field in a legacy encoding. Compare exact
    // values only when they are guaranteed to fit; otherwise save best-effort.
    private static bool CanCompareId3v1Exactly(string value) =>
        value.Length <= 30 && value.All(c => c >= ' ' && c <= '~');

    private static bool Matches(TagLib.Tag? tag, string artist, string title) =>
        tag is not null &&
        string.Equals(string.Join("; ", tag.Performers ?? Array.Empty<string>()).Trim(), artist,
            StringComparison.Ordinal) &&
        string.Equals(tag.Title?.Trim(), title, StringComparison.Ordinal);

    private static bool MatchesId3v1(TagLib.Tag? tag, string artist, string title) =>
        CanCompareId3v1Exactly(artist) && CanCompareId3v1Exactly(title) &&
        Matches(tag, artist, title);

    internal static TagPlan Prepare(string suppliedPath)
    {
        var source = Path.GetFullPath(suppliedPath);
        if (!File.Exists(source))
            throw new FileNotFoundException("MP3-Datei nicht gefunden.", source);
        if (!string.Equals(Path.GetExtension(source), ".mp3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nur MP3-Dateien erlaubt: " + source);
        if ((File.GetAttributes(source) & FileAttributes.ReadOnly) != 0)
            throw new IOException("Schreibgeschützte Datei: " + source);

        var stem = Path.GetFileNameWithoutExtension(source);
        // The first exact delimiter separates Artist from Title.
        var separator = stem.IndexOf(" - ", StringComparison.Ordinal);
        if (separator < 1)
            throw new InvalidDataException("Dateiname hat nicht das Format 'Künstler - Titel.mp3': " + source);

        var artist = stem[..separator].Trim();
        var title = stem[(separator + 3)..].Trim();
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            throw new InvalidDataException("Künstler oder Titel fehlt im Dateinamen: " + source);

        using var audio = TagLib.File.Create(source);
        // Do not use audio.Tag, which merges metadata from multiple tag types.
        var v2 = audio.GetTag(TagLib.TagTypes.Id3v2, false);
        var v1 = audio.GetTag(TagLib.TagTypes.Id3v1, false);
        var needsUpdate = !Matches(v2, artist, title) || !MatchesId3v1(v1, artist, title);
        return new TagPlan(source, artist, title, needsUpdate);
    }

    internal static void ApplyBatch(IReadOnlyList<TagPlan> plans)
    {
        var toWrite = plans.Where(p => p.NeedsUpdate).ToArray();
        if (toWrite.Length == 0)
            return;

        // Full-file backups (audio, other metadata, artwork and existing tags)
        // must all exist before the first change is attempted.
        var backupDir = Path.Combine(Path.GetTempPath(), "ExplorerAudioTools", "Tags-" + Guid.NewGuid().ToString("N"));
        var backups = new List<(string Original, string Backup)>();
        Directory.CreateDirectory(backupDir);
        var preserveBackups = false;
        try
        {
            for (var i = 0; i < toWrite.Length; ++i)
            {
                var backup = Path.Combine(backupDir, i.ToString("D6") + ".mp3");
                File.Copy(toWrite[i].Source, backup);
                backups.Add((toWrite[i].Source, backup));
            }

            try
            {
                foreach (var item in toWrite)
                {
                    using (var audio = TagLib.File.Create(item.Source))
                    {
                        var v2 = audio.GetTag(TagLib.TagTypes.Id3v2, true)
                            ?? throw new InvalidOperationException("ID3v2-Tag konnte nicht erstellt werden: " + item.Source);
                        var v1 = audio.GetTag(TagLib.TagTypes.Id3v1, true)
                            ?? throw new InvalidOperationException("ID3v1-Tag konnte nicht erstellt werden: " + item.Source);

                        // Only overwrite Artist and Title. Existing Album, Year,
                        // Comments, Cover and other metadata should be preserved.
                        v2.Performers = new[] { item.Artist };
                        v2.Title = item.Title;
                        v1.Performers = new[] { item.Artist };
                        v1.Title = item.Title;
                        audio.Save();
                    }

                    using (var check = TagLib.File.Create(item.Source))
                    {
                        var savedV2 = check.GetTag(TagLib.TagTypes.Id3v2, false);
                        var savedV1 = check.GetTag(TagLib.TagTypes.Id3v1, false);
                        if (!Matches(savedV2, item.Artist, item.Title))
                            throw new IOException("ID3v2-Schreibprüfung fehlgeschlagen: " + item.Source);
                        if (savedV1 is null)
                            throw new IOException("ID3v1 wurde nicht gespeichert: " + item.Source);
                        // For simple ASCII values that fit, require exact ID3v1
                        // roundtrip. Longer/non-ASCII strings are format-limited.
                        if (CanCompareId3v1Exactly(item.Artist) &&
                            CanCompareId3v1Exactly(item.Title) &&
                            !Matches(savedV1, item.Artist, item.Title))
                            throw new IOException("ID3v1-Schreibprüfung fehlgeschlagen: " + item.Source);
                    }
                }
            }
            catch (Exception writeError)
            {
                var failures = new List<string>();
                foreach (var (original, backup) in backups)
                {
                    try { File.Copy(backup, original, overwrite: true); }
                    catch (Exception restoreError) { failures.Add(original + ": " + restoreError.Message); }
                }

                if (failures.Count > 0)
                {
                    preserveBackups = true;
                    throw new IOException(
                        "Tag-Schreiben fehlgeschlagen. Der Rollback ist unvollständig! " +
                        "Originalkopien liegen in: " + backupDir + Environment.NewLine +
                        string.Join(Environment.NewLine, failures), writeError);
                }
                throw new IOException("Tag-Schreiben fehlgeschlagen. Originaldateien wurden wiederhergestellt. " +
                    writeError.Message, writeError);
            }
        }
        finally
        {
            if (!preserveBackups)
            {
                try { Directory.Delete(backupDir, recursive: true); } catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
