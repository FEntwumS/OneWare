﻿using Avalonia.Media;
using OneWare.Settings.ViewModels.SettingTypes;
using OneWare.Shared.Controls;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Settings.ViewModels
{
    public class ApplicationSettingsViewModel : FlexibleWindowViewModelBase
    {
        public List<SettingsPageViewModel> SettingPages { get; } = new();

        private SettingsPageViewModel? _selectedPage;
        public SettingsPageViewModel? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (value == null) return;
                SetProperty(ref _selectedPage, value);
            }
        }

        private object? _selectedItem = new();
        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                SelectedPage = value as SettingsPageViewModel;
                if (SelectedPage != null && value is SettingsCollectionViewModel sub)
                {
                    SelectedPage = SettingPages.FirstOrDefault(x => x.SettingCollections.Contains(sub));
                }
                SetProperty(ref _selectedItem, value);
                if(SelectedPage != null) SelectedPage.IsExpanded = true;
            }
        }

        private readonly ISettingsService _settingsService;
        private readonly IPaths _paths;
        private readonly IWindowService _windowService;

        public ApplicationSettingsViewModel(ISettingsService settingsService, IPaths paths, IWindowService windowService)
        {
            Id = "Settings";
            Title = "Settings";
            
            _settingsService = settingsService;
            _paths = paths;
            _windowService = windowService;

            var s = settingsService as SettingsService;
            if (s == null) return;
            
            foreach (var category in s.SettingCategories.OrderBy(x => x.Value.Priority))
            {
                var pageModel = new SettingsPageViewModel(category.Key, category.Value.IconKey);

                foreach (var subCategory in category.Value.SettingSubCategories.OrderBy(x => x.Value.Priority))
                {
                    var subCategoryModel = new SettingsCollectionViewModel(subCategory.Key, subCategory.Value.IconKey);
                    
                    foreach (var setting in subCategory.Value.Settings)
                    {
                        if (setting is ComboBoxSetting cS)
                        {
                            subCategoryModel.SettingModels.Add(new ComboBoxSettingViewModel(cS));
                        }
                        else
                        {
                            switch (setting.Value)
                            {
                                case bool:
                                    subCategoryModel.SettingModels.Add(new CheckBoxSettingViewModel(setting));
                                    break;
                                case string:
                                case int:
                                case float:
                                case double:
                                    subCategoryModel.SettingModels.Add(new TextBoxSettingViewModel(setting));
                                    break;
                                case Color:
                                    subCategoryModel.SettingModels.Add(new ColorPickerSettingViewModel(setting));
                                    break;
                            }
                        }
                    }
                    
                    pageModel.SettingCollections.Add(subCategoryModel);
                }

                SettingPages.Add(pageModel);
            }

            SelectedPage = SettingPages.FirstOrDefault();
        }
        
        public void Okay(FlexibleWindow window)
        {
            this.Close(window);
            _settingsService.Save(_paths.SettingsPath);
        }

        public async Task ResetDialogAsync()
        {
            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                "Are you sure you want to reset all settings? Paths will not be affected by this!", MessageBoxIcon.Warning);
            
            if(result == MessageBoxStatus.Yes)
                _settingsService.Reset();
        }
    }
}