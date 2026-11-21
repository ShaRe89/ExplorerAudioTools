using System.IO;

namespace ExplorerAudioTools;

internal static class RenameFromTags
{
    internal readonly record struct RenamePlan(string Source, string TargetName);

    internal static RenamePlan? Prepare(string suppliedPath)
    {
        var source = Path.GetFullPath(suppliedPath);
        if (!File.Exists(source))
            throw new FileNotFoundException("MP3-Datei nicht gefunden.", source);
        if (!string.Equals(Path.GetExtension(source), ".mp3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Aktuell werden nur MP3-Dateien unterstützt.");
        if ((File.GetAttributes(source) & FileAttributes.ReadOnly) != 0)
            throw new IOException("Die MP3-Datei ist schreibgeschützt.");

        string artist;
        string title;

        using (var audio = TagLib.File.Create(source))
        {
            // Crucial: Do not use audio.Tag, which merges ID3v1 and ID3v2.
            // Request the ID3v2 tag explicitly and NEVER create a missing tag.
            var tag = audio.GetTag(TagLib.TagTypes.Id3v2, false) as TagLib.Id3v2.Tag;
            if (tag is null)
                throw new InvalidOperationException("Kein ID3v2-Tag vorhanden (ID3v1 wird ignoriert).");

            artist = string.Join("; ", tag.Performers ?? Array.Empty<string>()).Trim();
            title = (tag.Title ?? string.Empty).Trim();
        }

        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException(
                "ID3v2 Artist oder Title fehlt. Keine Datei wurde umbenannt.");

        var newName = $"{artist} - {title}.mp3";
        if (newName.Length > 255 || newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            newName.EndsWith(' ') || newName.EndsWith('.'))
            throw new InvalidOperationException("Ungültiger oder zu langer Dateiname aus den ID3v2-Tags: " + newName);

        var currentName = Path.GetFileName(source);
        if (string.Equals(newName, currentName, StringComparison.Ordinal))
            return null;

        var destination = Path.Combine(Path.GetDirectoryName(source)!, newName);
        // Case-only renames are permitted; Windows file systems are generally case insensitive.
        if (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) &&
            (File.Exists(destination) || Directory.Exists(destination)))
            throw new IOException("Zieldatei existiert bereits. Keine Umbenennung: " + destination);

        return new RenamePlan(source, newName);
    }
}
