﻿using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;
using ExposedObject;
using OneWare.Core.Data;
using OneWare.Core.Models;
using OneWare.Core.ViewModels.DockViews;
using Prism.Ioc;
using OneWare.Core.Extensions;
using OneWare.ErrorList.ViewModels;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.Enums;
using OneWare.Shared.Extensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.Core.Views.DockViews
{
    public partial class EditView : UserControl
    {
        private readonly ISettingsService _settingsService;
        private readonly IErrorService _errorService;

        private ExtendedTextEditor CodeBox =>
            this.ViewModel?.Editor ?? throw new NullReferenceException(nameof(CodeBox));

        private ITypeAssistance? TypeAssistance => this.ViewModel?.TypeAssistance;

        private IFile CurrentFile =>
            this.ViewModel?.CurrentFile ?? throw new NullReferenceException(nameof(CurrentFile));


        private IEnumerable<int> _lastSearchResultLines = new List<int>();
        public EditViewModel? ViewModel { get; set; }
        public ObjectValueModel? VaribleViewDataConext { get; private set; }


        static IBrush _errorBrush = new SolidColorBrush(Color.FromArgb(150, 175, 50, 50));
        static IBrush _warningBrush = new SolidColorBrush(Color.FromArgb(150, 155, 155, 0));

        public EditView()
        {
            _settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
            _errorService = ContainerLocator.Container.Resolve<IErrorService>();
            
            InitializeComponent();

            var exposedMarkdown = Exposed.From(HoverTextBox);
            exposedMarkdown._viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

            this.DataContextChanged += (_, _) =>
            {
                if (DataContext is not EditViewModel evm) return;
                ViewModel = evm;

                Observable.FromEventPattern(h => CodeBox.LayoutUpdated += h, h => CodeBox.LayoutUpdated -= h)
                    .Take(1)
                    .Subscribe((x) => Setup());
            };
        }


        private void WindowPointerPressed(object? sender, PointerPressedEventArgs i)
        {
            if (!Equals(Tools.VisualUpwardSearch<Popup>(i.Source as Interactive), HoverBox)) HoverBox.Close();
        }

        protected void Setup()
        {
            CodeBox.SearchPanel.OnSearch += Search_Updated;

            //Attach Events
            AttachEvents();

            CodeBox.MarkerService.SetDiagnostics(ContainerLocator.Container.Resolve<ErrorListViewModel>()
                .GetErrorsForFile(CurrentFile));

            UpdateScrollInfo();

            try
            {
                var completion = new CompletionWindow(CodeBox)
                {
                    CloseWhenCaretAtBeginning = false,
                    AdditionalOffset = new Vector(0, 3),
                    MaxHeight = 225
                };
                TypeAssistance?.Initialize(completion);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }

            if (TypeAssistance == null)
            {
                // var lang = EditorThemeManager.Instance.Languages.FirstOrDefault(x =>
                //     x.Value.SupportedFiles.Contains(CurrentFile.Extension));
                //
                // if (!lang.IsDefault()) CodeBox.SyntaxHighlighting = lang.Value.SelectedTheme?.Load();
            }

            // Global.Breakpoints.WhenValueChanged(x => x.CurrentBreakPoint)
            //     .Subscribe(UpdateDebuggerLine)
            //     .DisposeWith(disposableRegistration);

            if (this.GetVisualRoot() is Window window)
                window
                    .AddHandler(PointerPressedEvent, WindowPointerPressed, RoutingStrategies.Tunnel);

            this.AddHandler(KeyDownEvent, (o, e) =>
            {
                if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl) return;
                if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
                _ = GetControlHoverActionAsync();
            }, RoutingStrategies.Bubble, true);

            this.AddHandler(KeyUpEvent, (o, e) =>
            {
                if (e.Key is Key.LeftCtrl or Key.RightCtrl)
                {
                    CodeBox.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
                    var changed = CodeBox.MarkerService.SetUnderlined();
                    if (changed) CodeBox.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                }
            }, RoutingStrategies.Bubble, true);

            //Zoom in ctrl + wheel
            CodeBox.AddHandler(PointerWheelChangedEvent, (o, i) =>
            {
                if (i.KeyModifiers != Global.ControlKey) return;
                var fontSize = _settingsService.GetSettingValue<int>("Editor_FontSize");
                if (i.Delta.Y > 0) _settingsService.SetSettingValue("Editor_FontSize", fontSize + 1);
                else _settingsService.SetSettingValue("Editor_FontSize", fontSize - 1);
                ;
                i.Handled = true;
            }, RoutingStrategies.Tunnel, true);
        }
        //-------------------------General + Events------------------------------------------//

        #region General

        protected void AttachEvents()
        {
            CodeBox.TextArea.TextView.ScrollOffsetChanged += ScrollOffsetChanged;
            CodeBox.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            CodeBox.TextArea.TextEntering += TextEditor_TextArea_TextEntering;
            CodeBox.TextArea.TextEntered += TextEditor_TextArea_TextEntered;
            CodeBox.Document.TextChanged += Text_Changed;
            CodeBox.PointerHover += Pointer_Hover;
            CodeBox.PointerMoved += Pointer_Moved;
            CodeBox.AddHandler(KeyDownEvent, TextBox_KeyDown, RoutingStrategies.Bubble, true);

            CodeBox.AddHandler(PointerPressedEvent, PointerPressedBeforeCaretUpdate, RoutingStrategies.Tunnel);
            CodeBox.AddHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate, RoutingStrategies.Bubble, true);

            _errorService.ErrorRefresh += SetErrors;
        }

        protected void DetachEvents()
        {
            CodeBox.TextArea.TextView.ScrollOffsetChanged -= ScrollOffsetChanged;
            CodeBox.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            CodeBox.TextArea.TextEntering -= TextEditor_TextArea_TextEntering;
            CodeBox.TextArea.TextEntered -= TextEditor_TextArea_TextEntered;
            CodeBox.Document.TextChanged -= Text_Changed;
            CodeBox.PointerHover -= Pointer_Hover;
            CodeBox.PointerMoved -= Pointer_Moved;
            CodeBox.RemoveHandler(KeyDownEvent, TextBox_KeyDown);

            CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedBeforeCaretUpdate);
            CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate);

            if (CodeBox.SearchPanel != null)
                CodeBox.SearchPanel.OnSearch -= Search_Updated;
            
            _errorService.ErrorRefresh -= SetErrors;
        }

        private void Search_Updated(object? sender, IEnumerable<SearchResult> results)
        {
            try
            {
                _lastSearchResultLines =
                    results.Select(x => CodeBox.Document.GetLineByOffset(x.StartOffset).LineNumber);
                UpdateScrollInfo();
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        // public void UpdateDebuggerLine(BreakPoint active)
        // {
        //     CodeBox.TextArea.TextView.LineTransformers.RemoveMany(CodeBox.TextArea.TextView.LineTransformers
        //         .Where(b => b is LineColorizer { Id: "DebuggerLine" }));
        //     if (active != null && active.File.EqualPaths(CurrentFile.FullPath))
        //         CodeBox.TextArea.TextView.LineTransformers.Add(
        //             new LineColorizer(active.Line, null,
        //                 Application.Current.FindResource("DebuggerBreakLine") as IBrush, "DebuggerLine"));
        // }

        public void PointerPressedAfterCaretUpdate(object? sender, PointerPressedEventArgs e)
        {
            _ = PointerPressedAfterCaretUpdateAsync(sender, e);
        }

        public void Text_Changed(object? sender, EventArgs e)
        {
            CodeBox.WordRenderer.SetHighlight(null); //Reset wordhighlight
            CodeBox.BracketRenderer.SetHighlight(null);

            UpdateScrollInfo();
        }

        public void ScrollOffsetChanged(object? sender, EventArgs e)
        {
            HoverBox.Close();
            CodeBox?.TextArea.ContextMenu?.Close();
        }

         protected void SetErrors(object? sender, object? e)
         {
             if (e == CurrentFile)
             {
                 var errorsForFile = ContainerLocator.Container.Resolve<ErrorListViewModel>().GetErrorsForFile(CurrentFile);
                 CodeBox.MarkerService.SetDiagnostics(errorsForFile);

                 UpdateScrollInfo();
                 CodeBox?.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
             }
         }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            //EditorWrapper.Content = null;
            DetachEvents();
            //TypeAssistance?.Close();
            base.OnDetachedFromVisualTree(e);
        }

        private string _enteredString = "";

        /// <summary>
        ///     Saves if CTRL + S is pressed
        /// </summary>
        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyModifiers == Global.ControlKey && e.KeyModifiers == KeyModifiers.Shift)
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                    Dispatcher.UIThread.Post(AutoFormat, DispatcherPriority.Background);

            if (e.Key == Key.V && e.KeyModifiers == Global.ControlKey)
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                    Dispatcher.UIThread.Post(() => _ = AutoFormatDelayAsync(), DispatcherPriority.Background);

            if (e.Key == Key.Back)
                if (_enteredString.Length > 0)
                    _enteredString = _enteredString.Remove(_enteredString.Length - 1);

            if (e.Key == Key.S && e.KeyModifiers == Global.ControlKey) _ = ViewModel?.SaveAsync();
        }

        #endregion

        //-------------------------Quick Menu----------------------------------------------//

        #region Quick Menu

        /// <summary>
        ///     Sets event to handled if Cursor is over Selection
        /// </summary>
        private void PointerPressedBeforeCaretUpdate(object? sender, PointerEventArgs e)
        {
            //Left Button
            if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
            }
            else if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
            {
                var pos = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)); //gets position of mouse

                if (pos.HasValue && !CodeBox.TextArea.Selection.IsEmpty) //Check if right click is on selection
                {
                    var mouseOffset = CodeBox.Document.GetOffset(pos.Value.Line, pos.Value.Column);
                    if (mouseOffset > 0 && mouseOffset < CodeBox.Document.TextLength)
                    {
                        var selectionStartOffset = CodeBox.Document.GetOffset(
                            CodeBox.TextArea.Selection.StartPosition.Line,
                            CodeBox.TextArea.Selection.StartPosition.Column);
                        var selectionEndOffset = CodeBox.Document.GetOffset(CodeBox.TextArea.Selection.EndPosition.Line,
                            CodeBox.TextArea.Selection.EndPosition.Column);
                        if (selectionStartOffset > selectionEndOffset)
                        {
                            (selectionStartOffset, selectionEndOffset) = (selectionEndOffset, selectionStartOffset);
                        }

                        if (mouseOffset >= selectionStartOffset && mouseOffset <= selectionEndOffset) e.Handled = true;
                    }
                }
            }
        }

        private async Task PointerPressedAfterCaretUpdateAsync(object? sender, PointerEventArgs e)
        {
            //Left Button
            if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
            {
                if (e.KeyModifiers == Global.ControlKey)
                    if (_controlAction != null)
                    {
                        _controlAction.Invoke();
                        e.Handled = true;
                    }

                //CodeBox.WordRenderer.SetHighlight(VhdpHelpers.SearchSelectedWord(CodeBox.Document, CodeBox.CaretOffset)); TODO
                UpdateScrollInfo();
            }
            else if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
            {
                var contextMenuList = new ObservableCollection<object>();

                // //Check for merge
                // foreach (var merge in CodeBox.MergeService.Merges)
                //     if (CodeBox.CaretOffset > merge.StartIndex && CodeBox.CaretOffset < merge.EndIndex)
                //         //contextMenuList.Add(new MenuItemViewModel
                //         //{
                //         //    Header = "Keep HEAD",
                //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepCurrent),
                //         //    CommandParameter = merge
                //         //});
                //         //contextMenuList.Add(new MenuItemViewModel
                //         //{
                //         //    Header = "Keep Incoming",
                //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepIncoming),
                //         //    CommandParameter = merge
                //         //});
                //         //contextMenuList.Add(new MenuItemViewModel
                //         //{
                //         //    Header = "Keep Both",
                //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepBoth),
                //         //    CommandParameter = merge
                //         //});
                //         //contextMenuList.Add(new Separator());
                //
                //         break;

                if (TypeAssistance != null)
                {
                    var languageSpecificItems = await TypeAssistance.GetQuickMenuAsync(CodeBox.CaretOffset);
                    if (languageSpecificItems is not null && languageSpecificItems.Count > 0)
                    {
                        contextMenuList.AddRange(languageSpecificItems);
                        contextMenuList.Add(new Separator());
                    }
                }

                HoverBox.IsVisible = false;

                contextMenuList.Add(new MenuItemViewModel
                {
                    Header = "Cut", Icon = this.FindResource("BoxIcons.RegularCut") as IImage,
                    Command = new RelayCommand(CodeBox.Cut)
                });
                contextMenuList.Add(new MenuItemViewModel
                {
                    Header = "Copy", Icon = this.FindResource("BoxIcons.RegularCopy") as IImage,
                    Command = new RelayCommand(CodeBox.Copy)
                });
                contextMenuList.Add(new MenuItemViewModel
                {
                    Header = "Paste", Icon = this.FindResource("BoxIcons.RegularPaste") as IImage,
                    Command = new RelayCommand(CodeBox.Paste)
                });
                if (TypeAssistance != null)
                {
                    contextMenuList.Add(new Separator());
                    contextMenuList.Add(new MenuItemViewModel
                    {
                        Header = "Comment", Icon = this.FindResource("VsImageLib.CommentCode16X") as IImage,
                        Command = new RelayCommand(TypeAssistance.Comment)
                    });
                    contextMenuList.Add(new MenuItemViewModel
                    {
                        Header = "Uncomment", Icon = this.FindResource("VsImageLib.UncommentCode16X") as IImage,
                        Command = new RelayCommand(TypeAssistance.Uncomment)
                    });
                }

                if (!CodeBox.TextArea.Selection.IsEmpty)
                {
                    if (TypeAssistance != null)
                    {
                        var startLine = CodeBox.TextArea.Selection.StartPosition.Line;
                        var endLine = CodeBox.TextArea.Selection.EndPosition.Line;
                        if (startLine > endLine)
                        {
                            (startLine, endLine) = (endLine, startLine);
                        }
                        contextMenuList.Add(new Separator());
                        contextMenuList.Add(new MenuItemViewModel
                        {
                            Header = "Format Selection",
                            Icon = this.FindResource("BoxIcons.RegularCode") as IImage,
                            Command = new RelayCommand(() => TypeAssistance.Format(startLine, endLine))
                        });
                    }
                       
                }

                if (contextMenuList.Count > 0)
                    CodeBox.TextArea.ContextMenu = new ContextMenu
                    {
                        ItemsSource = contextMenuList,
                        Classes = { "BindMenu" }
                    };
            }
        }

        //int clickOffset = -1;        

        private ErrorListItemModel? GetErrorAtMousePos(PointerEventArgs e)
        {
            var pos = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)); //gets position of mouse
            if (pos.HasValue)
            {
                var offset = CodeBox.Document.GetOffset(pos.Value.Location);
                var location = CodeBox.Document.GetLocation(offset);
                foreach (var error in ContainerLocator.Container.Resolve<ErrorListViewModel>()
                             .GetErrorsForFile(CurrentFile))
                    if (location.Line >= error.StartLine && location.Line <= error.EndLine &&
                        location.Column >= error.StartColumn && location.Column <= error.EndColumn)
                        return error;
            }

            return null;
        }

        #endregion

        //-------------------------Hover Info------------------------------------------------//

        #region Hover info

        private void Pointer_Hover(object? sender, PointerEventArgs e)
        {
            _ = TextEditorMouseHoverAsync(sender, e);
        }

        /// <summary>
        ///     closes toolTip if hover stopped
        /// </summary>
        private void Pointer_Moved(object? sender, PointerEventArgs e)
        {
            var word = CodeBox.GetWordAtMousePos(e);
            if (word == "" || word != _lastHoverWord) HoverBox.Close();

            _lastMovedArgs = e;

            if (e.KeyModifiers == Global.ControlKey) _ = GetControlHoverActionAsync();
        }

        private Action? _controlAction;
        private string? _lastHoverWord;
        private Range? _lastControlWordBounds;
        private PointerEventArgs? _lastMovedArgs;

        public async Task GetControlHoverActionAsync()
        {
            if (_lastMovedArgs == null) return;
            var wordBounds = CodeBox.GetWordRangeAtPointerPosition(_lastMovedArgs);
            var word = CodeBox.GetWordAtPointerPosition(_lastMovedArgs);

            if (word == null) return;

            //if (!word.All(Char.IsLetterOrDigit)) return;
            if (Regex.IsMatch(word, @"\W")) return;

            if (string.IsNullOrWhiteSpace(word))
            {
                _controlAction = null;
                _lastControlWordBounds = null;
            }

            if (TypeAssistance != null && !string.IsNullOrWhiteSpace(word) && wordBounds.HasValue &&
                (_lastControlWordBounds == null || !wordBounds.Value.Equals(_lastControlWordBounds.Value)))
            {
                _lastControlWordBounds = wordBounds;
                _controlAction =
                    await TypeAssistance.GetActionOnControlWordAsync(
                        CodeBox.GetOffsetFromPointerPosition(_lastMovedArgs));
                if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
            }

            if (_controlAction != null && _lastControlWordBounds.HasValue)
            {
                var changed = CodeBox.MarkerService.SetUnderlined(new TextMarkerService.TextMarker(
                    _lastControlWordBounds.Value.Start.Value,
                    _lastControlWordBounds.Value.End.Value) { Brush = Foreground });
                if (changed) CodeBox.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            }
        }

        /// <summary>
        ///     opens toolTip if there is information about the word the mouse hovers over
        /// </summary>
        private async Task TextEditorMouseHoverAsync(object? sender, PointerEventArgs e)
        {
            if (!Equals(e.Source, CodeBox.TextArea.TextView)) return;

            var offset = CodeBox.GetOffsetFromPointerPosition(e);

            var word = CodeBox.GetWordAtMousePos(e);

            if (offset <= 0 || word == _lastHoverWord && HoverBox.IsOpen) return;

            HoverTextBox.Markdown = "";

            if (word != null)
            {
                if (TypeAssistance != null)
                {
                    var info = await TypeAssistance.GetHoverInfoAsync(offset);
                    if (info != null && info.StartsWith("%object:")) //Show debugInfo
                    {
                        var endInfo = info.IndexOf('%', 1);
                        var value = info[(endInfo + 1)..];
                        var name = info[8..endInfo];
                        var vm = ObjectValueModel.ParseValue(name, value);
                        if (vm != null)
                        {
                            vm.IsExpanded = true;
                            VaribleViewDataConext = new ObjectValueModel
                            {
                                Children = new ObservableCollection<ObjectValueModel> { vm },
                                DisplayName = name
                            };
                            VariableControl.IsVisible = true;
                            HoverTextBox.IsVisible = false;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(info))
                    {
                        HoverTextBox.Markdown = info;
                        VariableControl.IsVisible = false;
                        HoverTextBox.IsVisible = true;
                    }
                }

                if (string.IsNullOrEmpty(HoverTextBox.Markdown) && !VariableControl.IsVisible)
                    if (GetErrorAtMousePos(e) is { } error)
                        HoverTextBox.Markdown = error.Description.Split('\n')[0];
                if ((!string.IsNullOrEmpty(HoverTextBox.Markdown) || VariableControl.IsVisible) &&
                    !(CodeBox.TextArea.ContextMenu?.IsOpen ?? false) && !string.IsNullOrEmpty(HoverTextBox.Markdown))
                {
                    UpdatePopupPositionToCursor(HoverBox, e);
                    if (IsEffectivelyVisible) HoverBox.Open();

                    e.Handled = true;
                }
                else
                {
                    HoverBox.Close();
                }
            }
            else
            {
                HoverBox.Close();
            }

            _lastHoverWord = word;
        }

        private void UpdatePopupPositionToCursor(Popup popup, PointerEventArgs e)
        {
            var textPosition = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)) ?? new TextViewPosition(1, 1);
            var visualPosition = CodeBox.TextArea.TextView.GetVisualPosition(textPosition, VisualYPosition.LineBottom);
            visualPosition -= CodeBox.TextArea.TextView.ScrollOffset;
            popup.VerticalOffset = visualPosition.Y;
            popup.HorizontalOffset = visualPosition.X;
            popup.PlacementTarget = CodeBox.TextArea.TextView;
            popup.Placement = PlacementMode.AnchorAndGravity;
            popup.PlacementAnchor = PopupAnchor.TopLeft;
            popup.PlacementGravity = PopupGravity.BottomRight;
        }

        #endregion

        //-------------------------Highlighting----------------------------------------------//

        #region Highlighting

        private int _lastLength = -1;
        private int _lastChar = ' ';

        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            ContainerLocator.Current.Resolve<IDockService>().CurrentDocument = null;
            ContainerLocator.Current.Resolve<IDockService>().CurrentDocument = ViewModel;

            if (true)
                if (CodeBox.Text.Length - _lastLength == -1 && CodeBox.CaretOffset > -1 &&
                    CodeBox.CaretOffset < CodeBox.Text.Length)
                {
                    if (_lastChar == '´')
                    {
                        var lastStart = CodeBox.Text.LastIndexOf('´', CodeBox.CaretOffset);
                        var lastLine = CodeBox.Text.LastIndexOf('\n', CodeBox.CaretOffset - 1);
                        if (lastStart > -1 && lastStart > lastLine)
                        {
                            if (lastLine < 0) lastLine = 0;
                            var str = CodeBox.Text.Substring(lastLine, lastStart - lastLine);
                            var pl = str.Count(x => x == '´');
                            if (pl % 2 == 0)
                            {
                                CodeBox.Document.Remove(lastStart, CodeBox.CaretOffset - lastStart);
                                return;
                            }
                        }

                        var nextStart = CodeBox.Text.IndexOf('´', CodeBox.CaretOffset + 1);
                        var nextLine = CodeBox.Text.IndexOf('\n', CodeBox.CaretOffset + 1);
                        if (nextStart > -1 && nextStart < nextLine)
                            CodeBox.Document.Remove(CodeBox.CaretOffset, nextStart - CodeBox.CaretOffset + 1);
                    }
                }

            _lastLength = CodeBox.Text.Length;
            if (CodeBox.CaretOffset - 1 > -1) _lastChar = CodeBox.Text[CodeBox.CaretOffset - 1];

            CodeBox.TextArea.ContextMenu?.Close();
            HoverBox.Close();

            var searcher = new CBracketSearcher();
            CodeBox.BracketRenderer.SetHighlight(searcher.SearchBracket(CodeBox.Document, CodeBox.CaretOffset));
        }

        #endregion

        //-------------------------Type Assistance-------------------------------------------//

        #region Type Assistance

        private readonly IBrush _wordResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#502859af")!;
        private readonly IBrush _searchResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#50af7e28")!;

        /// <summary>
        ///     Shows erros in scrollbar
        /// </summary>
        public void UpdateScrollInfo()
        {
            if (ViewModel == null) return;

            Dictionary<IBrush, int[]> scrollInfo = new();

            var errors = ContainerLocator.Container.Resolve<ErrorListViewModel>().GetErrorsForFile(CurrentFile)
                .ToList();

            var errorLines = errors
                .Where(x => x.Type is ErrorType.Error)
                .Select(x => x.StartLine)
                .Distinct().ToArray();

            var warningLines = errors
                .Where(x => x.Type is ErrorType.Warning)
                .Select(x => x.StartLine)
                .Distinct().ToArray();

            scrollInfo.Add(_errorBrush, errorLines);
            scrollInfo.Add(_warningBrush, warningLines);

            if (CodeBox.WordRenderer.Result != null)
            {
                scrollInfo.Add(_wordResultScrollBrush, CodeBox.WordRenderer.Result.WordOffset
                    .Select(x => CodeBox.Document.GetLineByOffset(x).LineNumber)
                    .Distinct()
                    .ToArray());
            }

            scrollInfo.Add(_searchResultScrollBrush, _lastSearchResultLines.ToArray());

            ViewModel.ScrollInfo = scrollInfo;
        }

        /// <summary>
        /// Fills in Data for the Completion Window
        /// </summary>
        private void TextEditor_TextArea_TextEntered(object? sender, TextInputEventArgs e)
        {
            //Apply Caret difference
            if (CodeBox.CaretOffset + _caretDiff < 0) CodeBox.CaretOffset = 0;
            else if (CodeBox.CaretOffset + _caretDiff > CodeBox.Text.Length) CodeBox.CaretOffset = CodeBox.Text.Length;
            else CodeBox.CaretOffset += _caretDiff;
            _caretDiff = 0;

            //Language Specific Type Assistance
            _ = TypeAssistance?.TextEnteredAsync(e);

            #region Detect Auto Format / Language Specific?

            _enteredString += e.Text;
            var startOffset = -1;
            if (e.Text == "}")
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("{", StringComparison.Ordinal);
            else if (e.Text == ")")
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("(", StringComparison.Ordinal);
            else if (_enteredString.Contains("#endregion"))
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("#region", StringComparison.Ordinal);
            if (_enteredString.Length > 10) _enteredString = _enteredString.Remove(0, 1);

            if (startOffset >= 0)
            {
                var startLineNumber = CodeBox.Document.GetLineByOffset(startOffset).LineNumber;
                var endLineNumber = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                    TypeAssistance?.Format(startLineNumber, endLineNumber);
            }

            #endregion
        }

        //Difference between TextEntering and TextEntered
        private int _caretDiff;

        private void TextEditor_TextArea_TextEntering(object? sender, TextInputEventArgs e)
        {
            if (e.Text is not { } input) return;
            
            //TODO 
            if (CodeBox.CaretOffset > 1 && CodeBox.CaretOffset <= CodeBox.Text.Length)
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoBracket"))
                {
                    if (e.Text[0] == '(')
                    {
                        input += ')';
                        _caretDiff = -1;
                    }
                    else if (e.Text[0] == '{')
                    {
                        input += '}';
                        _caretDiff = -1;
                    }
                    else if (e.Text[0] == ')' && CodeBox.CaretOffset > 1 &&
                             CodeBox.CaretOffset < CodeBox.Document.TextLength &&
                             CodeBox.Text[CodeBox.CaretOffset] == ')')
                    {
                        var br = 0;
                        for (var i = CodeBox.CaretOffset - 1; i >= 0; i--)
                        {
                            if (CodeBox.Text[i] == '{') break;
                            if (CodeBox.Text[i] == '(') br++;
                            if (CodeBox.Text[i] == ')') br--;
                        }

                        if (br > 0)
                        {
                            input = "";
                            _caretDiff = 1;
                            return; //Dont continue without text           
                        }
                    }
                    else if (e.Text[0] == '}' && CodeBox.CaretOffset > 1 &&
                             CodeBox.Text[CodeBox.CaretOffset - 1] == '{')
                    {
                        input = "";
                        _caretDiff = 1;
                        return; //Dont continue without text
                    }
                }

            //Language Specific TypeAssistance
            TypeAssistance?.TextEntering(e);
        }

        /// <summary>
        /// Auto format entire document
        /// </summary>
        public void AutoFormat()
        {
            if (!CodeBox.IsReadOnly)
                TypeAssistance?.Format();
        }

        public async Task AutoFormatDelayAsync()
        {
            await Task.Delay(50);
            TypeAssistance?.Format();
        }

        #endregion
    }
}