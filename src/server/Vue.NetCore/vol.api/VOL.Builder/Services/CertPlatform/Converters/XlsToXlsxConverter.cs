/*
 * XLS 转 XLSX 转换器
 * 使用 NPOI 库实现
 */
using System;
using System.IO;
using System.Threading.Tasks;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace VOL.Builder.Services.CertPlatform.Converters
{
    /// <summary>
    /// XLS 转 XLSX 转换器
    /// </summary>
    public class XlsToXlsxConverter
    {
        /// <summary>
        /// 将 XLS 文件转换为 XLSX 格式
        /// </summary>
        /// <param name="inputStream">输入 XLS 文件流</param>
        /// <param name="outputStream">输出 XLSX 文件流</param>
        /// <returns>转换结果</returns>
        public async Task<ConvertResult> ConvertAsync(Stream inputStream, Stream outputStream)
        {
            try
            {
                // 复制流到新的内存流，避免 NPOI 关闭原始流
                var memoryStream = new MemoryStream();
                await inputStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                // 读取旧版 XLS 文件
                var hssfWorkbook = new HSSFWorkbook(memoryStream);
                
                // 创建新版 XLSX 工作簿
                var xssfWorkbook = new XSSFWorkbook();
                
                // 复制每个工作表
                for (int i = 0; i < hssfWorkbook.NumberOfSheets; i++)
                {
                    var sourceSheet = hssfWorkbook.GetSheetAt(i);
                    var targetSheet = xssfWorkbook.CreateSheet(sourceSheet.SheetName);
                    
                    // 复制工作表内容
                    CopySheet(sourceSheet, targetSheet, hssfWorkbook, xssfWorkbook);
                }
                
                // 写入输出流（leaveOpen: true 不关闭流）
                xssfWorkbook.Write(outputStream, true);
                
                return new ConvertResult
                {
                    Success = true,
                    Message = "转换成功"
                };
            }
            catch (Exception ex)
            {
                return new ConvertResult
                {
                    Success = false,
                    Message = $"转换失败: {ex.Message}"
                };
            }
        }
        
        /// <summary>
        /// 复制工作表内容
        /// </summary>
        private void CopySheet(ISheet sourceSheet, ISheet targetSheet, 
                               IWorkbook sourceWorkbook, IWorkbook targetWorkbook)
        {
            // 复制列宽
            for (int i = 0; i <= sourceSheet.LastRowNum; i++)
            {
                var sourceRow = sourceSheet.GetRow(i);
                if (sourceRow == null) continue;
                
                var targetRow = targetSheet.CreateRow(i);
                
                // 复制行高
                targetRow.Height = sourceRow.Height;
                
                for (int j = 0; j < sourceRow.LastCellNum; j++)
                {
                    var sourceCell = sourceRow.GetCell(j);
                    if (sourceCell == null) continue;
                    
                    var targetCell = targetRow.CreateCell(j);
                    
                    // 复制单元格值
                    CopyCellValue(sourceCell, targetCell);
                    
                    // 复制单元格样式
                    CopyCellStyle(sourceCell, targetCell, sourceWorkbook, targetWorkbook);
                }
            }
            
            // 复制合并单元格
            for (int i = 0; i < sourceSheet.NumMergedRegions; i++)
            {
                var region = sourceSheet.GetMergedRegion(i);
                targetSheet.AddMergedRegion(region);
            }
            
            // 复制列宽（获取源工作表的最大列数）
            int maxColumnNum = 0;
            for (int i = 0; i <= sourceSheet.LastRowNum; i++)
            {
                var row = sourceSheet.GetRow(i);
                if (row != null && row.LastCellNum > maxColumnNum)
                {
                    maxColumnNum = row.LastCellNum;
                }
            }
            
            for (int i = 0; i <= maxColumnNum; i++)
            {
                targetSheet.SetColumnWidth(i, sourceSheet.GetColumnWidth(i));
            }
        }
        
        /// <summary>
        /// 复制单元格值
        /// </summary>
        private void CopyCellValue(ICell sourceCell, ICell targetCell)
        {
            switch (sourceCell.CellType)
            {
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(sourceCell) && sourceCell.DateCellValue.HasValue)
                    {
                        targetCell.SetCellValue(sourceCell.DateCellValue.Value);
                    }
                    else
                    {
                        targetCell.SetCellValue(sourceCell.NumericCellValue);
                    }
                    break;
                    
                case CellType.String:
                    targetCell.SetCellValue(sourceCell.StringCellValue);
                    break;
                    
                case CellType.Boolean:
                    targetCell.SetCellValue(sourceCell.BooleanCellValue);
                    break;
                    
                case CellType.Formula:
                    targetCell.SetCellFormula(sourceCell.CellFormula);
                    break;
                    
                case CellType.Error:
                    targetCell.SetCellErrorValue(sourceCell.ErrorCellValue);
                    break;
                    
                default:
                    targetCell.SetCellValue(sourceCell.ToString());
                    break;
            }
        }
        
        /// <summary>
        /// 复制单元格样式
        /// </summary>
        private void CopyCellStyle(ICell sourceCell, ICell targetCell, 
                                   IWorkbook sourceWorkbook, IWorkbook targetWorkbook)
        {
            if (sourceCell.CellStyle == null) return;
            
            // 创建新样式
            var newStyle = targetWorkbook.CreateCellStyle();
            var sourceStyle = sourceCell.CellStyle;
            
            // 复制对齐方式
            newStyle.Alignment = sourceStyle.Alignment;
            newStyle.VerticalAlignment = sourceStyle.VerticalAlignment;
            
            // 复制边框
            newStyle.BorderTop = sourceStyle.BorderTop;
            newStyle.BorderBottom = sourceStyle.BorderBottom;
            newStyle.BorderLeft = sourceStyle.BorderLeft;
            newStyle.BorderRight = sourceStyle.BorderRight;
            
            // 复制填充：仅当源单元格存在真实填充时复制。
            // HSSF 对无填充单元格的 FillForegroundColor 默认返回 0x40（系统默认/黑色），若直接复制，
            // XSSF 会输出 patternType="none" + fgColor indexed="64"，前端 x-spreadsheet 忽略 patternType
            // 将 fgColor 当作背景色渲染 → 整张表变成黑底、深色文字不可见（“列完全看不到”）。
            if (sourceStyle.FillPattern != FillPattern.NoFill)
            {
                newStyle.FillPattern = sourceStyle.FillPattern;
                newStyle.FillForegroundColor = sourceStyle.FillForegroundColor;
                newStyle.FillBackgroundColor = sourceStyle.FillBackgroundColor;
            }
            
            // 复制字体（简化处理，只复制基本属性）
            if (sourceStyle.GetFont(sourceWorkbook) != null)
            {
                var sourceFont = sourceStyle.GetFont(sourceWorkbook);
                var newFont = targetWorkbook.CreateFont();
                newFont.FontName = sourceFont.FontName;
                newFont.FontHeightInPoints = sourceFont.FontHeightInPoints;
                newFont.IsBold = sourceFont.IsBold;
                newFont.IsItalic = sourceFont.IsItalic;
                newFont.Underline = sourceFont.Underline;
                newStyle.SetFont(newFont);
            }
            
            // 复制数据格式
            newStyle.DataFormat = sourceStyle.DataFormat;
            
            targetCell.CellStyle = newStyle;
        }
    }
    
    /// <summary>
    /// 转换结果
    /// </summary>
    public class ConvertResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
