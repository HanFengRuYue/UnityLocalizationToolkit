using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityLocalizationToolkit.Models;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// 游戏项目服务 - 用于加载和检测Unity游戏项目
/// </summary>
public class GameProjectService
{
    private static GameProjectService? _instance;
    public static GameProjectService Instance => _instance ??= new GameProjectService();

    /// <summary>
    /// 当前加载的游戏项目
    /// </summary>
    public GameProject? CurrentProject { get; private set; }

    /// <summary>
    /// 当前选择的源语言
    /// </summary>
    public SourceLanguage CurrentSourceLanguage { get; set; } = SourceLanguage.NoFilter;

    /// <summary>
    /// 从目录加载游戏项目
    /// </summary>
    /// <param name="path">游戏根目录路径</param>
    /// <returns>游戏项目信息</returns>
    public GameProject LoadProject(string path)
    {
        Trace.WriteLine($"[GameProject] ========== Loading project ==========");
        Trace.WriteLine($"[GameProject] Root path: {path}");
        
        var project = new GameProject
        {
            RootPath = path
        };

        // 查找游戏数据目录
        FindDataDirectory(project);
        Trace.WriteLine($"[GameProject] Data path: {project.DataPath}");
        Trace.WriteLine($"[GameProject] Managed path: {project.ManagedPath}");

        // 检测后端类型
        DetectBackendType(project);
        Trace.WriteLine($"[GameProject] Backend type: {project.BackendType}");
        Trace.WriteLine($"[GameProject] Assembly path: {project.AssemblyCSharpPath}");

        // 查找资源文件
        FindAssetFiles(project);
        Trace.WriteLine($"[GameProject] Asset files found: {project.AssetFiles.Count}");
        foreach (var assetFile in project.AssetFiles)
        {
            Trace.WriteLine($"[GameProject]   - {assetFile}");
        }
        Trace.WriteLine($"[GameProject] Bundle files found: {project.BundleFiles.Count}");
        foreach (var bundleFile in project.BundleFiles.Take(10))
        {
            Trace.WriteLine($"[GameProject]   - {bundleFile}");
        }
        if (project.BundleFiles.Count > 10)
        {
            Trace.WriteLine($"[GameProject]   ... and {project.BundleFiles.Count - 10} more");
        }
        Trace.WriteLine($"[GameProject] ========== Project loaded ==========");

        CurrentProject = project;
        return project;
    }

    /// <summary>
    /// 查找游戏数据目录
    /// </summary>
    private void FindDataDirectory(GameProject project)
    {
        // 查找 *_Data 目录
        var directories = Directory.GetDirectories(project.RootPath);
        foreach (var dir in directories)
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.EndsWith("_Data", StringComparison.OrdinalIgnoreCase))
            {
                project.DataPath = dir;
                project.ManagedPath = Path.Combine(dir, "Managed");
                
                // 查找可执行文件
                var exeName = dirName[..^5] + ".exe";
                var exePath = Path.Combine(project.RootPath, exeName);
                if (File.Exists(exePath))
                {
                    project.ExecutablePath = exePath;
                }
                break;
            }
        }

        // 如果没找到 *_Data 目录，检查是否是数据目录本身
        if (string.IsNullOrEmpty(project.DataPath))
        {
            var managedPath = Path.Combine(project.RootPath, "Managed");
            if (Directory.Exists(managedPath))
            {
                project.DataPath = project.RootPath;
                project.ManagedPath = managedPath;
            }
        }
    }

    /// <summary>
    /// 检测游戏后端类型
    /// </summary>
    private void DetectBackendType(GameProject project)
    {
        // 检查IL2CPP标志
        var gameAssemblyPath = Path.Combine(project.RootPath, "GameAssembly.dll");
        if (File.Exists(gameAssemblyPath))
        {
            project.BackendType = UnityBackendType.IL2CPP;
            project.GameAssemblyPath = gameAssemblyPath;

            // 查找metadata
            var metadataPath = Path.Combine(project.DataPath, "il2cpp_data", "Metadata", "global-metadata.dat");
            if (File.Exists(metadataPath))
            {
                project.MetadataPath = metadataPath;
            }
            return;
        }

        // 检查Mono标志
        if (!string.IsNullOrEmpty(project.ManagedPath) && Directory.Exists(project.ManagedPath))
        {
            var assemblyCSharpPath = Path.Combine(project.ManagedPath, "Assembly-CSharp.dll");
            if (File.Exists(assemblyCSharpPath))
            {
                project.BackendType = UnityBackendType.Mono;
                project.AssemblyCSharpPath = assemblyCSharpPath;
                return;
            }
        }

        project.BackendType = UnityBackendType.Unknown;
    }

    /// <summary>
    /// 查找资源文件
    /// </summary>
    private void FindAssetFiles(GameProject project)
    {
        if (string.IsNullOrEmpty(project.DataPath) || !Directory.Exists(project.DataPath))
        {
            Trace.WriteLine($"[GameProject] FindAssetFiles: DataPath is empty or doesn't exist");
            return;
        }

        Trace.WriteLine($"[GameProject] FindAssetFiles: Searching in {project.DataPath}");

        // 列出Data目录中的所有文件用于诊断
        try
        {
            var allFilesInData = Directory.GetFiles(project.DataPath, "*", SearchOption.TopDirectoryOnly);
            Trace.WriteLine($"[GameProject] Total files in Data directory: {allFilesInData.Length}");
            foreach (var file in allFilesInData.Take(20))
            {
                Trace.WriteLine($"[GameProject]   File: {Path.GetFileName(file)}");
            }
            if (allFilesInData.Length > 20)
            {
                Trace.WriteLine($"[GameProject]   ... and {allFilesInData.Length - 20} more files");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error listing files: {ex.Message}");
        }

        // 查找.assets文件（使用多种搜索模式）
        try
        {
            // 尝试搜索 *.assets
            var assetFiles = Directory.GetFiles(project.DataPath, "*.assets", SearchOption.TopDirectoryOnly);
            Trace.WriteLine($"[GameProject] Found {assetFiles.Length} .assets files");
            project.AssetFiles.AddRange(assetFiles);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching .assets files: {ex.Message}");
        }

        // 查找sharedassets文件（排除.resource文件，它们是资源流而非资产文件）
        try
        {
            var sharedAssets = Directory.GetFiles(project.DataPath, "sharedassets*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) &&
                           !f.EndsWith(".resS", StringComparison.OrdinalIgnoreCase));
            var sharedAssetsList = sharedAssets.ToList();
            Trace.WriteLine($"[GameProject] Found {sharedAssetsList.Count} sharedassets files (excluding .resource/.resS)");
            foreach (var file in sharedAssetsList)
            {
                if (!project.AssetFiles.Contains(file))
                {
                    project.AssetFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching sharedassets files: {ex.Message}");
        }

        // 查找resources.assets
        var resourcesPath = Path.Combine(project.DataPath, "resources.assets");
        if (File.Exists(resourcesPath) && !project.AssetFiles.Contains(resourcesPath))
        {
            Trace.WriteLine($"[GameProject] Found resources.assets");
            project.AssetFiles.Add(resourcesPath);
        }

        // 查找level文件（无扩展名的level文件）
        try
        {
            var levelFiles = Directory.GetFiles(project.DataPath, "level*", SearchOption.TopDirectoryOnly);
            Trace.WriteLine($"[GameProject] Found {levelFiles.Length} level* files");
            foreach (var file in levelFiles)
            {
                var ext = Path.GetExtension(file);
                if (string.IsNullOrEmpty(ext) || ext.Equals(".assets", StringComparison.OrdinalIgnoreCase))
                {
                    if (!project.AssetFiles.Contains(file))
                    {
                        project.AssetFiles.Add(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching level files: {ex.Message}");
        }

        // 查找globalgamemanagers和相关文件
        var specialFiles = new[] { "globalgamemanagers", "globalgamemanagers.assets", "mainData" };
        foreach (var specialFile in specialFiles)
        {
            var specialPath = Path.Combine(project.DataPath, specialFile);
            if (File.Exists(specialPath) && !project.AssetFiles.Contains(specialPath))
            {
                Trace.WriteLine($"[GameProject] Found special file: {specialFile}");
                project.AssetFiles.Add(specialPath);
            }
        }

        // 查找StreamingAssets中的bundle文件
        var streamingAssetsPath = Path.Combine(project.DataPath, "StreamingAssets");
        Trace.WriteLine($"[GameProject] Checking StreamingAssets at: {streamingAssetsPath}");
        if (Directory.Exists(streamingAssetsPath))
        {
            Trace.WriteLine($"[GameProject] StreamingAssets directory exists");
            
            // 查找常见的bundle扩展名
            var bundleExtensions = new[] { "*.bundle", "*.unity3d", "*.assets", "*.ab" };
            foreach (var ext in bundleExtensions)
            {
                try
                {
                    var bundles = Directory.GetFiles(streamingAssetsPath, ext, SearchOption.AllDirectories);
                    Trace.WriteLine($"[GameProject] Found {bundles.Length} {ext} files in StreamingAssets");
                    project.BundleFiles.AddRange(bundles);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[GameProject] Error searching {ext} in StreamingAssets: {ex.Message}");
                }
            }

            // 也查找没有扩展名的文件（可能是bundle）
            try
            {
                var allBundleFiles = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories)
                    .Where(f => string.IsNullOrEmpty(Path.GetExtension(f)));
                var noExtFiles = allBundleFiles.ToList();
                Trace.WriteLine($"[GameProject] Found {noExtFiles.Count} files without extension in StreamingAssets");
                project.BundleFiles.AddRange(noExtFiles);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[GameProject] Error searching no-extension files: {ex.Message}");
            }
        }
        else
        {
            Trace.WriteLine($"[GameProject] StreamingAssets directory does not exist");
        }

        // 也在Data目录中搜索.unity3d文件（有些游戏放在Data目录而非StreamingAssets）
        try
        {
            var unity3dFiles = Directory.GetFiles(project.DataPath, "*.unity3d", SearchOption.AllDirectories);
            Trace.WriteLine($"[GameProject] Found {unity3dFiles.Length} .unity3d files in Data directory (recursive)");
            project.BundleFiles.AddRange(unity3dFiles);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching .unity3d in Data: {ex.Message}");
        }

        // 在Data目录递归搜索所有.assets文件
        try
        {
            var allAssetsRecursive = Directory.GetFiles(project.DataPath, "*.assets", SearchOption.AllDirectories);
            Trace.WriteLine($"[GameProject] Found {allAssetsRecursive.Length} .assets files in Data directory (recursive)");
            foreach (var file in allAssetsRecursive)
            {
                if (!project.AssetFiles.Contains(file) && !project.BundleFiles.Contains(file))
                {
                    // 如果在StreamingAssets中，添加到BundleFiles；否则添加到AssetFiles
                    if (file.Contains("StreamingAssets", StringComparison.OrdinalIgnoreCase))
                    {
                        project.BundleFiles.Add(file);
                    }
                    else
                    {
                        project.AssetFiles.Add(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching .assets recursively: {ex.Message}");
        }

        // 在游戏根目录也搜索（有些游戏把资源放在根目录）
        try
        {
            var rootAssets = Directory.GetFiles(project.RootPath, "*.assets", SearchOption.TopDirectoryOnly);
            Trace.WriteLine($"[GameProject] Found {rootAssets.Length} .assets files in root directory");
            project.AssetFiles.AddRange(rootAssets);

            var rootUnity3d = Directory.GetFiles(project.RootPath, "*.unity3d", SearchOption.TopDirectoryOnly);
            Trace.WriteLine($"[GameProject] Found {rootUnity3d.Length} .unity3d files in root directory");
            project.BundleFiles.AddRange(rootUnity3d);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error searching root directory: {ex.Message}");
        }

        // 列出Data目录的所有子目录
        try
        {
            var subDirs = Directory.GetDirectories(project.DataPath);
            Trace.WriteLine($"[GameProject] Subdirectories in Data folder:");
            foreach (var dir in subDirs)
            {
                Trace.WriteLine($"[GameProject]   Dir: {Path.GetFileName(dir)}");
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[GameProject] Error listing subdirectories: {ex.Message}");
        }

        // 去重
        project.AssetFiles = project.AssetFiles.Distinct().ToList();
        project.BundleFiles = project.BundleFiles.Distinct().ToList();

        Trace.WriteLine($"[GameProject] Final asset files count: {project.AssetFiles.Count}");
        Trace.WriteLine($"[GameProject] Final bundle files count: {project.BundleFiles.Count}");
    }

    /// <summary>
    /// 获取后端类型的显示名称
    /// </summary>
    public static string GetBackendDisplayName(UnityBackendType type)
    {
        return type switch
        {
            UnityBackendType.Mono => "Mono",
            UnityBackendType.IL2CPP => "IL2CPP",
            _ => "未知"
        };
    }
}
