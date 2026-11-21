using System.Runtime.InteropServices;
using System.IO;

namespace ExplorerAudioTools;

/// <summary>
/// Performs the rename via the Windows Shell so Windows can place it in the
/// Explorer session's Undo history. No console or PowerShell is launched.
/// </summary>
internal static class ShellRename
{
    // Windows 8+: mark this as a user-invoked operation eligible for Explorer Undo.
    private const uint FOFX_ADDUNDORECORD = 0x20000000;
    private const uint FOF_SILENT = 0x0004;
    private const uint FOF_NOERRORUI = 0x0400;
    private const uint FOFX_EARLYFAILURE = 0x00100000;
    private const uint FOF_NOCONFIRMATION = 0x0010;

    internal static void Rename(string source, string newName)
    {
        var itemId = typeof(IShellItem).GUID;
        SHCreateItemFromParsingName(source, IntPtr.Zero, ref itemId, out IShellItem item);
        IFileOperation operation = (IFileOperation)new FileOperationComObject();
        try
        {
            operation.SetOperationFlags(FOFX_ADDUNDORECORD | FOF_SILENT |
                FOF_NOERRORUI | FOFX_EARLYFAILURE | FOF_NOCONFIRMATION);
            operation.RenameItem(item, newName, IntPtr.Zero);
            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out bool aborted);

            var destination = Path.Combine(Path.GetDirectoryName(source)!, newName);
            if (aborted || !File.Exists(destination) ||
                (!string.Equals(source, destination, StringComparison.OrdinalIgnoreCase) && File.Exists(source)))
                throw new IOException("Windows konnte die Umbenennung nicht abschließen.");
        }
        finally
        {
            Marshal.ReleaseComObject(operation);
            Marshal.ReleaseComObject(item);
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
