using CertPlatform.Auditor.Services.Ent;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent
{
    /// <summary>
    /// 过程域召回：只决定"这份文档该不该被找出来"，⛔ 绝不产出条款号。
    /// <para>口径来自 2026-11-16 决策：证据不能只靠条款找——程序/指导书按过程+文件类型，
    /// 记录按过程+部门；任何拿不到过程属性的情况都退回"不召回"。</para>
    /// </summary>
    public class ProcessAreaRecallTests
    {
        [Theory]
        [InlineData("[\"purchase\"]", "purchase")]
        [InlineData("[\"purchase\",\"inspection\"]", "purchase", "inspection")]
        [InlineData("purchase", "purchase")]
        [InlineData(" purchase , design ", "purchase", "design")]
        public void ParseKeys_HandlesBothShapes(string attr, params string[] expect)
        {
            Assert.Equal(expect.ToList(), ProcessAreaRecall.ParseKeys(attr));
        }

        [Fact]
        public void ParseKeys_EmptyInputYieldsNothing()
        {
            Assert.Empty(ProcessAreaRecall.ParseKeys(""));
            Assert.Empty(ProcessAreaRecall.ParseKeys(null));
        }

        [Fact]
        public void KeywordsFor_MapsEnglishKeysToChineseAreas()
        {
            var kw = ProcessAreaRecall.KeywordsFor(new[] { "purchase" });
            Assert.Contains("采购", kw);
            Assert.Contains("供应商", kw);
        }

        [Theory]
        [InlineData("采购过程", true)]
        [InlineData("供应商准入评审", true)]
        [InlineData("来料检验记录", true)]
        [InlineData("生产和服务提供", false)]
        [InlineData("", false)]
        public void Matches_OnlyHitsSameProcessArea(string area, bool expected)
        {
            var kw = ProcessAreaRecall.KeywordsFor(new[] { "purchase" });
            Assert.Equal(expected, ProcessAreaRecall.Matches(area, kw));
        }

        [Fact]
        public void UnknownKey_YieldsNoKeywords_SoNoRecall()
        {
            // 属性里出现没见过的键 ⇒ 不猜，直接不召回
            var kw = ProcessAreaRecall.KeywordsFor(new[] { "something_new" });
            Assert.Empty(kw);
            Assert.False(ProcessAreaRecall.Matches("采购过程", kw));
        }

        [Fact]
        public void NoKeywords_DisablesAxis()
        {
            // 检查项条款没有 Process 属性 ⇒ 过程轴整体不启用
            Assert.False(ProcessAreaRecall.Matches("采购过程", new HashSet<string>()));
        }
    }
}
