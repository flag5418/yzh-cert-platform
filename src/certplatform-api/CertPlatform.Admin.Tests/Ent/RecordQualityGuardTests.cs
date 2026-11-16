using System.Text.Json;
using CertPlatform.Auditor.Services.Ent;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent
{
    /// <summary>表格数据识别护栏：空表格不算记录、RowCount 由代码定、摘要复数只告警不改原文。</summary>
    public class RecordQualityGuardTests
    {
        private static JsonElement Apply(string json)
            => RecordQualityGuard.Apply(EnterpriseDocExtractionRouter.RecordProfile, JsonDocument.Parse(json).RootElement);

        private static string Mode(string records, string summary = "记录供应商准入信息")
            => "{\"ReportSummary\":\"" + summary + "\",\"Records\":[" + records + "]}";

        private static string One(string rows, int rowCount = 0, string state = "filled")
            => "{\"RecordName\":\"供应商评审表\",\"RowCount\":" + rowCount
                + ",\"RecordState\":\"" + state + "\",\"Rows\":[" + rows + "]}";

        private static JsonElement Rec(JsonElement res, int index = 0)
            => res.GetProperty("Records")[index];

        private static bool HasWarning(JsonElement record, string code)
            => record.GetProperty("Warnings").EnumerateArray()
                .Any(w => w.GetProperty("Code").GetString() == code);

        [Fact]
        public void BlankTemplateRow_IsDropped()
        {
            var res = Apply(Mode(One("""
                {"Values":["","","",""]},
                {"Values":["江阴市金祺达金属制品有限公司","316L","2026.6.10","胥加娟"]}
                """, 2)));

            var rows = Rec(res).GetProperty("Rows");
            Assert.Equal(1, rows.GetArrayLength());
            Assert.Equal("江阴市金祺达金属制品有限公司", rows[0].GetProperty("Values")[0].GetString());
            Assert.True(HasWarning(Rec(res), "blank_rows_dropped"));
        }

        [Fact]
        public void PlaceholderFirstCell_IsDropped()
        {
            var res = Apply(Mode(One("""
                {"Values":["—","符合","优"]},
                {"Values":["江阴市金祺达金属制品有限公司","符合","优"]}
                """, 2)));

            Assert.Equal(1, Rec(res).GetProperty("Rows").GetArrayLength());
        }

        [Fact]
        public void StructuralRow_IsDropped()
        {
            var res = Apply(Mode(One("""
                {"Values":["合计","符合","优"]},
                {"Values":["备注","",""]},
                {"Values":["江阴市金祺达金属制品有限公司","符合","优"]}
                """, 3)));

            Assert.Equal(1, Rec(res).GetProperty("Rows").GetArrayLength());
        }

        [Fact]
        public void RowCount_IsRecomputedFromRealRows_NotModelClaim()
        {
            // 97 号 §10.6 的现场：6 个评价项 + 1 条结论被数成 7 行，实际对象只有 1 家
            var res = Apply(Mode(One("""
                {"Values":["江阴市金祺达金属制品有限公司","符合","基本满足","成批供应","优","优","较好"]}
                """, 7)));

            var rec = Rec(res);
            Assert.Equal(1, rec.GetProperty("RowCount").GetInt32());
            Assert.True(HasWarning(rec, "row_count_recomputed"));
        }

        [Fact]
        public void NoRowsButSamples_CountsSamplesAndSaysSo()
        {
            var res = Apply(Mode("""
                {"RecordName":"进料检验报告","RowCount":5,"RecordState":"filled",
                 "Samples":[{"Values":["江阴市金祺达金属制品有限公司","316L","2026.6.10","胥加娟"]}]}
                """));

            var rec = Rec(res);
            Assert.Equal(1, rec.GetProperty("RowCount").GetInt32());
            Assert.True(HasWarning(rec, "row_count_from_samples"));
        }

        [Fact]
        public void ObjectShapedValues_KeepsRow()
        {
            // 实库里有两种 Values：数组（无表头）与对象（键=字段名）。对象结构不能被判成空行
            var res = Apply(Mode("""
                {"RecordName":"供应商业绩考核评估表","RowCount":1,
                 "Rows":[{"Date":"2026-09-30","Conclusion":"A级供应商",
                   "Values":{"供应商名称":"江阴市金祺达金属制品有限公司","材料类别":"316L","总得分":"96","级别":"A"}}]}
                """));

            var rec = Rec(res);
            Assert.Equal(1, rec.GetProperty("RowCount").GetInt32());
            Assert.Equal(1, rec.GetProperty("Rows").GetArrayLength());
        }

        [Fact]
        public void EmptyTableWithoutConclusionOrSignoff_IsBlank()
        {
            var res = Apply(Mode("""
                {"RecordName":"设备点检表","RowCount":8,"RecordState":"filled","Rows":[{"Values":["","",""]}]}
                """));

            var rec = Rec(res);
            Assert.Equal(0, rec.GetProperty("RowCount").GetInt32());
            Assert.Equal("blank", rec.GetProperty("RecordState").GetString());
            Assert.True(HasWarning(rec, "no_data_rows"));
        }

        [Fact]
        public void NoRowsButHasSignoff_KeepsStateAndOnlyZeroesCount()
        {
            // 没抽到行但签署齐全 ⇒ 不轻易断言"空文档"
            var res = Apply(Mode("""
                {"RecordName":"纠正预防措施处理单","RowCount":2,"RecordState":"filled",
                 "Rows":[],"Signoff":["品质部|张红红|2026.6.10"]}
                """));

            var rec = Rec(res);
            Assert.Equal(0, rec.GetProperty("RowCount").GetInt32());
            Assert.Equal("filled", rec.GetProperty("RecordState").GetString());
        }

        [Fact]
        public void CatalogRecord_KeepsRowCountUntouched()
        {
            // 目录类条数在 Catalog.Count 里，不能用 Rows 重算
            var res = Apply(Mode("""
                {"RecordName":"文件清单","RowCount":128,"Catalog":{"Count":128,"Items":[]},"Rows":[]}
                """));

            Assert.Equal(128, Rec(res).GetProperty("RowCount").GetInt32());
        }

        [Fact]
        public void PluralClaim_SingleObject_WarnsButKeepsOriginalText()
        {
            var res = Apply(Mode("""
                {"RecordName":"合格供应商清单","RowCount":1,
                 "Rows":[{"Values":["江阴市金祺达金属制品有限公司","316L","2026.6.10","胥加娟"]}]}
                """, "记录江阴市金祺达金属制品有限公司等供应商的准入评审信息"));

            var mo = res;
            Assert.Equal("记录江阴市金祺达金属制品有限公司等供应商的准入评审信息",
                mo.GetProperty("ReportSummary").GetString());
            Assert.True(HasWarning(Rec(res), "summary_plural_mismatch"));
        }

        [Fact]
        public void PluralWordInNonObjectContext_DoesNotWarn()
        {
            // 「等六项内容」是合法用法，不能误报
            var res = Apply(Mode("""
                {"RecordName":"供应商评审表","RowCount":1,
                 "Rows":[{"Values":["江阴市金祺达金属制品有限公司","符合","优"]}]}
                """, "记录对江阴市金祺达金属制品有限公司的资质、能力、产品质量等六项内容的评审结果"));

            Assert.False(HasWarning(Rec(res), "summary_plural_mismatch"));
        }

        [Fact]
        public void OtherModes_AreUntouched()
        {
            var raw = JsonDocument.Parse(
                "{\"Records\":[{\"RecordName\":\"x\",\"RowCount\":7,\"Rows\":[]}]}").RootElement;

            var res = RecordQualityGuard.Apply(EnterpriseDocExtractionRouter.ProcessProfile, raw);
            Assert.Equal(7, res.GetProperty("Records")[0].GetProperty("RowCount").GetInt32());
        }
    }
}
