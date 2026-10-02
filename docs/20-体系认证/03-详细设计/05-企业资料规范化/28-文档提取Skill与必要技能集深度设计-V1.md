# 文档提取Skill与必要技能集深度设计-V1.md

> **状态**：**工程级深度设计 —— 待审批**
> **目标**：定义严谨的文档数据提取协议，实现 `Word/Excel/PDF/OCR` 技能的受控化与插件化。

---

## 一、 技能元数据与注册机制

### 1.1 核心接口 `IDocExtractionSkill`
所有提取技能必须实现此接口，确保输入（原始文件路径/流）与输出（标准化画像/字段清单）的一致性。

```csharp
public interface IDocExtractionSkill
{
    /// <summary>技能唯一标识（对应字典 DOC_EXTRACT_SKILL）</summary>
    string SkillCode { get; }

    /// <summary>
    /// 执行提取任务
    /// </summary>
    /// <param name="context">包含文件路径、预定义的字段清单、锚点信息</param>
    /// <returns>提取结果清单（含置信度、证据位置）</returns>
    Task<ExtractionResult> ExtractAsync(ExtractionContext context);
}
```

### 1.2 受控技能字典 (Necessary Skill Set)
| SkillCode | 适用场景 | 核心实现库 | 提取策略 |
| :--- | :--- | :--- | :--- |
| **`word`** | 标准 .docx 文档 | NPOI / OpenXML | 基于段落样式、书签 (Bookmark) 和表格索引 |
| **`excel`** | 记录类 .xlsx 表格 | NPOI / ExcelDataReader | 基于单元格坐标 (Cell Address) 和命名区域 (Named Range) |
| **`pdf`** | 扫描件/矢量 PDF | PdfPig / iTextSharp | 文本层提取 + 坐标映射 |
| **`ocr`** | 图片或纯图 PDF | PaddleOCR / Azure OCR | 视觉布局分析 (VLA) + 文本识别 |

---

## 二、 提取契约：画像与画像落库 (Extracted Metadata)

系统不再直接从原始文件读取数据，而是消费“画像层”。

### 2.1 语义画像模型 (`DocSemanticProfile`)
```csharp
public class DocSemanticProfile
{
    public string DocCategory { get; set; } // 分类：手册/程序/记录
    public string DocPurpose { get; set; }  // 作用：阐述质量目标/记录内审计划
    public List<ExtractedField> Fields { get; set; }
    public List<ExtractedTable> Tables { get; set; }
    public string FullMarkdown { get; set; } // 用于 LLM 语义分析的纯文本底座
}
```

---

## 三、 详细技能实现逻辑

### 3.1 Excel 技能 (`ExcelExtractionSkill`)
- **坐标感知**：支持 `Sheet1!A1:B10` 这种强坐标匹配。
- **动态行扩展**：支持定义“表格起始行”，自动向下遍历直到空行，用于提取不定长的记录清单。

### 3.2 OCR 技能 (`OcrExtractionSkill`)
- **预处理**：灰度化、去噪、纠偏。
- **模板匹配**：针对格式高度固定的表格（如执照），先进行特征点匹配，再在指定坐标区域执行 OCR。

---

## 四、 稳定性与准确性保障
1. **置信度模型**：每个提取字段必须返回 `Confidence` (0.0-1.0)。低于 0.6 的项强制标记为“待专家核对”。
2. **证据留痕 (Grounding)**：提取结果必须包含 `EvidenceRef`，即数据在原文中的坐标（页码、行号或单元格地址），支持点击结果直接定位原文预览。
3. **容错重试**：AI 节点提取失败时，自动切换到“规则降级”模式（如：正则匹配）。
