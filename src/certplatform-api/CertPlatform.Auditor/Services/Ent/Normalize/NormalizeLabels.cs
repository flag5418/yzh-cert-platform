namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     ★ <b>后端自拼文案的「短码 / 标准名」助手</b> —— 全项目唯一实现。
    ///
    ///     <para><b>为什么上提到这里</b>（2026-10-14 第 0 批通用 Service 抽取）：
    ///     原本这两个方法是 <c>EnterpriseNormalizeController</c> 的私有静态成员；
    ///     而「缺参预检」下沉到 <see cref="FillParamService"/> 后，<b>Service 也要拼同一句话</b>
    ///     （<c>NormalizeParamGapGroup.StandardName</c>）。留在 Controller 里 ⇒ Service 只能复制一份，
    ///     两处必然漂移，且<b>都不报错</b> —— 症状是「干跑说缺 X、补参弹窗说缺 Y」。</para>
    ///
    ///     <para>⚠️ 本类只服务<b>后端自己拼的文案</b>（缺参拦截、干跑拒绝原因、留痕人话）。
    ///     <c>tree</c> 端点的标准节点仍回传<b>原始事实</b>（空串 + <c>StandardRegistered</c>），
    ///     由前端决定怎么显示 —— 职责边界不要混。</para>
    /// </summary>
    internal static class NormalizeLabels
    {
        /// <summary>
        ///     ★ <b>短码</b> —— 用于「主数据缺失」时的人话占位（如 <c>475da4fe</c>）。
        ///
        ///     <para>取前 8 位：既能唯一指认（本仓 Code 是 GUID 或 32 位 hex），
        ///     又不会像完整 GUID 那样把版面撑爆、把人劝退。</para>
        /// </summary>
        public static string ShortCode(string? code)
            => string.IsNullOrEmpty(code) ? "?" : (code.Length > 8 ? code[..8] : code);

        /// <summary>
        ///     ★ <b>标准显示名（后端生成文案用）</b> —— ⛔ <b>绝不把裸 GUID 当名字</b>。
        ///
        ///     <para>取值顺序：<c>StandardName</c> → <c>StandardCode</c>（业务编号，如 <c>iso9001</c>）
        ///     → 「未登记标准（短码）」。</para>
        ///
        ///     <para><b>为什么不让它回退成 GUID</b>：2026-10-09 用户报障 ——
        ///     <c>cert_iso_standard</c> 里 <c>475da4fe-…</c>（食品标准）被删、但企业阶段关联仍在，
        ///     于是页面 Tab 直接显示一串 GUID。<b>用户看到的是「系统坏了」，不是「数据缺了」</b>。
        ///     ⇒ 宁可显示「未登记标准（475da4fe）」，也不能显示 GUID。</para>
        /// </summary>
        public static string StandardLabel(string? name, string? no, string? code)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name!;
            if (!string.IsNullOrWhiteSpace(no)) return no!;
            return $"未登记标准（{ShortCode(code)}）";
        }
    }
}
