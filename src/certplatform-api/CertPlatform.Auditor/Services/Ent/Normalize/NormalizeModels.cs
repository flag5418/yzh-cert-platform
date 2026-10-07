using CertPlatform.Shared.Office;

namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     单文件规范化请求 —— 编排器 <see cref="DocumentFillOrchestrator.FillOneAsync"/> 的唯一入参。
    ///
    ///     <para><b>★ 一个文件 = 一个任务</b>（`55` §3.1：一个范围 = 一个批次，一个文件 = 一个任务）。
    ///     范围展开与入队由调用方（Controller / 队列执行器）负责，⛔ 编排器不做范围判断。</para>
    ///
    ///     <para>⚠️ <b>输入源是「画像」不是「原始文件」</b>（26 号 A-1 / `55` §4.1 ②）：
    ///     <see cref="StandardFileCode"/> 指向的是<b>企业侧的标准文档实例行</b>
    ///     （<c>cert_standard_directory_file</c>，<c>EnterpriseCode</c> = 真实企业），
    ///     ⛔ 不是标准域那 168 行。</para>
    /// </summary>
    public sealed class FillOneRequest
    {
        /// <summary>认证机构 Code</summary>
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>企业 Code</summary>
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>标准 Code（GUID）</summary>
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（GUID）</summary>
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 目标标准文件 —— <c>cert_standard_directory_file.Code</c>
        ///     <para>⚠️ 指 <b>标准域行</b>（<c>EnterpriseCode = YzhVirtualEnterprise.Code</c>，
        ///     即 <c>cert_doc_template.StandardFileCode</c> 指向的那一行），
        ///     ⛔ <b>不是</b>企业侧实例行。</para>
        ///     <para><b>为什么</b>：规范化驱动源 = <c>cert_doc_template</c>（`60` §六之补三），
        ///     而模板挂的正是标准域行；企业侧实例行由编排器按
        ///     <c>(EnterpriseCode, StandardFileCode)</c> 定位，不存在则创建。</para>
        /// </summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>★ 批次 → <c>yzh_queue.QueueCode</c>（可空；单文件直跑时为空）</summary>
        public string? QueueCode { get; set; }

        /// <summary>★ 单文件任务 → <c>yzh_queue_task.Code</c>（可空）</summary>
        public string? QueueTaskCode { get; set; }

        /// <summary>触发人 Code（写入 <c>cert_doc_fill_log.CreateBy</c>）</summary>
        public string? OperatorCode { get; set; }
    }

    /// <summary>
    ///     单文件规范化结果。
    ///
    ///     <para><b>★ <see cref="Status"/> 的六态（⛔ 不要合并成 bool）</b>：
    ///     「跳过」与「失败」是<b>两件事</b> —— 前者是<b>设计内</b>（已锁定 / 未配模板 / 模板未发布），
    ///     后者是<b>异常</b>。混在一起会让「一键规范化」的进度条显示成一片红。</para>
    /// </summary>
    public sealed class FillOneResult
    {
        /// <summary>整体是否成功（<c>Status</c> ∈ {filled, partial, skipped_*} ⇒ true）</summary>
        public bool Success { get; set; }

        /// <summary>
        ///     <c>filled</c>（全部写入且自验收通过）｜<c>partial</c>（写了但自验收未过或有待办）｜
        ///     <c>skipped_locked</c>（已锁定）｜<c>skipped_no_template</c>（无模板 / 未发布）｜
        ///     <c>skipped_no_anchor</c>（锚点 0，纯复制即可）｜<c>failed</c>（异常）。
        /// </summary>
        public string Status { get; set; } = "failed";

        /// <summary>人话说明（⛔ 不是堆栈 —— `54` §3.4：失败原因要人话）</summary>
        public string? Message { get; set; }

        /// <summary>产物路径（MinIO，<c>enterprise-documents/…</c>）；跳过/失败时为空</summary>
        public string? OutputPath { get; set; }

        /// <summary>文件级完成率 <c>(FillAnchorCount − FillPendingCount) / FillAnchorCount</c>；分母 0 ⇒ 0</summary>
        public decimal Completion { get; set; }

        /// <summary>文件级加权可信度（只对 <c>filled</c> 行求均值）</summary>
        public decimal Confidence { get; set; } = 1.00m;

        /// <summary>锚点总数（分母快照）</summary>
        public int AnchorCount { get; set; }

        /// <summary>待办数（分子缺口）</summary>
        public int PendingCount { get; set; }

        /// <summary>自验收：无残留锚点且无残留标记</summary>
        public bool Verified { get; set; }

        /// <summary>本次写入 <c>cert_doc_fill_value</c> 的行数</summary>
        public int LedgerRows { get; set; }

        /// <summary>本次写入 <c>cert_doc_fill_log</c> 的 Code</summary>
        public string? FillLogCode { get; set; }

        /// <summary>非致命问题清单（越界 / 丢弃 / 未写入）—— ⛔ 不静默</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        ///     ★ <b>待办明细（含「可操作归因」）</b> —— ⛔ 不能只给 <see cref="PendingCount"/> 一个数。
        ///
        ///     <para><b>为什么必须有</b>：只报「待办 1 个」时，用户<b>无法区分</b>这四种原因，
        ///     而它们指向四个<b>完全不同</b>的修复动作：</para>
        ///     <list type="number">
        ///       <item>模板锚点<b>没配数据源</b> → 后台「填写规则」页配</item>
        ///       <item>参数<b>未在后台定义</b> → 后台「体系认证全局参数定义」新增</item>
        ///       <item>参数已定义但<b>企业没填值</b> → 专家端「企业资料参数」页填</item>
        ///       <item>参数是 <c>auto</c> 但<b>企业档案字段为空</b> → 先补企业档案</item>
        ///     </list>
        ///
        ///     <para>⛔ 不区分 = <b>指错方向</b>（`DocumentFillController:237-240` 原话：
        ///     「把人引到企业端去找一个根本不存在的参数 —— <b>指错方向比不报错更耗时</b>」）。
        ///     归因逻辑见 <c>DocumentFillOrchestrator.DiagnoseNoValueAsync</c>。</para>
        ///
        ///     <para>⚠️ <b>同一 token 在文档里出现 N 次 ⇒ 本清单有 N 条</b>
        ///     （每条带各自的 <c>Location</c>，如「表格[0] 行 2 列 3」），共享同一句归因。</para>
        /// </summary>
        public List<OfficeFillPending> Pendings { get; set; } = new();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ★ 试填 / 预览（2026-10-06，`52` B7/B8）
    //    定位：`59` §三「试填预览 = `FillOneAsync` 的**只读调用**（⛔ 不新建独立填充链）」
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     试填请求 —— <see cref="DocumentFillOrchestrator.FillPreviewAsync"/> 的唯一入参。
    ///
    ///     <para><b>★ 与 <see cref="FillOneRequest"/> 的本质差别</b>：正式填充<b>必须</b>有企业
    ///     （取值靠企业档案 / 画像 / 企业已填值，见 <c>FillParamValueProvider</c>）；
    ///     试填<b>没有企业</b> —— 它用「模板自带信息 + 类型占位」产出一份<b>可辨识的样张</b>，
    ///     回答的是「<b>规则配的位置对不对</b>」，⛔ 不是「值对不对」。</para>
    ///
    ///     <para><b>★ 门槛刻意低于正式填充</b>：⛔ <b>不要求</b> <c>PublishStatus='published'</c>。
    ///     正式规范化要求「已发布」（`60` §六之补三 用户裁决），但试填正是
    ///     「<b>发布前</b>验证规则」的工具 —— 要求已发布就等于「发布后才能验证」= 死锁。</para>
    /// </summary>
    public sealed class FillPreviewRequest
    {
        /// <summary>目标模板 → <c>cert_doc_template.Code</c></summary>
        public string TemplateCode { get; set; } = string.Empty;

        /// <summary>触发人 Code（仅回传，⛔ 试填不写库）</summary>
        public string? OperatorCode { get; set; }

        /// <summary>
        ///     <b>人工覆盖值</b>（2026-10-07 预览 Tab「自动填写 → 用户改 → 再预览」）。
        ///
        ///     <para><b>语义</b>：命中 <see cref="PreviewOverrideInput.AnchorRef"/> 的锚点
        ///     <b>不再走</b> <c>PreviewValueFactory</c>（模板自带信息 + 类型占位），
        ///     改用调用方给的 <see cref="PreviewOverrideInput.Value"/> 直接落笔。
        ///     ⚠️ 命中的锚点即使值为空串也<b>算「已填」</b>（用户显式清空 ≠ 未取到值）——
        ///     ⛔ 不进 <c>Pendings</c>。</para>
        ///
        ///     <para><b>仍不写库</b>：覆盖只作用于<b>本次</b>试填产物，
        ///     ⛔ 不回写 <c>cert_doc_fill_value</c>、不回写锚点规则。</para>
        /// </summary>
        public List<PreviewOverrideInput> Overrides { get; set; } = new();
    }

    /// <summary>
    ///     试填覆盖值 —— 一个锚点的人工值（<c>PascalCase</c>，`22` 号契约）。
    /// </summary>
    public sealed class PreviewOverrideInput
    {
        /// <summary>锚点引用 → <c>cert_doc_template_anchor.AnchorRef</c>（定位锚点的业务键）</summary>
        public string AnchorRef { get; set; } = string.Empty;

        /// <summary>锚点 Key（冗余传入，便于日志对账；以 <see cref="AnchorRef"/> 为准）</summary>
        public string? Key { get; set; }

        /// <summary>覆盖后的文本值（空串 = 显式清空，仍算已填）</summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    ///     试填结果 —— <b>⛔ 不含任何落库产物</b>（无账本 / 无日志 / 无宿主行 / 不传企业产物）。
    ///
    ///     <para><see cref="Output"/> 是<b>填充后的 Office 字节</b>，由 Controller 负责
    ///     转 PDF 并上传到 <c>_preview/</c>（`52` §12.2 链路）。⛔ 编排器不碰这一段 ——
    ///     保持「编排器 = 纯填充」与「控制器 = 产物落点」的职责分离。</para>
    /// </summary>
    public sealed class FillPreviewResult
    {
        /// <summary>整体是否成功（能产出 <see cref="Output"/> 即 true）</summary>
        public bool Success { get; set; }

        /// <summary>
        ///     <c>filled</c>（全部写入且自验收通过）｜<c>partial</c>（写了但自验收未过或有待办）｜
        ///     <c>skipped_no_anchor</c>（锚点 0，产物 = 模板副本）｜<c>failed</c>（异常 / 模板不可读）。
        /// </summary>
        public string Status { get; set; } = "failed";

        /// <summary>人话说明（⛔ 不是堆栈）</summary>
        public string? Message { get; set; }

        /// <summary>填充后的 Office 字节（<c>.docx</c> / <c>.xlsx</c>）</summary>
        public byte[] Output { get; set; } = Array.Empty<byte>();

        /// <summary><c>docx</c> / <c>xlsx</c>（决定 PDF 转换与 MIME）</summary>
        public string FileKind { get; set; } = "docx";

        /// <summary>产物文件名（模板原名换扩展名）—— 用于 <c>ConvertToPdfAsync</c> 的入参</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>模板的 MinIO 路径 —— 供 Controller 派生 <c>_preview/</c> 落点</summary>
        public string TemplateStoragePath { get; set; } = string.Empty;

        /// <summary>锚点总数（分母快照）</summary>
        public int AnchorCount { get; set; }

        /// <summary>待办数（未取到值的锚点数）</summary>
        public int PendingCount { get; set; }

        /// <summary>完成率 <c>(AnchorCount − PendingCount) / AnchorCount</c>；分母 0 ⇒ 0</summary>
        public decimal Completion { get; set; }

        /// <summary>自验收：无残留锚点且无残留标记</summary>
        public bool Verified { get; set; }

        /// <summary>非致命问题清单（越界 / 丢弃 / 自验收未过）—— ⛔ 不静默</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>待办明细 —— 供页面回答「哪些位置没填上」</summary>
        public List<PreviewPendingItem> Pendings { get; set; } = new();

        /// <summary>取值明细 —— 供页面核对「这个位置配的是哪个锚点」（试填专属）</summary>
        public List<PreviewAnchorValue> Values { get; set; } = new();
    }

    /// <summary>试填待办一行</summary>
    public sealed class PreviewPendingItem
    {
        /// <summary>锚点原文（含花括号，如 <c>{{ENT_NAME}}</c>）</summary>
        public string AnchorRef { get; set; } = string.Empty;

        /// <summary>归一后的键（与文档 <c>{{ }}</c> 内文本逐字一致）</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>未取到值的原因</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>试填取值一行（来源标注，⛔ 不落库）</summary>
    public sealed class PreviewAnchorValue
    {
        public string AnchorRef { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        /// <summary><c>原始文字</c> / <c>试填占位值</c> / <c>示例数据（合规清空）</c></summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        ///     写入产物的**完整**值（预览 Tab 编辑框的初值、回传 <c>Overrides[].Value</c> 的原文）。
        ///
        ///     <para><b>★ 与 <see cref="Display"/> 的分工</b>：<c>Display</c> 是给表格看的
        ///     120 字符截断版；回传覆盖必须用本字段 —— 拿截断值回传会把长值
        ///     <b>静默截断后写进产物</b>（第二次预览比第一次短一截，且两边都不报错）。</para>
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>写入产物的展示值（截断，仅供表格列显示）</summary>
        public string Display { get; set; } = string.Empty;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ★ 范围展开 / 干跑 / 入队（2026-10-07，`54` §5.2 的 plan + run）
    //    定位：`54` §3.2 五级范围（企业 › 阶段 › 标准 › 文件夹 › 文件）的**后三级**展开，
    //          以及 `55` §3.1「一个范围 = 一个批次，一个文件 = 一个任务」的批次入口。
    //    ⛔ 与 `FillOneRequest` 的分工：后者是「一个文件」的载荷，本组是「一批文件」的范围。
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     范围请求 —— <c>plan</c>（干跑）与 <c>run</c>（入队）<b>共用同一入参</b>。
    ///
    ///     <para><b>★ 为什么两个端点同一个入参</b>：干跑的意义就是「<b>先说清楚要干什么</b>，
    ///     用户确认后再真干」。若两边入参形状不同，就会出现「预览的范围」与「真跑的范围」
    ///     是两次独立计算 ⇒ 结果对不上且无人发现。同参 + 同一份服务端算法 ⇒ 干跑即承诺。</para>
    /// </summary>
    public sealed class NormalizeScopeRequest
    {
        /// <summary>企业 Code</summary>
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（GUID）</summary>
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 选中的目标文件 —— <c>cert_standard_directory_file.Code</c>
        ///     <para>⚠️ 指 <b>标准域行</b>（<c>EnterpriseCode = YZH-STD-ENT</c>），
        ///     ⛔ 不是企业侧实例行 —— 与 <see cref="FillOneRequest.StandardFileCode"/> 同口径。</para>
        ///
        ///     <para>⚠️ 服务端<b>不信任</b>这个清单：<c>run</c> 会重新按「已发布模板」复核一遍
        ///     （防止前端拿着过期页面把已取消发布的模板投进队列）。</para>
        /// </summary>
        public List<string> StandardFileCodes { get; set; } = new();
    }

    /// <summary>
    ///     干跑结果 —— <b>纯读、零副作用</b>（不写库 / 不产文件 / 不调 LLM / 不入队）。
    ///
    ///     <para><b>★ 口径出处</b>：`54` §3.3「干跑 <c>plan</c> = 强制前置、纯读零副作用」；
    ///     计数分组 = 「将规范化 N · 新增 M · 重新生成 K · <b>三类跳过</b>」。</para>
    ///
    ///     <para><b>★ 为什么「跳过」要分三类而不是一个数字</b>：三种跳过的<b>处置人不同</b> ——
    ///     <c>locked</c> 是审核员自己锁的（要他自己解锁）｜<c>no_template</c> 是后台还没配规则
    ///     （要实施人员去配）｜<c>no_anchor</c> 是模板配了但没锚点（规则问题）。
    ///     合成一个数字 ⇒ 用户只知道「有 3 个没跑」，不知道该找谁。</para>
    /// </summary>
    public sealed class NormalizePlanResult
    {
        /// <summary>将首次规范化（企业侧还没有产物）</summary>
        public int WillFill { get; set; }

        /// <summary>将重新生成（企业侧已有产物，会被覆盖）</summary>
        public int WillRegenerate { get; set; }

        /// <summary>跳过 —— 企业侧实例已锁定</summary>
        public int SkipLocked { get; set; }

        /// <summary>跳过 —— 没有已发布的空白模板（未配规则 / 未发布）</summary>
        public int SkipNoTemplate { get; set; }

        /// <summary>跳过 —— 模板没有任何锚点（无需填充）</summary>
        public int SkipNoAnchor { get; set; }

        /// <summary>本次选中总数</summary>
        public int Total { get; set; }

        /// <summary>★ 真正会入队的数量 = <see cref="WillFill"/> + <see cref="WillRegenerate"/></summary>
        public int Queued { get; set; }

        /// <summary>逐条明细（含跳过项及其原因 —— ⛔ 不静默丢弃）</summary>
        public List<NormalizePlanItem> Items { get; set; } = new();
    }

    /// <summary>干跑明细一行</summary>
    public sealed class NormalizePlanItem
    {
        public string StandardFileCode { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        /// <summary>
        ///     <c>fill</c>（首次规范化）｜<c>regenerate</c>（重新生成）｜
        ///     <c>skip_locked</c>｜<c>skip_no_template</c>｜<c>skip_no_anchor</c>
        /// </summary>
        public string Action { get; set; } = "fill";

        /// <summary>人话说明（⛔ 不是编码 —— 页面直接显示）</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>模板锚点总数（0 ⇒ <c>skip_no_anchor</c>）</summary>
        public int AnchorCount { get; set; }

        /// <summary>企业侧实例是否已锁定</summary>
        public bool IsLocked { get; set; }

        /// <summary>企业侧是否已有产物（true ⇒ 本次是覆盖重生成）</summary>
        public bool HasOutput { get; set; }
    }

    /// <summary>
    ///     入队结果 —— <c>run</c> 的返回（<b>不阻塞等待执行</b>，只回批次号）。
    /// </summary>
    public sealed class NormalizeRunResult
    {
        /// <summary>批次号 → <c>yzh_queue.QueueCode</c></summary>
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>本次入队任务数（= 干跑的 <c>Queued</c>）</summary>
        public int Queued { get; set; }

        /// <summary>被跳过数（三类之和）</summary>
        public int Skipped { get; set; }

        /// <summary>逐条明细（与干跑同形状，便于页面直接把预览结果「落定」成执行结果）</summary>
        public List<NormalizePlanItem> Items { get; set; } = new();
    }
}
