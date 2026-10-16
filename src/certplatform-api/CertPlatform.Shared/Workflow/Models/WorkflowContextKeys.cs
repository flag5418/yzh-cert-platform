namespace CertPlatform.Shared.Workflow.Models
{
    /// <summary>
    /// ★ <b>工作流运行上下文键名 —— 唯一出处</b>（2026-10-15 新增）。
    ///
    /// <para><b>为什么必须有这个类</b>：<c>[FromContext("…")]</c> 的键名是<b>字符串</b>，
    /// 而「装配方」（<c>WfExecutionTaskService</c> / <c>WorkflowInterpreter</c>）与
    /// 「消费方」（各 skill 的 <c>[FromContext]</c>）分处两个程序集。
    /// 字符串写错<b>不报错</b>，只是静默拿到 <c>null</c> —— 本项目最典型的「配了不生效」。
    /// ⇒ 键名<b>只允许在这里定义一次</b>，两侧都引用常量（⛔ 禁止裸写字面量）。</para>
    ///
    /// <para><b>命名口径</b>：一律 <b>camelCase</b>，与 <c>WorkflowNodeConfig.Inputs</c> 的
    /// 端口名风格一致（既有 <c>contextParams</c> 就是 camelCase：
    /// <c>enterpriseCode</c> / <c>standardCode</c> / <c>phaseCode</c>）。</para>
    /// </summary>
    public static class WorkflowContextKeys
    {
        // ──────────── 归属四键（引擎自动装配，⛔ 画布上不出现）────────────

        /// <summary>
        /// <b>认证机构编码</b>（<c>cert_conclusion_rule.OrgCode</c> 等的租户隔离键）。
        /// <para><b>推导优先级</b>（由调用方装配，⛔ 不在 skill 里查库）：
        /// ① 规则 <c>cert_validation_rule.OrgCode</c> → ② 企业 <c>cert_enterprise.OrgCode</c>
        /// → ③ 专家端工作区 <c>CertBodyCode</c> → ④ 登录人 <c>Sys_User.OrgCode</c>
        /// → ⑤ 空串（= <b>平台标准级</b>，合法值，⛔ 不是缺失）。</para>
        /// </summary>
        public const string OrgCode = "orgCode";

        /// <summary>标准编码（如 <c>iso9001</c>）。空 = 全部标准通用规则。</summary>
        public const string StandardCode = "standardCode";

        /// <summary>审核阶段编码。空 = 全阶段通用规则。</summary>
        public const string PhaseCode = "phaseCode";

        /// <summary>企业编码（<c>cert_enterprise.Code</c>）。空 = 标准域 / 虚拟企业。</summary>
        public const string EnterpriseCode = "enterpriseCode";

        /// <summary>规则编码（<c>cert_validation_rule.Code</c>）。</summary>
        public const string RuleCode = "ruleCode";

        /// <summary>
        /// <b>本节点已连接的输入端口名集合</b>（<c>ISet&lt;string&gt;</c>）—— 引擎<b>逐节点</b>注入。
        ///
        /// <para><b>为什么必须单独给</b>：运行期「<b>端口没接</b>」与「<b>接了但上游返回空</b>」
        /// 在 <c>Inputs</c> 取值上<b>都是 <c>null</c></b>，⛔ 无法区分。
        /// 而 <c>stamp_judge_level</c> 的语义恰恰要求区分：</para>
        /// <list type="bullet">
        /// <item>两个端口<b>都没接</b> ⇒ <b>配置错</b>（<c>CONFIG_ERROR</c>）</item>
        /// <item>接了「证据」但<b>材料没拿到</b> ⇒ <b>不可判定</b>（<c>unverifiable</c>，✅ 合法）</item>
        /// </list>
        ///
        /// <para>判据 = <b>端口名是否出现在本集合里</b>（<c>NodeExecutor.ResolveInputs</c>
        /// 只为已声明的端口写 <c>resolved[portName]</c> ⇒ 未连的端口<b>键不存在</b>）。</para>
        ///
        /// <para>⚠️ <b>取不到时（<c>null</c>）skill 必须保守处理</b>：不因「判据缺失」而报错，
        /// 与 branch 裁剪的保守策略一致（拿不到判据不跳过）。</para>
        /// </summary>
        public const string ConnectedPorts = "__connectedPorts";

        // ──────────── 运行级登记池 ────────────

        /// <summary>
        /// <b>判定清单池</b> —— 与 <c>stamp_judge_level</c> 的
        /// <c>[Skill(RegistersTo = WorkflowContextKeys.JudgePool)]</c> 配对。
        ///
        /// <para>语义：该节点每成功执行一次，就把主输出（一条 judge 记录）追加进本池。
        /// 池的生命周期 = <b>一个 item</b>（一次检查项），跨路径共享。</para>
        ///
        /// <para><b>★ 为什么是「池」而不是「汇聚节点」</b>：引擎的跨路径复用机制使
        /// 「多条线拉到一个节点」的汇聚<b>只看得见第一条路径</b>；而登记是<b>节点成功那一刻的副作用</b>，
        /// 与「谁来汇总」无关 ⇒ 分支跳过不登记、跨路径复用不重复登记，天然正确。</para>
        ///
        /// <para>消费方：<c>nc_conclusion_v2</c> 用
        /// <c>[FromContext(WorkflowContextKeys.JudgePool)] List&lt;object&gt;</c> 一次取全。</para>
        /// </summary>
        public const string JudgePool = "judge";
    }
}
