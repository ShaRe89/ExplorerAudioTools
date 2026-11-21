// ExplorerAudioTools - native, lightweight Explorer COM verb adapter.
// Only collects the complete IShellItemArray selection and launches the
// existing C# batch executable once. No MP3 mutation takes place in Explorer.
#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <shobjidl.h>
#include <atomic>
#include <new>
#include <string>
#include <vector>

namespace
{
// Both CLSIDs must match Install-ExplorerAudioTools.cmd.
// A separate class identity lets Explorer dispatch each verb reliably.
constexpr CLSID kClsidRenameFromTags = {0xc78ffb2a, 0x2ceb, 0x475c,
    {0x92, 0xd9, 0xf8, 0x6a, 0x59, 0xc1, 0x06, 0x74}};
constexpr CLSID kClsidTagsFromFilename = {0xb8a64e28, 0x7451, 0x47c8,
    {0x86, 0xda, 0x8e, 0x94, 0xe7, 0x61, 0xd0, 0xc1}};
enum class Action { RenameFromTags, TagsFromFilename };
HMODULE g_module = nullptr;
std::atomic<long> g_objects{0};
std::atomic<long> g_locks{0};

// Writes an exact, complete selection in UTF-16 LE. If anything fails,
// deletion is attempted and the C# process is NOT started.
HRESULT SaveSelection(const std::vector<std::wstring>& paths, std::wstring& manifest)
{
    wchar_t folder[MAX_PATH]{};
    const DWORD chars = GetTempPathW(MAX_PATH, folder);
    if (chars == 0 || chars >= MAX_PATH)
        return HRESULT_FROM_WIN32(GetLastError() ? GetLastError() : ERROR_BUFFER_OVERFLOW);

    wchar_t file[MAX_PATH]{};
    if (!GetTempFileNameW(folder, L"EAT", 0, file))
        return HRESULT_FROM_WIN32(GetLastError());

    HANDLE handle = CreateFileW(file, GENERIC_WRITE, 0, nullptr, TRUNCATE_EXISTING,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (handle == INVALID_HANDLE_VALUE)
    {
        const HRESULT error = HRESULT_FROM_WIN32(GetLastError());
        DeleteFileW(file);
        return error;
    }

    bool ok = true;
    DWORD written = 0;
    constexpr BYTE bom[2] = {0xff, 0xfe};
    ok = WriteFile(handle, bom, sizeof(bom), &written, nullptr) && written == sizeof(bom);

    for (const auto& path : paths)
    {
        if (!ok || path.size() > (MAXDWORD / sizeof(wchar_t)) - 2)
        {
            ok = false;
            break;
        }
        const DWORD byteCount = static_cast<DWORD>(path.size() * sizeof(wchar_t));
        ok = WriteFile(handle, path.data(), byteCount, &written, nullptr) && written == byteCount;
        if (!ok)
            break;
        constexpr wchar_t separator[] = L"\r\n";
        ok = WriteFile(handle, separator, 2 * sizeof(wchar_t), &written, nullptr)
            && written == 2 * sizeof(wchar_t);
    }
    const DWORD saveError = ok ? ERROR_SUCCESS : (GetLastError() ? GetLastError() : ERROR_WRITE_FAULT);
    if (!CloseHandle(handle))
        ok = false;

    if (!ok)
    {
        DeleteFileW(file);
        return HRESULT_FROM_WIN32(saveError == ERROR_SUCCESS ? ERROR_WRITE_FAULT : saveError);
    }
    manifest = file;
    return S_OK;
}

HRESULT StartBatch(const std::wstring& manifest, Action action)
{
    wchar_t modulePath[32768]{};
    const DWORD chars = GetModuleFileNameW(g_module, modulePath, 32768);
    if (chars == 0 || chars >= 32768)
        return HRESULT_FROM_WIN32(GetLastError() ? GetLastError() : ERROR_INSUFFICIENT_BUFFER);
    std::wstring exe(modulePath);
    const auto slash = exe.find_last_of(L"\\/");
    if (slash == std::wstring::npos)
        return E_FAIL;
    exe.resize(slash + 1);
    exe += L"ExplorerAudioTools.exe";

    // Quote both paths; manifest filenames have no double-quote characters.
    std::wstring command = L"\"" + exe + L"\"";
    if (action == Action::TagsFromFilename)
        command += L" --tags-from-filename";
    command += L" --selection-file \"" + manifest + L"\"";
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(exe.c_str(), command.data(), nullptr, nullptr, FALSE,
                        0, nullptr, nullptr, &startup, &process))
        return HRESULT_FROM_WIN32(GetLastError());
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return S_OK;
}

class Command final : public IExecuteCommand, public IObjectWithSelection
{
public:
    explicit Command(Action action) : action_(action) { ++g_objects; }
    ~Command()
    {
        if (selection_) selection_->Release();
        --g_objects;
    }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, __uuidof(IExecuteCommand)))
            *output = static_cast<IExecuteCommand*>(this);
        else if (IsEqualIID(iid, __uuidof(IObjectWithSelection)))
            *output = static_cast<IObjectWithSelection*>(this);
        else return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs_; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG remaining = --refs_;
        if (!remaining) delete this;
        return remaining;
    }

    // IObjectWithSelection
    HRESULT STDMETHODCALLTYPE SetSelection(IShellItemArray* selection) override
    {
        if (selection) selection->AddRef();
        if (selection_) selection_->Release();
        selection_ = selection;
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE GetSelection(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        return selection_ ? selection_->QueryInterface(iid, output) : E_FAIL;
    }

    // IExecuteCommand; Explorer supplies optional context through these.
    HRESULT STDMETHODCALLTYPE SetKeyState(DWORD) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE SetParameters(LPCWSTR) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE SetPosition(POINT) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE SetShowWindow(int) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE SetNoShowUI(BOOL) override { return S_OK; }
    HRESULT STDMETHODCALLTYPE SetDirectory(LPCWSTR) override { return S_OK; }

    HRESULT STDMETHODCALLTYPE Execute() override
    {
        if (!selection_) return E_INVALIDARG;
        DWORD count = 0;
        HRESULT hr = selection_->GetCount(&count);
        if (FAILED(hr)) return hr;
        if (count == 0) return E_INVALIDARG;

        std::vector<std::wstring> paths;
        paths.reserve(count);
        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            hr = selection_->GetItemAt(i, &item);
            if (FAILED(hr)) return hr;
            PWSTR path = nullptr;
            hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
            item->Release();
            if (FAILED(hr)) return hr;
            if (!path) return E_FAIL;
            paths.emplace_back(path);
            CoTaskMemFree(path);
        }

        std::wstring manifest;
        hr = SaveSelection(paths, manifest);
        if (FAILED(hr)) return hr;
        hr = StartBatch(manifest, action_);
        if (FAILED(hr)) DeleteFileW(manifest.c_str());
        return hr;
    }

private:
    std::atomic<ULONG> refs_{1};
    IShellItemArray* selection_ = nullptr;
    Action action_;
};

class Factory final : public IClassFactory
{
public:
    explicit Factory(Action action) : action_(action) { ++g_objects; }
    ~Factory() { --g_objects; }

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (!IsEqualIID(iid, IID_IUnknown) && !IsEqualIID(iid, IID_IClassFactory))
            return E_NOINTERFACE;
        *output = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++refs_; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        const ULONG remaining = --refs_;
        if (!remaining) delete this;
        return remaining;
    }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (outer) return CLASS_E_NOAGGREGATION;
        Command* command = new (std::nothrow) Command(action_);
        if (!command) return E_OUTOFMEMORY;
        HRESULT hr = command->QueryInterface(iid, output);
        command->Release();
        return hr;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override
    {
        if (lock) ++g_locks;
        else --g_locks;
        return S_OK;
    }
private:
    std::atomic<ULONG> refs_{1};
    Action action_;
};
} // namespace

// Windows SDK already declares these functions via STDAPI (C linkage).
// Export undecorated COM entry points explicitly for the x64 DLL.
#pragma comment(linker, "/EXPORT:DllCanUnloadNow")
#pragma comment(linker, "/EXPORT:DllGetClassObject")

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow(void)
{
    return g_objects == 0 && g_locks == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(
    REFCLSID clsid, REFIID iid, void** output)
{
    if (!output) return E_POINTER;
    *output = nullptr;
    Action action;
    if (IsEqualCLSID(clsid, kClsidRenameFromTags))
        action = Action::RenameFromTags;
    else if (IsEqualCLSID(clsid, kClsidTagsFromFilename))
        action = Action::TagsFromFilename;
    else
        return CLASS_E_CLASSNOTAVAILABLE;
    Factory* factory = new (std::nothrow) Factory(action);
    if (!factory) return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(iid, output);
    factory->Release();
    return hr;
}
