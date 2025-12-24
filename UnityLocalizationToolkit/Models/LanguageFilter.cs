using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityLocalizationToolkit.Pages;

namespace UnityLocalizationToolkit.Models;

/// <summary>
/// 语言类型
/// </summary>
public enum SourceLanguage
{
    Japanese,
    English,
    Korean,
    ChineseSimplified,
    ChineseTraditional,
    Other,
    /// <summary>
    /// 不过滤任何语言，扫描所有字符串
    /// </summary>
    NoFilter
}

/// <summary>
/// 语言过滤器 - 用于识别和过滤不需要翻译的文本
/// </summary>
public static partial class LanguageFilter
{
    /// <summary>
    /// Unity引擎保留关键字（不应翻译）
    /// </summary>
    public static readonly HashSet<string> EngineKeywords =
    [
        // Unity核心类
        "UnityEngine", "GameObject", "Transform", "MonoBehaviour", "ScriptableObject",
        "Component", "Behaviour", "Object", "Rigidbody", "Rigidbody2D", "Collider",
        "Collider2D", "Renderer", "MeshRenderer", "SkinnedMeshRenderer", "Camera",
        "Light", "AudioSource", "AudioListener", "Animator", "Animation",
        
        // 生命周期方法
        "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnDestroy",
        "OnEnable", "OnDisable", "OnGUI", "OnTriggerEnter", "OnTriggerExit",
        "OnCollisionEnter", "OnCollisionExit", "OnMouseDown", "OnMouseUp",
        
        // 常见Unity类
        "Vector2", "Vector3", "Vector4", "Quaternion", "Matrix4x4", "Color",
        "Rect", "Bounds", "Ray", "RaycastHit", "Mathf", "Random", "Time",
        "Input", "Screen", "Application", "Debug", "PlayerPrefs", "Resources",
        
        // UI相关
        "Canvas", "RectTransform", "Image", "Text", "Button", "Toggle",
        "Slider", "Dropdown", "InputField", "ScrollRect", "EventSystem",
        
        // TextMeshPro
        "TMP_Text", "TextMeshPro", "TextMeshProUGUI", "TMP_FontAsset",
        "TMP_SpriteAsset", "TMP_StyleSheet", "TMP_Settings",
        
        // 资源类型
        "Texture", "Texture2D", "Sprite", "Material", "Shader", "Mesh",
        "AudioClip", "AnimationClip", "Font", "TextAsset", "AssetBundle",
        
        // 常见编程关键字
        "null", "true", "false", "this", "base", "new", "void", "int",
        "float", "string", "bool", "double", "long", "short", "byte",
        "public", "private", "protected", "static", "const", "readonly",
        "class", "struct", "enum", "interface", "namespace", "using",
        "if", "else", "for", "foreach", "while", "do", "switch", "case",
        "break", "continue", "return", "try", "catch", "finally", "throw",
        
        // 常见变量名模式
        "gameObject", "transform", "renderer", "collider", "rigidbody",
        "animator", "audio", "camera", "light", "enabled", "active",
        "position", "rotation", "scale", "localPosition", "localRotation",
        "localScale", "forward", "right", "up", "parent", "root", "tag",
        "layer", "name", "hideFlags"
    ];

    /// <summary>
    /// 不应翻译的字符串模式
    /// </summary>
    public static readonly string[] SkipPatterns =
    [
        @"^[A-Za-z_][A-Za-z0-9_]*$",  // 纯变量名模式
        @"^\d+$",                       // 纯数字
        @"^[A-Za-z0-9_]+\.[A-Za-z0-9_]+$",  // 类似路径或命名空间
        @"^<[^>]+>$",                   // XML/HTML标签
        @"^\{[^}]+\}$",                 // 占位符
        @"^%[^%]+%$",                   // 变量占位符
        @"^#[A-Fa-f0-9]{6}$",           // 颜色代码
        @"^https?://",                   // URL
        @"^[A-Za-z]:\\",                // Windows路径
        @"^/[A-Za-z]",                  // Unix路径
    ];

    private static readonly Regex[] _skipRegexes = SkipPatterns.Select(p => new Regex(p, RegexOptions.Compiled)).ToArray();

    /// <summary>
    /// 检查文本是否包含指定语言的字符
    /// </summary>
    public static bool ContainsLanguage(string text, SourceLanguage language)
    {
        return language switch
        {
            SourceLanguage.NoFilter => true, // 不过滤，返回所有文本
            SourceLanguage.Japanese => ContainsJapanese(text),
            SourceLanguage.Korean => ContainsKorean(text),
            SourceLanguage.ChineseSimplified or SourceLanguage.ChineseTraditional => ContainsChinese(text),
            SourceLanguage.English => ContainsEnglishLetters(text),
            _ => true
        };
    }

    /// <summary>
    /// 检查是否包含日语字符（平假名、片假名、日语汉字）
    /// </summary>
    public static bool ContainsJapanese(string text)
    {
        return text.Any(c =>
            (c >= '\u3040' && c <= '\u309F') ||  // 平假名
            (c >= '\u30A0' && c <= '\u30FF') ||  // 片假名
            (c >= '\u4E00' && c <= '\u9FFF'));   // CJK统一汉字
    }

    /// <summary>
    /// 检查是否包含韩语字符
    /// </summary>
    public static bool ContainsKorean(string text)
    {
        return text.Any(c =>
            (c >= '\uAC00' && c <= '\uD7AF') ||  // 韩语音节
            (c >= '\u1100' && c <= '\u11FF'));   // 韩语字母
    }

    /// <summary>
    /// 检查是否包含中文字符
    /// </summary>
    public static bool ContainsChinese(string text)
    {
        return text.Any(c => c >= '\u4E00' && c <= '\u9FFF');
    }

    /// <summary>
    /// 检查是否只包含英文字母和常见符号
    /// </summary>
    public static bool ContainsEnglishLetters(string text)
    {
        return text.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
    }

    /// <summary>
    /// 检查文本是否应该跳过翻译
    /// </summary>
    public static bool ShouldSkipTranslation(string text, out string reason)
    {
        return ShouldSkipTranslation(text, out reason, null);
    }

    /// <summary>
    /// 检查文本是否应该跳过翻译（带扫描选项）
    /// </summary>
    public static bool ShouldSkipTranslation(string text, out string reason, ScanOptions? options)
    {
        reason = string.Empty;
        var minLength = options?.MinTextLength ?? 2;
        var useEngineKeywords = options?.UseEngineKeywords ?? true;

        // 空文本
        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "空文本";
            return true;
        }

        // 太短
        if (text.Length < minLength)
        {
            reason = "文本太短";
            return true;
        }

        // 引擎关键字
        if (useEngineKeywords && EngineKeywords.Contains(text))
        {
            reason = "引擎保留关键字";
            return true;
        }

        // 自定义排除关键字
        if (options?.CustomKeywords != null && options.CustomKeywords.Contains(text))
        {
            reason = "自定义排除关键字";
            return true;
        }

        // 匹配跳过模式
        foreach (var regex in _skipRegexes)
        {
            if (regex.IsMatch(text))
            {
                reason = "匹配跳过模式";
                return true;
            }
        }

        // 自定义正则表达式
        if (options?.CustomPatterns != null)
        {
            foreach (var regex in options.CustomPatterns)
            {
                if (regex.IsMatch(text))
                {
                    reason = "匹配自定义正则表达式";
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断文本是否可能是游戏文本（需要翻译）
    /// </summary>
    public static bool IsLikelyGameText(string text, SourceLanguage sourceLanguage)
    {
        return IsLikelyGameText(text, sourceLanguage, null);
    }

    /// <summary>
    /// 判断文本是否可能是游戏文本（需要翻译，带扫描选项）
    /// </summary>
    public static bool IsLikelyGameText(string text, SourceLanguage sourceLanguage, ScanOptions? options)
    {
        // 首先检查是否应该跳过
        if (ShouldSkipTranslation(text, out _, options))
        {
            return false;
        }

        // 检查是否包含源语言字符
        return ContainsLanguage(text, sourceLanguage);
    }
}
