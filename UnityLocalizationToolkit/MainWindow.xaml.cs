using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnityLocalizationToolkit.Pages;
using Windows.Graphics;

namespace UnityLocalizationToolkit;

/// <summary>
/// 主窗口 - 包含导航视图和内容框架
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // 设置窗口尺寸
        var appWindow = this.AppWindow;
        appWindow.Resize(new SizeInt32(1200, 800));
        
        // 窗口居中显示
        CenterWindow();
        
        // 设置自定义标题栏
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    /// <summary>
    /// 将窗口居中显示在屏幕上
    /// </summary>
    private void CenterWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (area == null) return;
        AppWindow.Move(new PointInt32(
            (area.Value.Width - AppWindow.Size.Width) / 2,
            (area.Value.Height - AppWindow.Size.Height) / 2));
    }

    /// <summary>
    /// 导航视图加载完成时调用
    /// </summary>
    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // 默认选中主页
        NavView.SelectedItem = HomeItem;
        ContentFrame.Navigate(typeof(HomePage));
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
            "HomePage" => typeof(HomePage),
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
