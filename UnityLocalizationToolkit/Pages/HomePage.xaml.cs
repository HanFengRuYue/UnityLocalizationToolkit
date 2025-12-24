using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using UnityLocalizationToolkit.Models;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit.Pages;

/// <summary>
/// 主页 - 用于配置游戏项目路径和基本设置
/// </summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        
        // 如果已经加载了项目，显示项目信息
        UpdateProjectDisplay();
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
            var project = GameProjectService.Instance.LoadProject(folder.Path);
            
            // 更新显示
            UpdateProjectDisplay();
        }
    }

    /// <summary>
    /// 更新项目信息显示
    /// </summary>
    private void UpdateProjectDisplay()
    {
        var project = GameProjectService.Instance.CurrentProject;
        
        if (project != null)
        {
            GamePathTextBox.Text = project.RootPath;
            
            // 更新后端类型显示
            if (project.IsValid)
            {
                BackendInfoBar.Title = "后端类型";
                BackendInfoBar.Message = GameProjectService.GetBackendDisplayName(project.BackendType);
                BackendInfoBar.Severity = InfoBarSeverity.Success;
                
                // 显示项目信息
                ProjectInfoExpander.Visibility = Visibility.Visible;
                ProjectPathText.Text = project.RootPath;
                DataPathText.Text = project.DataPath ?? "-";
                AssetCountText.Text = project.AssetFiles.Count.ToString();
                BundleCountText.Text = project.BundleFiles.Count.ToString();
                
                // 启用快速操作按钮
                GoToTextButton.IsEnabled = true;
                GoToFontButton.IsEnabled = true;
            }
            else
            {
                BackendInfoBar.Title = "警告";
                BackendInfoBar.Message = "未能识别Unity游戏目录";
                BackendInfoBar.Severity = InfoBarSeverity.Warning;
                
                ProjectInfoExpander.Visibility = Visibility.Collapsed;
                GoToTextButton.IsEnabled = false;
                GoToFontButton.IsEnabled = false;
            }
        }
        else
        {
            BackendInfoBar.Title = "后端类型";
            BackendInfoBar.Message = "请先选择游戏目录";
            BackendInfoBar.Severity = InfoBarSeverity.Informational;
            
            ProjectInfoExpander.Visibility = Visibility.Collapsed;
            GoToTextButton.IsEnabled = false;
            GoToFontButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// 源语言选择改变
    /// </summary>
    private void SourceLanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceLanguageComboBox.SelectedItem is ComboBoxItem item)
        {
            GameProjectService.Instance.CurrentSourceLanguage = item.Tag?.ToString() switch
            {
                "ja" => SourceLanguage.Japanese,
                "en" => SourceLanguage.English,
                "ko" => SourceLanguage.Korean,
                "zh-CN" => SourceLanguage.ChineseSimplified,
                "zh-TW" => SourceLanguage.ChineseTraditional,
                "none" => SourceLanguage.NoFilter,
                _ => SourceLanguage.Other
            };
        }
    }

    /// <summary>
    /// 导航到文本翻译页面
    /// </summary>
    private void GoToTextButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame != null)
        {
            Frame.Navigate(typeof(TextTranslationPage));
        }
    }

    /// <summary>
    /// 导航到字体替换页面
    /// </summary>
    private void GoToFontButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame != null)
        {
            Frame.Navigate(typeof(FontReplacementPage));
        }
    }
}
