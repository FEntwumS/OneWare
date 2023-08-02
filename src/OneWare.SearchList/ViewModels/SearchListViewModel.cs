﻿using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using DynamicData.Binding;
using OneWare.SearchList.Models;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.SearchList.ViewModels
{
    public class SearchListViewModel : ExtendedTool
    {
        public const string IconKey = "VsImageLib.Search16XMd";
        
        private IDockService _dockService;
        private IProjectExplorerService _projectExplorerService;
        
        private CancellationTokenSource? _lastCancellationToken;
        
        public string LastSearchString { get; set; } = "";
        
        public ObservableCollection<SearchResultModel> Items { get; } = new();

        private SearchResultModel? _selectedItem;
        public SearchResultModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _caseSensitive;
        [DataMember]
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => SetProperty(ref _caseSensitive, value);
        }

        private bool _wholeWord;
        [DataMember]
        public bool WholeWord
        {
            get => _wholeWord;
            set => SetProperty(ref _wholeWord, value);
        }

        private bool _useRegex;
        [DataMember]
        public bool UseRegex
        {
            get => _useRegex;
            set => SetProperty(ref _useRegex, value);
        }

        private int _searchListFilterMode = 1;
        [DataMember]
        public int SearchListFilterMode
        {
            get => _searchListFilterMode;
            set => SetProperty(ref _searchListFilterMode, value);
        }

        public SearchListViewModel(IDockService dockService, IProjectExplorerService projectExplorerService) : base(IconKey)
        {
            _dockService = dockService;
            _projectExplorerService = projectExplorerService;

            Title = "Search";
            Id = "Search";
        }
        
        public void Search(string searchText)
        {
            Items.Clear();
            _lastCancellationToken?.Cancel();
            if (searchText.Length < 3) return;
            _ = SearchAsync(searchText);
        }

        private async Task SearchAsync(string searchText)
        {
            IsLoading = true;

            _lastCancellationToken = new CancellationTokenSource();

            switch (SearchListFilterMode)
            {
                case 0:
                    await SearchFolderRecursiveAsync(_projectExplorerService.Items, searchText,
                        _lastCancellationToken.Token);
                    break;
                case 1 when _projectExplorerService.ActiveProject != null:
                    await SearchFolderRecursiveAsync(_projectExplorerService.ActiveProject.Items, searchText,
                        _lastCancellationToken.Token);
                    break;
                case 2 when _dockService.CurrentDocument is IEditor editor:
                    Items.AddRange(await FindAllIndexesAsync(editor.CurrentFile,
                        searchText, CaseSensitive, UseRegex, WholeWord, _lastCancellationToken.Token));
                    break;
            }

            IsLoading = false;
        }

        private async Task SearchFolderRecursiveAsync(IEnumerable<IProjectEntry> folderItems, string searchText,
            CancellationToken cancel)
        {
            var result = new List<SearchResultModel>();
            if (cancel.IsCancellationRequested) return;
            foreach (var i in folderItems)
                switch (i)
                {
                    case IProjectFile file:
                    {
                        if (file.Header.Contains(searchText,
                            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                        {
                            var sI = file.Header.IndexOf(searchText,
                                CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                            var dL = file.Header[..sI].TrimStart();
                            var dM = searchText;
                            var dR = file.Header[(sI + searchText.Length)..].TrimEnd();
                            Items.Add(new SearchResultModel(file.Header, dL, dM, dR, searchText,
                                file.Root, file));
                        }

                        Items.AddRange(await FindAllIndexesAsync(file, searchText, CaseSensitive, WholeWord, UseRegex,
                            cancel));
                        break;
                    }
                    case IProjectFolder folder:
                        await SearchFolderRecursiveAsync(folder.Items, searchText, cancel);
                        break;
                }
        }

        private static async Task<IList<SearchResultModel>> FindAllIndexesAsync(IFile file, string search,
            bool caseSensitive, bool regex, bool words, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(search) || !File.Exists(file.FullPath)) return new List<SearchResultModel>();

            var text = await File.ReadAllTextAsync(file.FullPath, cancellationToken);
            var lines = text.Split('\n');
            var lastIndex = 0;
            var lastLineNr = 0;

            return await Task.Run(() =>
            {
                var indexes = new List<SearchResultModel>();
                if (regex)
                {
                    var matches = Regex.Matches(text, search,
                        caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        if (cancellationToken.IsCancellationRequested) return indexes;
                        var index = match.Index;
                        if (index == -1) return indexes;
                        var lineNr = text[lastIndex..index].Split('\n').Length + lastLineNr - 1;
                        var line = lines[lineNr];

                        var lineM = Regex.Match(line, search,
                            caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                        var sI = lineM.Index;
                        var dL = line[..sI].TrimStart();
                        var dM = line[sI..(sI + lineM.Length)];
                        var dR = line[(sI + lineM.Length)..].TrimEnd();
                        indexes.Add(new SearchResultModel(line.Trim(), dL, dM, dR, search,
                            file is IProjectFile pf ? pf.Root : null, file, lineNr + 1, index, search.Length));
                        lastIndex = index;
                        lastLineNr = lineNr;
                    }
                }
                else
                {
                    for (var index = 0;; index += search.Length)
                    {
                        if (cancellationToken.IsCancellationRequested) return indexes;
                        index = text.IndexOf(search, index,
                            caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        if (index == -1) return indexes;
                        var lineNr = text[lastIndex..index].Split('\n').Length + lastLineNr - 1;
                        var line = lines[lineNr];


                        var sI = line.IndexOf(search,
                            caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        var dL = line[..sI].TrimStart();
                        var dM = search;
                        var dR = line[(sI + search.Length)..].TrimEnd();

                        lastIndex = index;
                        lastLineNr = lineNr;
                        if (words) //Check word boundary
                        {
                            if (index > 0 && char.IsLetterOrDigit(text[index - 1])) continue; //before
                            if (index + search.Length < text.Length &&
                                char.IsLetterOrDigit(text[index + search.Length])) continue; //before
                        }

                        indexes.Add(new SearchResultModel(line.Trim(), dL, dM, dR, search,
                            file is IProjectFile pf ? pf.Root : null, file, lineNr + 1, index, search.Length));
                    }
                }

                return indexes;
            }, cancellationToken);
        }
        
        public async Task GoToSearchResultAsync()
        {
            if (SelectedItem?.File == null) return;

            if(await _dockService.OpenFileAsync(SelectedItem.File) is not IEditor evb) return;

            if(_dockService.GetWindowOwner(this) is IHostWindow);
                _dockService.CloseDockable(this);
            
            //JUMP TO LINE
            if (SelectedItem.Line > 0)
            {
                if (SelectedItem.StartOffset == 0 && SelectedItem.EndOffset == 0)
                {
                    if (SelectedItem.Line <= evb.CurrentDocument.LineCount)
                    {
                        var line = evb.CurrentDocument.GetLineByNumber(SelectedItem.Line);
                        evb.Select(line.Offset, line.EndOffset - line.Offset);
                    }
                }
                else
                {
                    evb.Select(SelectedItem.StartOffset, SelectedItem.EndOffset - SelectedItem.StartOffset);
                }
            }
            
            if(evb.Owner?.Owner is IRootDock { Window: { Host: Window win } }) win.Activate();
        }
    }
}