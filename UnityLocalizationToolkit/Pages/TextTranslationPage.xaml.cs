using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using UnityLocalizationToolkit.Models;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit.Pages;

/// <summary>
/// 文本翻译页面 - 用于扫描、导出和导入游戏文本
/// </summary>
public sealed partial class TextTranslationPage : Page
{
    private GameProject? _currentProject;
    private CancellationTokenSource? _scanCancellation;
    private readonly ObservableCollection<TextEntry> _textEntries = [];
    private readonly ObservableCollection<TextEntry> _filteredEntries = [];

    public TextTranslationPage()
    {
        InitializeComponent();
        
        // 订阅扫描进度事件
        TextScannerService.Instance.ProgressChanged += OnScanProgressChanged;
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
            
            // 加载项目
            _currentProject = GameProjectService.Instance.LoadProject(folder.Path);
            
            // 更新后端类型显示
            if (_currentProject.IsValid)
            {
                BackendInfoBar.Title = "后端类型";
                BackendInfoBar.Message = GameProjectService.GetBackendDisplayName(_currentProject.BackendType);
                BackendInfoBar.Severity = InfoBarSeverity.Success;
                ScanButton.IsEnabled = true;
            }
            else
            {
                BackendInfoBar.Title = "警告";
                BackendInfoBar.Message = "未能识别Unity游戏目录";
                BackendInfoBar.Severity = InfoBarSeverity.Warning;
                ScanButton.IsEnabled = false;
            }
        }
    }

    /// <summary>
    /// 获取当前选择的源语言
    /// </summary>
    private SourceLanguage GetSelectedSourceLanguage()
    {
        if (SourceLanguageComboBox.SelectedItem is ComboBoxItem item)
        {
            return item.Tag?.ToString() switch
            {
                "ja" => SourceLanguage.Japanese,
                "en" => SourceLanguage.English,
                "ko" => SourceLanguage.Korean,
                "zh-CN" => SourceLanguage.ChineseSimplified,
                "zh-TW" => SourceLanguage.ChineseTraditional,
                _ => SourceLanguage.Other
            };
        }
        return SourceLanguage.Japanese;
    }

    /// <summary>
    /// 开始扫描游戏文本
    /// </summary>
    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        _scanCancellation = new CancellationTokenSource();
        ScanButton.IsEnabled = false;
        StopScanButton.IsEnabled = true;
        ExportButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;

        try
        {
            var sourceLanguage = GetSelectedSourceLanguage();
            var entries = await TextScannerService.Instance.ScanAsync(_currentProject, sourceLanguage, _scanCancellation.Token);

            // 更新数据
            _textEntries.Clear();
            foreach (var entry in entries)
            {
                _textEntries.Add(entry);
            }

            // 更新UI
            UpdateTextListView();
            TextCountBadge.Value = _textEntries.Count(e => !e.ShouldSkip);
            
            ExportButton.IsEnabled = _textEntries.Count > 0;
            ImportButton.IsEnabled = _textEntries.Count > 0;
        }
        catch (OperationCanceledException)
        {
            ScanStatusText.Text = "扫描已取消";
        }
        catch (Exception ex)
        {
            ExportImportInfoBar.Title = "错误";
            ExportImportInfoBar.Message = $"扫描失败: {ex.Message}";
            ExportImportInfoBar.Severity = InfoBarSeverity.Error;
            ExportImportInfoBar.IsOpen = true;
        }
        finally
        {
            ScanButton.IsEnabled = true;
            StopScanButton.IsEnabled = false;
            ScanProgressBar.IsIndeterminate = false;
        }
    }

    /// <summary>
    /// 停止扫描
    /// </summary>
    private void StopScanButton_Click(object sender, RoutedEventArgs e)
    {
        _scanCancellation?.Cancel();
        ScanButton.IsEnabled = true;
        StopScanButton.IsEnabled = false;
        ScanStatusText.Text = "扫描已停止";
        ScanProgressBar.IsIndeterminate = false;
        ScanProgressBar.Value = 0;
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
    /// 更新文本列表视图
    /// </summary>
    private void UpdateTextListView()
    {
        _filteredEntries.Clear();

        var typeFilter = (TextTypeFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";

        var filtered = _textEntries.Where(e => !e.ShouldSkip);

        // 类型筛选
        filtered = typeFilter switch
        {
            "Script" => filtered.Where(e => e.SourceType == TextSourceType.Script),
            "MonoBehaviour" => filtered.Where(e => e.SourceType == TextSourceType.MonoBehaviour),
            "TextAsset" => filtered.Where(e => e.SourceType == TextSourceType.TextAsset),
            _ => filtered
        };

        // 搜索筛选
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(e => 
                e.OriginalText.ToLowerInvariant().Contains(searchText) ||
                e.TranslatedText.ToLowerInvariant().Contains(searchText));
        }

        foreach (var entry in filtered.Take(1000)) // 限制显示数量
        {
            _filteredEntries.Add(entry);
        }

        // 更新ListView
        TextListView.ItemsSource = _filteredEntries;
        TextListView.ItemTemplate = CreateTextEntryTemplate();
    }

    /// <summary>
    /// 创建文本条目模板
    /// </summary>
    private DataTemplate CreateTextEntryTemplate()
    {
        // 使用代码创建简单的模板
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(@"
            <DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <Grid Padding='16,8'>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width='3*' />
                        <ColumnDefinition Width='*' />
                        <ColumnDefinition Width='2*' />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column='0' Text='{Binding OriginalText}' TextTrimming='CharacterEllipsis' />
                    <TextBlock Grid.Column='1' Text='{Binding SourceType}' />
                    <TextBlock Grid.Column='2' Text='{Binding DisplayLocation}' TextTrimming='CharacterEllipsis' />
                </Grid>
            </DataTemplate>");
    }

    /// <summary>
    /// 导出到Excel
    /// </summary>
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new FileSavePicker();
        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("Excel文件", [".xlsx"]);
        savePicker.SuggestedFileName = "GameTexts";

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
        }

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            try
            {
                ExportImportInfoBar.Title = "导出中";
                ExportImportInfoBar.Message = "正在导出Excel文件...";
                ExportImportInfoBar.Severity = InfoBarSeverity.Informational;
                ExportImportInfoBar.IsOpen = true;

                await ExcelService.Instance.ExportAsync([.. _textEntries], file.Path);

                ExportImportInfoBar.Title = "导出成功";
                ExportImportInfoBar.Message = $"已导出 {_textEntries.Count(e => !e.ShouldSkip)} 条文本至: {file.Path}";
                ExportImportInfoBar.Severity = InfoBarSeverity.Success;
            }
            catch (Exception ex)
            {
                ExportImportInfoBar.Title = "导出失败";
                ExportImportInfoBar.Message = ex.Message;
                ExportImportInfoBar.Severity = InfoBarSeverity.Error;
            }
        }
    }

    /// <summary>
    /// 从Excel导入
    /// </summary>
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new FileOpenPicker();
        openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        openPicker.FileTypeFilter.Add(".xlsx");

        var window = App.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);
        }

        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                ExportImportInfoBar.Title = "导入中";
                ExportImportInfoBar.Message = "正在导入Excel文件...";
                ExportImportInfoBar.Severity = InfoBarSeverity.Informational;
                ExportImportInfoBar.IsOpen = true;

                var updatedCount = await ExcelService.Instance.ImportAsync([.. _textEntries], file.Path);

                ExportImportInfoBar.Title = "导入成功";
                ExportImportInfoBar.Message = $"已导入 {updatedCount} 条翻译";
                ExportImportInfoBar.Severity = InfoBarSeverity.Success;

                ApplyButton.IsEnabled = updatedCount > 0;
                UpdateTextListView();
            }
            catch (Exception ex)
            {
                ExportImportInfoBar.Title = "导入失败";
                ExportImportInfoBar.Message = ex.Message;
                ExportImportInfoBar.Severity = InfoBarSeverity.Error;
            }
        }
    }

    /// <summary>
    /// 应用修改
    /// </summary>
    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null) return;

        try
        {
            ApplyButton.IsEnabled = false;
            ExportImportInfoBar.Title = "应用中";
            ExportImportInfoBar.Message = "正在将翻译应用到游戏文件...";
            ExportImportInfoBar.Severity = InfoBarSeverity.Informational;
            ExportImportInfoBar.IsOpen = true;

            var modifiedCount = await AssetModifierService.Instance.ApplyTextTranslationsAsync(_currentProject, [.. _textEntries]);

            ExportImportInfoBar.Title = "应用成功";
            ExportImportInfoBar.Message = $"已成功修改 {modifiedCount} 条文本，原文件已备份";
            ExportImportInfoBar.Severity = InfoBarSeverity.Success;
        }
        catch (Exception ex)
        {
            ExportImportInfoBar.Title = "应用失败";
            ExportImportInfoBar.Message = ex.Message;
            ExportImportInfoBar.Severity = InfoBarSeverity.Error;
        }
        finally
        {
            ApplyButton.IsEnabled = true;
        }
    }
}
