using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UnityLocalizationToolkit.Models;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// 字体扫描服务 - 用于检测游戏中的字体资源
/// </summary>
public class FontScannerService
{
    private static FontScannerService? _instance;
    public static FontScannerService Instance => _instance ??= new FontScannerService();

    private readonly AssetsManager _assetsManager;

    /// <summary>
    /// 扫描进度变化事件
    /// </summary>
    public event Action<float, string>? ProgressChanged;

    /// <summary>
    /// 扫描到的字体资产列表
    /// </summary>
    public List<FontAsset> FontAssets { get; } = [];

    /// <summary>
    /// TMP字体的特征字段（包括旧版和新版TMP）
    /// </summary>
    private static readonly string[] TmpFontFields =
    [
        "m_FaceInfo",
        "m_GlyphTable",
        "m_CharacterTable",
        "m_AtlasPopulationMode",
        "materialHashCode",
        "m_AtlasTextures",
        "m_AtlasTextureIndex",
        "m_GlyphLookupDictionary",
        "m_CharacterLookupDictionary",
        "m_FontFeatureTable",
        "m_AtlasWidth",
        "m_AtlasHeight",
        "m_AtlasPadding",
        "m_FreeGlyphRects",
        "m_UsedGlyphRects"
    ];
    
    /// <summary>
    /// 用于追踪是否已记录过TMP检测信息
    /// </summary>
    private static bool _tmpDetectionLogged = false;

    public FontScannerService()
    {
        _assetsManager = new AssetsManager();
        
        var classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
        Trace.WriteLine($"[FontScanner] Initializing, looking for classdata.tpk at: {classDataPath}");
        if (File.Exists(classDataPath))
        {
            try
            {
                _assetsManager.LoadClassPackage(classDataPath);
                Trace.WriteLine($"[FontScanner] Successfully loaded classdata.tpk");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[FontScanner] ERROR loading classdata.tpk: {ex.Message}");
            }
        }
        else
        {
            Trace.WriteLine($"[FontScanner] WARNING: classdata.tpk not found!");
        }
    }

    /// <summary>
    /// 扫描游戏中的字体资源
    /// </summary>
    public async Task<List<FontAsset>> ScanAsync(GameProject project, CancellationToken cancellationToken = default)
    {
        FontAssets.Clear();

        Trace.WriteLine($"[FontScanner] ========== Starting font scan ==========");
        Trace.WriteLine($"[FontScanner] Project path: {project.RootPath}");
        Trace.WriteLine($"[FontScanner] Asset files count: {project.AssetFiles.Count}");
        Trace.WriteLine($"[FontScanner] Bundle files count: {project.BundleFiles.Count}");
        Trace.WriteLine($"[FontScanner] classdata.tpk exists: {File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk"))}");

        try
        {
            var totalFiles = project.AssetFiles.Count + project.BundleFiles.Count;
            var processedFiles = 0;

            // 扫描资产文件
            foreach (var assetFile in project.AssetFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var progress = (float)processedFiles / totalFiles;
                ReportProgress(progress, $"正在扫描: {Path.GetFileName(assetFile)}");

                await Task.Run(() => ScanAssetFile(assetFile), cancellationToken);
                processedFiles++;
            }

            // 扫描Bundle文件
            foreach (var bundleFile in project.BundleFiles)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var progress = (float)processedFiles / totalFiles;
                ReportProgress(progress, $"正在扫描Bundle: {Path.GetFileName(bundleFile)}");

                await Task.Run(() => ScanBundleFile(bundleFile), cancellationToken);
                processedFiles++;
            }

            Trace.WriteLine($"[FontScanner] ========== Scan complete ==========");
            Trace.WriteLine($"[FontScanner] Total fonts found: {FontAssets.Count}");
            ReportProgress(1, $"扫描完成，找到 {FontAssets.Count} 个字体");
        }
        catch (OperationCanceledException)
        {
            Trace.WriteLine($"[FontScanner] Scan cancelled");
            ReportProgress(0, "扫描已取消");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[FontScanner] Fatal error during scan: {ex.Message}");
            Trace.WriteLine($"[FontScanner] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            _assetsManager.UnloadAll();
        }

        return FontAssets;
    }

    /// <summary>
    /// 扫描资产文件
    /// </summary>
    private void ScanAssetFile(string assetPath)
    {
        try
        {
            // 跳过资源文件（.resource, .resS等），它们不是Unity资产文件
            var fileName = Path.GetFileName(assetPath);
            if (fileName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"[FontScanner] Skipping resource file: {fileName}");
                return;
            }
            
            Trace.WriteLine($"[FontScanner] Opening asset file: {assetPath}");
            
            // 使用文件路径直接加载（不使用stream，避免提前释放问题）
            var fileInst = _assetsManager.LoadAssetsFile(assetPath, true);
            
            if (fileInst?.file == null)
            {
                Trace.WriteLine($"[FontScanner] Failed to load asset file: {assetPath}");
                return;
            }
            
            Trace.WriteLine($"[FontScanner] Loaded asset file, Unity version: {fileInst.file.Metadata?.UnityVersion}");
            Trace.WriteLine($"[FontScanner] Asset count in file: {fileInst.file.AssetInfos?.Count ?? 0}");
            
            // 加载类型数据库（每个文件都需要检查版本）
            var version = fileInst.file.Metadata?.UnityVersion;
            if (!string.IsNullOrEmpty(version) && version != "0.0.0")
            {
                Trace.WriteLine($"[FontScanner] Loading class database for version: {version}");
                _assetsManager.LoadClassDatabaseFromPackage(version);
            }

            var fontsBeforeScan = FontAssets.Count;
            ScanAssetsFileInstance(fileInst);
            Trace.WriteLine($"[FontScanner] Found {FontAssets.Count - fontsBeforeScan} fonts in this file");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[FontScanner] Error scanning asset file {assetPath}: {ex.Message}");
            Trace.WriteLine($"[FontScanner] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描Bundle文件
    /// </summary>
    private void ScanBundleFile(string bundlePath)
    {
        try
        {
            Trace.WriteLine($"[FontScanner] Opening bundle file: {bundlePath}");
            var bunInst = _assetsManager.LoadBundleFile(bundlePath, true);
            
            if (bunInst?.file?.BlockAndDirInfo?.DirectoryInfos == null)
            {
                Trace.WriteLine($"[FontScanner] Bundle file has no directory info");
                return;
            }
            
            Trace.WriteLine($"[FontScanner] Bundle loaded, directory count: {bunInst.file.BlockAndDirInfo.DirectoryInfos.Count}");
            
            for (int i = 0; i < bunInst.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                try
                {
                    var dirInfo = bunInst.file.BlockAndDirInfo.DirectoryInfos[i];
                    var entryName = dirInfo.Name;
                    
                    // 跳过资源文件（.resource, .resS等）
                    if (entryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
                        entryName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
                    {
                        Trace.WriteLine($"[FontScanner] Skipping resource entry: {entryName}");
                        continue;
                    }
                    
                    var fileInst = _assetsManager.LoadAssetsFileFromBundle(bunInst, i);
                    
                    // 检查fileInst是否为null
                    if (fileInst?.file == null)
                    {
                        Trace.WriteLine($"[FontScanner] Bundle entry {i} ({entryName}) is not a valid assets file, skipping");
                        continue;
                    }
                    
                    // 加载类型数据库
                    var version = fileInst.file.Metadata?.UnityVersion;
                    if (!string.IsNullOrEmpty(version) && version != "0.0.0")
                    {
                        _assetsManager.LoadClassDatabaseFromPackage(version);
                    }
                    
                    ScanAssetsFileInstance(fileInst);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[FontScanner] Error processing bundle entry {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[FontScanner] Error scanning bundle file {bundlePath}: {ex.Message}");
            Trace.WriteLine($"[FontScanner] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描资产文件实例
    /// </summary>
    private void ScanAssetsFileInstance(AssetsFileInstance fileInst)
    {
        // 先收集所有Font和可能的TMP字体MonoBehaviour
        var fontInfos = new List<AssetFileInfo>();
        var monoBehaviourInfos = new List<AssetFileInfo>();
        int errorCount = 0;

        foreach (var info in fileInst.file.AssetInfos)
        {
            if (info.TypeId == (int)AssetClassID.Font)
            {
                fontInfos.Add(info);
            }
            else if (info.TypeId == (int)AssetClassID.MonoBehaviour)
            {
                monoBehaviourInfos.Add(info);
            }
        }

        Trace.WriteLine($"[FontScanner] Found {fontInfos.Count} Font assets, {monoBehaviourInfos.Count} MonoBehaviour assets");

        // 扫描传统字体
        foreach (var info in fontInfos)
        {
            try
            {
                ScanTraditionalFont(fileInst, info);
            }
            catch (Exception ex)
            {
                errorCount++;
                Trace.WriteLine($"[FontScanner] Error scanning font PathId={info.PathId}: {ex.Message}");
            }
        }

        // 扫描MonoBehaviour查找TMP字体
        foreach (var info in monoBehaviourInfos)
        {
            try
            {
                ScanMonoBehaviourForTmpFont(fileInst, info);
            }
            catch (Exception ex)
            {
                errorCount++;
                Trace.WriteLine($"[FontScanner] Error scanning MonoBehaviour PathId={info.PathId}: {ex.Message}");
            }
        }

        if (errorCount > 0)
        {
            Trace.WriteLine($"[FontScanner] Total errors in this file: {errorCount}");
        }
    }

    /// <summary>
    /// 扫描传统字体
    /// </summary>
    private void ScanTraditionalFont(AssetsFileInstance fileInst, AssetFileInfo info)
    {
        var baseField = _assetsManager.GetBaseField(fileInst, info);
        if (baseField == null) return;

        var name = baseField["m_Name"].AsString;

        var fontAsset = new FontAsset
        {
            Name = name,
            FontType = FontType.Traditional,
            SourceFile = fileInst.path,
            MonoBehaviourPathId = info.PathId
        };

        lock (FontAssets)
        {
            FontAssets.Add(fontAsset);
        }
    }

    /// <summary>
    /// 扫描MonoBehaviour查找TMP字体
    /// </summary>
    private void ScanMonoBehaviourForTmpFont(AssetsFileInstance fileInst, AssetFileInfo info)
    {
        var baseField = _assetsManager.GetBaseField(fileInst, info);
        if (baseField == null) return;

        // 首先尝试通过脚本引用检测TMP字体
        bool isTmpFont = false;
        string scriptName = "";
        
        // 获取m_Script引用
        var scriptField = baseField["m_Script"];
        if (scriptField != null && !scriptField.IsDummy)
        {
            try
            {
                // 尝试获取脚本资产以检查名称
                var scriptPtr = scriptField;
                var fileId = scriptPtr["m_FileID"]?.AsInt ?? 0;
                var pathId = scriptPtr["m_PathID"]?.AsLong ?? 0;
                
                if (pathId != 0)
                {
                    // 尝试从当前文件或外部文件获取脚本信息
                    AssetFileInfo? scriptInfo = null;
                    AssetsFileInstance? scriptFileInst = fileInst;
                    
                    if (fileId == 0)
                    {
                        scriptInfo = fileInst.file.GetAssetInfo(pathId);
                    }
                    else if (fileId > 0 && fileId <= fileInst.file.Metadata.Externals.Count)
                    {
                        // 外部文件引用，尝试获取
                        var ext = fileInst.file.Metadata.Externals[fileId - 1];
                        scriptFileInst = _assetsManager.LoadAssetsFileFromBundle(
                            _assetsManager.Files.FirstOrDefault()?.parentBundle, 
                            ext.PathName);
                        if (scriptFileInst != null)
                        {
                            scriptInfo = scriptFileInst.file.GetAssetInfo(pathId);
                        }
                    }
                    
                    if (scriptInfo != null && scriptFileInst != null)
                    {
                        var scriptBase = _assetsManager.GetBaseField(scriptFileInst, scriptInfo);
                        if (scriptBase != null)
                        {
                            scriptName = scriptBase["m_Name"]?.AsString ?? "";
                            // 检查是否是TMP相关脚本
                            if (scriptName.Contains("TMP_FontAsset", StringComparison.OrdinalIgnoreCase) ||
                                scriptName.Contains("TextMeshProFont", StringComparison.OrdinalIgnoreCase) ||
                                scriptName.Equals("FontAsset", StringComparison.OrdinalIgnoreCase))
                            {
                                isTmpFont = true;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略脚本解析错误
            }
        }
        
        // 如果脚本检测失败，尝试通过字段特征检测
        if (!isTmpFont)
        {
            isTmpFont = IsTmpFontAsset(baseField);
        }
        
        // 记录第一个MonoBehaviour的结构用于调试
        if (!_tmpDetectionLogged && baseField.Children.Count > 0)
        {
            _tmpDetectionLogged = true;
            Trace.WriteLine($"[FontScanner] Sample MonoBehaviour structure (first 10 fields):");
            foreach (var child in baseField.Children.Take(10))
            {
                Trace.WriteLine($"[FontScanner]   Field: {child.FieldName}, Type: {child.TypeName}, IsDummy: {child.IsDummy}");
            }
            if (baseField.Children.Count > 10)
            {
                Trace.WriteLine($"[FontScanner]   ... and {baseField.Children.Count - 10} more fields");
            }
        }
        
        if (!isTmpFont) return;
        
        Trace.WriteLine($"[FontScanner] Found TMP font! PathId={info.PathId}");

        var name = baseField["m_Name"]?.AsString ?? $"TMP_Font_{info.PathId}";
        
        // 获取字体信息
        var faceInfo = baseField["m_FaceInfo"];
        var familyName = "";
        var styleName = "";
        
        if (faceInfo != null && !faceInfo.IsDummy)
        {
            familyName = faceInfo["m_FamilyName"]?.AsString ?? "";
            styleName = faceInfo["m_StyleName"]?.AsString ?? "";
        }

        // 获取关联的Material
        long materialPathId = 0;
        var materialField = baseField["material"];
        if (materialField != null && !materialField.IsDummy)
        {
            materialPathId = materialField["m_PathID"]?.AsLong ?? 0;
        }

        // 获取关联的Texture2D（通过Material或atlas字段）
        long texturePathId = 0;
        var atlasTexturesField = baseField["m_AtlasTextures"];
        if (atlasTexturesField != null && !atlasTexturesField.IsDummy && atlasTexturesField.Children.Count > 0)
        {
            var firstTexture = atlasTexturesField.Children[0];
            texturePathId = firstTexture["m_PathID"]?.AsLong ?? 0;
        }

        var fontAsset = new FontAsset
        {
            Name = name,
            FontType = FontType.TMP,
            SourceFile = fileInst.path,
            MonoBehaviourPathId = info.PathId,
            MaterialPathId = materialPathId,
            Texture2DPathId = texturePathId,
            FamilyName = familyName,
            StyleName = styleName
        };

        lock (FontAssets)
        {
            FontAssets.Add(fontAsset);
        }
    }

    /// <summary>
    /// 检查MonoBehaviour是否是TMP字体资产
    /// </summary>
    private static bool IsTmpFontAsset(AssetTypeValueField baseField)
    {
        if (baseField == null) return false;
        
        // 检查是否包含TMP字体的特征字段
        int matchCount = 0;
        foreach (var field in TmpFontFields)
        {
            var child = baseField[field];
            if (child != null && !child.IsDummy)
            {
                matchCount++;
            }
        }

        // 如果匹配2个或更多特征字段，认为是TMP字体（降低阈值以提高检测率）
        if (matchCount >= 2) return true;
        
        // 备用检测：检查字段名称中是否包含TMP特征
        try
        {
            foreach (var child in baseField.Children)
            {
                var fieldName = child.FieldName?.ToLowerInvariant() ?? "";
                if (fieldName.Contains("glyph") || 
                    fieldName.Contains("faceinfo") || 
                    fieldName.Contains("atlastexture") ||
                    fieldName.Contains("charactertable") ||
                    fieldName.Contains("fontfeature"))
                {
                    return true;
                }
            }
        }
        catch
        {
            // 忽略枚举错误
        }
        
        return false;
    }

    /// <summary>
    /// 报告进度
    /// </summary>
    private void ReportProgress(float progress, string message)
    {
        ProgressChanged?.Invoke(progress, message);
    }
}
