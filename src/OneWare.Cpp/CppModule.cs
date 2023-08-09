﻿using OneWare.Shared.Services;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.Cpp;

public class CppModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
         
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IModuleTracker>().RecordModuleInitialized(GetType().Name);
        
        containerProvider.Resolve<IErrorService>().RegisterErrorSource("Clang");
        containerProvider.Resolve<ILanguageManager>().RegisterService(typeof(LanguageServiceCpp),false, ".cpp", ".h", ".c", ".hpp");
    }
}