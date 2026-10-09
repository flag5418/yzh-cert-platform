using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CertPlatform.Shared.Fill;
using NPOI.XWPF.UserModel;

namespace CertPlatform.Shared.Office.Word;

/// <summary>
/// 段落级锚点替换 —— <b>W9（token 跨 run）的算法落点</b>。
///
/// <para><b>算法：字符索引映射法</b>（不是「合并 run」法）</para>
/// <list type="number">
///   <item>把段内所有 run 的文本**按顺序拼接**成 <c>full</c>，同时记录每个 run 在 <c>full</c> 中的区间。</item>
///   <item>在 <c>full</c> 上用<b>同一个</b> <see cref="FillSyntax.TokenPattern"/> 找锚点
///         —— 这一步天然解决 W9：Word 把 <c>{{company_name}}</c> 拆成
///         <c>{{comp</c> / <c>any_</c> / <c>name}}</c> 三个 run 时，拼接后仍是一个完整锚点。</item>
///   <item>把命中的字符区间反查回 <c>[r1,o1) … [r2,o2)</c>，只改这些 run 的 <c>&lt;w:t&gt;</c> 内容：
///         <c>r1</c> 保留前缀 + 写入值，<c>r2</c> 保留后缀，中间 run 清空。</item>
///   <item>★ <b>替换从后往前</b>（索引降序）⇒ 前面的锚点索引不受影响，无需重算。</item>
/// </list>
///
/// <para><b>为什么不用「先合并相邻同格式 run 再替换」</b>：合并会**破坏模板作者刻意保留的
/// 字符级差异**（如锚点前半加粗、后半不加粗），且合并动作本身可能被 Word 再次拆分，
/// 导致「这次能替换、下次不能」。索引映射法<b>只改文本、不动结构</b>，天然幂等。</para>
///
/// <para><b>样式保留的机制</b>：插入值继承 <c>r1</c> 的 <c>&lt;w:rPr&gt;</c>（锚点首个 run 的格式）
/// —— 这正是模板作者的意图（锚点写成什么格式，填进去的值就是什么格式）。</para>
/// </summary>
internal static class WordParagraphFiller
{
    /// <summary>填充一个段落。<paramref name="kind"/>/<paramref name="location"/> 仅用于报告。</summary>
    internal static void Fill(
        XWPFParagraph paragraph,
        OfficeFillRequest request,
        OfficeFillReport report,
        FillLocationKind kind,
        string location)
    {
        var runs = paragraph.Runs;
        if (runs == null || runs.Count == 0) return;

        // 1. 拼接 + 建索引
        var texts = new string[runs.Count];
        var sb = new StringBuilder();
        for (var i = 0; i < runs.Count; i++)
        {
            texts[i] = WordRunText.Get(runs[i]);
            sb.Append(texts[i]);
        }

        var full = sb.ToString();
        // 快路径：整段没有 "{{" ⇒ 不可能有锚点（覆盖绝大多数段落，避免无谓的正则开销）
        if (full.IndexOf("{{", StringComparison.Ordinal) < 0) return;

        var matches = Regex.Matches(full, FillSyntax.TokenPattern, RegexOptions.CultureInvariant);
        if (matches.Count == 0) return;

        // 2. ★ 特例：锚点独占整段 + 值类型为 Field ⇒ 走域写入（域是块级元素，不能混在文字中间）
        if (matches.Count == 1 && string.Equals(matches[0].Value, full.Trim(), StringComparison.Ordinal))
        {
            var (soloKey, _) = FillSyntax.SplitFormat(matches[0].Groups[1].Value);
            if (request.Values.TryGetValue(soloKey, out var soloValue) && soloValue.Kind == FillValueKind.Field)
            {
                WordFieldWriter.ReplaceParagraphWithField(
                    paragraph, soloValue.FieldInstruction ?? string.Empty, soloValue.Text ?? string.Empty);

                report.Hits.Add(new OfficeFillHit
                {
                    Token = matches[0].Value,
                    AnchorCode = soloKey,
                    LocationKind = kind,
                    Location = location,
                    Value = soloValue.ToDisplayText(),
                    CrossRun = false,
                    Source = soloValue.Source,
                });
                return;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★ 覆盖（`overwrite`）—— **整段换成取值**，段里的其它文字一并被替换
        //
        //  与 Excel 侧同一规格（用户 2026-10-09：「一句话，中间有 {{}}」）。
        //  Word 侧的「格」口径 = **一个段落**（正文段落与表格单元格内的段落同此处理）——
        //  NPOI 的 <c>XWPFTableCell</c> 内部就是若干段落，逐段进来即可，无需额外区分。
        //
        //  ⚠️ 与 Excel 侧同一条约束：仅在「本段恰好 1 个锚点」时成立；多锚点 ⇒ 退回填充 + 记待办。
        // ════════════════════════════════════════════════════════════════
        if (matches.Count == 1)
        {
            var (owKey, _) = FillSyntax.SplitFormat(matches[0].Groups[1].Value);
            if (request.Values.TryGetValue(owKey, out var owValue) && owValue.IsOverwrite())
            {
                // ★ 整段替换 = 把值写进首 run、其余 run 清空 —— 保持「只改文本、不动结构」
                //   （与下方索引映射法同一手法，故 <w:pPr> / <w:rPr> 等结构元素全部保留）
                WordRunText.Set(runs[0], owValue.ToDisplayText());
                for (var i = 1; i < runs.Count; i++) WordRunText.Set(runs[i], string.Empty);

                report.Hits.Add(new OfficeFillHit
                {
                    Token = matches[0].Value,
                    AnchorCode = owKey,
                    LocationKind = kind,
                    Location = location,
                    Value = Truncate(owValue.ToDisplayText(), 120),
                    CrossRun = runs.Count > 1,
                    Source = owValue.Source,
                });
                return;
            }
        }
        else
        {
            // 多锚点 + 有人要求覆盖 ⇒ 语义模糊，如实记一条待办（本段仍按「填充」处理）
            var owKey = matches
                .Select(m => FillSyntax.SplitFormat(m.Groups[1].Value).Key)
                .FirstOrDefault(k => request.Values.TryGetValue(k, out var v) && v.IsOverwrite());

            if (owKey != null)
            {
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = matches[0].Value,
                    AnchorCode = owKey,
                    LocationKind = kind,
                    Location = location,
                    Reason = $"写入方式配了「覆盖」（整段替换），但本段有 {matches.Count} 个锚点 —— "
                           + "整段该换成哪一个的值无法确定，已按「填充」处理（只替换 {{}}）",
                });
            }
        }

        // 3. 从后往前替换
        for (var mi = matches.Count - 1; mi >= 0; mi--)
        {
            var match = matches[mi];
            var token = match.Value;

            // ★ 支持 {{key:format}} —— Word 侧不使用 format（Word 的字符样式由 run 的 rPr 决定，
            //   不存在 Excel 那种「值 + 数字格式」的两段式），但**键必须拆对**，否则查不到值。
            var (key, _) = FillSyntax.SplitFormat(match.Groups[1].Value);

            FillValue? value;
            var hasValue = request.Values.TryGetValue(key, out value);

            if (!hasValue)
            {
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = token,
                    AnchorCode = key,
                    LocationKind = kind,
                    Location = location,
                    Reason = "未提供值",
                });

                // ★ 未命中 ⇒ 默认置空（2026-10-02 用户规格：
                //   「针对填写或替换，如果没有值则自动将填写内容赋值为空」）
                if (request.KeepUnresolvedAsIs) continue;
            }

            if (!TryLocate(texts, match.Index, match.Length, out var r1, out var o1, out var r2, out var o2))
            {
                // 理论上不可达（拼接串里的区间必然可定位）；留作防御
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = token, AnchorCode = key, LocationKind = kind, Location = location,
                    Reason = "无法定位锚点所在 run",
                });
                continue;
            }

            // ★ 锚点跨越「非纯文本 run」（域 / 图片 / 换行）⇒ 跳过：替换会删掉锚点却留下原子内容
            var blocked = false;
            for (var i = r1; i <= r2; i++)
            {
                if (!WordRunText.IsTextOnly(runs[i])) { blocked = true; break; }
            }
            if (blocked)
            {
                report.Pendings.Add(new OfficeFillPending
                {
                    Token = token, AnchorCode = key, LocationKind = kind, Location = location,
                    Reason = "跨域锚点（锚点跨越域/图片/换行，请拆开）",
                });
                continue;
            }

            var valueText = value?.ToDisplayText() ?? string.Empty;

            // 3.1 计算新文本
            var head = texts[r1][..o1] + valueText;
            var tail = r2 == r1 ? texts[r1][o2..] : texts[r2][o2..];

            // 3.2 写回
            if (r2 == r1)
            {
                WordRunText.Set(runs[r1], head + tail);
                texts[r1] = head + tail;
            }
            else
            {
                WordRunText.Set(runs[r1], head);
                for (var i = r1 + 1; i < r2; i++) WordRunText.Set(runs[i], string.Empty);
                WordRunText.Set(runs[r2], tail);

                texts[r1] = head;
                for (var i = r1 + 1; i < r2; i++) texts[i] = string.Empty;
                texts[r2] = tail;
            }

            if (!hasValue) continue;   // 置空不进 Hits（它已在 Pendings 里，不是「填上了」）

            report.Hits.Add(new OfficeFillHit
            {
                Token = token,
                AnchorCode = key,
                LocationKind = kind,
                Location = location,
                Value = Truncate(valueText, 120),
                CrossRun = r2 != r1,
                Source = value!.Source,
            });
        }
    }

    /// <summary>
    /// 把 <paramref name="texts"/> 拼接串中的字符区间 <c>[start, start+length)</c> 反查为 run 区间。
    /// <para><c>r2/o2</c> 中的 <c>o2</c> 是 <b>排他</b>偏移（即「保留后缀」的起点）。</para>
    /// </summary>
    private static bool TryLocate(
        string[] texts, int start, int length,
        out int r1, out int o1, out int r2, out int o2)
    {
        r1 = -1; o1 = 0; r2 = -1; o2 = 0;
        var end = start + length;
        var acc = 0;

        for (var i = 0; i < texts.Length; i++)
        {
            var len = texts[i].Length;
            if (r1 < 0 && start < acc + len)
            {
                r1 = i;
                o1 = start - acc;
            }

            if (end <= acc + len)
            {
                r2 = i;
                o2 = end - acc;
                break;
            }

            acc += len;
        }

        return r1 >= 0 && r2 >= 0;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
