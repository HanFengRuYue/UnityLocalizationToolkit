using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Mono.Cecil;
using UnityLocalizationToolkit.Models;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// 文本扫描服务 - 用于从游戏资产中提取文本
/// </summary>
public class TextScannerService
{
    private static TextScannerService? _instance;
    public static TextScannerService Instance => _instance ??= new TextScannerService();

    private readonly AssetsManager _assetsManager;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 扫描进度变化事件
    /// </summary>
    public event Action<float, string>? ProgressChanged;

    /// <summary>
    /// 扫描到的文本条目列表
    /// </summary>
    public List<TextEntry> TextEntries { get; } = [];

    public TextScannerService()
    {
        _assetsManager = new AssetsManager();
        
        // 加载类型数据库
        var classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
        Trace.WriteLine($"[TextScanner] Initializing, looking for classdata.tpk at: {classDataPath}");
        if (File.Exists(classDataPath))
        {
            try
            {
                _assetsManager.LoadClassPackage(classDataPath);
                Trace.WriteLine($"[TextScanner] Successfully loaded classdata.tpk");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[TextScanner] ERROR loading classdata.tpk: {ex.Message}");
            }
        }
        else
        {
            Trace.WriteLine($"[TextScanner] WARNING: classdata.tpk not found!");
        }
    }

    /// <summary>
    /// 开始扫描游戏文本
    /// </summary>
    public async Task<List<TextEntry>> ScanAsync(GameProject project, SourceLanguage sourceLanguage, CancellationToken cancellationToken = default)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        TextEntries.Clear();

        Trace.WriteLine($"[TextScanner] ========== Starting text scan ==========");
        Trace.WriteLine($"[TextScanner] Project path: {project.RootPath}");
        Trace.WriteLine($"[TextScanner] Data path: {project.DataPath}");
        Trace.WriteLine($"[TextScanner] Backend type: {project.BackendType}");
        Trace.WriteLine($"[TextScanner] Asset files count: {project.AssetFiles.Count}");
        Trace.WriteLine($"[TextScanner] Bundle files count: {project.BundleFiles.Count}");
        Trace.WriteLine($"[TextScanner] Source language: {sourceLanguage}");
        Trace.WriteLine($"[TextScanner] classdata.tpk exists: {File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk"))}");

        try
        {
            // 1. 扫描Assembly-CSharp.dll (Mono后端)
            if (project.BackendType == UnityBackendType.Mono && !string.IsNullOrEmpty(project.AssemblyCSharpPath))
            {
                Trace.WriteLine($"[TextScanner] Scanning Assembly-CSharp.dll: {project.AssemblyCSharpPath}");
                ReportProgress(0, "正在扫描Assembly-CSharp.dll...");
                await Task.Run(() => ScanAssemblyCSharp(project.AssemblyCSharpPath, sourceLanguage), _cancellationTokenSource.Token);
                Trace.WriteLine($"[TextScanner] Assembly-CSharp.dll scan complete, found {TextEntries.Count} entries so far");
            }

            // 2. 扫描资产文件
            var totalFiles = project.AssetFiles.Count + project.BundleFiles.Count;
            var processedFiles = 0;

            foreach (var assetFile in project.AssetFiles)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                var progress = (float)processedFiles / totalFiles;
                ReportProgress(progress, $"正在扫描: {Path.GetFileName(assetFile)}");

                await Task.Run(() => ScanAssetFile(assetFile, sourceLanguage), _cancellationTokenSource.Token);
                processedFiles++;
            }

            // 3. 扫描Bundle文件
            foreach (var bundleFile in project.BundleFiles)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                var progress = (float)processedFiles / totalFiles;
                ReportProgress(progress, $"正在扫描Bundle: {Path.GetFileName(bundleFile)}");

                await Task.Run(() => ScanBundleFile(bundleFile, sourceLanguage), _cancellationTokenSource.Token);
                processedFiles++;
            }

            Trace.WriteLine($"[TextScanner] ========== Scan complete ==========");
            Trace.WriteLine($"[TextScanner] Total text entries found: {TextEntries.Count}");
            ReportProgress(1, $"扫描完成，找到 {TextEntries.Count} 条文本");
        }
        catch (OperationCanceledException)
        {
            Trace.WriteLine($"[TextScanner] Scan cancelled");
            ReportProgress(0, "扫描已取消");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TextScanner] Fatal error during scan: {ex.Message}");
            Trace.WriteLine($"[TextScanner] Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            _assetsManager.UnloadAll();
        }

        return TextEntries;
    }

    /// <summary>
    /// 停止扫描
    /// </summary>
    public void StopScan()
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// 扫描Assembly-CSharp.dll中的字符串
    /// </summary>
    private void ScanAssemblyCSharp(string dllPath, SourceLanguage sourceLanguage)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(dllPath);
            
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    ScanType(type, dllPath, sourceLanguage);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TextScanner] Error scanning assembly: {ex.Message}");
            Trace.WriteLine($"[TextScanner] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描类型中的字符串
    /// </summary>
    private void ScanType(TypeDefinition type, string dllPath, SourceLanguage sourceLanguage)
    {
        // 扫描嵌套类型
        foreach (var nestedType in type.NestedTypes)
        {
            ScanType(nestedType, dllPath, sourceLanguage);
        }

        // 扫描方法
        foreach (var method in type.Methods)
        {
            if (method.HasBody)
            {
                ScanMethodBody(method, dllPath, sourceLanguage);
            }
        }
    }

    /// <summary>
    /// 扫描方法体中的字符串
    /// </summary>
    private void ScanMethodBody(MethodDefinition method, string dllPath, SourceLanguage sourceLanguage)
    {
        try
        {
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr && instruction.Operand is string str)
                {
                    if (LanguageFilter.IsLikelyGameText(str, sourceLanguage))
                    {
                        var entry = new TextEntry
                        {
                            Id = $"script_{method.DeclaringType.FullName}_{method.Name}_{instruction.Offset}",
                            OriginalText = str,
                            SourceType = TextSourceType.Script,
                            SourceFile = dllPath,
                            ClassName = method.DeclaringType.FullName,
                            MethodName = method.Name,
                            Offset = instruction.Offset
                        };

                        if (LanguageFilter.ShouldSkipTranslation(str, out var reason))
                        {
                            entry.ShouldSkip = true;
                            entry.SkipReason = reason;
                        }

                        lock (TextEntries)
                        {
                            TextEntries.Add(entry);
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略无法解析的方法
        }
    }

    /// <summary>
    /// 扫描资产文件
    /// </summary>
    private void ScanAssetFile(string assetPath, SourceLanguage sourceLanguage)
    {
        try
        {
            // 跳过资源文件（.resource, .resS等），它们不是Unity资产文件
            var fileName = Path.GetFileName(assetPath);
            if (fileName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
            {
                Trace.WriteLine($"[TextScanner] Skipping resource file: {fileName}");
                return;
            }
            
            Trace.WriteLine($"[TextScanner] Opening asset file: {assetPath}");
            
            // 使用文件路径直接加载（不使用stream，避免提前释放问题）
            var fileInst = _assetsManager.LoadAssetsFile(assetPath, true);
            
            if (fileInst?.file == null)
            {
                Trace.WriteLine($"[TextScanner] Failed to load asset file: {assetPath}");
                return;
            }
            
            Trace.WriteLine($"[TextScanner] Loaded asset file, Unity version: {fileInst.file.Metadata?.UnityVersion}");
            Trace.WriteLine($"[TextScanner] Asset count in file: {fileInst.file.AssetInfos?.Count ?? 0}");
            
            // 加载类型数据库（每个文件都需要检查版本）
            var version = fileInst.file.Metadata?.UnityVersion;
            if (!string.IsNullOrEmpty(version) && version != "0.0.0")
            {
                Trace.WriteLine($"[TextScanner] Loading class database for version: {version}");
                _assetsManager.LoadClassDatabaseFromPackage(version);
            }

            var entriesBeforeScan = TextEntries.Count;
            ScanAssetsFileInstance(fileInst, sourceLanguage);
            Trace.WriteLine($"[TextScanner] Found {TextEntries.Count - entriesBeforeScan} text entries in this file");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TextScanner] Error scanning asset file {assetPath}: {ex.Message}");
            Trace.WriteLine($"[TextScanner] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描Bundle文件
    /// </summary>
    private void ScanBundleFile(string bundlePath, SourceLanguage sourceLanguage)
    {
        try
        {
            Trace.WriteLine($"[TextScanner] Opening bundle file: {bundlePath}");
            var bunInst = _assetsManager.LoadBundleFile(bundlePath, true);
            
            if (bunInst?.file?.BlockAndDirInfo?.DirectoryInfos == null)
            {
                Trace.WriteLine($"[TextScanner] Bundle file has no directory info");
                return;
            }
            
            Trace.WriteLine($"[TextScanner] Bundle loaded, directory count: {bunInst.file.BlockAndDirInfo.DirectoryInfos.Count}");
            
            // 扫描bundle中的每个资产文件
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
                        continue;
                    }
                    
                    var fileInst = _assetsManager.LoadAssetsFileFromBundle(bunInst, i);
                    
                    // 检查fileInst是否为null
                    if (fileInst?.file == null)
                    {
                        Trace.WriteLine($"[TextScanner] Bundle entry {i} ({entryName}) is not a valid assets file, skipping");
                        continue;
                    }
                    
                    // 加载类型数据库
                    var version = fileInst.file.Metadata?.UnityVersion;
                    if (!string.IsNullOrEmpty(version) && version != "0.0.0")
                    {
                        _assetsManager.LoadClassDatabaseFromPackage(version);
                    }
                    
                    ScanAssetsFileInstance(fileInst, sourceLanguage);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[TextScanner] Error processing bundle entry {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[TextScanner] Error scanning bundle file {bundlePath}: {ex.Message}");
            Trace.WriteLine($"[TextScanner] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 扫描资产文件实例
    /// </summary>
    private void ScanAssetsFileInstance(AssetsFileInstance fileInst, SourceLanguage sourceLanguage)
    {
        int textAssetCount = 0;
        int monoBehaviourCount = 0;
        int errorCount = 0;

        foreach (var info in fileInst.file.AssetInfos)
        {
            try
            {
                // 扫描TextAsset
                if (info.TypeId == (int)AssetClassID.TextAsset)
                {
                    textAssetCount++;
                    ScanTextAsset(fileInst, info, sourceLanguage);
                }
                // 扫描MonoBehaviour
                else if (info.TypeId == (int)AssetClassID.MonoBehaviour)
                {
                    monoBehaviourCount++;
                    ScanMonoBehaviour(fileInst, info, sourceLanguage);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                Trace.WriteLine($"[TextScanner] Error processing asset PathId={info.PathId}, TypeId={info.TypeId}: {ex.Message}");
            }
        }

        Trace.WriteLine($"[TextScanner] Processed {textAssetCount} TextAssets, {monoBehaviourCount} MonoBehaviours, {errorCount} errors");
    }

    /// <summary>
    /// 扫描TextAsset
    /// </summary>
    private void ScanTextAsset(AssetsFileInstance fileInst, AssetFileInfo info, SourceLanguage sourceLanguage)
    {
        try
        {
            var baseField = _assetsManager.GetBaseField(fileInst, info);
            if (baseField == null) return;

            var name = baseField["m_Name"].AsString;
            var scriptData = baseField["m_Script"].AsByteArray;

            if (scriptData == null || scriptData.Length == 0) return;

            // 尝试将数据解释为文本
            var text = Encoding.UTF8.GetString(scriptData);
            
            if (LanguageFilter.IsLikelyGameText(text, sourceLanguage))
            {
                var entry = new TextEntry
                {
                    Id = $"textasset_{fileInst.name}_{info.PathId}",
                    OriginalText = text,
                    SourceType = TextSourceType.TextAsset,
                    SourceFile = fileInst.path,
                    PathId = info.PathId,
                    FieldPath = name
                };

                if (LanguageFilter.ShouldSkipTranslation(text, out var reason))
                {
                    entry.ShouldSkip = true;
                    entry.SkipReason = reason;
                }

                lock (TextEntries)
                {
                    TextEntries.Add(entry);
                }
            }
        }
        catch
        {
            // 跳过无法解析的TextAsset
        }
    }

    /// <summary>
    /// 用于追踪是否已记录过MonoBehaviour结构信息
    /// </summary>
    private static bool _monoStructureLogged = false;
    
    /// <summary>
    /// 扫描MonoBehaviour中的字符串字段
    /// </summary>
    private void ScanMonoBehaviour(AssetsFileInstance fileInst, AssetFileInfo info, SourceLanguage sourceLanguage)
    {
        try
        {
            var baseField = _assetsManager.GetBaseField(fileInst, info);
            if (baseField == null) return;

            // 记录第一个有字段的MonoBehaviour结构用于调试
            if (!_monoStructureLogged && baseField.Children.Count > 3)
            {
                _monoStructureLogged = true;
                Trace.WriteLine($"[TextScanner] Sample MonoBehaviour structure (PathId={info.PathId}):");
                foreach (var child in baseField.Children.Take(15))
                {
                    var valuePreview = "";
                    if (child.TypeName == "string" && child.Value != null)
                    {
                        var str = child.AsString;
                        valuePreview = str.Length > 50 ? $" = \"{str.Substring(0, 50)}...\"" : $" = \"{str}\"";
                    }
                    Trace.WriteLine($"[TextScanner]   {child.FieldName}: {child.TypeName}, IsDummy={child.IsDummy}{valuePreview}");
                }
            }

            ScanFieldRecursive(fileInst, info, baseField, "", sourceLanguage);
        }
        catch
        {
            // 跳过无法解析的MonoBehaviour
        }
    }

    /// <summary>
    /// 递归扫描字段中的字符串
    /// </summary>
    private void ScanFieldRecursive(AssetsFileInstance fileInst, AssetFileInfo info, AssetTypeValueField field, string path, SourceLanguage sourceLanguage)
    {
        if (field == null || field.IsDummy) return;
        
        var fieldName = field.FieldName ?? "";
        var currentPath = string.IsNullOrEmpty(path) ? fieldName : $"{path}.{fieldName}";

        // 如果是字符串类型
        if (field.TypeName == "string" && field.Value != null)
        {
            var text = field.AsString;
            if (!string.IsNullOrEmpty(text) && LanguageFilter.IsLikelyGameText(text, sourceLanguage))
            {
                var entry = new TextEntry
                {
                    Id = $"mono_{fileInst.name}_{info.PathId}_{currentPath}",
                    OriginalText = text,
                    SourceType = TextSourceType.MonoBehaviour,
                    SourceFile = fileInst.path,
                    PathId = info.PathId,
                    FieldPath = currentPath
                };

                if (LanguageFilter.ShouldSkipTranslation(text, out var reason))
                {
                    entry.ShouldSkip = true;
                    entry.SkipReason = reason;
                }

                lock (TextEntries)
                {
                    TextEntries.Add(entry);
                }
            }
        }

        // 递归扫描子字段
        foreach (var child in field.Children)
        {
            ScanFieldRecursive(fileInst, info, child, currentPath, sourceLanguage);
        }
    }

    /// <summary>
    /// 报告进度
    /// </summary>
    private void ReportProgress(float progress, string message)
    {
        ProgressChanged?.Invoke(progress, message);
    }
}
