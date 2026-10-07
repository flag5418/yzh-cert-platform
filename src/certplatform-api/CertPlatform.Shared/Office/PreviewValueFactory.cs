using System;
using System.Globalization;
using CertPlatform.Shared.Entities.Doc;

namespace CertPlatform.Shared.Office;

/// <summary>
/// 「试填」取值链 —— 给后台「标准资料填写规则」页的 <b>试填 / 预览</b> 产出一套
/// <b>可辨识的测试值</b>。
///
/// <para><b>★ 为什么需要它（而不是直接跑真实取值链）</b>：后台「标准资料填写规则」页
/// 是给<b>认证机构维护人员配规则</b>用的，<b>没有企业上下文</b>。而真实取值链的两个
/// 确定性来源<b>都必须要企业</b> ——
/// <see cref="CertPlatform.Shared.Services.Fill.FillParamValueProvider"/> 要
/// <c>EnterpriseInfo</c> + 企业已填值；<c>manual</c> 按 <c>EnterpriseCode</c> 查
/// <c>cert_doc_ai_suggestion</c>。
/// ⇒ 在后台页跑真实链<b>必然全空</b>，看不出任何东西。</para>
///
/// <para><b>★ 用户裁决（2026-10-06）</b>：试填值 = <b>用模板自带信息回填</b> ——
/// ① 优先回填该锚点的<b>原始文字</b>（<see cref="DocTemplateAnchor.OriginalText"/>）；
/// ② 没有则按声明的 <c>ValueType</c> 生成占位（<c>date</c> = 今天 / <c>number</c> = 1 /
/// <c>text</c> = <c>【试填】锚点名</c>）。</para>
///
/// <para><b>★ 试填的目的 = 验证「规则配的位置对不对」</b>，⛔ 不是「验证值对不对」
/// （那需要真企业数据，属专家端主链）。所以占位值刻意带上锚点名 ——
/// 产物里出现 <c>【试填】ENT_NAME</c> 就能一眼看出「这个位置配的是 <c>ENT_NAME</c>」，
/// 而出现空白就知道「这个位置没被任何规则覆盖」。同时它<b>不可能被误当成真实产物</b>。</para>
///
/// <para><b>⚠️ 实测提醒（2026-10-06）</b>：现有锚点的 <c>OriginalText</c> <b>全为 NULL</b>
/// （该列注释写明「仅 <c>replace</c> / <c>remove</c> 需要」，而默认 <c>WriteMode=overwrite</c>）
/// ⇒ 实际以「按类型生成占位」为主。这是<b>正常</b>的，不影响试填的用途。</para>
///
/// <para><b>★ 纯函数纪律</b>：本类<b>不查库、不写库、不碰存储</b>（守 G-4）——
/// 调用方负责把锚点读出来喂进来。</para>
/// </summary>
public static class PreviewValueFactory
{
    /// <summary>占位值前缀 —— 让试填产物<b>一眼可辨</b>（⛔ 不要改成看起来像真实数据的东西）</summary>
    public const string PlaceholderPrefix = "【试填】";

    /// <summary>一次试填取值的产物（含「值从哪来」的如实标注）</summary>
    public sealed class PreviewValue
    {
        /// <summary>是否取到（<c>false</c> ⇒ <see cref="Value"/> 为 null，调用方记待办）</summary>
        public bool Ok { get; set; }

        /// <summary>试填值（<see cref="FillValueFactory"/> 产出，类型随锚点声明）</summary>
        public FillValue? Value { get; set; }

        /// <summary>
        /// 值的来源标注（写入报告的 <c>Source</c>）：
        /// <c>原始文字</c> / <c>试填占位值</c> / <c>示例数据（合规清空）</c> / <c>生成失败</c>。
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>失败原因（<see cref="Ok"/> = false 时非空）</summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// 为<b>一个锚点</b>产出试填值。
    ///
    /// <para><b>三级顺序</b>（⛔ 不可颠倒）：</para>
    /// <list type="number">
    ///   <item><b>示例数据锚点 ⇒ 清空</b>：与真实填充链<b>逐字一致</b>（合规铁律，
    ///         25 号 Q-5：模板常含上一家企业的真实姓名与日期）。试填<b>也必须清</b>，
    ///         否则预览会显示「真实链会清空的东西」，掩盖真实效果。</item>
    ///   <item><b>原始文字 ⇒ 回填</b>：按锚点声明的类型解析；<b>解析失败不降级</b>，
    ///         落到第 ③ 步（否则 <c>number</c> 声明会静默变成文本，Excel 里数字变文本
    ///         <b>不报错但结果错</b>）。</item>
    ///   <item><b>按类型生成占位</b>。</item>
    /// </list>
    /// </summary>
    /// <param name="anchor">目标锚点（提供 <c>OriginalText</c> / <c>ValueType</c> / <c>NumberFormat</c> / <c>SampleData</c>）</param>
    /// <param name="key">
    /// 归一后的锚点键（与文档 <c>{{ }}</c> 内文本逐字一致，⛔ 不含花括号）。
    /// <para>⚠️ 由调用方用<b>与写入器同一套</b>归一逻辑算出（见
    /// <c>DocumentFillOrchestrator.AnchorKeyOf</c>）—— 键不一致 = 「填不进去且不报错」。</para>
    /// </param>
    public static PreviewValue Create(DocTemplateAnchor anchor, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new PreviewValue { Ok = false, Source = "锚点键为空", Error = "锚点键为空" };

        // ── ① 示例数据锚点：清空（与真实链一致，合规铁律）──
        if (anchor.SampleData)
        {
            var (okEmpty, emptyVal, emptyErr) = FillValueFactory.TryCreate(
                key, string.Empty, anchor.ValueType, anchor.NumberFormat);

            return okEmpty && emptyVal != null
                ? new PreviewValue { Ok = true, Value = emptyVal, Source = "示例数据（合规清空）" }
                : new PreviewValue { Ok = false, Source = "示例数据（合规清空）", Error = emptyErr };
        }

        // ── ② 原始文字回填（用户裁决的第一优先）──
        var original = (anchor.OriginalText ?? string.Empty).Trim();
        if (original.Length > 0)
        {
            var (okOrig, origVal, _) = FillValueFactory.TryCreate(
                key, original, anchor.ValueType, anchor.NumberFormat);

            // ⚠️ 解析失败（如声明 number 但原文是「叁万元」）**不降级**，落到 ③ 生成占位。
            //    「降级成文本」会让 Excel 数字变文本 —— 不报错但结果错。
            if (okOrig && origVal != null)
            {
                origVal.Source = "试填 · 原始文字";
                return new PreviewValue { Ok = true, Value = origVal, Source = "原始文字" };
            }
        }

        // ── ③ 按声明的类型生成占位 ──
        var generated = GenerateByType(anchor.ValueType, key);
        var (ok, val, err) = FillValueFactory.TryCreate(
            key, generated, anchor.ValueType, anchor.NumberFormat);

        if (!ok || val == null)
            return new PreviewValue { Ok = false, Source = "生成失败", Error = err };

        val.Source = $"试填 · 占位值（{generated}）";
        return new PreviewValue { Ok = true, Value = val, Source = "试填占位值" };
    }

    /// <summary>
    /// 按声明的 <c>ValueType</c> 生成占位文本。
    ///
    /// <para><b>★ 为什么 <c>text</c> 用「<c>【试填】+ 锚点名</c>」而不是「示例文本」</b>：
    /// 试填要回答的是「<b>这个位置配的是哪个锚点</b>」—— 带上名字才回答得了。
    /// 一个通用的「示例文本」只能告诉你「这里被填过」，告诉不了你「填的是谁」。</para>
    /// </summary>
    private static string GenerateByType(string? valueType, string key) =>
        (valueType ?? "text").Trim().ToLowerInvariant() switch
        {
            "number" => "1",
            // ★ InvariantCulture：日期占位必须与服务器区域性无关，否则跨环境预览不一致
            "date" => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "bool" => "是",
            // text / enum / 未知
            _ => PlaceholderPrefix + key,
        };
}
