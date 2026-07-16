using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Debugger;

public class GdbDebugAdapter(ILogger logger, ISettingsService settingsService) : IDebugAdapter
{
    public string Id => "gdb-debug-adapter";
    public string DisplayName => "GDB-Debug-Adapter";
    public string Description => "GNU Debugger Adapter via MI - includes GDB Binary Resolution";
 
    public static string? SuggestGdbPath()
    {
        // Plattformüblicher Standard-Installationsort zuerst prüfen.
        string? defaultLocation = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            defaultLocation = @"C:\Program Files\gdb\bin\gdb.exe";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            defaultLocation = "/opt/homebrew/bin/gdb";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            defaultLocation = "/usr/bin/gdb";

        if (defaultLocation != null && File.Exists(defaultLocation))
            return defaultLocation;

        // Kein GDB am Standardort -> im System-PATH suchen.
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gdb.exe" : "gdb";
        return PlatformHelper.GetFullPath(binaryName); //if null "gdb"
    }
    

    private string ResolveGdbPath()
    {
        if (!settingsService.HasSetting(DebuggerModule.GdbPathSetting)) return SuggestGdbPath();
        var configured = settingsService.GetSettingValue<string>(DebuggerModule.GdbPathSetting);
        if (!string.IsNullOrWhiteSpace(configured) &&
            (File.Exists(configured) || PlatformHelper.ExistsOnPath(configured)))
            return configured;

        return SuggestGdbPath();
    }
    
    public static bool IsValidGdbInstallation(string gdbPath)
    {
        return File.Exists(gdbPath);
    }
}