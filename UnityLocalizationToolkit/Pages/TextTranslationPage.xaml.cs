using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using UnityLocalizationToolkit.Models;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit.Pages;

/// <summary>
/// 扫描选项配置
/// </summary>
public class ScanOptions
{
    public bool ScanAssembly { get; set; } = true;
    public bool ScanMonoBehaviour { get; set; } = true;
    public bool ScanTextAsset { get; set; } = true;
    public int MinTextLength { get; set; } = 2;
    public bool UseEngineKeywords { get; set; } = true;
    public HashSet<string> CustomKeywords { get; set; } = [];
    public List<Regex> CustomPatterns { get; set; } = [];
}

/// <summary>
/// 文本翻译页面 - 用于扫描、导出和导入游戏文本
/// </summary>
public sealed partial class TextTranslationPage : Page
{
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
    /// 页面导航到时检查项目状态
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        UpdateProjectStatus();
    }

    /// <summary>
    /// 更新项目状态显示
    /// </summary>
    private void UpdateProjectStatus()
    {
        var project = GameProjectService.Instance.CurrentProject;
        
        if (project != null && project.IsValid)
        {
            ProjectStatusInfoBar.Title = $"当前项目: {GameProjectService.GetBackendDisplayName(project.BackendType)}";
            ProjectStatusInfoBar.Message = project.RootPath;
            ProjectStatusInfoBar.Severity = InfoBarSeverity.Success;
            ScanButton.IsEnabled = true;
        }
        else
        {
            ProjectStatusInfoBar.Title = "提示";
            ProjectStatusInfoBar.Message = "请先在主页中选择游戏目录";
            ProjectStatusInfoBar.Severity = InfoBarSeverity.Warning;
            ScanButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// 从UI收集扫描选项
    /// </summary>
    private ScanOptions CollectScanOptions()
    {
        var options = new ScanOptions
        {
            ScanAssembly = ScanAssemblyCheckBox.IsChecked ?? true,
            ScanMonoBehaviour = ScanMonoBehaviourCheckBox.IsChecked ?? true,
            ScanTextAsset = ScanTextAssetCheckBox.IsChecked ?? true,
            MinTextLength = (int)(MinTextLengthBox.Value is double.NaN ? 2 : MinTextLengthBox.Value),
            UseEngineKeywords = UseEngineKeywordsCheckBox.IsChecked ?? true
        };

        // 解析自定义排除关键字
        if (!string.IsNullOrWhiteSpace(CustomKeywordsTextBox.Text))
        {
            var keywords = CustomKeywordsTextBox.Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k));
            foreach (var keyword in keywords)
            {
                options.CustomKeywords.Add(keyword);
            }
        }

        // 解析自定义正则表达式
        if (!string.IsNullOrWhiteSpace(CustomPatternsTextBox.Text))
        {
            var patterns = CustomPatternsTextBox.Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p));
            foreach (var pattern in patterns)
            {
                try
                {
                    options.CustomPatterns.Add(new Regex(pattern, RegexOptions.Compiled));
                }
                catch
                {
                    // 忽略无效的正则表达式
                }
            }
        }

        return options;
    }

    /// <summary>
    /// 开始扫描游戏文本
    /// </summary>
    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var currentProject = GameProjectService.Instance.CurrentProject;
        if (currentProject == null) return;

        _scanCancellation = new CancellationTokenSource();
        ScanButton.IsEnabled = false;
        StopScanButton.IsEnabled = true;
        ExportButton.IsEnabled = false;
        ImportButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;

        try
        {
            var sourceLanguage = GameProjectService.Instance.CurrentSourceLanguage;
            var scanOptions = CollectScanOptions();
            var entries = await TextScannerService.Instance.ScanAsync(currentProject, sourceLanguage, scanOptions, _scanCancellation.Token);

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
        var currentProject = GameProjectService.Instance.CurrentProject;
        if (currentProject == null) return;

        try
        {
            ApplyButton.IsEnabled = false;
            ExportImportInfoBar.Title = "应用中";
            ExportImportInfoBar.Message = "正在将翻译应用到游戏文件...";
            ExportImportInfoBar.Severity = InfoBarSeverity.Informational;
            ExportImportInfoBar.IsOpen = true;

            var modifiedCount = await AssetModifierService.Instance.ApplyTextTranslationsAsync(currentProject, [.. _textEntries]);

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
