using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using UnityLocalizationToolkit.Models;

namespace UnityLocalizationToolkit.Services;

/// <summary>
/// Excel服务 - 用于导出和导入翻译文本
/// </summary>
public class ExcelService
{
    private static ExcelService? _instance;
    public static ExcelService Instance => _instance ??= new ExcelService();

    /// <summary>
    /// 导出文本到Excel文件
    /// </summary>
    /// <param name="entries">文本条目列表</param>
    /// <param name="filePath">导出文件路径</param>
    public async Task ExportAsync(List<TextEntry> entries, string filePath)
    {
        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            
            // 按来源类型分组创建工作表
            var scriptEntries = entries.Where(e => e.SourceType == TextSourceType.Script && !e.ShouldSkip).ToList();
            var monoEntries = entries.Where(e => e.SourceType == TextSourceType.MonoBehaviour && !e.ShouldSkip).ToList();
            var textAssetEntries = entries.Where(e => e.SourceType == TextSourceType.TextAsset && !e.ShouldSkip).ToList();
            var skippedEntries = entries.Where(e => e.ShouldSkip).ToList();

            if (scriptEntries.Count > 0)
            {
                CreateWorksheet(workbook, "Script", scriptEntries);
            }

            if (monoEntries.Count > 0)
            {
                CreateWorksheet(workbook, "MonoBehaviour", monoEntries);
            }

            if (textAssetEntries.Count > 0)
            {
                CreateWorksheet(workbook, "TextAsset", textAssetEntries);
            }

            if (skippedEntries.Count > 0)
            {
                CreateWorksheet(workbook, "Skipped", skippedEntries, includeSkipReason: true);
            }

            // 如果没有任何数据，创建一个空的说明工作表
            if (workbook.Worksheets.Count == 0)
            {
                var ws = workbook.AddWorksheet("Info");
                ws.Cell("A1").Value = "没有找到需要翻译的文本";
            }

            workbook.SaveAs(filePath);
        });
    }

    /// <summary>
    /// 创建工作表
    /// </summary>
    private void CreateWorksheet(XLWorkbook workbook, string sheetName, List<TextEntry> entries, bool includeSkipReason = false)
    {
        var worksheet = workbook.AddWorksheet(sheetName);

        // 设置表头
        var headers = new List<string> { "ID", "原文", "译文", "类型", "位置" };
        if (includeSkipReason)
        {
            headers.Add("跳过原因");
        }

        for (int i = 0; i < headers.Count; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // 填充数据
        for (int row = 0; row < entries.Count; row++)
        {
            var entry = entries[row];
            var rowNum = row + 2;

            worksheet.Cell(rowNum, 1).Value = entry.Id;
            worksheet.Cell(rowNum, 2).Value = entry.OriginalText;
            worksheet.Cell(rowNum, 3).Value = entry.TranslatedText;
            worksheet.Cell(rowNum, 4).Value = entry.SourceType.ToString();
            worksheet.Cell(rowNum, 5).Value = entry.DisplayLocation;

            if (includeSkipReason)
            {
                worksheet.Cell(rowNum, 6).Value = entry.SkipReason;
            }
        }

        // 调整列宽
        worksheet.Columns().AdjustToContents(1, 100);
        
        // 限制原文和译文列的最大宽度
        if (worksheet.Column(2).Width > 60) worksheet.Column(2).Width = 60;
        if (worksheet.Column(3).Width > 60) worksheet.Column(3).Width = 60;

        // 冻结首行
        worksheet.SheetView.FreezeRows(1);
    }

    /// <summary>
    /// 从Excel文件导入翻译文本
    /// </summary>
    /// <param name="entries">原始文本条目列表</param>
    /// <param name="filePath">导入文件路径</param>
    /// <returns>更新的条目数量</returns>
    public async Task<int> ImportAsync(List<TextEntry> entries, string filePath)
    {
        return await Task.Run(() =>
        {
            var updatedCount = 0;
            var entryDict = entries.ToDictionary(e => e.Id, e => e);

            using var workbook = new XLWorkbook(filePath);

            foreach (var worksheet in workbook.Worksheets)
            {
                // 跳过说明工作表
                if (worksheet.Name == "Info" || worksheet.Name == "Skipped") continue;

                var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                for (int row = 2; row <= rowCount; row++)
                {
                    var id = worksheet.Cell(row, 1).GetString();
                    var translatedText = worksheet.Cell(row, 3).GetString();

                    if (!string.IsNullOrEmpty(id) && entryDict.TryGetValue(id, out var entry))
                    {
                        if (!string.IsNullOrEmpty(translatedText) && translatedText != entry.OriginalText)
                        {
                            entry.TranslatedText = translatedText;
                            updatedCount++;
                        }
                    }
                }
            }

            return updatedCount;
        });
    }

    /// <summary>
    /// 验证Excel文件格式
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否有效</returns>
    public bool ValidateExcelFile(string filePath)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            return workbook.Worksheets.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
