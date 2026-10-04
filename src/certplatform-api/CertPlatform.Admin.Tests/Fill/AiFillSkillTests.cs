using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill
{
    /// <summary>
    /// AI 数据来源 Skill（<c>src_semantic</c> / <c>src_ai_field</c> / <c>src_ai_table</c>）
    /// 与三个公共件的单测（39 号 §六~§八 + §十二）。
    ///
    /// <para><b>★ 测什么、不测什么</b>：只测<b>不依赖 DB / 不依赖模型</b>的部分 ——
    /// 参数校验失败路径、JSON 取值、类型转换、提示词渲染。
    /// ⛔ <b>不在这里打真模型</b>（那是集成测试，需要 key + 网络，会拖慢 CI 且不可复现）。
    /// 故 <c>db</c> / <c>llm</c> 一律传 <c>null!</c> —— 失败在参数校验阶段就返回了，
    /// <b>根本不会解引用它们</b>；若哪天失败路径被改到校验之后，这些用例会立刻 NRE 报错
    /// （这本身就是一道「不要偷偷打网络」的护栏）。</para>
    /// </summary>
    public class AiFillSkillTests
    {
        // ────────────────────────────────────────────────────────────────────
        // ① FillValueFactory.FromRaw —— 类型随**锚点**走（39 号 §7.3）
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void FromRaw_数字_应支持千分位()
        {
            var v = FillValueFactory.FromRaw("A", "1,234.5", "number");

            Assert.NotNull(v);
            Assert.Equal(FillValueKind.Number, v!.Kind);
            Assert.Equal(1234.5, v.Number);
        }

        [Fact]
        public void FromRaw_数字_非法值应返回null而不是降级成文本()
        {
            // ★ 38 号 §4.2：静默降级会让 Excel 数字变文本 —— 不报错但结果错
            Assert.Null(FillValueFactory.FromRaw("A", "叁万元", "number"));
        }

        [Fact]
        public void FromRaw_日期_ISO格式()
        {
            var v = FillValueFactory.FromRaw("A", "2026-03-11", "date");

            Assert.NotNull(v);
            Assert.Equal(FillValueKind.Date, v!.Kind);
            Assert.Equal(new System.DateTime(2026, 3, 11), v.Date);
        }

        [Fact]
        public void FromRaw_日期_中文格式也应识别()
        {
            var v = FillValueFactory.FromRaw("A", "2026年3月11日", "date");

            Assert.NotNull(v);
            Assert.Equal(new System.DateTime(2026, 3, 11), v!.Date);
        }

        [Fact]
        public void FromRaw_布尔_中文写法()
        {
            Assert.True(FillValueFactory.FromRaw("A", "是", "bool")!.Bool);
            Assert.False(FillValueFactory.FromRaw("A", "否", "bool")!.Bool);
        }

        [Fact]
        public void FromRaw_空值不是失败_类型按声明给()
        {
            // ★ 「取到了值，但值是空的」≠「没取到值」—— 前者由本方法表达，后者由调用方 Fail
            var v = FillValueFactory.FromRaw("A", null, "number");

            Assert.NotNull(v);
            Assert.Equal(FillValueKind.Number, v!.Kind);   // 类型仍按锚点声明
            Assert.Null(v.Number);                          // 但值为空
        }

        [Fact]
        public void FromRaw_CLR数值_应按不变区域性文本化()
        {
            var v = FillValueFactory.FromRaw("A", 1234.5d, "number");

            Assert.NotNull(v);
            Assert.Equal(1234.5, v!.Number);
        }

        [Fact]
        public void FromRaw_已是FillValue应透传并补锚点()
        {
            var src = new FillValue { Kind = FillValueKind.Text, Text = "x" };
            var v = FillValueFactory.FromRaw("A", src, "text");

            Assert.Same(src, v);
            Assert.Equal("A", v!.AnchorCode);
        }

        [Fact]
        public void FromRaw_锚点为空应返回null()
        {
            Assert.Null(FillValueFactory.FromRaw("", "x", "text"));
        }

        // ────────────────────────────────────────────────────────────────────
        // ② AiFillJsonReader —— 三段取值（39 号 §12.5）
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void NormalizeRoot_应深转换JsonElement()
        {
            using var doc = JsonDocument.Parse(
                """{"semantic":{"A":"改写后"},"fields":{"B":{"value":1,"confidence":0.8}}}""");

            var root = AiFillJsonReader.NormalizeRoot(doc);

            Assert.NotNull(root);
            // ★ 若不深转换，这里拿到的是 JsonElement ⇒ 后面取不到值
            Assert.Equal("改写后", AiFillJsonReader.ReadSectionValue(root, "semantic", "A"));

            var node = AiFillJsonReader.ReadObject(root, "fields", "B");
            Assert.NotNull(node);
            Assert.Equal(1L, AiFillJsonReader.ReadRaw(node, "value"));
            Assert.Equal(0.8, AiFillJsonReader.ReadConfidence(node)!.Value, 5);
        }

        [Fact]
        public void ReadSectionRaw_tables段应能取到数组()
        {
            // ★ tables.{tag} 是数组 ⇒ 必须用 ReadSectionRaw（ReadObject 只处理对象）
            using var doc = JsonDocument.Parse("""{"tables":{"t1":[{"name":"张三"}]}}""");
            var root = AiFillJsonReader.NormalizeRoot(doc);

            var node = AiFillJsonReader.ReadSectionRaw(root, "tables", "t1");

            Assert.IsType<List<object?>>(node);
        }

        [Fact]
        public void ReadConfidence_模型未给应返回null而不是默认值()
        {
            // ★ ⛔ 不默认 1.0 —— 那会掩盖「模型没给」这个事实
            using var doc = JsonDocument.Parse("""{"fields":{"B":{"value":1}}}""");
            var root = AiFillJsonReader.NormalizeRoot(doc);
            var node = AiFillJsonReader.ReadObject(root, "fields", "B");

            Assert.Null(AiFillJsonReader.ReadConfidence(node));
        }

        [Fact]
        public void NormalizeRoot_大整数不应丢精度()
        {
            // ★★ 回归：`e.TryGetInt64(out var l) ? l : e.GetDouble()` 会被编译器
            //   **提升为 double**（long 也被转 double 装箱）⇒ 大整数静默丢精度。
            //   必须写成 `? (object)l : e.GetDouble()`。此用例由「跑单测发现」而来。
            using var doc = JsonDocument.Parse("""{"fields":{"B":{"value":1234567890123456789}}}""");
            var root = AiFillJsonReader.NormalizeRoot(doc);
            var node = AiFillJsonReader.ReadObject(root, "fields", "B");

            Assert.Equal(1234567890123456789L, AiFillJsonReader.ReadRaw(node, "value"));
        }

        [Fact]
        public void ReadSectionValue_键名大小写不敏感回退()
        {
            using var doc = JsonDocument.Parse("""{"Semantic":{"a":"x"}}""");
            var root = AiFillJsonReader.NormalizeRoot(doc);

            Assert.Equal("x", AiFillJsonReader.ReadSectionValue(root, "semantic", "A"));
        }

        [Fact]
        public void ReadSectionValue_段缺失应返回null()
        {
            using var doc = JsonDocument.Parse("""{"fields":{}}""");
            var root = AiFillJsonReader.NormalizeRoot(doc);

            Assert.Null(AiFillJsonReader.ReadSectionValue(root, "semantic", "A"));
        }

        // ────────────────────────────────────────────────────────────────────
        // ③ AiFillPromptBuilder —— 渲染（唯一装配器，39 号 §12.2）
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void Render_只替换已知占位符_不吃掉提示词里的业务token()
        {
            var values = new Dictionary<string, string>
            {
                ["anchors"] = "A",
                ["enterprise_docs"] = "B",
            };

            var r = AiFillPromptBuilder.Render(
                "x {{__FILL__.anchors}} y {{ENT_NAME}} z {{__FILL__.enterprise_docs}}", values);

            // ★ {{ENT_NAME}} 必须原样保留 —— 它是给模型的「说明」，不是引擎占位符
            Assert.Equal("x A y {{ENT_NAME}} z B", r);
        }

        [Fact]
        public void RenderAnchors_空清单应给人话提示而不是空白()
        {
            Assert.Contains("没有需要 AI 填写", AiFillPromptBuilder.RenderAnchors(null));
            Assert.Contains("没有需要 AI 填写",
                AiFillPromptBuilder.RenderAnchors(new List<AiFillAnchorSpec>()));
        }

        [Fact]
        public void RenderAnchors_三段应分别渲染且带类型与原文()
        {
            var list = new List<AiFillAnchorSpec>
            {
                new()
                {
                    AnchorCode = "A1", Instruction = "改写这段",
                    Section = "semantic", SourceText = "标准原文…",
                },
                new()
                {
                    AnchorCode = "B1", Instruction = "企业名称",
                    Section = "field", ValueKind = "text",
                },
                new()
                {
                    AnchorCode = "T1", Instruction = "人员表", Section = "table",
                    Columns = new List<AiFillColumnSpec>
                    {
                        new() { FieldCode = "name", Title = "姓名", ValueKind = "text" },
                    },
                },
            };

            var s = AiFillPromptBuilder.RenderAnchors(list);

            Assert.Contains("semantic 段", s);
            Assert.Contains("fields 段", s);
            Assert.Contains("tables 段", s);
            Assert.Contains("待改写原文：标准原文…", s);   // ★ semantic 的核心输入
            Assert.Contains("name(姓名,text)", s);          // ★ 列定义进提示词
        }

        [Fact]
        public async Task BuildSingleAsync_无提示词时应回落到内置默认模板()
        {
            // ★ 实测库里 UserTemplate 全为 NULL —— 若这里报「提示词为空」，Skill 永远跑不起来
            var anchor = new AiFillAnchorSpec
            {
                AnchorCode = "ENT_NAME", Instruction = "企业名称",
                Section = "field", ValueKind = "text",
            };

            var req = await AiFillPromptBuilder.BuildSingleAsync(
                db: null!, promptCode: null, skillCode: "src_ai_field",
                anchor: anchor, enterpriseDocs: "（企业资料）");

            Assert.Contains("ENT_NAME", req.UserPrompt);
            Assert.Contains("（企业资料）", req.UserPrompt);
            Assert.Contains("输出结构", req.UserPrompt);
            Assert.False(string.IsNullOrWhiteSpace(req.SystemPrompt));
            Assert.Equal(0m, req.Temperature);                    // ★ 低温可复现
            Assert.Equal(AiFillInvoker.DefaultMaxTokens, req.MaxTokens);
        }

        [Fact]
        public async Task BuildSingleAsync_单锚点应把说明渲染进锚点清单()
        {
            var anchor = new AiFillAnchorSpec
            {
                AnchorCode = "ENT_NAME", Instruction = "企业全称",
                Section = "field", ValueKind = "text",
            };

            var req = await AiFillPromptBuilder.BuildSingleAsync(
                db: null!, promptCode: null, skillCode: "src_ai_field", anchor: anchor);

            // 默认模板用 {{__FILL__.anchors}} 展开清单 ⇒ 说明与类型都应出现
            Assert.Contains("ENT_NAME", req.UserPrompt);
            Assert.Contains("企业全称", req.UserPrompt);
            Assert.Contains("类型：text", req.UserPrompt);
        }

        [Fact]
        public void RenderAnchors_多锚点应逐条渲染()
        {
            // ★ 批量路径必须列全 —— 漏一条模型就不会返回它，那一格永远空着且不报错
            var list = new List<AiFillAnchorSpec>
            {
                new() { AnchorCode = "A", Instruction = "甲", Section = "field" },
                new() { AnchorCode = "B", Instruction = "乙", Section = "field" },
            };

            var s = AiFillPromptBuilder.RenderAnchors(list);

            Assert.Contains("A：甲", s);
            Assert.Contains("B：乙", s);
        }

        // ────────────────────────────────────────────────────────────────────
        // ④ TablePayloadFactory —— 列来自输入，⛔ 不来自 AI（39 号 §8.3）
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public void ParseColumns_三套键名写法都应识别()
        {
            var json = """
                [{"field_code":"name","title":"姓名","kind":"text"},
                 {"FieldCode":"age","Title":"年龄","ValueKind":"number","NumberFormat":"#,##0"}]
                """;

            var cols = TablePayloadFactory.ParseColumns(json);

            Assert.Equal(2, cols.Count);
            Assert.Equal("name", cols[0].FieldCode);
            Assert.Equal("姓名", cols[0].Title);
            Assert.Equal("age", cols[1].FieldCode);
            Assert.Equal("number", cols[1].ValueKind);
            Assert.Equal("#,##0", cols[1].NumberFormat);
        }

        [Fact]
        public void ParseColumns_坏JSON应返回空列表而不是抛异常()
        {
            Assert.Empty(TablePayloadFactory.ParseColumns("{不是数组"));
            Assert.Empty(TablePayloadFactory.ParseColumns(null));
            Assert.Empty(TablePayloadFactory.ParseColumns("[]"));
        }

        [Fact]
        public void ParseColumns_无FieldCode的列应被丢弃()
        {
            var cols = TablePayloadFactory.ParseColumns("""[{"title":"没有编码"}]""");

            Assert.Empty(cols);
        }

        [Fact]
        public void FromAiNode_数组形态()
        {
            var cols = new List<TableColumn> { new() { FieldCode = "name", Title = "姓名" } };
            var node = new List<object?>
            {
                new Dictionary<string, object> { ["name"] = "张三" },
                new Dictionary<string, object> { ["name"] = "李四" },
            };

            var p = TablePayloadFactory.FromAiNode("t1", cols, node);

            Assert.Equal(2, p.Rows.Count);
            Assert.Equal("张三", p.Rows[0]["name"]);
            Assert.Equal("李四", p.Rows[1]["name"]);
        }

        [Fact]
        public void FromAiNode_对象形态应取rows并忽略AI返回的列()
        {
            // ★ 39 号 §8.3：列由模板决定 —— AI 返回的 columns 必须被丢弃
            var cols = new List<TableColumn> { new() { FieldCode = "name", Title = "姓名" } };
            var node = new Dictionary<string, object>
            {
                ["columns"] = new List<object?> { new Dictionary<string, object> { ["field_code"] = "WRONG" } },
                ["rows"] = new List<object?>
                {
                    new Dictionary<string, object> { ["name"] = "王五", ["extra"] = "应被忽略" },
                },
            };

            var p = TablePayloadFactory.FromAiNode("t1", cols, node);

            Assert.Single(p.Rows);
            Assert.Equal("王五", p.Rows[0]["name"]);
            Assert.False(p.Rows[0].ContainsKey("extra"));      // ★ 不在列定义里的键被忽略
            Assert.Equal("name", p.Columns[0].FieldCode);      // ★ 列来自输入
        }

        [Fact]
        public void FromAiNode_没有列定义应返回空payload()
        {
            var node = new List<object?> { new Dictionary<string, object> { ["name"] = "张三" } };

            var p = TablePayloadFactory.FromAiNode("t1", null, node);

            Assert.Empty(p.Rows);
            Assert.Empty(p.Columns);
        }

        [Fact]
        public void FromAiNode_maxRows应截断()
        {
            var cols = new List<TableColumn> { new() { FieldCode = "n" } };
            var node = new List<object?>();
            for (var i = 0; i < 10; i++)
                node.Add(new Dictionary<string, object> { ["n"] = i.ToString() });

            var p = TablePayloadFactory.FromAiNode("t1", cols, node, maxRows: 3);

            Assert.Equal(3, p.Rows.Count);
        }

        // ────────────────────────────────────────────────────────────────────
        // ⑤ 3 个 Skill 的失败路径（★ 不打网络 —— db / llm 传 null! 就够）
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SrcSemantic_空锚点应失败()
        {
            var r = await SrcSemanticSkill.ExecuteAsync(anchor_code: "", source_text: "原文");

            Assert.False(r.Success);
            Assert.Contains("anchor_code", r.Error);
        }

        [Fact]
        public async Task SrcSemantic_空原文应失败()
        {
            // ★ 39 号 §6.3：没有原文就没有可改的东西 ⇒ Fail，⛔ 不产空值
            var r = await SrcSemanticSkill.ExecuteAsync(anchor_code: "A", source_text: "");

            Assert.False(r.Success);
            Assert.Contains("source_text", r.Error);
        }

        [Fact]
        public async Task SrcAiField_空锚点应失败()
        {
            var r = await SrcAiFieldSkill.ExecuteAsync(anchor_code: "", instruction: "企业名称");

            Assert.False(r.Success);
            Assert.Contains("anchor_code", r.Error);
        }

        [Fact]
        public async Task SrcAiField_空说明应失败()
        {
            // ★ 39 号 §7.2：instruction 无默认值 = 必填
            var r = await SrcAiFieldSkill.ExecuteAsync(anchor_code: "A", instruction: "");

            Assert.False(r.Success);
            Assert.Contains("instruction", r.Error);
        }

        [Fact]
        public async Task SrcAiTable_空表格标签应失败()
        {
            var r = await SrcAiTableSkill.ExecuteAsync(table_tag: "", columns_json: "[]");

            Assert.False(r.Success);
            Assert.Contains("table_tag", r.Error);
        }

        [Fact]
        public async Task SrcAiTable_空列定义应失败()
        {
            var r = await SrcAiTableSkill.ExecuteAsync(table_tag: "t1", columns_json: "");

            Assert.False(r.Success);
            Assert.Contains("columns_json", r.Error);
        }

        [Fact]
        public async Task SrcAiTable_列定义解析不出列应失败()
        {
            // ★ 39 号 §8.3：列必须来自模板 ⇒ 解析不出就 Fail，⛔ 不让 AI 自己定列
            var r = await SrcAiTableSkill.ExecuteAsync(table_tag: "t1", columns_json: "[]");

            Assert.False(r.Success);
            Assert.Contains("解析不出任何列", r.Error);
        }

        // ────────────────────────────────────────────────────────────────────
        // ⑥ Skill 元数据（反射登记依赖它，写错只在运行期炸）
        // ────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("src_semantic", typeof(SrcSemanticSkill))]
        [InlineData("src_ai_field", typeof(SrcAiFieldSkill))]
        [InlineData("src_ai_table", typeof(SrcAiTableSkill))]
        public void Skill特性_Code应与注册行一致(string expectedCode, System.Type type)
        {
            var attr = (SkillAttribute?)System.Attribute.GetCustomAttribute(type, typeof(SkillAttribute));

            Assert.NotNull(attr);
            Assert.Equal(expectedCode, attr!.Code);
            Assert.Equal("ExecuteAsync",
                type.GetMethod("ExecuteAsync")!.Name);   // ★ wf_skill_reflection.MethodName 依赖它
        }

        /// <summary>
        /// ★★ <b>注册生效的最终证据</b>：用<b>注册 SQL 里写的那个字符串</b>去解析类型。
        ///
        /// <para>模拟 <c>SkillExecutor.ResolveType</c> 的两步（<c>Type.GetType</c> → 扫已加载程序集）。
        /// <c>ClassPath</c> 写错一个字，<b>编译期毫无提示</b>，只在运行期抛
        /// 「无法找到类型」—— 这条用例把它提前到构建阶段。</para>
        ///
        /// <para>同时断言 SQL 文件里确实写着这个字符串 —— 防「改了命名空间忘了改 SQL」
        /// （本批 3 个 Skill 放在 <c>Fill/Ai/</c> 子目录，命名空间比 phase13 多一段 <c>.Ai</c>，
        /// 正是最容易漏改的地方）。</para>
        /// </summary>
        [Theory]
        [InlineData("src_semantic", "CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcSemanticSkill")]
        [InlineData("src_ai_field", "CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcAiFieldSkill")]
        [InlineData("src_ai_table", "CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai.SrcAiTableSkill")]
        public void ClassPath_应能被反射解析且与注册SQL一致(string skillCode, string classPath)
        {
            // ① 模拟 SkillExecutor.ResolveType
            var type = System.Type.GetType(classPath)
                       ?? typeof(SrcAiFieldSkill).Assembly.GetType(classPath, throwOnError: false);

            Assert.NotNull(type);   // ★ ClassPath 写错 ⇒ 这里就炸（否则要等到运行期）
            Assert.NotNull(type!.GetMethod("ExecuteAsync",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static));

            // ② 注册 SQL 里必须写着同一个字符串
            var sql = ReadRepoFile("DB/mysql/phase14_doc_fill_ai_skills.sql");

            Assert.Contains(classPath, sql);
            Assert.Contains($"'{skillCode}'", sql);
        }

        /// <summary>从测试运行目录向上找仓库文件（`bin/Debug/net8.0` → 仓库根）</summary>
        private static string ReadRepoFile(string relativePath)
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);

            while (dir != null)
            {
                var path = System.IO.Path.Combine(dir.FullName, relativePath);
                if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
                dir = dir.Parent;
            }

            throw new System.IO.FileNotFoundException($"找不到仓库文件：{relativePath}");
        }
    }
}
