using System.Collections.Generic;

namespace UnityLocalizationToolkit.Models;

/// <summary>
/// Unity游戏后端类型
/// </summary>
public enum UnityBackendType
{
    Unknown,
    Mono,
    IL2CPP
}

/// <summary>
/// 游戏项目信息
/// </summary>
public class GameProject
{
    /// <summary>
    /// 游戏根目录
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// 游戏数据目录 (xxx_Data)
    /// </summary>
    public string DataPath { get; set; } = string.Empty;

    /// <summary>
    /// Managed目录路径
    /// </summary>
    public string ManagedPath { get; set; } = string.Empty;

    /// <summary>
    /// 后端类型
    /// </summary>
    public UnityBackendType BackendType { get; set; } = UnityBackendType.Unknown;

    /// <summary>
    /// Unity版本
    /// </summary>
    public string UnityVersion { get; set; } = string.Empty;

    /// <summary>
    /// 游戏可执行文件路径
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// GameAssembly.dll路径 (IL2CPP)
    /// </summary>
    public string GameAssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// global-metadata.dat路径 (IL2CPP)
    /// </summary>
    public string MetadataPath { get; set; } = string.Empty;

    /// <summary>
    /// Assembly-CSharp.dll路径 (Mono)
    /// </summary>
    public string AssemblyCSharpPath { get; set; } = string.Empty;

    /// <summary>
    /// 资源文件列表
    /// </summary>
    public List<string> AssetFiles { get; set; } = [];

    /// <summary>
    /// 资源包文件列表
    /// </summary>
    public List<string> BundleFiles { get; set; } = [];

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid => BackendType != UnityBackendType.Unknown && !string.IsNullOrEmpty(DataPath);
}
