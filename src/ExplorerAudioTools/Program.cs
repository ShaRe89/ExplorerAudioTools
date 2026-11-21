using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ExplorerAudioTools;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();

        try
        {
            // Default stays compatible with the currently installed Explorer verb.
            // The reverse operation uses an explicit opt-in argument.
            var arguments = new List<string>(args);
            var preview = false;
            var fromFilename = false;
            while (arguments.Count > 0)
            {
                if (arguments[0] == "--dry-run")
                    preview = true;
                else if (arguments[0] == "--tags-from-filename")
                    fromFilename = true;
                else
                    break;
                arguments.RemoveAt(0);
            }

            string[] paths = arguments.ToArray();
            if (paths.Length > 0 && paths[0] == "--selection-file")
            {
                if (paths.Length != 2)
                    throw new ArgumentException("Ungültige Explorer-Auswahldatei-Parameter.");

                var manifestPath = Path.GetFullPath(paths[1]);
                try
                {
                    if (new FileInfo(manifestPath).Length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Die Explorer-Auswahldatei ist zu groß.");
                    paths = File.ReadAllLines(manifestPath, Encoding.Unicode);
                }
                finally
                {
                    try { File.Delete(manifestPath); } catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            if (paths.Length == 0 || paths.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Mindestens eine gültige MP3-Datei angeben.");

            var normalized = paths.Select(Path.GetFullPath).ToArray();
            if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
                throw new InvalidOperationException("Dieselbe Datei wurde mehrfach übergeben. Abbruch ohne Änderungen.");

            if (fromFilename)
            {
                // Validate the ENTIRE selection before any ID3v2 tag is changed.
                var changes = normalized.Select(TagsFromFilename.Prepare).ToArray();
                if (preview)
                {
                    var summary = string.Join(Environment.NewLine,
                        changes.Select(p => $"{Path.GetFileName(p.Source)}  →  Artist: {p.Artist}; Title: {p.Title}"
                            + (p.NeedsUpdate ? "" : " (bereits korrekt)")));
                    MessageBox.Show(summary, "ExplorerAudioTools – Vorschau (keine Änderungen)",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    TagsFromFilename.ApplyBatch(changes);
                }
                return 0;
            }

            // Existing, tested file-rename path is intentionally unchanged.
            var plans = new List<RenameFromTags.RenamePlan>();
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in normalized)
            {
                var candidate = RenameFromTags.Prepare(path);
                if (candidate is not { } plan)
                    continue;

                var destination = Path.Combine(Path.GetDirectoryName(plan.Source)!, plan.TargetName);
                if (!destinations.Add(destination))
                    throw new IOException("Mehrere Dateien würden denselben Zielnamen erhalten: " + destination);
                plans.Add(plan);
            }

            if (preview)
            {
                var summary = plans.Count == 0
                    ? "Keine Umbenennung erforderlich."
                    : string.Join(Environment.NewLine,
                        plans.Select(p => $"{Path.GetFileName(p.Source)}  →  {p.TargetName}"));
                MessageBox.Show(summary, "ExplorerAudioTools – Vorschau (keine Änderungen)",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            if (plans.Count > 0)
                ShellRename.RenameBatch(plans);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "ExplorerAudioTools",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
