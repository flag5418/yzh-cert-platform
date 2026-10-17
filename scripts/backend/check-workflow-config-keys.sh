#!/usr/bin/env bash
# ============================================================
# scripts/backend/check-workflow-config-keys.sh
# ★ 工作流跨边界命名统一守卫 G1 + G5 + G6（只读）
#
# 依据：docs/40-实施/开发计划/工作流跨边界命名统一方案-V1.md §2.2（边界 B / C）
#       + TODO 清单 P7.1 / P7.5 / P11.3
#
# 规则：
#   G1 —— ⛔ 禁止以小写 B 键读取 node.config（rule_json.config.*）。
#         允许的入口只有 WorkflowConfigAccess（GetConfigValue / TryGetConfigValue /
#         GetConfigString / GetConfig），且键名必须 PascalCase。
#         ⚠️ 不扫 Dictionary.TryGetValue("params")/["value"] 等：
#         那是 HTTP 请求体（边界 D）与运行期 payload 形状，本就不属 B 键（有意排除）。
#         ⚠️ 不扫 *.Tests（P2 契约测试**故意**用小写键证明大小写不敏感兜底，是 G5 的证据）。
#   G5 —— WorkflowConfigParser 必须保留 PropertyNameCaseInsensitive = true
#         （读取存量/混合大小写 JSON 的兜底；删掉会让旧数据静默变空图）。
#   G6 —— ⛔ 禁止小写上下文键（边界 C）字面量：contextParams 的键名一律 PascalCase
#         （2026-10-17 用户裁定；唯一出处 = WorkflowContextKeys，两侧只准引用常量）。
#
# 用法：./scripts/backend/check-workflow-config-keys.sh   # 有违规 → 退出码 1
# ============================================================
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

BKEYS='docType|ruleCode|fieldCode|tableCode|standardFileCode|promptTemplate|outputType|outputSchema|value|iterateMode|params|model|systemPrompt'
PATTERN="GetValueOrDefault\\([[:space:]]*\"(${BKEYS})\"[[:space:]]*\\)|(GetConfigValue|GetConfigString|TryGetConfigValue|GetConfig)\\(.*\"(${BKEYS})\""

# G6：小写上下文键字面量（边界 C）。键名唯一出处 = WorkflowContextKeys（值全 Pascal）。
CKKEYS='orgCode|standardCode|phaseCode|enterpriseCode|ruleCode|nodeTitle|judge|__connectedPorts'
CK_PATTERN="GetValueOrDefault\\([[:space:]]*\"(${CKKEYS})\"[[:space:]]*\\)|TryGetValue\\([[:space:]]*\"(${CKKEYS})\"[[:space:]]*,|ReadContextParam\\(.*\"(${CKKEYS})\"|FromContext\\([[:space:]]*\"(${CKKEYS})\""

hits="$(grep -rnE "$PATTERN" src/yzh-core src/certplatform-api 2>/dev/null \
        | grep -v "/bin/" | grep -v "/obj/" | grep -v "\.Tests/" \
        | grep -vE ':[0-9]+:[[:space:]]*(//|///|\*)' || true)"

ck_hits="$(grep -rnE "$CK_PATTERN" src/yzh-core src/certplatform-api 2>/dev/null \
        | grep -v "/bin/" | grep -v "/obj/" | grep -v "\.Tests/" \
        | grep -vE ':[0-9]+:[[:space:]]*(//|///|\*)' || true)"

violations=""
count=0
while IFS= read -r line; do
  [ -z "$line" ] && continue
  violations="${violations}${line}"$'\n'
  count=$((count + 1))
done <<< "$hits"

ck_violations=""
ck_count=0
while IFS= read -r line; do
  [ -z "$line" ] && continue
  ck_violations="${ck_violations}${line}"$'\n'
  ck_count=$((ck_count + 1))
done <<< "$ck_hits"

# G5：大小写不敏感兜底必须在位
PARSER="src/certplatform-api/CertPlatform.Admin/Services/Workflow/WorkflowConfigParser.cs"
g5_fail=0
if ! grep -q "PropertyNameCaseInsensitive[[:space:]]*=[[:space:]]*true" "$PARSER" 2>/dev/null; then
  g5_fail=1
fi

if [ "$count" -eq 0 ] && [ "$ck_count" -eq 0 ] && [ "$g5_fail" -eq 0 ]; then
  echo "✅ 工作流配置键通过：G1 无小写 config 读取 + G5 大小写兜底在位 + G6 无小写上下文键"
  exit 0
fi

if [ "$count" -ne 0 ]; then
  echo "❌ G1 违规：$count 处以小写 B 键读取 node.config（应为 PascalCase，见方案 §2.2 / 边界 B）"
  echo "------------------------------------------------------------"
  printf '%s' "$violations"
  echo "------------------------------------------------------------"
fi
if [ "$ck_count" -ne 0 ]; then
  echo "❌ G6 违规：$ck_count 处使用小写上下文键（边界 C 一律 PascalCase，唯一出处 WorkflowContextKeys）"
  echo "------------------------------------------------------------"
  printf '%s' "$ck_violations"
  echo "------------------------------------------------------------"
fi
if [ "$g5_fail" -ne 0 ]; then
  echo "❌ G5 违规：$PARSER 缺少 PropertyNameCaseInsensitive = true（存量 JSON 将静默反序列化为空图）"
fi
exit 1
