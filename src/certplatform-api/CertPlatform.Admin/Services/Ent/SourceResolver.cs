using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CertPlatform.Admin.Entities.Doc;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Office;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Ent
{
    /// <summary>
    /// 来源解析器 —— 负责解析 DocTemplateAnchor.SourceSpec 并执行取值逻辑。
    /// <para>遵循 22 号 §三 / §八 的取值来源模型。</para>
    /// </summary>
    public class SourceResolver
    {
        private readonly IDbOrm _db;

        public SourceResolver(IDbOrm db)
        {
            _db = db;
        }

        /// <summary>解析 SourceSpec JSON 字符串</summary>
        public SourceSpecModel? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<SourceSpecModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 尝试从 global 来源取值
        /// </summary>
        public async Task<(bool ok, FillValue? value)> TryResolveGlobalAsync(
            SourceSpecEntry entry, 
            DocTemplateAnchor anchor,
            CertPlatform.Shared.Fill.EnterpriseInfo entInfo,
            string? standardCode,
            string? stageCode,
            string? orgCode)
        {
            if (entry.Kind != "global" || string.IsNullOrEmpty(entry.Ref))
                return (false, null);

            // 复用 SrcGlobalParamSkill 的核心逻辑
            // 由于 SrcGlobalParamSkill 是静态类且高度耦合 SkillResult，我们这里直接调用 ParamValueResolver
            var defs = string.IsNullOrWhiteSpace(orgCode)
                ? (await _db.GetListAsync<CertPlatform.Shared.Entities.Cert.FillParamDef>(x =>
                        x.ParamCode == entry.Ref &&
                        (x.StandardCode == "" || x.StandardCode == standardCode) &&
                        (x.StageCode == "" || x.StageCode == stageCode))).Data
                : (await _db.GetListAsync<CertPlatform.Shared.Entities.Cert.FillParamDef>(x =>
                        x.ParamCode == entry.Ref &&
                        x.OrgCode == orgCode &&
                        (x.StandardCode == "" || x.StandardCode == standardCode) &&
                        (x.StageCode == "" || x.StageCode == stageCode))).Data;

            var def = ParamValueResolver.PickMostSpecific(defs ?? new List<CertPlatform.Shared.Entities.Cert.FillParamDef>()).FirstOrDefault();
            if (def == null) return (false, null);

            // TODO: 这里需要处理 saved_value，但在规范化执行引擎中，我们通常是“初次生成”
            // 如果有历史值，可以从 cert_fill_param_value 中读取
            var decision = ParamValueResolver.Resolve(def, entInfo, null, null, false);
            
            if (string.IsNullOrWhiteSpace(decision.Value)) return (false, null);

            var (createOk, val, _) = FillValueFactory.TryCreate(
                anchor.FieldCode ?? anchor.AnchorRef, 
                decision.Value, 
                anchor.ValueType, 
                anchor.NumberFormat);

            if (createOk && val != null)
            {
                val.Source = decision.SourceRef;
                val.Confidence = 1.0;
                return (true, val);
            }

            return (false, null);
        }

        /// <summary>
        /// 尝试从 manual 来源取值（从历史裁决或人工录入库中取）
        /// </summary>
        public async Task<(bool ok, FillValue? value)> TryResolveManualAsync(
            SourceSpecEntry entry,
            DocTemplateAnchor anchor,
            string enterpriseCode)
        {
            if (entry.Kind != "manual") return (false, null);

            // 1. 优先从 AI 建议裁决表中找（人工选定或改写的值）
            var suggestion = (await _db.GetOneAsync<DocAiSuggestion>(x => 
                x.EnterpriseCode == enterpriseCode && 
                x.AnchorCode == anchor.AnchorRef && 
                x.IsSelected == true &&
                x.IsValid == 1)).Data;

            if (suggestion != null)
            {
                var valStr = string.IsNullOrEmpty(suggestion.ManualValue) ? suggestion.SuggestedValue : suggestion.ManualValue;
                if (!string.IsNullOrEmpty(valStr))
                {
                    var (createOk, val, _) = FillValueFactory.TryCreate(
                        anchor.FieldCode ?? anchor.AnchorRef,
                        valStr,
                        anchor.ValueType,
                        anchor.NumberFormat);

                    if (createOk && val != null)
                    {
                        val.Source = "manual_suggestion";
                        val.Confidence = 1.0;
                        return (true, val);
                    }
                }
            }

            // 2. 备选：从全局参数值表里找（如果该锚点绑定了 FieldCode 且人工填过）
            if (!string.IsNullOrEmpty(anchor.FieldCode))
            {
                var paramValue = (await _db.GetOneAsync<CertPlatform.Shared.Entities.Cert.FillParamDef>(x => 
                    x.ParamCode == anchor.FieldCode && 
                    x.IsValid == 1)).Data; // 这里逻辑待完善，通常锚点直接对应来源规则
            }

            return (false, null);
        }
    }

    public class SourceSpecModel
    {
        public string Combine { get; set; } = "firstHit";
        public string? Separator { get; set; }
        public string? Expr { get; set; }
        public List<SourceSpecEntry> Sources { get; set; } = new();
    }

    public class SourceSpecEntry
    {
        public string Kind { get; set; } = string.Empty;
        public string? Ref { get; set; }
        public string? Field { get; set; }
        public double? MinConfidence { get; set; }
        public string? PromptGroup { get; set; }
        public string OnMissing { get; set; } = "next";
    }
}
