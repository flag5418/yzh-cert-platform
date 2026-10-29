using System.Collections.Generic;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 企业文档模块的队列标识常量（36 号 §4.1 / §八）。
    ///
    /// <para><b>★ 与 <see cref="EnterpriseOriginalQueue"/> 是两套</b>：老表 <c>cert_enterprise_original_file</c>
    /// 的任务类型保留给历史队列读，新表 <c>cert_enterprise_doc</c> 用本类的新类型。
    /// ⛔ 不许复用老 TaskType —— 两个执行器都按 Code 去读各自的表，串用会读不到行。</para>
    ///
    /// <para>⚠️ 全仓没有集中的 TaskType 常量类，<c>IYzhTaskExecutor.TaskType</c> 是字符串属性，
    /// <c>QueueManager</c> 按字面建字典 ⇒ <b>漏注册不报错</b>，只在跑到该任务时抛
    /// <c>NotSupportedException</c>。因此集中在这里定义。</para>
    ///
    /// <para>长度检查：<c>enterprise_doc_convert</c> = 22 字符，&lt; <c>yzh_queue_task.TaskType VARCHAR(30)</c>、
    /// <c>yzh_queue.QueueType VARCHAR(30)</c> ⇒ ⛔ 无需改列宽。</para>
    /// </summary>
    public static class EnterpriseDocQueue
    {
        /// <summary>
        /// ★ 转换任务（当前唯一写入口）：格式归一 → PDF（预览）→ Markdown（原文/提取输入）。
        /// <para>一个文件 = 一个队列 = 一个任务；任务内两链串行，与老
        /// <c>enterprise_original_file</c> 的转换段同构，但<b>没有分析段</b> ——
        /// 语义提取改由下游 <c>enterprise_clause_extract</c>（文档 × 标准）承担。</para>
        /// </summary>
        public const string TaskTypeConvert = "enterprise_doc_convert";

        /// <summary>队列类型 = 任务类型（一任务一队列，框架按 QueueType 分派执行器）</summary>
        public const string QueueTypeConvert = TaskTypeConvert;

        /// <summary>资源锁表名 —— 本模块用自己的表，⛔ 不用框架的 <c>RESOURCE_FILE</c>（指向标准目录表）</summary>
        public const string ResourceTable = "cert_enterprise_doc";

        /// <summary>
        /// ★ <b>按标准提取条款</b>（P2）：读 Markdown → 按 <c>DocCheckMode ≠ none</c> 的条款清单
        /// 让模型摘录原文 → 落 <c>cert_document_extraction</c>。
        /// <para>粒度 = 文档 × 标准 = 一个任务（条款号体系不同，同一份文档要跑 N 次）。</para>
        /// <para>长度：<c>enterprise_clause_extract</c> = 25 字符 &lt; <c>VARCHAR(30)</c> ⇒ ⛔ 无需改列宽。</para>
        /// </summary>
        public const string TaskTypeExtract = "enterprise_clause_extract";

        /// <summary>提取队列类型</summary>
        public const string QueueTypeExtract = TaskTypeExtract;

        /// <summary>
        /// 提取的资源锁表名。
        /// <para>⚠️ <b>刻意与转换分开</b>：转换锁 <c>cert_enterprise_doc</c>（按文件），
        /// 提取锁 <c>cert_document_extraction</c>（按「文件 × 标准」）——
        /// 同一份文档的 N 个标准可以并行，且提取不会把排队中的转换挡住。</para>
        /// </summary>
        public const string ResourceTableExtract = "cert_document_extraction";
    }

    /// <summary>
    /// 转换任务载荷。
    /// <para>⚠️ 字段名 <c>Code</c> 与老载荷**逐字相同** ⇒ 队列明细的 <c>ExtractPayloadCode</c>
    /// 无需改动即可把任务挂到文件名上（AGENTS ③ 字段名 PascalCase 三处一致）。</para>
    /// </summary>
    public class EnterpriseDocConvertPayload
    {
        /// <summary>文件业务键 Code（⛔ 不用 Id）</summary>
        public string Code { get; set; } = "";

        public string EnterpriseCode { get; set; } = "";

        /// <summary>
        /// 追溯标识（<b>只作日志/业务引用，⛔ 不参与调度</b>）：
        /// 上传批次 Code（confirm）/ <c>regenerate</c>（重新生成）/ <c>restore:v{n}</c>。
        /// </summary>
        public string BatchCode { get; set; } = "";
    }

    /// <summary>
    /// ★ 提取任务载荷（文档 × 标准 = 一个任务）。
    ///
    /// <para><b>为什么一个标准一个任务</b>：条款号体系不同（iso9001 的 <c>7.5</c> ≠ iso13485 的 <c>4.2.4</c>），
    /// 一份 Markdown 对 N 个标准要跑 N 次；拆成 N 个任务 ⇒ 单标准可独立重跑、独立失败、独立留痕。</para>
    /// </summary>
    public class DocumentExtractPayload
    {
        /// <summary>文档 Code（<c>cert_enterprise_doc.Code</c>）</summary>
        public string FileCode { get; set; } = "";

        /// <summary>标准 Code（<c>cert_iso_standard.Code</c>）</summary>
        public string StandardCode { get; set; } = "";

        public string EnterpriseCode { get; set; } = "";

        /// <summary>触发来源：<c>batch:{上传批次}</c> / <c>regen</c> / <c>plan-change</c></summary>
        public string BatchCode { get; set; } = "";
    }
}
