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
            // The initial Explorer verb explicitly handles one file per invocation.
            // Do not silently process a subset if several arguments were provided.
            if (args.Length != 1)
                throw new InvalidOperationException(
                    "Bitte genau eine MP3-Datei über das Explorer-Kontextmenü auswählen.");

            var plan = RenameFromTags.Prepare(args[0]); // No changes during validation.
            if (plan is null) // Already named correctly.
                return 0;

            ShellRename.Rename(plan.Value.Source, plan.Value.TargetName);
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
