using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnityLocalizationToolkit.Pages;

namespace UnityLocalizationToolkit;

/// <summary>
/// 主窗口 - 包含导航视图和内容框架
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 设置窗口最小尺寸
        var appWindow = this.AppWindow;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
    }

    /// <summary>
    /// 导航视图加载完成时调用
    /// </summary>
    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // 默认选中第一个菜单项
        NavView.SelectedItem = TextTranslationItem;
        ContentFrame.Navigate(typeof(TextTranslationPage));
    }

    /// <summary>
    /// 导航项选择改变时调用
    /// </summary>
    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            NavigateToPage(tag);
        }
    }

    /// <summary>
    /// 根据标签导航到对应页面
    /// </summary>
    /// <param name="pageTag">页面标签</param>
    private void NavigateToPage(string? pageTag)
    {
        var pageType = pageTag switch
        {
            "TextTranslationPage" => typeof(TextTranslationPage),
            "FontReplacementPage" => typeof(FontReplacementPage),
            "SettingsPage" => typeof(SettingsPage),
            _ => null
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
