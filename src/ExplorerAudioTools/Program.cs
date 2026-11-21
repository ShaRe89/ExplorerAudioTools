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
            var preview = args.Length > 0 && args[0] == "--dry-run";
            var paths = preview ? args[1..] : args;

            // The shell COM bridge writes ALL selected paths to a UTF-16 LE
            // manifest before launching this process exactly once.
            // Never fall back to operating on a subset of the selection.
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
                    // The bridge creates a unique temporary manifest per invocation.
                    // Even failed validation must not leave it behind.
                    try { File.Delete(manifestPath); } catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            if (paths.Length == 0 || paths.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Mindestens eine gültige MP3-Datei angeben.");

            var normalized = paths.Select(Path.GetFullPath).ToArray();
            if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
                throw new InvalidOperationException("Dieselbe Datei wurde mehrfach übergeben. Abbruch ohne Änderungen.");

            // Full validation first. The IFileOperation batch only starts after
            // EVERY source has passed and target collisions have been excluded.
            var plans = new List<RenameFromTags.RenamePlan>();
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in normalized)
            {
                var candidate = RenameFromTags.Prepare(path);
                if (candidate is not { } plan)
                    continue; // Already named correctly, still validated above.

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
