using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityLocalizationToolkit.Models;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// 资产修改服务 - 用于将翻译应用到游戏文件
/// </summary>
public class AssetModifierService
{
    private static AssetModifierService? _instance;
    public static AssetModifierService Instance => _instance ??= new AssetModifierService();

    /// <summary>
    /// 进度变化事件
    /// </summary>
    public event Action<float, string>? ProgressChanged;

    /// <summary>
    /// 应用文本翻译
    /// </summary>
    /// <param name="project">游戏项目</param>
    /// <param name="entries">已翻译的文本条目</param>
    /// <returns>成功修改的条目数量</returns>
    public async Task<int> ApplyTextTranslationsAsync(GameProject project, List<TextEntry> entries)
    {
        var translatedEntries = entries.Where(e => e.IsTranslated && !e.ShouldSkip).ToList();
        if (translatedEntries.Count == 0) return 0;

        var modifiedCount = 0;

        // 按来源类型分组处理
        var scriptEntries = translatedEntries.Where(e => e.SourceType == TextSourceType.Script).ToList();
        var assetEntries = translatedEntries.Where(e => e.SourceType != TextSourceType.Script).ToList();

        // 处理脚本文本
        if (scriptEntries.Count > 0 && project.BackendType == UnityBackendType.Mono)
        {
            ReportProgress(0.1f, "正在修改Assembly-CSharp.dll...");
            modifiedCount += await Task.Run(() => ModifyAssemblyCSharp(project.AssemblyCSharpPath, scriptEntries));
        }

        // 处理资产文本
        if (assetEntries.Count > 0)
        {
            ReportProgress(0.5f, "正在修改资产文件...");
            modifiedCount += await Task.Run(() => ModifyAssetEntries(assetEntries));
        }

        ReportProgress(1f, $"完成，成功修改 {modifiedCount} 条文本");
        return modifiedCount;
    }

    /// <summary>
    /// 修改Assembly-CSharp.dll中的字符串
    /// </summary>
    private int ModifyAssemblyCSharp(string dllPath, List<TextEntry> entries)
    {
        var modifiedCount = 0;

        try
        {
            // 创建备份
            var backupPath = dllPath + ".backup";
            if (!File.Exists(backupPath))
            {
                File.Copy(dllPath, backupPath);
            }

            // 构建查找字典
            var entryDict = entries.ToDictionary(e => (e.ClassName, e.MethodName, e.Offset), e => e);

            using var assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = true });

            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    modifiedCount += ModifyTypeStrings(type, entryDict);
                }
            }

            assembly.Write();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AssetModifier] Error modifying assembly: {ex.Message}");
            Trace.WriteLine($"[AssetModifier] Stack trace: {ex.StackTrace}");
        }

        return modifiedCount;
    }

    /// <summary>
    /// 递归修改类型中的字符串
    /// </summary>
    private int ModifyTypeStrings(TypeDefinition type, Dictionary<(string, string, long), TextEntry> entryDict)
    {
        var modifiedCount = 0;

        foreach (var nestedType in type.NestedTypes)
        {
            modifiedCount += ModifyTypeStrings(nestedType, entryDict);
        }

        foreach (var method in type.Methods)
        {
            if (method.HasBody)
            {
                modifiedCount += ModifyMethodStrings(method, entryDict);
            }
        }

        return modifiedCount;
    }

    /// <summary>
    /// 修改方法中的字符串
    /// </summary>
    private int ModifyMethodStrings(MethodDefinition method, Dictionary<(string, string, long), TextEntry> entryDict)
    {
        var modifiedCount = 0;

        try
        {
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode.Code == Code.Ldstr)
                {
                    var key = (method.DeclaringType.FullName, method.Name, (long)instruction.Offset);
                    if (entryDict.TryGetValue(key, out var entry))
                    {
                        instruction.Operand = entry.TranslatedText;
                        modifiedCount++;
                    }
                }
            }
        }
        catch
        {
            // 忽略无法修改的方法
        }

        return modifiedCount;
    }

    /// <summary>
    /// 修改资产文件中的文本
    /// </summary>
    private int ModifyAssetEntries(List<TextEntry> entries)
    {
        var modifiedCount = 0;
        var assetsManager = new AssetsManager();

        try
        {
            var classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                assetsManager.LoadClassPackage(classDataPath);
            }

            // 按源文件分组
            var entriesByFile = entries.GroupBy(e => e.SourceFile).ToList();

            foreach (var group in entriesByFile)
            {
                var filePath = group.Key;
                var fileEntries = group.ToList();

                try
                {
                    modifiedCount += ModifySingleAssetFile(assetsManager, filePath, fileEntries);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[AssetModifier] Error modifying file {filePath}: {ex.Message}");
                    Trace.WriteLine($"[AssetModifier] Stack trace: {ex.StackTrace}");
                }
            }
        }
        finally
        {
            assetsManager.UnloadAll();
        }

        return modifiedCount;
    }

    /// <summary>
    /// 修改单个资产文件
    /// </summary>
    private int ModifySingleAssetFile(AssetsManager manager, string filePath, List<TextEntry> entries)
    {
        var modifiedCount = 0;

        // 创建备份
        var backupPath = filePath + ".backup";
        if (!File.Exists(backupPath))
        {
            File.Copy(filePath, backupPath);
        }

        // 读取文件（使用文件路径直接加载）
        var fileInst = manager.LoadAssetsFile(filePath, false);

        // 加载类型数据库
        var version = fileInst.file.Metadata.UnityVersion;
        if (!string.IsNullOrEmpty(version) && version != "0.0.0")
        {
            manager.LoadClassDatabaseFromPackage(version);
        }

        bool hasModifications = false;

        foreach (var entry in entries)
        {
            try
            {
                var info = fileInst.file.GetAssetInfo(entry.PathId);
                if (info == null) continue;

                var baseField = manager.GetBaseField(fileInst, info);
                if (baseField == null) continue;

                bool modified = false;

                if (entry.SourceType == TextSourceType.TextAsset)
                {
                    // 修改TextAsset的m_Script字段
                    var scriptField = baseField["m_Script"];
                    if (scriptField != null)
                    {
                        scriptField.AsByteArray = Encoding.UTF8.GetBytes(entry.TranslatedText);
                        modified = true;
                    }
                }
                else if (entry.SourceType == TextSourceType.MonoBehaviour)
                {
                    // 修改MonoBehaviour的指定字段
                    modified = ModifyFieldByPath(baseField, entry.FieldPath, entry.TranslatedText);
                }

                if (modified)
                {
                    // 使用SetNewData更新资产数据
                    var newData = baseField.WriteToByteArray();
                    info.SetNewData(newData);
                    hasModifications = true;
                    modifiedCount++;
                }
            }
            catch
            {
                // 跳过无法修改的条目
            }
        }

        if (hasModifications)
        {
            // 写入修改后的文件
            using var writeStream = File.Open(filePath, FileMode.Create, FileAccess.Write);
            using var writer = new AssetsFileWriter(writeStream);
            fileInst.file.Write(writer);
        }

        return modifiedCount;
    }

    /// <summary>
    /// 根据路径修改字段值
    /// </summary>
    private bool ModifyFieldByPath(AssetTypeValueField baseField, string path, string newValue)
    {
        var parts = path.Split('.');
        var current = baseField;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var child = current[parts[i]];
            if (child == null || child.IsDummy) return false;
            current = child;
        }

        var targetField = current[parts[^1]];
        if (targetField != null && targetField.TypeName == "string")
        {
            targetField.AsString = newValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 替换字体资源
    /// </summary>
    /// <param name="fontAsset">要替换的字体资产</param>
    /// <param name="newFontPath">新字体文件路径</param>
    /// <returns>是否成功</returns>
    public async Task<bool> ReplaceFontAsync(FontAsset fontAsset, string newFontPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                ReportProgress(0.1f, $"正在替换字体: {fontAsset.Name}");

                if (fontAsset.FontType == FontType.Traditional)
                {
                    return ReplaceTraditionalFont(fontAsset, newFontPath);
                }
                else
                {
                    return ReplaceTmpFont(fontAsset, newFontPath);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[AssetModifier] Error replacing font: {ex.Message}");
                Trace.WriteLine($"[AssetModifier] Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                ReportProgress(1f, "字体替换完成");
            }
        });
    }

    /// <summary>
    /// 替换传统字体
    /// </summary>
    private bool ReplaceTraditionalFont(FontAsset fontAsset, string newFontPath)
    {
        var assetsManager = new AssetsManager();

        try
        {
            var classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
            {
                assetsManager.LoadClassPackage(classDataPath);
            }

            // 创建备份
            var backupPath = fontAsset.SourceFile + ".backup";
            if (!File.Exists(backupPath))
            {
                File.Copy(fontAsset.SourceFile, backupPath);
            }

            // 读取新字体数据
            var fontData = File.ReadAllBytes(newFontPath);

            // 读取资产文件（使用文件路径直接加载）
            var fileInst = assetsManager.LoadAssetsFile(fontAsset.SourceFile, false);

            // 加载类型数据库
            var version = fileInst.file.Metadata.UnityVersion;
            if (!string.IsNullOrEmpty(version) && version != "0.0.0")
            {
                assetsManager.LoadClassDatabaseFromPackage(version);
            }

            var info = fileInst.file.GetAssetInfo(fontAsset.MonoBehaviourPathId);
            if (info == null) return false;

            var baseField = assetsManager.GetBaseField(fileInst, info);
            if (baseField == null) return false;

            // 更新字体数据
            var fontDataField = baseField["m_FontData"];
            if (fontDataField != null)
            {
                fontDataField.AsByteArray = fontData;
            }

            var newAssetData = baseField.WriteToByteArray();
            info.SetNewData(newAssetData);

            // 写入文件
            using var writeStream = File.Open(fontAsset.SourceFile, FileMode.Create, FileAccess.Write);
            using var writer = new AssetsFileWriter(writeStream);
            fileInst.file.Write(writer);

            fontAsset.Status = FontReplacementStatus.Replaced;
            fontAsset.ReplacementFontPath = newFontPath;
            return true;
        }
        catch
        {
            fontAsset.Status = FontReplacementStatus.Failed;
            return false;
        }
        finally
        {
            assetsManager.UnloadAll();
        }
    }

    /// <summary>
    /// 替换TMP字体（简化版本，实际需要更复杂的处理）
    /// </summary>
    private bool ReplaceTmpFont(FontAsset fontAsset, string newFontPath)
    {
        // TMP字体替换需要重新生成字体图集，这是一个复杂的过程
        // 完整实现需要：
        // 1. 使用新字体生成新的SDF纹理图集
        // 2. 更新MonoBehaviour中的字形表和字符表
        // 3. 更新Material中的纹理引用
        // 4. 更新Texture2D资产

        // 这里提供一个简化的实现框架
        ReportProgress(0.3f, "TMP字体替换需要额外的处理...");
        
        // 标记为需要手动处理
        fontAsset.Status = FontReplacementStatus.Failed;
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
