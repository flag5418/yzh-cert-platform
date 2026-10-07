using System;
using System.Collections.Generic;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 企业原始资料模块的 DTO（36 号 §6.1 三层同构的一部分）。
    /// <para>⚠️ 字段名 <b>PascalCase 逐字一致</b>（AGENTS.md ③）—— 写错大小写 = 前端渲染空行且零报错。</para>
    /// </summary>
    public class PlanItemDto
    {
        /// <summary>文件原名（含扩展名）</summary>
        public string? FileName { get; set; }

        /// <summary>相对 <c>{Ent}/{Stage}/</c> 的文件夹路径（<c>YzhFolderUpload</c> 的 <c>webkitRelativePath</c> 去掉文件名）</summary>
        public string? RelFolderPath { get; set; }

        public long FileSize { get; set; }

        /// <summary>
        /// ★ 内容指纹（可选，64 位 hex）。前端算不了就留空 —— 服务端在 <c>upload/file</c> 收到字节后会
        /// 自行计算并以那个为准（D7 判定的权威依据永远是<b>实际字节</b>，不是前端声称的 hash）。
        /// </summary>
        public string? Sha256 { get; set; }
    }

    /// <summary>人工修正画像请求（D6 全量编辑）；未提交的字段沿用上一版</summary>
    public class CorrectProfileDto
    {
        /// <summary>★ 宿主文件业务键（⛔ 不用 Id，D9）</summary>
        public string? FileCode { get; set; }

        public string? EnterpriseCode { get; set; }

        /// <summary>
        /// ★ <b>M6（2026-10-06）</b>：要修正的是<b>哪个标准下</b>的画像行。
        ///
        /// <para>同一文件在 N 个标准下各有<b>一行</b>画像（选项 A 多行画像）⇒ 不指定标准时
        /// 「改标签/作用」只能改到 <c>FirstOrDefault</c> 那一行（= 文件级一份），
        /// <b>改不到用户真正在看的那个标准</b>（用户点名痛点：「手动要修改不同标准的分组和文档作用，
        /// 又有很多问题」）。</para>
        /// <para>空 = 兼容旧前端：退化为「该文件 <c>IsLatest</c> 中版本号最大的一行」。</para>
        /// </summary>
        public string? StandardCode { get; set; }

        /// <summary>受控标签多值 JSON 数组；值必须 ∈ <c>cert_tag_dict.TagCode</c></summary>
        public string? TagsJson { get; set; }

        public string? TagsReason { get; set; }

        public decimal? TagsConfidence { get; set; }

        /// <summary>作用四段式：【是什么】【审核关注点】【来源口径】【包含信息】</summary>
        public string? DocPurpose { get; set; }

        public decimal? DocPurposeConfidence { get; set; }

        /// <summary>结构化条目 <c>[{itemName,itemDesc,valueType,isKey}]</c></summary>
        public string? InfoItemsJson { get; set; }

        public string? FieldsJson { get; set; }

        public string? TablesJson { get; set; }

        /// <summary>可同时改策略（<c>analyze/skip/ignore</c>）</summary>
        public string? AnalyzePolicy { get; set; }

        public string? PolicyReason { get; set; }
    }

    /// <summary>
    /// 队列标识常量（36 号 §5.2）。
    /// <para>⚠️ <b>全仓没有集中的 TaskType 常量类</b>，<c>IYzhTaskExecutor.TaskType</c> 是字符串属性，
    /// <c>QueueManager</c> 按字面建字典 ⇒ 漏注册<b>启动不报错</b>，只在跑到该任务时抛
    /// <c>NotSupportedException</c>。因此这里集中定义，避免各处硬编码字面量拼错。</para>
    /// <para>三个值长度 26 / 27 / 24，均 &lt; <c>yzh_queue_task.TaskType VARCHAR(30)</c>、
    /// <c>yzh_queue.QueueType VARCHAR(30)</c>，⛔ 无需改列宽。</para>
    /// </summary>
    public static class EnterpriseOriginalQueue
    {
        /// <summary>入库任务：格式归一 + 双产物（PDF/Markdown）+ 链式入队 analyze（⚠️ 2026-10-06 起<b>停止写入</b>，仅读历史队列）</summary>
        public const string TaskTypeIngest = "enterprise_original_ingest";

        /// <summary>语义分析任务：<c>doc_group</c> + <c>doc_content</c> + 画像 upsert（⚠️ 2026-10-06 起<b>停止写入</b>，仅读历史队列）</summary>
        public const string TaskTypeAnalyze = "enterprise_original_analyze";

        /// <summary>
        /// ★ <b>文件级任务（2026-10-06 队列重构，当前唯一写入口）</b>：
        /// 一个文件 = 一个队列 = 一个任务，任务内<b>串行</b>跑「转 PDF → 转 Markdown → 逐标准 分组/作用 + 画像 upsert」。
        /// </summary>
        public const string TaskTypeFile = "enterprise_original_file";

        public const string QueueTypeIngest = TaskTypeIngest;
        public const string QueueTypeAnalyze = TaskTypeAnalyze;
        public const string QueueTypeFile = TaskTypeFile;

        /// <summary>批次的 TaskId 前缀（让 <c>CancelBatchAsync</c> 能按前缀整批取消）</summary>
        public static string BatchId(string taskId) => $"eob:{taskId}";
    }

    /// <summary>
    /// ★ <b>文件级处理队列载荷（2026-10-06 队列重构）</b>：一个文件一个任务，
    /// 执行器（<c>EnterpriseOriginalFileExecutor</c>）内**串行**跑完转换段 + 分析段。
    ///
    /// <para>★ 为什么从「批次双队列（ingest + analyze）」改成「文件单队列」：</para>
    /// <list type="number">
    ///   <item>框架 <c>GetNextPendingTaskAsync</c> 每秒领 1 个任务 fire-and-forget
    ///         ⇒ <b>队列内任务无顺序保证</b>，「整批转换完才补建 analyze」在并发下必然抢跑
    ///         （实测 analyze 先跑 ⇒ 3 份全部「Markdown 未就绪」）。</item>
    ///   <item>批次级忙碌闸门把「一个文件在转换」放大成「整个阶段拒绝上传 / 拒绝重跑」（实测误伤）。</item>
    ///   <item>「文件独立增删改」下批次语义永远凑不齐（一个文件重传，其余的不在同一队列里）。</item>
    /// </list>
    ///
    /// <para>⚠️ 字段名 <c>Code</c> 与 ingest 载荷**逐字相同** ⇒ 队列明细的 <c>ExtractPayloadCode</c>
    /// 无需改动即可把任务挂到文件名上（AGENTS ③ 字段名 PascalCase 三处一致）。</para>
    /// </summary>
    public class EnterpriseOriginalFilePayload
    {
        /// <summary>文件业务键 Code（⛔ 不用 Id —— 准则 A / D9）</summary>
        public string Code { get; set; } = "";

        public string EnterpriseCode { get; set; } = "";

        public string StageCode { get; set; } = "";

        /// <summary>
        /// 追溯标识（<b>只作日志/业务引用，⛔ 不参与调度</b>）：
        /// 上传批次 Code（confirm）/ <c>file:{Code}</c>（重新生成）/ <c>restore:v{n}</c> / <c>policy:analyze</c>。
        /// </summary>
        public string BatchCode { get; set; } = "";

        /// <summary>
        /// 是否跳过语义分析段（true = 只重转 Markdown，画像不动 —— 「重新生成」勾掉「重新识别」时用）。
        /// <para>★ 这是 <c>reanalyze=false</c> 的<b>真正开关</b>：旧批次链路里 ingest 收尾
        /// 无条件补建 analyze 队列，<c>reanalyze=false</c> 实际管不住（历史缺陷，随重构一并修）。</para>
        /// </summary>
        public bool SkipAnalyze { get; set; }

        /// <summary>
        /// 本文件要分析的<b>标准</b>（GUID 列表，入队时按「企业×阶段」关联解析出的快照）。
        /// <para>空 = 执行侧按「企业×阶段」自动解析（兼容旧入队方）。</para>
        /// </summary>
        public List<string> StandardCodes { get; set; } = new();
    }

    /// <summary>analyze 队列载荷（ingest 成功后链式入队，或页面「重跑分析」直接入队）</summary>
    public class EnterpriseOriginalAnalyzePayload
    {
        public string EnterpriseCode { get; set; } = "";
        public string StageCode { get; set; } = "";

        /// <summary>
        /// 批次标识：<c>batch:{上传批次Code}</c>（整批）或 <c>file:{文件Code}</c>（单文件重跑）。
        /// <para>★ 用于把「同一次上传的 N 个文件」聚成一次 <c>doc_group</c> 调用（33 号：它是<b>批次</b>提示词）。</para>
        /// </summary>
        public string BatchCode { get; set; } = "";

        /// <summary>本批待分析的文件业务键（⛔ 不用 Id）</summary>
        public List<string> FileCodes { get; set; } = new();

        /// <summary>
        /// ★ <b>M6（2026-10-06）</b>：本批要分析的<b>标准</b>（GUID 列表）。
        ///
        /// <para>一个企业 × 阶段可以关联<b>多个标准</b>（<c>cert_enterprise_stage</c>），而
        /// <c>doc_group</c> / <c>doc_content</c> 的提示词是<b>按标准绑定</b>的 ⇒ 同一份原始资料
        /// 在标准 A 与标准 B 下的「分组 / 文档作用」<b>本就不同</b>，必须<b>按标准各分析一次</b>。</para>
        ///
        /// <para>空 = 兼容旧入队方：执行器退化为「按企业×阶段自动解析出<b>所有</b>能解析到提示词的标准」。</para>
        /// </summary>
        public List<string> StandardCodes { get; set; } = new();
    }
}
