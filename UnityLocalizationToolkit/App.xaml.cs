using Microsoft.UI.Xaml;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit;

/// <summary>
/// 应用程序入口类
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// 获取主窗口实例
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// 初始化应用程序
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 应用程序启动时调用
    /// </summary>
    /// <param name="args">启动参数</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        
        // 初始化主题服务
        ThemeService.Instance.Initialize(_window);
        
        _window.Activate();
    }
}
