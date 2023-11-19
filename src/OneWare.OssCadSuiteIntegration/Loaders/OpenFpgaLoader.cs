using Asmichi.ProcessManagement;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.OssCadSuiteIntegration.Loaders;

public class OpenFpgaLoader : IFpgaLoader
{
    private readonly IChildProcessService _childProcessService;
    public string Name => "OpenFpgaLoader";

    public OpenFpgaLoader(IChildProcessService childProcess)
    {
        _childProcessService = childProcess;
    }
    
    public async Task DownloadAsync(UniversalFpgaProjectRoot project)
    {
        await _childProcessService.ExecuteShellAsync("openFPGALoader", "-b ice40_generic ./build/pack.bin",
            project.FullPath, "Running OpenFPGALoader");
    }
}