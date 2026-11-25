using System;
using System.Text.Json;
using CertPlatform.Shared.DocExtraction;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 文档画像 → 提取模式路由。
    ///
    /// <para><b>唯一判据</b>：<c>cert_enterprise_doc.DocProfileJson.ExtractionMode</c>
    /// （V4 由结构指纹确定性产出，见 <c>DocFingerprintExtractor</c>）。
    /// 画像里没有模式 ⇒ <see cref="Unclassified"/>（待人工确认）。</para>
    ///
    /// <para>⛔ V4 起**不再**按 <c>DocType</c> 关键词或**文件名**兜底 —— 那是「猜」，
    /// 与「专家花钱买严谨」相悖（见 <c>docs/80-v4/17-文档分类：结构指纹与角色定位-V1.md</c> 五）。</para>
    ///
    /// <para>⛔ 本类只做路由，不判断文档是否与标准相关；相关性由
    /// <c>ExtractionMode=none</c> 决定。</para>
    /// </summary>
    public static class EnterpriseDocExtractionRouter
    {
        /// <summary>旧「逐条款」模式名（历史产物/画像里仍有该值；<see cref="NormalizeMode"/> 会归一为 <see cref="ManualChapter"/>）。</summary>
        public const string ClauseFull = "clause_full";

        /// <summary>
        /// ★ 手册章节模式：手册/方针类文档**整篇一次**提取，按章节组织成 JSON（章号/标题/摘要/要点），
        /// 条款号由手册目录（<c>ManualIndexParser</c>）确定性挂载 —— 彻底替代「手册逐条款」慢链。
        /// </summary>
        public const string ManualChapter = "manual_chapter";
        public const string ProcessProfile = "process_profile";
        public const string RecordProfile = "record_profile";
        public const string IndexOnly = "index_only";
        public const string DocumentControl = "document_control";
        public const string WorkInstructionCitation = "work_instruction_citation";
        public const string PolicyProfile = "policy_profile";
        public const string ContextList = "context_list";
        public const string AuditProfile = "audit_profile";
        public const string None = "none";
        public const string Legacy = "legacy";

        /// <summary>★ V4：判不出类型（未命中任何结构指纹）⇒ 待人工确认；⛔ 不按文件名/关键词猜。</summary>
        public const string Unclassified = "unclassified";

        /// <summary>新提示词已覆盖的模式（含手册章节模式；旧「手册逐条款」链已由 <see cref="ManualChapter"/> 取代）。</summary>
        public static bool IsModePrompt(string? mode)
            => mode is ManualChapter or ProcessProfile or RecordProfile or IndexOnly or DocumentControl
                or WorkInstructionCitation or PolicyProfile or ContextList or AuditProfile;

        /// <summary>是否跳过提取：<c>none</c>（无标准相关性）与 <c>unclassified</c>（待人工确认）都不跑模型。</summary>
        public static bool IsSkip(string? mode) => mode == None || mode == Unclassified;

        public static string Resolve(string? docProfileJson, string? fileName)
        {
            // ⛔ V4：分类唯一判据 = 画像里的 ExtractionMode（由结构指纹确定性产出）。
            //    fileName 仅为签名兼容保留 —— ⛔ 不再参与任何判断（文件名兜底已删除）。
            _ = fileName;

            var mode = ReadProfile(docProfileJson);
            if (string.IsNullOrWhiteSpace(mode))
                return Unclassified; // 旧画像无模式 ⇒ 待人工确认，⛔ 不猜

            return NormalizeMode(mode);
        }

        /// <summary>
        /// 模式 → 提示词 Code。★ 映射唯一事实源 = <see cref="DocExtractionPromptCatalog"/>
        /// （Admin 页面下拉与运行时共用，避免漂移）。
        /// </summary>
        public static string PromptCodeForMode(string mode)
            => DocExtractionPromptCatalog.PromptCodeForMode(mode);

        /// <summary>
        /// 模式名归一：兼容新画像英文枚举、旧画像中文 <c>ExtractionMode</c>。
        /// <para>⛔ V4：无法归一 ⇒ <see cref="Unclassified"/>（待人工确认）；⛔ 不再按 DocType 关键词猜。</para>
        /// </summary>
        public static string NormalizeMode(string mode)
        {
            var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
            switch (m)
            {
                case ClauseFull:
                case ManualChapter:
                case "手册章节":
                case "逐条款":
                case "逐条":
                    // ★ 2026-11-24：手册不再「逐条款」（慢且漏/串条款），统一归一为章节模式。
                    return ManualChapter;
                case ProcessProfile:
                case "过程画像":
                    return ProcessProfile;
                case RecordProfile:
                case "记录画像":
                    return RecordProfile;
                case IndexOnly:
                case "仅索引":
                case "index":
                    return IndexOnly;
                case DocumentControl:
                case "文件控制":
                case "文件清单":
                case "记录清单":
                case "受控文件清单":
                case "汇编封面":
                    return DocumentControl;
                case WorkInstructionCitation:
                case "作业指导书引用":
                case "作业指导":
                case "指导书":
                case "操作规程":
                    return WorkInstructionCitation;
                case PolicyProfile:
                case "制度画像":
                case "制度":
                case "规范":
                case "管理办法":
                    return PolicyProfile;
                case ContextList:
                case "因素清单":
                case "相关方清单":
                case "组织环境清单":
                    return ContextList;
                case AuditProfile:
                case "审核画像":
                    return AuditProfile;
                case None:
                case "irrelevant":
                case "不提取":
                    return None;
                case Legacy:
                    return Legacy;
                case Unclassified:
                    return Unclassified;
                default:
                    // ⛔ V4：模式字符串无法归一 ⇒ 待人工确认（⛔ 不按 DocType / 文件名兜底）。
                    return Unclassified;
            }
        }

        private static string? ReadProfile(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                return Str(root, "ExtractionMode");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Str(JsonElement obj, string name)
        {
            if (obj.ValueKind != JsonValueKind.Object) return null;
            if (!TryProp(obj, name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            var s = (v.GetString() ?? string.Empty).Trim();
            return s.Length == 0 ? null : s;
        }

        private static bool TryProp(JsonElement obj, string name, out JsonElement value)
        {
            value = default;
            if (obj.TryGetProperty(name, out value)) return true;
            foreach (var p in obj.EnumerateObject())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = p.Value;
                    return true;
                }
            }
            return false;
        }
    }
}
