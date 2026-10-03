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
    /// <para>两个值长度 26 / 28，均 &lt; <c>yzh_queue_task.TaskType VARCHAR(30)</c>，⛔ 无需改列宽。</para>
    /// </summary>
    public static class EnterpriseOriginalQueue
    {
        /// <summary>入库任务：格式归一 + 双产物（PDF/Markdown）+ 链式入队 analyze</summary>
        public const string TaskTypeIngest = "enterprise_original_ingest";

        /// <summary>语义分析任务：<c>doc_group</c>（按批次一次）+ <c>doc_content</c>（逐份）+ 画像 upsert</summary>
        public const string TaskTypeAnalyze = "enterprise_original_analyze";

        public const string QueueTypeIngest = TaskTypeIngest;
        public const string QueueTypeAnalyze = TaskTypeAnalyze;

        /// <summary>批次的 TaskId 前缀（让 <c>CancelBatchAsync</c> 能按前缀整批取消）</summary>
        public static string BatchId(string taskId) => $"eob:{taskId}";
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
    }
}
