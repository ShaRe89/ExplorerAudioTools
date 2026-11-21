using System.IO;
using System.Runtime.InteropServices;

namespace ExplorerAudioTools;

/// <summary>
/// Queues all renames in ONE IFileOperation so the normal Explorer Undo mechanism
/// can track them. A best-effort rollback is performed if Windows reports failure.
/// Note: Windows does not provide an atomic multi-file rename transaction.
/// </summary>
internal static class ShellRename
{
    private const uint FOFX_ADDUNDORECORD = 0x20000000;
    private const uint FOF_SILENT = 0x0004;
    private const uint FOF_NOERRORUI = 0x0400;
    private const uint FOFX_EARLYFAILURE = 0x00100000;

    internal static void RenameBatch(IReadOnlyList<RenameFromTags.RenamePlan> plans)
    {
        if (plans.Count == 0)
            return;

        // Perform one batch; this operation must not be split into separate
        // Explorer activations, otherwise collective validation is impossible.
        try
        {
            Execute(plans.Select(p => (p.Source, p.TargetName)).ToArray(), recordUndo: true);
            if (plans.Any(p => !HasExactFileName(p.Source, p.TargetName)))
                throw new IOException("Mindestens eine Umbenennung wurde nicht ausgeführt.");
        }
        catch (Exception originalError)
        {
            // Only revert entries that now have exactly the intended target name.
            // Source/target conflicts were rejected by the validation phase.
            var completed = plans
                .Where(p => HasExactFileName(p.Source, p.TargetName))
                .Select(p => (Source: Path.Combine(Path.GetDirectoryName(p.Source)!, p.TargetName),
                              TargetName: Path.GetFileName(p.Source)))
                .Reverse()
                .ToArray();

            try
            {
                if (completed.Length > 0)
                    Execute(completed, recordUndo: false);
                if (plans.Any(p => !HasExactFileName(p.Source, Path.GetFileName(p.Source))))
                    throw new IOException("Nicht alle ursprünglichen Dateinamen konnten wiederhergestellt werden.");
            }
            catch (Exception rollbackError)
            {
                throw new IOException(
                    "Die Umbenennung ist fehlgeschlagen und das Zurückrollen war unvollständig. " +
                    "Bitte die Dateinamen manuell kontrollieren. " + rollbackError.Message,
                    new AggregateException(originalError, rollbackError));
            }

            throw new IOException(
                "Die Umbenennung ist fehlgeschlagen. Bereits geänderte Namen wurden zurückgesetzt. " +
                "Bitte die Explorer-Rückgängig-Historie vor weiteren Aktionen prüfen. " + originalError.Message,
                originalError);
        }
    }

    private static bool HasExactFileName(string originalSource, string name)
    {
        var directory = Path.GetDirectoryName(originalSource)!;
        var requestedPath = Path.Combine(directory, name);
        if (!File.Exists(requestedPath))
            return false;

        // Exact enumeration also detects case-only renames on case-insensitive NTFS.
        return Directory.EnumerateFiles(directory, name, SearchOption.TopDirectoryOnly)
            .Any(p => string.Equals(Path.GetFileName(p), name, StringComparison.Ordinal));
    }

    private static void Execute((string Source, string TargetName)[] changes, bool recordUndo)
    {
        var items = new List<IShellItem>();
        IFileOperation? operation = null;
        try
        {
            operation = (IFileOperation)new FileOperationComObject();
            var flags = FOF_SILENT | FOF_NOERRORUI | FOFX_EARLYFAILURE;
            if (recordUndo)
                flags |= FOFX_ADDUNDORECORD;
            operation.SetOperationFlags(flags);

            foreach (var (source, targetName) in changes)
            {
                var itemId = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(source, IntPtr.Zero, ref itemId, out var item);
                items.Add(item);
                operation.RenameItem(item, targetName, IntPtr.Zero);
            }

            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out bool aborted);
            if (aborted)
                throw new IOException("Die Windows-Dateioperation wurde abgebrochen.");
        }
        finally
        {
            foreach (var item in items)
                Marshal.ReleaseComObject(item);
            if (operation is not null)
                Marshal.ReleaseComObject(operation);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [ComImport]
    [Guid("3AD05575-8857-4850-9277-11B85BDB8E09")]
    private class FileOperationComObject { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    private interface IShellItem { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    private interface IFileOperation
    {
        void Advise(IntPtr progressSink, out uint cookie);
        void Unadvise(uint cookie);
        void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr progressDialog);
        void SetProperties(IntPtr properties);
        void SetOwnerWindow(IntPtr ownerWindow);
        void ApplyPropertiesToItem(IntPtr item);
        void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr progressSink);
        void RenameItems(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        void MoveItem(IntPtr item, IntPtr destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string newName, IntPtr progressSink);
        void MoveItems(IntPtr items, IntPtr destinationFolder);
        void CopyItem(IntPtr item, IntPtr destinationFolder, [MarshalAs(UnmanagedType.LPWStr)] string copyName, IntPtr progressSink);
        void CopyItems(IntPtr items, IntPtr destinationFolder);
        void DeleteItem(IntPtr item, IntPtr progressSink);
        void DeleteItems(IntPtr items);
        void NewItem(IntPtr destinationFolder, uint attributes,
            [MarshalAs(UnmanagedType.LPWStr)] string name,
            [MarshalAs(UnmanagedType.LPWStr)] string templateName, IntPtr progressSink);
        void PerformOperations();
        void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
