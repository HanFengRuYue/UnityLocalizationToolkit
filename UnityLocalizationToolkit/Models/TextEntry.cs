using System.IO;

namespace UnityLocalizationToolkit.Models;

/// <summary>
/// 文本来源类型
/// </summary>
public enum TextSourceType
{
    /// <summary>
    /// Assembly-CSharp.dll中的字符串
    /// </summary>
    Script,
    
    /// <summary>
    /// MonoBehaviour资产中的字符串
    /// </summary>
    MonoBehaviour,
    
    /// <summary>
    /// TextAsset资产中的文本
    /// </summary>
    TextAsset
}

/// <summary>
/// 文本条目
/// </summary>
public class TextEntry
{
    /// <summary>
    /// 唯一标识符
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 原始文本
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// 翻译后的文本
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// 来源类型
    /// </summary>
    public TextSourceType SourceType { get; set; }

    /// <summary>
    /// 来源文件路径
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// 资产路径ID (用于MonoBehaviour和TextAsset)
    /// </summary>
    public long PathId { get; set; }

    /// <summary>
    /// 字段路径 (用于MonoBehaviour)
    /// </summary>
    public string FieldPath { get; set; } = string.Empty;

    /// <summary>
    /// 类名 (用于Script类型)
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// 方法名 (用于Script类型)
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 在文件中的偏移量
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// 是否已翻译
    /// </summary>
    public bool IsTranslated => !string.IsNullOrEmpty(TranslatedText) && TranslatedText != OriginalText;

    /// <summary>
    /// 是否应该跳过翻译（引擎保留变量等）
    /// </summary>
    public bool ShouldSkip { get; set; }

    /// <summary>
    /// 跳过原因
    /// </summary>
    public string SkipReason { get; set; } = string.Empty;

    /// <summary>
    /// 显示位置信息
    /// </summary>
    public string DisplayLocation
    {
        get
        {
            return SourceType switch
            {
                TextSourceType.Script => $"{ClassName}.{MethodName}",
                TextSourceType.MonoBehaviour => $"{Path.GetFileName(SourceFile)}:{PathId}",
                TextSourceType.TextAsset => $"{Path.GetFileName(SourceFile)}:{PathId}",
                _ => SourceFile
            };
        }
    }
}
