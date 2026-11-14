using System;
using System.Text.Json;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 文档画像 → 提取模式路由。
    ///
    /// <para><b>唯一判据</b>：先读 <c>cert_enterprise_doc.DocProfileJson</c> 的 <c>ExtractionMode</c>；
    /// 旧画像没有该字段时，退回 <c>DocType</c> 中文类型；再没有才按文件名兜底。</para>
    ///
    /// <para>⛔ 本类只做路由，不判断文档是否与标准相关；相关性由画像字段
    /// <c>StandardRelevance</c> / <c>ExtractionMode=none</c> 决定。</para>
    /// </summary>
    public static class EnterpriseDocExtractionRouter
    {
        public const string ClauseFull = "clause_full";
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

        /// <summary>新提示词已覆盖的模式（手册逐条款仍走旧链，暂不在此列）。</summary>
        public static bool IsModePrompt(string? mode)
            => mode is ProcessProfile or RecordProfile or IndexOnly or DocumentControl
                or WorkInstructionCitation or PolicyProfile or ContextList or AuditProfile;

        public static bool IsSkip(string? mode) => mode == None;

        public static string Resolve(string? docProfileJson, string? fileName)
        {
            // ★ 文件控制/作业指导/制度/因素清单优先看文件名：旧画像常把“体系文件/文件索引”混在一起
            var fromName = FromFileName(fileName);
            if (fromName is DocumentControl or WorkInstructionCitation or PolicyProfile or ContextList)
                return fromName;

            var fromJson = ReadProfile(docProfileJson);
            if (!string.IsNullOrWhiteSpace(fromJson.Mode))
                return NormalizeMode(fromJson.Mode!, fromJson.DocType);

            var fromDocType = FromDocType(fromJson.DocType);
            if (!string.IsNullOrEmpty(fromDocType)) return fromDocType;

            return fromName ?? Legacy;
        }

        public static string PromptCodeForMode(string mode) => mode switch
        {
            ProcessProfile => "doc_extract_process_profile",
            RecordProfile => "doc_extract_record_profile",
            IndexOnly => "doc_extract_index",
            DocumentControl => "doc_extract_document_control",
            WorkInstructionCitation => "doc_extract_work_instruction_citation",
            PolicyProfile => "doc_extract_policy_profile",
            ContextList => "doc_extract_context_list",
            AuditProfile => "doc_extract_audit",
            _ => string.Empty,
        };

        /// <summary>
        /// 模式名归一：兼容新画像英文枚举、旧画像中文 `ExtractionMode`、以及直接取到的 `DocType`。
        /// </summary>
        public static string NormalizeMode(string mode, string? docType = null)
        {
            var m = (mode ?? string.Empty).Trim().ToLowerInvariant();
            switch (m)
            {
                case ClauseFull:
                case "逐条款":
                case "逐条":
                    return ClauseFull;
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
                default:
                    return FromDocType(docType) ?? FromDocType(mode) ?? Legacy;
            }
        }

        private static string? FromDocType(string? docType)
        {
            var t = (docType ?? string.Empty).Trim();
            if (t.Length == 0) return null;

            // 新画像英文枚举
            switch (t.ToLowerInvariant())
            {
                case "manual":
                    return ClauseFull;
                case "policy":
                    return PolicyProfile;
                case "procedure":
                case "work_instruction":
                    return WorkInstructionCitation;
                case "record":
                case "form":
                    return RecordProfile;
                case "audit_record":
                case "review_record":
                    return AuditProfile;
                case "index":
                    return IndexOnly;
                case "other":
                    return Legacy;
            }

            // 旧画像中文类型（EnterpriseDocProfileBuilder.GuessDocType）
            if (t.Contains("文件索引") || t.Contains("封面") || t.Contains("目录")
                || t.Contains("汇编") || t.Contains("清单")) return IndexOnly;
            if (t.Contains("审核") || t.Contains("评审")) return AuditProfile;
            if (t.Contains("手册") || t.Contains("方针") || t.Contains("目标")) return ClauseFull;
            if (t.Contains("作业指导") || t.Contains("操作规程") || t.Contains("指导书")) return WorkInstructionCitation;
            if (t.Contains("制度") || t.Contains("规范") || t.Contains("管理办法")) return PolicyProfile;
            if (t.Contains("程序")) return ProcessProfile;
            if (t.Contains("记录") || t.Contains("表单") || t.Contains("台账")
                || t.Contains("表格")) return RecordProfile;
            return null;
        }

        private static string? FromFileName(string? fileName)
        {
            var fn = (fileName ?? string.Empty).Trim();
            if (fn.Length == 0) return null;

            // 与 QMS 无直接关系的制度：直接跳过，不耗一次 LLM
            if (fn.Contains("财务") || fn.Contains("考勤") || fn.Contains("宿舍")
                || fn.Contains("报销") || fn.Contains("薪资") || fn.Contains("工资"))
                return None;

            // 文件控制类：封面/清单本身就是 7.5.3 证据，不能降级成普通索引
            if (fn.Contains("程序文件汇编") || fn.Contains("三阶文件汇编")
                || fn.Contains("受控文件清单") || fn.Contains("记录清单")
                || fn.Contains("外来文件清单") || fn.Contains("文件发放")
                || fn.Contains("文件回收") || fn.Contains("文件销毁")
                || fn.Contains("文件记录销毁")) return DocumentControl;

            // 组织环境/因素清单：支撑 4.1，不是 7.5.3 文件控制
            if ((fn.Contains("外部因素") || fn.Contains("内部因素")) && fn.Contains("清单"))
                return ContextList;
            if (fn.Contains("相关方") && fn.Contains("清单")) return ContextList;

            // 作业指导书/操作规程：报告只引用+关键步骤，不按过程链展开
            if (fn.Contains("作业指示书") || fn.Contains("作业指导书")
                || fn.Contains("操作规程") || fn.Contains("指导书")) return WorkInstructionCitation;

            // 制度/规范/管理办法：报告只取摘要、责任和关键规则
            if (fn.Contains("制度") || fn.Contains("规范") || fn.Contains("管理办法"))
                return PolicyProfile;

            if (fn.Contains("封面") || fn.Contains("目录") || fn.Contains("汇编")
                || fn.Contains("清单")) return IndexOnly;
            if (fn.Contains("程序")) return ProcessProfile;
            if (fn.Contains("内审") || fn.Contains("管理评审")) return AuditProfile;
            if (fn.Contains("手册") || fn.Contains("方针") || fn.Contains("目标")) return ClauseFull;
            if (fn.Contains("作业指导") || fn.Contains("操作规程") || fn.Contains("指导书")) return ProcessProfile;
            if (fn.Contains("记录") || fn.Contains("表单") || fn.Contains("台账")
                || fn.Contains("表格")) return RecordProfile;
            return null;
        }

        private static (string? Mode, string? DocType) ReadProfile(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return (null, null);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                return (Str(root, "ExtractionMode"), Str(root, "DocType"));
            }
            catch (JsonException)
            {
                return (null, null);
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
