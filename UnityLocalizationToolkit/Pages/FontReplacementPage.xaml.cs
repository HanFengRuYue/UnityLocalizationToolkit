using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using UnityLocalizationToolkit.Models;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit.Pages;

/// <summary>
/// 字体替换页面 - 用于扫描和替换游戏字体
/// </summary>
public sealed partial class FontReplacementPage : Page
{
    private GameProject? _currentProject;
    private FontAsset? _selectedFont;
    private readonly ObservableCollection<FontAsset> _fontAssets = [];
    private readonly ObservableCollection<FontAsset> _filteredFonts = [];

    public FontReplacementPage()
    {
        InitializeComponent();
        
        // 订阅扫描进度事件
        FontScannerService.Instance.ProgressChanged += OnScanProgressChanged;
        AssetModifierService.Instance.ProgressChanged += OnModifyProgressChanged;
    }

    /// <summary>
    /// 选择游戏目录
    /// </summary>
    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        folderPicker.FileTypeFilter.Add("*");

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        }

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            GamePathTextBox.Text = folder.Path;
            
            // 加载项目（如果尚未加载或路径不同）
            if (_currentProject == null || _currentProject.RootPath != folder.Path)
            {
                _currentProject = GameProjectService.Instance.LoadProject(folder.Path);
            }
            
            ScanFontsButton.IsEnabled = _currentProject.IsValid;
        }
    }

    /// <summary>
    /// 扫描字体资源
    /// </summary>
    private async void ScanFontsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        ScanFontsButton.IsEnabled = false;

        try
        {
            var fonts = await FontScannerService.Instance.ScanAsync(_currentProject);

            _fontAssets.Clear();
            foreach (var font in fonts)
            {
                _fontAssets.Add(font);
            }

            UpdateFontListView();
            FontCountBadge.Value = _fontAssets.Count;
        }
        catch (Exception ex)
        {
            ReplacementStatusInfoBar.Title = "扫描失败";
            ReplacementStatusInfoBar.Message = ex.Message;
            ReplacementStatusInfoBar.Severity = InfoBarSeverity.Error;
            ReplacementStatusInfoBar.IsOpen = true;
        }
        finally
        {
            ScanFontsButton.IsEnabled = true;
            ScanProgressBar.IsIndeterminate = false;
        }
    }

    /// <summary>
    /// 扫描进度变化回调
    /// </summary>
    private void OnScanProgressChanged(float progress, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ScanStatusText.Text = message;
            if (progress < 1)
            {
                ScanProgressBar.IsIndeterminate = true;
            }
            else
            {
                ScanProgressBar.IsIndeterminate = false;
                ScanProgressBar.Value = 100;
            }
        });
    }

    /// <summary>
    /// 修改进度变化回调
    /// </summary>
    private void OnModifyProgressChanged(float progress, string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ReplacementStatusInfoBar.Title = "替换中";
            ReplacementStatusInfoBar.Message = message;
            ReplacementStatusInfoBar.Severity = InfoBarSeverity.Informational;
            ReplacementStatusInfoBar.IsOpen = true;
        });
    }

    /// <summary>
    /// 更新字体列表视图
    /// </summary>
    private void UpdateFontListView()
    {
        _filteredFonts.Clear();

        var typeFilter = (FontTypeFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";

        var filtered = typeFilter switch
        {
            "Traditional" => _fontAssets.Where(f => f.FontType == FontType.Traditional),
            "TMP" => _fontAssets.Where(f => f.FontType == FontType.TMP),
            _ => _fontAssets
        };

        foreach (var font in filtered)
        {
            _filteredFonts.Add(font);
        }

        FontListView.ItemsSource = _filteredFonts;
        FontListView.ItemTemplate = CreateFontEntryTemplate();
    }

    /// <summary>
    /// 创建字体条目模板
    /// </summary>
    private DataTemplate CreateFontEntryTemplate()
    {
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
            <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <Grid Padding='16,8'>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='2*' />
                        <ColumnDefinition Width='*' />
                        <ColumnDefinition Width='*' />
                        <ColumnDefinition Width='*' />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column='0' Text='{Binding Name}' TextTrimming='CharacterEllipsis' />
                    <TextBlock Grid.Column='1' Text='{Binding DisplayType}' />
                    <TextBlock Grid.Column='2' Text='{Binding DisplayStatus}' />
                    <TextBlock Grid.Column='3' Text='{Binding AssociatedAssets}' TextTrimming='CharacterEllipsis' />
                </Grid>
            </DataTemplate>");
    }

    /// <summary>
    /// 字体列表选择变化
    /// </summary>
    private void FontListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedFont = FontListView.SelectedItem as FontAsset;
        
        if (_selectedFont != null)
        {
            SelectedFontPanel.Visibility = Visibility.Visible;
            NoSelectionInfoBar.IsOpen = false;
            SelectReplacementFontButton.IsEnabled = true;
            
            // 更新选中字体信息
            SelectedFontNameText.Text = _selectedFont.Name;
            SelectedFontTypeText.Text = _selectedFont.DisplayType;
            SelectedFontAssetsText.Text = _selectedFont.AssociatedAssets;
            
            // TMP字体显示额外信息
            TMPInfoBar.IsOpen = _selectedFont.FontType == FontType.TMP;
        }
        else
        {
            SelectedFontPanel.Visibility = Visibility.Collapsed;
            NoSelectionInfoBar.IsOpen = true;
            SelectReplacementFontButton.IsEnabled = false;
            ApplyReplacementButton.IsEnabled = false;
            TMPInfoBar.IsOpen = false;
        }
    }

    /// <summary>
    /// 选择替换字体文件
    /// </summary>
    private async void SelectReplacementFontButton_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new FileOpenPicker();
        openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        openPicker.FileTypeFilter.Add(".ttf");
        openPicker.FileTypeFilter.Add(".otf");

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
        }

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            ReplacementFontPathTextBox.Text = file.Path;
            ApplyReplacementButton.IsEnabled = _selectedFont != null;
        }
    }

    /// <summary>
    /// 应用字体替换
    /// </summary>
    private async void ApplyReplacementButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFont == null || string.IsNullOrEmpty(ReplacementFontPathTextBox.Text))
            return;

        ApplyReplacementButton.IsEnabled = false;

        try
        {
            var success = await AssetModifierService.Instance.ReplaceFontAsync(_selectedFont, ReplacementFontPathTextBox.Text);

            if (success)
            {
                ReplacementStatusInfoBar.Title = "替换成功";
                ReplacementStatusInfoBar.Message = $"字体 {_selectedFont.Name} 已成功替换，原文件已备份";
                ReplacementStatusInfoBar.Severity = InfoBarSeverity.Success;
            }
            else
            {
                if (_selectedFont.FontType == FontType.TMP)
                {
                    ReplacementStatusInfoBar.Title = "TMP字体替换需要额外处理";
                    ReplacementStatusInfoBar.Message = "TMP字体需要重新生成字体图集，请使用Unity编辑器或专门的TMP工具进行替换";
                    ReplacementStatusInfoBar.Severity = InfoBarSeverity.Warning;
                }
                else
                {
                    ReplacementStatusInfoBar.Title = "替换失败";
                    ReplacementStatusInfoBar.Message = "无法替换字体，请检查文件是否被占用";
                    ReplacementStatusInfoBar.Severity = InfoBarSeverity.Error;
                }
            }
            ReplacementStatusInfoBar.IsOpen = true;

            // 刷新列表
            UpdateFontListView();
        }
        catch (Exception ex)
        {
            ReplacementStatusInfoBar.Title = "替换失败";
            ReplacementStatusInfoBar.Message = ex.Message;
            ReplacementStatusInfoBar.Severity = InfoBarSeverity.Error;
            ReplacementStatusInfoBar.IsOpen = true;
        }
        finally
        {
            ApplyReplacementButton.IsEnabled = true;
        }
    }
}
