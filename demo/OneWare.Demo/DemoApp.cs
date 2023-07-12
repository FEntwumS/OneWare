using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using OneWare.Core;
using OneWare.Core.Data;
using OneWare.Core.Services;
using OneWare.Cpp;
using OneWare.Settings;
using OneWare.Shared.Services;
using OneWare.Verilog;
using OneWare.Vhdl;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Demo;

public class DemoApp : App
{
    public static readonly ISettingsService SettingsService = new SettingsService();
    
    public static readonly IPaths Paths = new Paths("OneWare Studio", "avares://OneWare.Demo/Assets/icon.ico",
        "avares://OneWare.Demo/Assets/Startup.jpg");

    private static readonly ILogger Logger = new Logger(Paths);

    static DemoApp()
    {
        SettingsService.Register("LastVersion", Global.VersionCode);
        SettingsService.RegisterSettingCategory("Experimental", 100, "MaterialDesign.Build");
        SettingsService.RegisterTitled("Experimental", "Misc", "Experimental_UseManagedFileDialog", "Use Managed File Dialog",
            "", RuntimeInformation.IsOSPlatform(OSPlatform.Linux));
        SettingsService.Load(Paths.SettingsPath);
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterInstance(SettingsService);
        containerRegistry.RegisterInstance(Paths);
        containerRegistry.RegisterInstance(Logger);
        
        base.RegisterTypes(containerRegistry);
    }

    public override void Initialize()
    {
        var themeManager = new ThemeManager(SettingsService, Paths);
        base.Initialize();
        
        this.Styles.Add(new StyleInclude(new Uri("avares://OneWare.Demo"))
        {
            Source = new Uri("avares://OneWare.Demo/Styles/Theme.axaml")
        });
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        base.ConfigureModuleCatalog(moduleCatalog);
        moduleCatalog.AddModule<VhdlModule>();
        moduleCatalog.AddModule<VerilogModule>();
    }
}