﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace OneWare.Essentials.Controls;

public class SearchComboBox : ComboBox
{
    private TextBox? _searchBox;
    
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<SearchComboBox, bool>(nameof(IsInteractive), true);
    
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new SearchComboBoxItem();
    }
    
    private int _resultIndex = -1;

    public int ResultIndex
    {
        get => _resultIndex;
        set
        {
            _resultIndex = value;
            ResultItem = ContainerFromIndex(value) as SearchComboBoxItem;
            ScrollIntoView(value);
        }
    }

    private SearchComboBoxItem? _resultItem;
    
    public SearchComboBoxItem? ResultItem
    {
        get => _resultItem;
        private set
        {
            if(_resultItem != null) _resultItem.IsSearchResult = false;
            _resultItem = value;

            if (_resultItem != null)
            {
                _resultItem.IsSearchResult = true;
            }
        }
    }
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox")!;

        _searchBox!.TextChanged += (sender, args) =>
        {
            var item = Items.FirstOrDefault(x =>
                x?.ToString()?.StartsWith(_searchBox.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase) 
                ?? x?.ToString()?.Contains(_searchBox.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase) 
                ?? false);   
            
            if (IsInteractive)
            {
                SelectedItem = item;
            }
            else
            {
                if (item != null)
                {
                    ResultIndex = Items.IndexOf(item);
                }
            }
            
            _searchBox.Focus();
        };
        
        this.DropDownOpened += (sender, args) =>
        {
            _searchBox.Text = string.Empty;
            _searchBox.Focus();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(_searchBox == null) return;
        
        if (e.Key == Key.Down)
        {
            if (IsInteractive)
            {
                if (SelectedIndex < Items.Count - 1)
                    SelectedIndex++;
            }
            else
            {
                if (ResultIndex < Items.Count - 1)
                    ResultIndex++;
            }
  
            e.Handled = true;

            _searchBox.Focus();
            return;
        } 
        if (e.Key == Key.Up)
        {
            if (IsInteractive)
            {
                if (SelectedIndex > 0)
                    SelectedIndex--;
            }
            else
            {
                if (ResultIndex > 0)
                    ResultIndex--;
            }

            e.Handled = true;
            
            _searchBox.Focus();
            return;
        }
        if (e.Key == Key.Enter && !IsInteractive)
        {
            SelectedIndex = ResultIndex;
            _searchBox.Text = string.Empty;
        }
        if (e.Key == Key.Space && _searchBox.IsFocused)
        {
            return;
        }
        base.OnKeyDown(e);
    }
}