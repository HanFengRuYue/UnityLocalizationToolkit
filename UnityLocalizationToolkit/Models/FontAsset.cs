using System.Collections.Generic;

namespace UnityLocalizationToolkit.Models;

/// <summary>
/// 字体类型
/// </summary>
public enum FontType
{
    /// <summary>
    /// 传统Unity字体
    /// </summary>
    Traditional,
    
    /// <summary>
    /// TextMeshPro字体
    /// </summary>
    TMP
}

/// <summary>
/// 字体替换状态
/// </summary>
public enum FontReplacementStatus
{
    /// <summary>
    /// 未替换
    /// </summary>
    NotReplaced,
    
    /// <summary>
    /// 已替换
    /// </summary>
    Replaced,
    
    /// <summary>
    /// 替换失败
    /// </summary>
    Failed
}

/// <summary>
/// 字体资产信息
/// </summary>
public class FontAsset
{
    /// <summary>
    /// 字体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 字体类型
    /// </summary>
    public FontType FontType { get; set; }

    /// <summary>
    /// 替换状态
    /// </summary>
    public FontReplacementStatus Status { get; set; } = FontReplacementStatus.NotReplaced;

    /// <summary>
    /// 来源文件路径
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// MonoBehaviour PathId
    /// </summary>
    public long MonoBehaviourPathId { get; set; }

    /// <summary>
    /// Texture2D PathId (TMP字体)
    /// </summary>
    public long Texture2DPathId { get; set; }

    /// <summary>
    /// Material PathId (TMP字体)
    /// </summary>
    public long MaterialPathId { get; set; }

    /// <summary>
    /// 字体家族名称
    /// </summary>
    public string FamilyName { get; set; } = string.Empty;

    /// <summary>
    /// 字体样式名称
    /// </summary>
    public string StyleName { get; set; } = string.Empty;

    /// <summary>
    /// 替换字体文件路径
    /// </summary>
    public string ReplacementFontPath { get; set; } = string.Empty;

    /// <summary>
    /// 关联资源描述
    /// </summary>
    public string AssociatedAssets
    {
        get
        {
            if (FontType == FontType.Traditional)
            {
                return "MonoBehaviour";
            }
            else
            {
                var assets = new List<string> { "MonoBehaviour" };
                if (Texture2DPathId != 0) assets.Add("Texture2D");
                if (MaterialPathId != 0) assets.Add("Material");
                return string.Join(", ", assets);
            }
        }
    }

    /// <summary>
    /// 显示类型
    /// </summary>
    public string DisplayType => FontType == FontType.Traditional ? "传统字体" : "TMP字体";

    /// <summary>
    /// 显示状态
    /// </summary>
    public string DisplayStatus => Status switch
    {
        FontReplacementStatus.NotReplaced => "未替换",
        FontReplacementStatus.Replaced => "已替换",
        FontReplacementStatus.Failed => "失败",
        _ => "未知"
    };
}
