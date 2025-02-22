﻿using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Collections;
using DynamicData.Binding;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using ListEx = DynamicData.ListEx;

namespace OneWare.ErrorList.ViewModels;

public enum ErrorListFilterMode
{
    All,
    CurrentProject,
    CurrentFile
}

public class ErrorListViewModel : ExtendedTool, IErrorService
{
    public const string IconKey = "MaterialDesign.ErrorOutline";

    private readonly IDockService _dockService;

    private readonly ObservableCollection<ErrorListItem> _items = new();
    private readonly IProjectExplorerService _projectExplorerExplorerViewModel;
    private readonly ISettingsService _settingsService;

    private int _errorCount;

    private bool _errorEnabled = true;

    private ErrorListFilterMode _errorListFilterMode;

    private string? _errorListVisibleSource = "All Sources";

    private int _hintCount;

    private bool _hintEnabled = true;

    private ErrorListItem? _selectedItem;

    private bool _showExternalErrors = true;

    private int _warningCount;

    private bool _warningEnabled = true;

    public ErrorListViewModel(IDockService dockService, ISettingsService settingsService,
        IProjectExplorerService projectExplorerExplorerViewModel) : base(IconKey)
    {
        _dockService = dockService;
        _settingsService = settingsService;
        _projectExplorerExplorerViewModel = projectExplorerExplorerViewModel;

        Id = "Problems";
        Title = "Problems";

        Collection = new DataGridCollectionView(_items, false, true)
        {
            Filter = Filter
        };

        Observable.FromEventPattern<IFile>(projectExplorerExplorerViewModel,
            nameof(projectExplorerExplorerViewModel.FileRemoved)).Subscribe(
            x => { Clear(x.EventArgs); });

        Observable.FromEventPattern<IProjectRoot>(projectExplorerExplorerViewModel,
            nameof(projectExplorerExplorerViewModel.ProjectRemoved)).Subscribe(
            x => { Clear(x.EventArgs); });

        _settingsService.Bind(ErrorListModule.KeyErrorListFilterMode, this.WhenValueChanged(x => x.ErrorListFilterMode))
            .Subscribe(x => ErrorListFilterMode = x);
        _settingsService.Bind(ErrorListModule.KeyErrorListShowExternalErrors,
                this.WhenValueChanged(x => x.ShowExternalErrors))
            .Subscribe(x => ShowExternalErrors = x);

        _dockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(_ => Filter());
    }

    public ObservableCollection<string> ErrorListVisibleSources { get; } = new() { "All Sources" };

    public ErrorListFilterMode ErrorListFilterMode
    {
        get => _errorListFilterMode;
        set
        {
            SetProperty(ref _errorListFilterMode, value);
            Filter();
        }
    }

    public bool ShowExternalErrors
    {
        get => _showExternalErrors;
        set
        {
            SetProperty(ref _showExternalErrors, value);
            Filter();
        }
    }

    public string? ErrorListVisibleSource
    {
        get => _errorListVisibleSource;
        set
        {
            SetProperty(ref _errorListVisibleSource, value);
            Filter();
        }
    }

    public string ErrorCountVisible
    {
        get
        {
            if (ErrorEnabled) return _errorCount + " Errors";
            return "0 of " + _errorCount + " Errors";
        }
    }

    public string WarningCountVisible
    {
        get
        {
            if (WarningEnabled) return _warningCount + " Warnings";
            return "0 of " + _warningCount + " Warnings";
        }
    }

    public string HintCountVisible
    {
        get
        {
            if (HintEnabled) return _hintCount + " Hints";
            return "0 of " + _hintCount + " Hints";
        }
    }

    public bool ErrorEnabled
    {
        get => _errorEnabled;
        set
        {
            SetProperty(ref _errorEnabled, value);
            ErrorRefresh?.Invoke(this, null);
            Filter();
        }
    }

    public bool WarningEnabled
    {
        get => _warningEnabled;
        set
        {
            SetProperty(ref _warningEnabled, value);
            ErrorRefresh?.Invoke(this, null);
            Filter();
        }
    }

    public bool HintEnabled
    {
        get => _hintEnabled;
        set
        {
            SetProperty(ref _hintEnabled, value);
            ErrorRefresh?.Invoke(this, null);
            Filter();
        }
    }

    public DataGridCollectionView Collection { get; }
    public string SearchString { get; set; } = "";

    public ErrorListItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public event EventHandler<object?>? ErrorRefresh;

    public void RegisterErrorSource(string source)
    {
        ErrorListVisibleSources.Add(source);
    }

    public void Clear(IFile file)
    {
        ListEx.RemoveMany(_items, _items.Where(x => x.File == file));
        ErrorRefresh?.Invoke(this, file);
        RefreshCountToggle();
    }

    public void Clear(string source)
    {
        var errors = _items.Where(x => x.Source == source).ToList();
        var files = errors.Select(x => x.File).Distinct();
        
        ListEx.RemoveMany(_items, errors);

        foreach (var file in files)
        {
            ErrorRefresh?.Invoke(this, file);
        }
        
        RefreshCountToggle();
    }

    public IEnumerable<ErrorListItem> GetErrorsForFile(IFile file)
    {
        foreach (var error in _items.Where(x => x.File == file))
        {
            if (ErrorEnabled && error.Type == ErrorType.Error) yield return error;
            if (WarningEnabled && error.Type == ErrorType.Warning) yield return error;
            if (HintEnabled && error.Type == ErrorType.Hint) yield return error;
        }
    }

    /// <summary>
    ///     Adds new Errors and filters old errors out
    /// </summary>
    public void RefreshErrors(IList<ErrorListItem> errors, string source, IFile entry)
    {
        ListEx.RemoveMany(_items, _items.Where(x => x.File == entry && x.Source == source && !errors.Contains(x)));

        foreach (var e in errors) Add(e);

        ErrorRefresh?.Invoke(this, entry);
        RefreshCountToggle();
    }

    private bool Filter(object arg)
    {
        if (arg is not ErrorListItem error) return false;
        var f = FilterMode(error) && FilterErrorSource(error) &&
                FilterSearchString(error) && FilterExternal(error);

        return f && FilterEnabledType(error);
    }

    private bool FilterMode(ErrorListItem error)
    {
        switch (ErrorListFilterMode)
        {
            case ErrorListFilterMode.All:
                return true;
            case ErrorListFilterMode.CurrentProject:
                if (error.File is IProjectFile pf && _projectExplorerExplorerViewModel.ActiveProject == pf.Root)
                    return true;
                break;
            case ErrorListFilterMode.CurrentFile:
                if ((_dockService.CurrentDocument as IEditor)?.CurrentFile == error.File) return true;
                break;
        }

        return false;
    }

    private bool FilterExternal(ErrorListItem error)
    {
        return ShowExternalErrors || error.File is IProjectFile || _dockService.OpenFiles.ContainsKey(error.File);
    }

    private bool FilterEnabledType(ErrorListItem error)
    {
        switch (error.Type)
        {
            case ErrorType.Error when ErrorEnabled:
            case ErrorType.Warning when WarningEnabled:
            case ErrorType.Hint when WarningEnabled:
                return true;
            default:
                return false;
        }
    }

    private bool FilterErrorSource(ErrorListItem error)
    {
        return ErrorListVisibleSource is "All Sources" || ErrorListVisibleSource == error.Source;
    }

    private bool FilterSearchString(ErrorListItem error)
    {
        if (string.IsNullOrWhiteSpace(SearchString)) return true;
        return error.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase) ||
               (error.Code?.Contains(SearchString, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private int CountToggle(ErrorType type)
    {
        return _items.Count(error =>
            error.Type == type && FilterMode(error) && FilterErrorSource(error) && FilterSearchString(error) &&
            FilterExternal(error));
    }

    public void Filter()
    {
        Collection.Refresh();
        RefreshCountToggle();
    }

    private void RefreshCountToggle()
    {
        _errorCount = CountToggle(ErrorType.Error);
        _hintCount = CountToggle(ErrorType.Hint);
        _warningCount = CountToggle(ErrorType.Warning);

        OnPropertyChanged(nameof(ErrorCountVisible));
        OnPropertyChanged(nameof(WarningCountVisible));
        OnPropertyChanged(nameof(HintCountVisible));

        Collection.SortDescriptions.Clear();
    }

    public void Clear(IProjectRoot project)
    {
        ListEx.RemoveMany(_items, _items.Where(x => x.File is IProjectFile pf && pf.Root == project));
        ErrorRefresh?.Invoke(this, project);
        RefreshCountToggle();
    }

    public void Clear(IProjectRoot project, string source)
    {
        ListEx.RemoveMany(_items,
            _items.Where(x => x.File is IProjectFile pf && pf.Root == project && x.Source == source));
        ErrorRefresh?.Invoke(this, project);
        RefreshCountToggle();
    }

    public void Add(ErrorListItem entry)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var comparison = entry.CompareTo(_items[i]);
            switch (comparison)
            {
                case 0: //Items equal
                    return;
                case < 0:
                    _items.Insert(i, entry);
                    return;
            }
        }

        _items.Add(entry);
    }

    public async Task GoToErrorAsync()
    {
        if (SelectedItem is not { } error) return;
        var doc = await _dockService.OpenFileAsync(error.File);

        if (doc is not IEditor evb) return;
        var offset = error.GetOffset(evb.CurrentDocument);
        evb.Select(offset.startOffset, offset.endOffset - offset.startOffset);
    }
}