using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Tests.Extractor
{
    /// <summary>
    /// Step 3 测试：验证提示词模板与提取结果的结合效果
    /// 测试提示词是否正确渲染，输出格式是否符合预期
    /// </summary>
    public class S3_PromptEffectivenessTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IFileExtractor _extractor;

        // 测试文件基础路径
        private readonly string _testFilesBasePath = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docs/历史文档/案例";

        // Word V2 分析提示词模板（与 SQL 中定义的保持一致）
        private readonly string _wordAnalyzePromptV2 = @"你是专业的体系认证文档分析专家。请深度分析以下 Word 文档的内容结构，识别所有可提取的关键信息字段和表格。

## 分析任务

1. **识别字段**：从文档中提取以下类型的字段
   - 文档标识：文件编号、版本号、生效日期、编制人、审核人、批准人
   - 组织信息：企业名称、部门名称、岗位名称
   - 时间信息：日期、时间周期、有效期
   - 状态信息：文件状态、审批状态、执行状态
   - 数值信息：数量、比例、百分比、金额
   - 描述信息：标题、摘要、备注、说明

2. **识别表格**：文档中可能包含的表格类型
   - 记录表格：签到表、检查表、评审记录表
   - 清单表格：文件清单、设备清单、人员清单
   - 统计表格：数据统计、趋势分析、汇总报表
   - 流程表格：流程步骤、职责分工、时间节点

## 输出格式（严格 JSON）

```json
{
  ""fields"": [
    {
      ""field_name_cn"": ""文件编号"",
      ""field_name_en"": ""wen_jian_bian_hao"",
      ""field_type"": ""string"",
      ""is_required"": true,
      ""description"": ""文档的唯一标识编号，如 XASL-QM-001"",
      ""extracted_value"": ""从文档中提取的实际值，如 XASL-QP-024""
    }
  ],
  ""tables"": [
    {
      ""table_name_cn"": ""审批记录表"",
      ""table_name_en"": ""shen_pi_ji_lu_biao"",
      ""description"": ""记录文件编制、审核、批准的流程信息"",
      ""columns"": [
        {""column_name_cn"": ""角色"", ""column_name_en"": ""jiao_se"", ""column_type"": ""string""},
        {""column_name_cn"": ""姓名"", ""column_name_en"": ""xing_ming"", ""column_type"": ""string""},
        {""column_name_cn"": ""日期"", ""column_name_en"": ""ri_qi"", ""column_type"": ""date""}
      ],
      ""extracted_data"": [
        {""角色"": ""编制"", ""姓名"": ""张三"", ""日期"": ""2024-01-15""},
        {""角色"": ""审核"", ""姓名"": ""李四"", ""日期"": ""2024-01-16""}
      ]
    }
  ]
}
```

## 字段命名规则

### 中文字段名（field_name_cn）
- 使用简洁的中文名称
- 示例：""文件编号""、""企业名称""、""生效日期""

### 英文字段名（field_name_en）
- 将中文翻译成英文后转换为 snake_case（小写+下划线）
- 转换规则：
  * ""文件编号"" → ""wen_jian_bian_hao""
  * ""企业名称"" → ""qi_ye_ming_cheng""
  * ""生效日期"" → ""sheng_xiao_ri_qi""
  * ""审批记录表"" → ""shen_pi_ji_lu_biao""

## 字段类型规范
- string: 文本、名称、描述
- number: 整数、小数、计数
- date: 日期、时间（格式：YYYY-MM-DD）
- boolean: 是/否、有/无（true/false）
- money: 金额、价格
- percent: 百分比、比例

## 分析原则
1. 只提取文档中实际存在的信息，不要臆测
2. 优先识别体系认证相关字段（文件控制、质量记录、审核证据）
3. 表格列定义要完整，包含表头和数据样例反映的列
4. 必须字段标记 is_required: true（如文件编号、版本等关键标识）
5. extracted_value 和 extracted_data 用于前端预览，展示 AI 实际提取的内容

## 文档内容

{{document_content}}

请只输出 JSON，不要任何解释文字。";

        public S3_PromptEffectivenessTests(ITestOutputHelper output)
        {
            _output = output;
            _extractor = new FileExtractorService();
        }

        #region 提示词渲染测试

        [Fact]
        public void Test_WordPromptV2_TemplateStructure()
        {
            _output.WriteLine("========== Word V2 提示词模板结构验证 ==========");

            // 验证提示词包含必要的占位符
            Assert.Contains("{{document_content}}", _wordAnalyzePromptV2);

            // 验证 V2 特有字段
            Assert.Contains("field_name_cn", _wordAnalyzePromptV2);
            Assert.Contains("field_name_en", _wordAnalyzePromptV2);
            Assert.Contains("extracted_value", _wordAnalyzePromptV2);
            Assert.Contains("table_name_cn", _wordAnalyzePromptV2);
            Assert.Contains("table_name_en", _wordAnalyzePromptV2);
            Assert.Contains("extracted_data", _wordAnalyzePromptV2);

            // 验证字段命名规则说明
            Assert.Contains("snake_case", _wordAnalyzePromptV2);
            Assert.Contains("wen_jian_bian_hao", _wordAnalyzePromptV2);

            // 验证体系认证专业术语
            Assert.Contains("文件编号", _wordAnalyzePromptV2);
            Assert.Contains("版本号", _wordAnalyzePromptV2);
            Assert.Contains("质量记录", _wordAnalyzePromptV2);

            _output.WriteLine("✓ Word V2 提示词模板结构正确");
            _output.WriteLine($"✓ 提示词长度: {_wordAnalyzePromptV2.Length} 字符");

            // 输出提示词预览
            _output.WriteLine("\n提示词预览（前 1000 字符）：");
            _output.WriteLine(_wordAnalyzePromptV2.Substring(0, Math.Min(1000, _wordAnalyzePromptV2.Length)));
        }

        #endregion

        #region 文档提取 + 提示词渲染测试

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-024 过程和产品监视测量程序.docx")]
        public async Task Test_Word_Document_Extract_And_PromptRender(string relativePath)
        {
            var filePath = Path.Combine(_testFilesBasePath, relativePath);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"[SKIP] 文件不存在: {filePath}");
                return;
            }

            _output.WriteLine($"\n========== Word 提取 + 提示词渲染测试: {Path.GetFileName(filePath)} ==========");

            // 1. 提取文档内容
            var extraction = await _extractor.ExtractAsync(filePath);
            Assert.Equal(ExtractStatus.Success, extraction.Status);

            _output.WriteLine($"✓ 提取成功: {extraction.Sections.Count} 段落, {extraction.Tables.Count} 表格");

            // 2. 构建结构化上下文
            var docContent = BuildStructuredContext(extraction);
            _output.WriteLine($"✓ 结构化上下文长度: {docContent.Length} 字符");

            // 3. 渲染提示词
            var renderedPrompt = _wordAnalyzePromptV2.Replace("{{document_content}}", docContent);
            _output.WriteLine($"✓ 渲染后提示词长度: {renderedPrompt.Length} 字符");

            // 4. 验证渲染后的提示词
            Assert.Contains(docContent.Substring(0, Math.Min(100, docContent.Length)), renderedPrompt);
            Assert.DoesNotContain("{{document_content}}", renderedPrompt);

            // 5. 输出渲染后的提示词预览
            _output.WriteLine("\n渲染后的提示词预览（最后 1500 字符，即文档内容部分）：");
            var previewStart = Math.Max(0, renderedPrompt.Length - 1500);
            _output.WriteLine(renderedPrompt.Substring(previewStart));

            _output.WriteLine("\n========== 测试完成 ==========\n");
        }

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-027 成品检验记录.xlsx")]
        public async Task Test_Excel_Document_Extract_And_PromptRender(string relativePath)
        {
            var filePath = Path.Combine(_testFilesBasePath, relativePath);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"[SKIP] 文件不存在: {filePath}");
                return;
            }

            _output.WriteLine($"\n========== Excel 提取 + 提示词渲染测试: {Path.GetFileName(filePath)} ==========");

            // 1. 提取文档内容
            var extraction = await _extractor.ExtractAsync(filePath);
            Assert.Equal(ExtractStatus.Success, extraction.Status);

            _output.WriteLine($"✓ 提取成功: {extraction.Sections.Count} 段落, {extraction.Tables.Count} 表格");

            // 2. 构建结构化上下文
            var docContent = BuildStructuredContext(extraction);
            _output.WriteLine($"✓ 结构化上下文长度: {docContent.Length} 字符");

            // 3. 输出前 20 行作为预览
            _output.WriteLine("\n结构化上下文预览（前 20 行）：");
            var lines = docContent.Split('\n').Take(20);
            foreach (var line in lines)
            {
                _output.WriteLine(line);
            }
            if (docContent.Split('\n').Length > 20)
            {
                _output.WriteLine("... (省略后续内容)");
            }

            _output.WriteLine("\n========== 测试完成 ==========\n");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 将结构化 Sections 转为 LLM 可读的带位置标记文本
        /// </summary>
        private static string BuildStructuredContext(FileExtractionResult extraction)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# 文档类型：{extraction.SourceType}");
            sb.AppendLine($"# 文件名：{extraction.FileName}");
            sb.AppendLine($"# 段落总数：{extraction.Sections.Count} | 表格数：{extraction.Tables.Count}");
            sb.AppendLine();

            foreach (var sec in extraction.Sections)
            {
                var location = sec.PositionInfo != null ? $" [{sec.PositionInfo}]" : "";
                var typeTag = sec.SectionType != "paragraph" ? $" ({sec.SectionType})" : "";
                sb.AppendLine($"[Section:{sec.SectionIndex}{typeTag}{location}]");
                sb.AppendLine(sec.Content);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        #endregion
    }
}
