using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UnityLocalizationToolkit.Services;

namespace UnityLocalizationToolkit.Pages;

/// <summary>
/// 设置页面 - 用于配置应用程序设置
/// </summary>
public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 页面加载完成时调用
    /// </summary>
    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        
        // 加载当前主题设置
        var currentTheme = ThemeService.Instance.GetCurrentTheme();
        
        switch (currentTheme)
        {
            case ThemeService.AppTheme.Light:
                LightThemeRadioButton.IsChecked = true;
                break;
            case ThemeService.AppTheme.Dark:
                DarkThemeRadioButton.IsChecked = true;
                break;
            default:
                SystemThemeRadioButton.IsChecked = true;
                break;
        }
        
        _isInitializing = false;
    }

    /// <summary>
    /// 主题选择变化时调用
    /// </summary>
    private void ThemeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        
        if (ThemeRadioButtons.SelectedItem is RadioButton selectedRadioButton)
        {
            var theme = selectedRadioButton.Tag?.ToString() switch
            {
                "Light" => ThemeService.AppTheme.Light,
                "Dark" => ThemeService.AppTheme.Dark,
                _ => ThemeService.AppTheme.System
            };
            
            ThemeService.Instance.SetTheme(theme);
        }
    }
}
