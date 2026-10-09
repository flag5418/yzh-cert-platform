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

        /// <summary>
        /// ★ 按标准分组的「缺失必填全局参数」（§6 缺参预检，2026-10-07）。
        /// <para>每个入队标准列一组：该 企业×阶段×标准 下 <c>IsRequired=true</c> 且企业尚未填有效值的
        /// 全局参数。空 = 该标准必填参数已齐。⛔ 纯读零副作用（plan 不写库）。</para>
        /// </summary>
        public List<NormalizeParamGapGroup> ParamGaps { get; set; } = new();

        /// <summary>★ 必填缺失参数总数（= 所有 <see cref="ParamGaps"/> 里 <c>IsRequired=true</c> 项之和）。</summary>
        public int MissingRequired { get; set; }

        /// <summary>
        /// ★ §9.5 视觉可达校验（2026-10-07）：入队文件是否含「图片 / PDF」（需视觉模型 OCR 才能出 Markdown）。
        /// <para>true 且 <c>IOcrProvider.IsAvailable == false</c>（视觉模型未配 / 非视觉模型）时，
        /// <c>run</c> 端点<b>整批拒绝</b>（能力是文档必要条件，⛔ 不降级 partial —— §9.5 铁律）。</para>
        /// </summary>
        public bool NeedsVision { get; set; }

        // ──── ★ 以下为「重写预检」追加段（`rewrite` 端点填充；`plan` / `run` 保持默认值）────
        //
        //  ⚠️ 为什么追加到本类而不是另开一个包装类：`41-03` §1.6 把 `rewrite` 的出参
        //     逐字写成 `NormalizePlan` ⇒ 另开包装会让前端拿到 `data.Plan.*` 的形状，
        //     与契约不符、也与 `plan` 端点不同形状（同一件事两种形状 = 必然漂移）。
        //
        //  ⚠️ 为什么必须有 `RewritePrecheck` 这个开关：下面那些计数<b>默认全 0</b>，
        //     而 0 有两种完全不同的含义 ——「预检过，确实一个都没有」与「根本没做预检」。
        //     不区分 = 页面会把「未预检」显示成「一个都不会保留」= 假信息。

        /// <summary>
        ///     ★ <b>本次结果是否包含「重写预检」</b>。
        ///     <para><c>true</c> ⇒ <c>rewrite</c> 端点，下面的 <c>WillKeep*</c> / <c>*Total</c> 有意义；
        ///     <c>false</c> ⇒ <c>plan</c> / <c>run</c> 的普通干跑，下面那些字段<b>无意义</b>
        ///     （⛔ 页面不得据此显示「将保留 0 个」）。</para>
        /// </summary>
        public bool RewritePrecheck { get; set; }

        /// <summary>回显「保留人工改过的值」开关（规则③）</summary>
        public bool KeepManual { get; set; }

        /// <summary>回显「保留钉住的锚点」开关（规则②）</summary>
        public bool KeepPinned { get; set; }

        /// <summary>基准账本里「钉住」的锚点数（<b>事实</b>，与开关无关）</summary>
        public int PinnedAnchorTotal { get; set; }

        /// <summary>基准账本里「人工改过值」的锚点数（<b>事实</b>，与开关无关）</summary>
        public int ManualValueTotal { get; set; }

        /// <summary>模板示例数据的锚点数（<b>事实</b>，规则④ 必清，⛔ 不受开关影响）</summary>
        public int SampleAnchorTotal { get; set; }

        /// <summary>★ 按开关算出的<b>将保留</b>锚点数（= 规则②③ 的合并结果，去重计行）</summary>
        public int WillKeepAnchorTotal { get; set; }

        /// <summary>★ 按开关算出的<b>将重新取值</b>锚点数</summary>
        public int WillRecomputeAnchorTotal { get; set; }

        /// <summary>逐文件预检明细（只含<b>将要跑</b>的文件 —— 跳过的项在 <see cref="Items"/> 里）</summary>
        public List<NormalizeRewriteFilePrecheck> RewriteFiles { get; set; } = new();
    }

    /// <summary>
    /// ★ 按标准分组的缺参组（§6：执行前"分标准看每个标准缺哪些全局参数"，2026-10-07）。
    /// </summary>
    public sealed class NormalizeParamGapGroup
    {
        /// <summary>标准 Code（<c>cert_iso_standard.Code</c>；入队标准）。</summary>
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>标准名称（人话显示）。</summary>
        public string StandardName { get; set; } = string.Empty;

        /// <summary>该标准下缺失的必填参数逐条。</summary>
        public List<NormalizeParamGapItem> Items { get; set; } = new();
    }

    /// <summary>★ 单个缺失的全局参数（§6 缺参预检）。</summary>
    public sealed class NormalizeParamGapItem
    {
        /// <summary>参数 Code（<c>cert_fill_param_def.ParamCode</c>）。</summary>
        public string ParamCode { get; set; } = string.Empty;

        /// <summary>参数名（人话）。</summary>
        public string ParamName { get; set; } = string.Empty;

        /// <summary>参数分组（如「基础信息」，前端按组渲染补填表单）。</summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>取值类型（text/number/…，前端出对应控件）。</summary>
        public string ValueType { get; set; } = string.Empty;

        /// <summary>是否必填（拦截判据只看 IsRequired；选填缺失放行照旧 partial）。</summary>
        public bool IsRequired { get; set; }
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

    // ════════════════════════════════════════════════════════════════════
    //  批次进度与取消（`54` §5.2 端点 5 / 6）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     ★ <b>批次进度</b> —— <c>batch/{queueCode}</c> 的返回（`54` §5.2 端点 5）。
    ///
    ///     <para>页面对本端点的轮询是<b>全页唯一</b>的轮询点（`41-03` §1.6.1）。</para>
    /// </summary>
    public sealed class NormalizeBatchResult
    {
        /// <summary>批次号 → <c>yzh_queue.QueueCode</c></summary>
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>批次名（如「企业资料规范化 - 5 个文件」）</summary>
        public string QueueName { get; set; } = string.Empty;

        /// <summary><c>pending</c> / <c>running</c> / <c>completed</c> / <c>failed</c> / <c>cancelled</c></summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 是否已到终态 —— 前端据此<b>停止轮询</b>。
        ///     <para>⛔ 刻意不让前端自己按 <see cref="Status"/> 字符串猜：终态集合一旦扩展
        ///     （比如将来加 <c>timeout</c>），前端那份副本就会漏判 ⇒ 轮询永不停止。</para>
        /// </summary>
        public bool IsFinished { get; set; }

        public int Total { get; set; }
        public int Completed { get; set; }
        public int Failed { get; set; }
        public int Cancelled { get; set; }
        public int Processing { get; set; }
        public int Pending { get; set; }

        /// <summary>进度 0~100（<c>yzh_queue.Progress</c>，整数）</summary>
        public int Progress { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        /// <summary>
        ///     失败明细 —— ⛔ 不能只给 <see cref="Failed"/> 那个计数。
        ///     <para>只回「失败 2 个」等于把「哪个文件、为什么」的排查成本转嫁给用户。</para>
        /// </summary>
        public List<NormalizeBatchFailure> Failures { get; set; } = new();
    }

    /// <summary>批次内单条失败（含可操作归因）</summary>
    public sealed class NormalizeBatchFailure
    {
        /// <summary>标准域行 Code（= 队列任务的 <c>TaskId</c>）</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>文件名（查标准域行带出；行已不存在时为空串）</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>错误分类（队列执行器写入）</summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>错误详情（人话）</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>已重试次数</summary>
        public int RetryCount { get; set; }

        public DateTime? CompleteTime { get; set; }
    }

    /// <summary>取消批次入参 —— <c>cancel</c>（`54` §5.2 端点 6）</summary>
    public sealed class NormalizeCancelRequest
    {
        /// <summary>批次号 → <c>yzh_queue.QueueCode</c></summary>
        public string QueueCode { get; set; } = string.Empty;
    }

    /// <summary>取消批次结果</summary>
    public sealed class NormalizeCancelResult
    {
        /// <summary>本次被终止的任务数（= 取消时的 <c>Pending + Processing</c>）</summary>
        public int CancelledCount { get; set; }

        /// <summary>
        ///     ⚠️ 审计留痕写入失败时的提示。
        ///     <para>⛔ 非空即表示「<b>取消已生效，但审计链断了</b>」—— 页面必须显式提示，
        ///     ⛔ 不能吞掉：审计链断裂是必须让人知道的事，而谎报「取消失败」又是另一种错。</para>
        /// </summary>
        public string? AuditWarning { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  锁定 / 解锁（`54` §5.2 端点 7 / 8）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     锁定入参 —— <c>lock</c>（`54` §5.2 端点 7）。
    ///
    ///     <para><b>⚠️ 比 `41-03` §1.6 的入参表多一个 <c>EnterpriseCode</c>（必需）</b>：
    ///     那张表只列了 <c>{StandardFileCodes, IsFolderScope, FolderCode}</c>，但<b>锁落在哪一行</b>
    ///     决定了它必须知道企业 —— <c>IsLocked</c> 在<b>企业侧实例行</b>上
    ///     （同一个标准文件，企业 A 锁了不影响企业 B），而 <c>StandardFileCodes</c> 是<b>标准域行</b> Code，
    ///     本身不含企业信息 ⇒ 没有 <c>EnterpriseCode</c> 就无从定位要锁的那一行。</para>
    /// </summary>
    public sealed class NormalizeLockRequest
    {
        /// <summary>企业 Code（必填）</summary>
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（用于收窄范围；空 = 不限）</summary>
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 选中的<b>标准域行</b> Code 清单（<c>cert_standard_directory_file.Code</c>）。
        ///
        ///     <para>⚠️ 文件夹级锁定<b>由前端展开成文件清单后传入</b>，后端不再实现一遍文件夹展开 ——
        ///     前端已持有完整树数据，后端再算一次是重复实现（且两份算法必然漂移）。
        ///     与 <c>plan</c> / <c>run</c> 的入参口径一致。</para>
        /// </summary>
        public List<string> StandardFileCodes { get; set; } = new();
    }

    /// <summary>锁定结果</summary>
    public sealed class NormalizeLockResult
    {
        /// <summary>本次锁定成功数</summary>
        public int LockedCount { get; set; }

        /// <summary>
        ///     ★ 其中「<b>实例行原本不存在、本次新建</b>」的数量。
        ///
        ///     <para>为什么必须报出来：锁定一个<b>从未生成过</b>的文件，会在库里凭空多出一行实例行
        ///     （<c>InstanceState='none'</c> + 无产物）。这是<b>有意的</b>（否则 <c>plan</c> 的
        ///     <c>skip_locked</c> 判不出来），但对用户是「我明明只是点了个锁」⇒ 必须可见。</para>
        /// </summary>
        public int CreatedCount { get; set; }

        /// <summary>被跳过数（不在当前范围 / 写入失败）</summary>
        public int SkippedCount { get; set; }

        /// <summary>⚠️ 审计留痕写入失败时的提示（同 <see cref="NormalizeCancelResult.AuditWarning"/>）</summary>
        public string? AuditWarning { get; set; }
    }

    /// <summary>
    ///     解锁入参 —— <c>unlock</c>（`54` §5.2 端点 8）。
    ///
    ///     <para>⛔ <b><see cref="Reason"/> 必填</b>（审计要求，`54` §4.2）：
    ///     解锁是「放开一道保护」，必须留下<b>为什么放开</b> —— 否则事后无法回答
    ///     「这份文件为什么被改过」。</para>
    /// </summary>
    public sealed class NormalizeUnlockRequest
    {
        public string EnterpriseCode { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public List<string> StandardFileCodes { get; set; } = new();

        /// <summary>★ 解锁理由（<b>必填</b>，写入 <c>cert_doc_normalize_action.Reason</c>）</summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>解锁结果</summary>
    public sealed class NormalizeUnlockResult
    {
        /// <summary>本次解锁成功数</summary>
        public int UnlockedCount { get; set; }

        /// <summary>被跳过数（不在当前范围 / 本来就没锁 / 写入失败）</summary>
        public int SkippedCount { get; set; }

        /// <summary>⚠️ 审计留痕写入失败时的提示</summary>
        public string? AuditWarning { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  账本写 / 重写预告 / 候选（`54` §5.2 端点 11 / 12 / 13）
    //  —— 「单元格三交互」的后两个：改值·改来源（`value/override`）· 全部重写（`rewrite`）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     ★ <b>账本写请求</b> —— <c>value/override</c>（`54` §5.2 端点 11）。
    ///
    ///     <para><b>★ 一个端点覆盖两件事</b>（`55` §17.2 / §17.3）：
    ///     <see cref="Kind"/> = <c>value</c> 只改值 · <c>source</c> 只改来源（值不变）·
    ///     <c>both</c> 两者都改。<b>合并不是偷懒</b>：三者写的是<b>同一行账本</b>、
    ///     写的是同一批列、留同一条痕 —— 拆成三个端点只会让「谁先谁后」变成新问题。</para>
    ///
    ///     <para><b>⚠️ 比 `41-03` §1.6 的入参表多两个字段（有意的）</b>：
    ///     ① <see cref="SourceDetailJson"/> —— 少了它，「<b>从候选清单里换一个原始件</b>」
    ///     （`55` §17.2 方式①）就落不了库，而那是 L-b「字段 ↔ 文档」关联的唯一载体；
    ///     ② <see cref="SourceLabel"/> —— 来源标签是给人看的，换了来源不换标签会出现
    ///     「来源写着『企业基础信息』、实际取的是画像」这种自相矛盾的显示。</para>
    ///
    ///     <para><b>⚠️ <see cref="SourceKind"/> 是「改前的旧值」，不是「目标值」</b>：
    ///     目标值在 <see cref="NewSourceKind"/>。旧值可空 —— 传了则做一次
    ///     <b>乐观并发校验</b>（与库中不一致 ⇒ 拒绝，提示刷新），不传则不校验。</para>
    /// </summary>
    public sealed class NormalizeOverrideRequest
    {
        /// <summary>本次填充留痕 → <c>cert_doc_fill_log.Code</c>（定位是哪一批账本）</summary>
        public string FillLogCode { get; set; } = string.Empty;

        /// <summary>目标锚点 → <c>cert_doc_template_anchor.Code</c></summary>
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>干预类型 <c>value</c> / <c>source</c> / <c>both</c>（⛔ 其它值一律拒绝）</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        ///     ⚠️ <b>改前的来源类别</b>（旧值，可选）。传了即做乐观并发校验：
        ///     与库中不一致 ⇒ 说明用户看到的是旧状态 ⇒ 拒绝并提示刷新。
        /// </summary>
        public string? SourceKind { get; set; }

        /// <summary>★ <b>目标来源类别</b>（<see cref="Kind"/> ∈ {source, both} 时必填）</summary>
        public string? NewSourceKind { get; set; }

        /// <summary>来源人话标签（不传则按 <see cref="NewSourceKind"/> 兜底生成）</summary>
        public string? SourceLabel { get; set; }

        /// <summary>★ 完整来源链 JSON（换原始件时改 <c>originalFileCode</c>；⛔ 后端不解析、原样存）</summary>
        public string? SourceDetailJson { get; set; }

        /// <summary>
        ///     ★ <b>人工值</b>（<see cref="Kind"/> ∈ {value, both} 时必填 —— 但<b>允许空串</b>）。
        ///     <para>⚠️ <b>空串 ≠ 没传</b>：空串 = <b>显式清空</b>（用户就是要把它抹掉）
        ///     ⇒ 落 <c>FillStatus='removed'</c>，<b>⛔ 不计入完成率分子</b>。
        ///     <c>null</c> = 没给值 ⇒ 业务拒绝（⛔ 不猜用户想清空还是忘填）。</para>
        /// </summary>
        public string? ValueText { get; set; }

        /// <summary>★ 改的理由（<b>必填</b>，`55` §17.4：改值 / 换来源都要留痕为什么）</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        ///     ★ <b>钉住开关</b>（`55` §18.4 的 L3）。
        ///     <para><c>null</c> = <b>本次不改钉住状态</b>（⛔ 不要用 <c>false</c> 表达「不改」——
        ///     那会把用户没碰过的钉住状态静默解开）；<c>true</c>/<c>false</c> = 明确设/取消。</para>
        /// </summary>
        public bool? IsPin { get; set; }
    }

    /// <summary>
    ///     账本写结果 —— <c>value/override</c> 的返回。
    ///
    ///     <para><b>⚠️ 比 `41-03` 表里的 <c>{Code}</c> 多几个字段（有意的）</b>：
    ///     写完之后页面要<b>就地更新那一行</b>，而 <c>ValueDisplay</c> / <c>SourceLabel</c>
    ///     都是后端算出来的（前端自己拼会出现两套显示口径）。回传它们
    ///     ⛔ 不是为了省一次请求，是为了<b>不出现第二个显示口径</b>。</para>
    /// </summary>
    public sealed class NormalizeOverrideResult
    {
        /// <summary>被改写的账本行 → <c>cert_doc_fill_value.Code</c></summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>本次干预类型（回显 <c>value</c> / <c>source</c> / <c>both</c>）</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>落库后的展示值（截断 500，与列宽同口径）</summary>
        public string ValueDisplay { get; set; } = string.Empty;

        /// <summary>落库后的来源类别</summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>落库后的来源标签（人话）</summary>
        public string SourceLabel { get; set; } = string.Empty;

        /// <summary>落库后的钉住状态（<b>事实</b>，不是本次请求的回显）</summary>
        public bool IsPinned { get; set; }

        /// <summary>⚠️ 审计留痕写入失败时的提示（同 <see cref="NormalizeCancelResult.AuditWarning"/>）</summary>
        public string? AuditWarning { get; set; }
    }

    /// <summary>
    ///     ★ <b>「全部重写」请求</b> —— <c>rewrite</c>（`54` §5.2 端点 12）。
    ///
    ///     <para><b>★ 本端点是「预告」，⛔ 不入队</b>（判据来自契约本身：
    ///     `41-03` §1.6 把 <c>rewrite</c> 的出参写成 <c>NormalizePlan</c> ——
    ///     <b>没有 <c>QueueCode</c></b>；而真入队的 <c>run</c> 出参是
    ///     <see cref="NormalizeRunResult"/>，带批次号）。它回答的是
    ///     「<b>如果现在全部重写，会保留什么、会覆盖什么</b>」——
    ///     这正是 `41-02` §5.3 弹窗要显示的东西（「预览条：其中 5 个锚点已锁定，将保留」）。</para>
    ///
    ///     <para><b>★ <see cref="KeepManual"/> / <see cref="KeepPinned"/> 是真实消费的参数</b>：
    ///     它们改变 <see cref="NormalizePlanResult.WillKeepAnchorTotal"/> 的算法
    ///     （⛔ 不是「收了但没用」）。默认 <c>true</c> —— `41-02` §5.3 规则③ 明令
    ///     「<b>默认必须是保留</b>」，否则专家核对的 3 格会在下次全量规范化时被冲掉。</para>
    /// </summary>
    public sealed class NormalizeRewriteRequest
    {
        /// <summary>企业 Code（必填）</summary>
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（收窄范围；空 = 不限）</summary>
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        ///     范围级别 <c>enterprise</c> / <c>stage</c> / <c>standard</c> / <c>folder</c> / <c>file</c>
        ///     （`55` §3.2 五级）。默认 <c>file</c>。
        /// </summary>
        public string ScopeType { get; set; } = "file";

        /// <summary>
        ///     范围标识 —— 按 <see cref="ScopeType"/> 解释：
        ///     阶段 Code / 标准 Code / 文件夹 Code / 标准域行 Code。
        ///     <para>⚠️ <c>enterprise</c> 级忽略本字段（范围就是整个企业）。</para>
        /// </summary>
        public string? ScopeCode { get; set; }

        /// <summary>
        ///     ★ 显式文件清单（<b>优先级最高</b>）。
        ///     <para>与 <c>plan</c> / <c>run</c> / <c>lock</c> 同口径：<b>前端已持有完整树</b>
        ///     ⇒ 由前端展开成文件清单传入，后端 ⛔ 不再实现一遍文件夹递归
        ///     （两份算法必然漂移，且漂移时无人发现）。</para>
        /// </summary>
        public List<string> StandardFileCodes { get; set; } = new();

        /// <summary>★ 保留「人工改过的值」（规则③，默认 <c>true</c>）</summary>
        public bool KeepManual { get; set; } = true;

        /// <summary>★ 保留「钉住的锚点」（规则②，默认 <c>true</c>）</summary>
        public bool KeepPinned { get; set; } = true;
    }

    /// <summary>
    ///     ★ <b>重写预检：单份文件</b>。
    ///
    ///     <para>⚠️ 这里全是 <b>事实计数</b>（库里现在有多少行被钉住 / 被人工改过 / 是示例数据），
    ///     ⛔ 不是「已经保留了」的承诺 —— 承诺在执行期（编排器）兑现。</para>
    /// </summary>
    public sealed class NormalizeRewriteFilePrecheck
    {
        /// <summary>标准域行 Code</summary>
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>文件名（人话）</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>干跑判定（<c>fill</c> / <c>regenerate</c> / <c>skip_*</c>，同 <c>plan</c>）</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>企业侧实例是否已锁定</summary>
        public bool IsLocked { get; set; }

        /// <summary>作为基准的账本留痕（= 该文件<b>最近一次</b>填充）；空 = 从未规范化过</summary>
        public string? FillLogCode { get; set; }

        /// <summary>基准账本的时间（UTC，JSON 带 <c>Z</c>）</summary>
        public DateTime? FillLogTime { get; set; }

        /// <summary>基准账本的账本行数（分母）</summary>
        public int LedgerRowCount { get; set; }

        /// <summary>其中「钉住」的行数（事实）</summary>
        public int PinnedCount { get; set; }

        /// <summary>其中「人工改过值」的行数（事实，<c>OverrideKind ∈ {value, both}</c>）</summary>
        public int ManualCount { get; set; }

        /// <summary>其中「模板示例数据」的锚点数（事实，<b>规则④ 必清，⛔ 不可跳过</b>）</summary>
        public int SampleCount { get; set; }

        /// <summary>按开关算出的<b>将保留</b>行数</summary>
        public int WillKeepCount { get; set; }

        /// <summary>按开关算出的<b>将重新取值</b>行数</summary>
        public int WillRecomputeCount { get; set; }
    }

    // ════════════════════════════════════════════════════════════════════
    //  账本读（`54` §5.2 端点 9 / 10 / 17）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     ★ <b>取值账本</b> —— <c>values</c> 的返回（`54` §5.2 端点 9）。
    ///
    ///     <para><b>⚠️ 与 `41-03` 表里的 <c>FillValueDto[]</c> 的差异（有意的）</b>：
    ///     这里多包了一层<b>日志上下文</b>。原因：<c>FillLogCode</c> 是可选入参，
    ///     不传时后端要替用户<b>挑一条</b>（最近一次填充）—— 挑了哪条必须回传，
    ///     ⛔ 否则页面显示的是「某一次」的账本却不知道是哪一次，
    ///     用户核对「上次跑成什么样」时会拿到错的那一份。</para>
    ///
    ///     <para>同时，<b>空账本有两种完全不同的成因</b>，只有带上日志上下文才分得清：</para>
    ///     <list type="number">
    ///       <item><b>这份文件从来没规范化过</b> ⇒ <see cref="FillLogCode"/> 为空</item>
    ///       <item><b>跑过，但模板一个锚点都没有</b> ⇒ <see cref="FillLogCode"/> 有值、
    ///       <see cref="FillLogTotalAnchors"/> = 0（此时「空」是正常的，不是缺陷）</item>
    ///     </list>
    /// </summary>
    public sealed class NormalizeLedgerResult
    {
        /// <summary>
        ///     ★ 本次实际读取的填充留痕 Code。
        ///     <para>空 = 该企业侧<b>从未规范化过</b>这份文件（⛔ 不是错误，页面据此显示「尚未规范化」）。</para>
        /// </summary>
        public string FillLogCode { get; set; } = string.Empty;

        /// <summary>留痕状态 <c>success</c> / <c>partial</c> / <c>failed</c>（空 = 无留痕）</summary>
        public string FillLogStatus { get; set; } = string.Empty;

        /// <summary>留痕人话说明（如「已生成」「部分完成，有待办」）—— ⛔ 不是堆栈</summary>
        public string FillLogMessage { get; set; } = string.Empty;

        /// <summary>★ 留痕时间（<b>UTC，带 Z</b> —— 由 <c>BaseEntity.CreateTime</c> 写入）</summary>
        public DateTime? FillLogTime { get; set; }

        /// <summary>该次填充的锚点总数（分母快照）—— 0 且 <see cref="FillLogCode"/> 非空 ⇒ 空账本是正常的</summary>
        public int FillLogTotalAnchors { get; set; }

        /// <summary>该次填充的待办数</summary>
        public int FillLogPendingCount { get; set; }

        /// <summary>该次填充的产物路径（MinIO）</summary>
        public string? OutputPath { get; set; }

        /// <summary>本次返回的行数（= <see cref="Items"/>.Count，供前端判断「空」）</summary>
        public int Total { get; set; }

        /// <summary>账本逐行</summary>
        public List<FillValueDto> Items { get; set; } = new();
    }

    /// <summary>
    ///     ★ <b>账本一行</b>（`54` §5.2 端点 9 的 <c>FillValueDto</c>；端点 10 的
    ///     <see cref="FillValueDetailDto"/> 在其上继承扩展）。
    ///
    ///     <para><b>★ 字段来源分两处</b>（⛔ 不要以为都在账本表里）：</para>
    ///     <list type="bullet">
    ///       <item><b>账本行</b>（<c>cert_doc_fill_value</c>）：值 / 来源 / 可信度 / 状态 / 人工覆盖</item>
    ///       <item><b>锚点行</b>（<c>cert_doc_template_anchor</c>，<b>JOIN 带出</b>）：
    ///       <see cref="AnchorRef"/> / <see cref="FieldCode"/> / <see cref="SampleData"/> /
    ///       <see cref="Required"/> / <see cref="IsOrphan"/></item>
    ///     </list>
    ///     <para>⚠️ <b>为什么必须 JOIN</b>：账本表<b>不存</b>锚点原文与语义字段 ——
    ///     它们是<b>规则侧</b>的属性，存在锚点表里才是唯一真相源（⛔ 冗余进账本 = 两份会漂移）。</para>
    /// </summary>
    public class FillValueDto
    {
        /// <summary>账本行 Code（<c>cert_doc_fill_value.Code</c>）—— ★ 人工覆盖（<c>value/override</c>）的定位键</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>锚点 Code → <c>cert_doc_template_anchor.Code</c></summary>
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>★ 锚点原文（含花括号，如 <c>{{ENT_NAME}}</c>）—— 页面显示「这是哪个位置」</summary>
        public string AnchorRef { get; set; } = string.Empty;

        /// <summary><c>scalar</c>/<c>block</c>/<c>table</c>/<c>table_total</c>/<c>domain</c>（页面按此分组）</summary>
        public string AnchorType { get; set; } = string.Empty;

        /// <summary><c>token</c>/<c>bookmark</c>/<c>range</c></summary>
        public string AnchorKind { get; set; } = string.Empty;

        /// <summary>绑定的语义字段（<c>cert_doc_field_def.FieldCode</c>）</summary>
        public string FieldCode { get; set; } = string.Empty;

        /// <summary>
        ///     位置类别 <c>body</c>/<c>table_cell</c>/…
        ///     <para>⚠️ <b>实测恒为 <c>body</c></b>：编排器 <c>BuildLedgerRow</c> 硬编码，
        ///     ⛔ 不要据此判断「在正文」—— 真位置见 <see cref="LocationDesc"/>（当前同样为空）。</para>
        /// </summary>
        public string LocationKind { get; set; } = string.Empty;

        /// <summary>
        ///     可读位置（如「正文·第 12 段」/「Sheet1!B7」）。
        ///     <para>⚠️ <b>实测恒为空串</b> —— 编排器尚未把写入器回报的 <c>Location</c> 回填到账本
        ///     （已知缺口，属「只给计数不给位置」同族）。页面须容忍空串，⛔ 不要显示成空白单元格无提示。</para>
        /// </summary>
        public string LocationDesc { get; set; } = string.Empty;

        /// <summary><c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c>/<c>enum</c></summary>
        public string ValueType { get; set; } = string.Empty;

        /// <summary>
        ///     展示值（≤500 字符，<b>后端已格式化并截断</b>）。
        ///     <para>⚠️ <b>只用于显示</b>；要回传给「改值」必须用 <see cref="ValueText"/> ——
        ///     拿截断值回传会把长值<b>静默截短</b>（两次操作后值越来越短，且两边都不报错）。</para>
        /// </summary>
        public string ValueDisplay { get; set; } = string.Empty;

        /// <summary>★ 完整文本值（<c>text</c> 列，<b>不截断</b>）—— 编辑框初值与回传都用它</summary>
        public string ValueText { get; set; } = string.Empty;

        /// <summary><c>global</c>/<c>self</c>/<c>profile</c>/<c>compute</c>/<c>ai</c>/<c>manual</c></summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>人话来源标签（如「企业基础信息 · 企业全称」）</summary>
        public string SourceLabel { get; set; } = string.Empty;

        /// <summary>单元格级可信度 0.00~1.00（恒有值，⛔ 不是「未知」）</summary>
        public decimal Confidence { get; set; }

        /// <summary>可信度依据（1.00 时为空串）</summary>
        public string ConfidenceReason { get; set; } = string.Empty;

        /// <summary><c>filled</c>=已写入 / <c>pending</c>=待办 / <c>kept_as_is</c> / <c>removed</c></summary>
        public string FillStatus { get; set; } = string.Empty;

        /// <summary><c>replace</c>/<c>overwrite</c>/<c>append</c>/<c>remove</c></summary>
        public string WriteMode { get; set; } = string.Empty;

        /// <summary>替换前原文快照（<c>WriteMode=replace</c> 时必有）</summary>
        public string? OriginalText { get; set; }

        /// <summary>★ 值或来源是否被人工改过</summary>
        public bool IsOverridden { get; set; }

        /// <summary><c>value</c>=只改值 / <c>source</c>=只改来源 / <c>both</c>=两者都改</summary>
        public string OverrideKind { get; set; } = string.Empty;

        /// <summary>人工干预理由（审计要求）</summary>
        public string? OverrideReason { get; set; }

        /// <summary>干预人 Code</summary>
        public string? OverriddenBy { get; set; }

        /// <summary>干预时间</summary>
        public DateTime? OverriddenTime { get; set; }

        /// <summary>★ 是否钉住（「全部重写」时跳过本行）</summary>
        public bool IsPinned { get; set; }

        /// <summary>★ 模板里的示例数据（合规：填充前无条件清空）</summary>
        public bool SampleData { get; set; }

        /// <summary>是否必填锚点</summary>
        public bool Required { get; set; }

        /// <summary>锚点是否已因重传消失（<c>IsOrphan</c>）</summary>
        public bool IsOrphan { get; set; }

        /// <summary>
        ///     ★ <b>锚点行当前是否还存在</b>（<c>cert_doc_template_anchor</c> 里查得到）。
        ///     <para><c>false</c> = 规则已被删除 / 换版后消失 ⇒ 页面必须提示
        ///     「该位置的填写规则已失效」，⛔ 不能显示成一条正常记录。</para>
        /// </summary>
        public bool AnchorExists { get; set; }

        /// <summary>排序号（锚点侧 <c>Sort</c> 优先，取不到时用账本快照）</summary>
        public int Sort { get; set; }
    }

    /// <summary>
    ///     ★ <b>账本一行 · 详情</b>（`54` §5.2 端点 10）—— 审计抽屉 Tab2「数据来源」的数据源。
    ///
    ///     <para><b>★ 在 <see cref="FillValueDto"/> 之上增加</b>：证据原文 / 证据位置 /
    ///     证据链 5 级 / AI 候选 / 该锚点的动作时间线。</para>
    /// </summary>
    public sealed class FillValueDetailDto : FillValueDto
    {
        /// <summary>证据原文片段（一键可看，⛔ 不做跨文档双向定位）</summary>
        public string? EvidenceText { get; set; }

        /// <summary>证据位置提示（如 <c>P3</c> / 第2段 / 表2行3）</summary>
        public string? EvidencePageHint { get; set; }

        /// <summary>
        ///     ★ 完整来源链（5 级下钻）：
        ///     <c>{paramCode, profileCode, originalFileCode, fieldPath, pageHint, tableHint}</c>。
        ///     <para>⚠️ 是<b>原样回传的 JSON 字符串</b>（⛔ 后端不解析）—— 形状由写入端决定，
        ///     解析失败时前端显示兜底文案，⛔ 不报错。</para>
        /// </summary>
        public string? SourceDetailJson { get; set; }

        /// <summary>
        ///     ★ AI 候选建议池（<c>cert_doc_ai_suggestion</c>，同锚点 + 同企业 + 同标准文件）。
        ///     <para>⚠️ <b>本批只含 AI 建议</b>；「可用来源枚举」（画像字段 / 全局参数）由
        ///     端点 13 <c>candidates</c> 提供（第 4 批），两者<b>同形状</b>。</para>
        /// </summary>
        public List<SourceCandidateDto> Candidates { get; set; } = new();

        /// <summary>★ 该锚点（<c>AnchorCode</c>）的动作时间线 —— 只追加、倒序</summary>
        public List<NormalizeActionDto> Actions { get; set; } = new();
    }

    /// <summary>
    ///     ★ <b>候选来源一行</b>（`41-03` §四 `SourceCandidateDto`；端点 10 的 <c>Candidates[]</c>
    ///     与端点 13 <c>candidates</c> 共用）。
    ///
    ///     <para><b>★ 为什么不能只给「值」</b>：用户要判断「这条候选可不可信」，
    ///     需要看到<b>值 + 来源 + 证据 + 理由</b>四件套；只给一个值等于让他盲选。</para>
    /// </summary>
    public sealed class SourceCandidateDto
    {
        /// <summary>候选标识（AI 建议行 Code；来源枚举时可为空）</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>候选来源类型 <c>ai</c> / <c>profile</c> / <c>global</c> / …</summary>
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>人话来源标签（如「企业资料画像 · 营业执照」）</summary>
        public string SourceLabel { get; set; } = string.Empty;

        /// <summary>候选值（展示用）</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>候选值类型 <c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c></summary>
        public string ValueKind { get; set; } = "text";

        /// <summary>置信度 0~1（⛔ 无分时留 <c>null</c>，不要填 1.00 假装确定）</summary>
        public decimal? Confidence { get; set; }

        /// <summary>证据原文片段（含高亮标记）</summary>
        public string? Evidence { get; set; }

        /// <summary>来源位置（如「第 3 页 · 表 2 行 1」）</summary>
        public string? Location { get; set; }

        /// <summary>
        ///     ★ <b>这条候选来自哪一份企业原始资料</b> → <c>cert_enterprise_original_file.Code</c>。
        ///
        ///     <para><b>为什么必须有</b>：`55` §17.2 方式①「<b>从候选清单里换一个原始件</b>」
        ///     的落点就是 <c>SourceDetailJson.originalFileCode</c> —— 前端要把它写回去，
        ///     就必须先知道每条候选来自哪份文件。⛔ 只给「值 + 证据片段」时，
        ///     用户选完却<b>无法把这次选择落库</b>（L-b「字段 ↔ 文档」关联断链）。</para>
        /// </summary>
        public string? SourceDocCode { get; set; }

        /// <summary>★ 为什么给这条候选（AI 理由 / 规则说明）—— ⛔ 不能只给分数</summary>
        public string? Reason { get; set; }

        /// <summary>是否已被人工选定</summary>
        public bool IsPicked { get; set; }

        /// <summary>候选序号（同锚点内稳定排序）</summary>
        public int Index { get; set; }
    }

    /// <summary>
    ///     ★ <b>动作留痕一行</b>（`54` §5.2 端点 17）—— 审计抽屉 Tab3「历史留痕时间线」。
    ///
    ///     <para><b>★ 只追加、永不修改</b>（<c>cert_doc_normalize_action</c>）：
    ///     误操作只能再写一条<b>反向动作</b>，⛔ 不能删、不能改。</para>
    /// </summary>
    public sealed class NormalizeActionDto
    {
        /// <summary>留痕行 Code</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        ///     <c>lock</c> / <c>unlock</c> / <c>rewrite</c> / <c>pin</c> / <c>unpin</c> /
        ///     <c>batch_run</c> / <c>batch_cancel</c>（字典 <c>normalize_action</c>）
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>作用范围 <c>file</c>/<c>folder</c>/<c>stage</c>/<c>standard</c>/<c>enterprise</c></summary>
        public string ScopeType { get; set; } = string.Empty;

        /// <summary>范围标识（文件 / 文件夹 Code）</summary>
        public string ScopeCode { get; set; } = string.Empty;

        /// <summary>范围名称快照（如「4 记录文件」）</summary>
        public string ScopeName { get; set; } = string.Empty;

        /// <summary>直接目标（文件 Code）；批量时为空</summary>
        public string TargetCode { get; set; } = string.Empty;

        /// <summary>锚点 Code（改单元格来源 / 钉住时填）</summary>
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>变更前快照（JSON 字符串，⛔ 后端不解析）</summary>
        public string? BeforeJson { get; set; }

        /// <summary>变更后快照（JSON 字符串）</summary>
        public string? AfterJson { get; set; }

        /// <summary>★ 理由（<c>unlock</c> / <c>rewrite</c> / <c>override</c> 必填）</summary>
        public string? Reason { get; set; }

        /// <summary>操作人 Code</summary>
        public string CreateBy { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 操作人姓名（人话）。
        ///     <para>⛔ <b>不能只给 Code</b>：审计时间线是给人看的，
        ///     显示 <c>df6e03e9…</c> 等于让审核员自己去查表。查不到时回退为 Code 本身（⛔ 不留空）。</para>
        /// </summary>
        public string OperatorName { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 发生时间。
        ///     <para>⚠️ <c>BaseEntity.CreateTime</c> 是 <c>DateTime.UtcNow</c>（<b>UTC</b>），
        ///     而 SqlSugar 读回时 <c>Kind = Unspecified</c> ⇒ 不显式标 UTC 的话
        ///     JSON 里<b>没有 Z</b> ⇒ 前端 <c>new Date()</c> 按本地解析 ⇒ <b>少 8 小时</b>。
        ///     ⇒ 控制器已 <c>DateTime.SpecifyKind(…, Utc)</c>（同 <c>tree</c> 的 <c>LastFillTime</c>）。</para>
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>关联批次（<c>batch_run</c> / <c>batch_cancel</c> 时填）</summary>
        public string QueueCode { get; set; } = string.Empty;
    }

    /// <summary>
    ///     ★ <b>动作时间线</b> —— <c>actions</c> 的返回（`54` §5.2 端点 17）。
    ///
    ///     <para><b>⚠️ 与 `41-03` 表里的 <c>NormalizeActionDto[]</c> 的差异（有意的）</b>：
    ///     这里多包一层<b>截断信息</b>。<c>cert_doc_normalize_action</c> <b>只追加、永不删除</b>
    ///     ⇒ 时间线会无限增长 ⇒ 必须设上限；而<b>静默截断</b>会让用户以为「就这些了」，
    ///     正好破坏审计的用途（审计要的是「完整」或「明确知道不完整」）。</para>
    /// </summary>
    public sealed class NormalizeActionListResult
    {
        /// <summary>本次生效的条数上限</summary>
        public int Limit { get; set; }

        /// <summary>★ 是否还有更早的留痕未返回（页面必须显式提示）</summary>
        public bool Truncated { get; set; }

        /// <summary>本次返回条数</summary>
        public int Total { get; set; }

        /// <summary>留痕逐条（<b>倒序</b>：最近的在最前）</summary>
        public List<NormalizeActionDto> Items { get; set; } = new();
    }
}
