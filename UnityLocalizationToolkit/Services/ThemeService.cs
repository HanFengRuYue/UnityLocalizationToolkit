using Microsoft.UI.Xaml;
using Windows.Storage;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// 主题服务 - 管理应用程序的主题切换
/// </summary>
public class ThemeService
{
    private const string ThemeSettingKey = "AppTheme";
    private static ThemeService? _instance;
    private Window? _window;

    public static ThemeService Instance => _instance ??= new ThemeService();

    public enum AppTheme
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>
    /// 初始化主题服务
    /// </summary>
    /// <param name="window">主窗口</param>
    public void Initialize(Window window)
    {
        _window = window;
        var savedTheme = LoadThemePreference();
        SetTheme(savedTheme);
    }

    /// <summary>
    /// 设置应用程序主题
    /// </summary>
    /// <param name="theme">目标主题</param>
    public void SetTheme(AppTheme theme)
    {
        if (_window?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
        SaveThemePreference(theme);
    }

    /// <summary>
    /// 获取当前主题设置
    /// </summary>
    /// <returns>当前主题</returns>
    public AppTheme GetCurrentTheme()
    {
        return LoadThemePreference();
    }

    /// <summary>
    /// 保存主题偏好设置
    /// </summary>
    /// <param name="theme">要保存的主题</param>
    private void SaveThemePreference(AppTheme theme)
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[ThemeSettingKey] = (int)theme;
        }
        catch
        {
            // Unpackaged模式下可能无法访问ApplicationData，忽略错误
        }
    }

    /// <summary>
    /// 加载主题偏好设置
    /// </summary>
    /// <returns>保存的主题，默认为跟随系统</returns>
    private AppTheme LoadThemePreference()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.TryGetValue(ThemeSettingKey, out var value) && value is int themeValue)
            {
                return (AppTheme)themeValue;
            }
        }
        catch
        {
            // Unpackaged模式下可能无法访问ApplicationData，忽略错误
        }
        return AppTheme.System;
    }
}
