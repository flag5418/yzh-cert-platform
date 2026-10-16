#!/usr/bin/env bash
# ============================================================
# scripts/backend/check-utcnow.sh
# ★ 铁律十守卫（只读）——「业务时间 = 本地时间（DateTime.Now）」
#
# 规则：⛔ 禁止用 DateTime.UtcNow / DateTimeOffset.UtcNow 写「落库/上屏」的时间列。
#       UtcNow 仅限内部机制（JWT / 缓存过期 / 超时 / 耗时 / ID / Unix 时间戳）。
#       详见 AGENTS.md「⑧ 业务时间唯一写法」。
#
# 用法：./scripts/backend/check-utcnow.sh        # 有违规 → 退出码 1
# ============================================================
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

# 白名单：确认为「内部机制」用途的文件（与 AGENTS.md 铁律十一致）
WHITELIST=(
  "YZH.Core.Stand/Helpers/JwtHelper.cs"                      # JWT exp/iat/nbf
  "YZH.Core.Api/Filters/YzhAuthFilter.cs"                    # JWT 过期比较
  "YZH.Core.Api/Filters/YzhAuditingFilter.cs"                # DurationMs 耗时
  "YZH.Core.Stand/Helpers/EntityConfigHelper.cs"             # 配置缓存过期
  "YZH.Core.Api/Services/PermissionCacheService.cs"          # 权限缓存过期
  "CertPlatform.Admin/Services/Workflow/PromptMarkdownCache.cs" # Redis 重连退避
  "YZH.Core.Stand/Helpers/IdGenerator.cs"                    # 雪花 ID 时间戳
  "YZH.Core.Api/Services/IAuditLogger.cs"                    # 日志文件名/行时间
  "YZH.Core.Api/Services/YzhAuditLogger.cs"                  # 审计 DTO Timestamp（内存）
  "YZH.Core.Api/Services/IYzhAuditLogger.cs"
  "YZH.Core.Api/Services/UserContext.cs"                     # JWT iat
  "YZH.Core.Stand/Interfaces/IUserContext.cs"                # JWT iat 默认值
  "YZH.Core.Stand/Models/Result/Result.cs"                   # 响应信封 Timestamp 元数据
  "YZH.Core.Api.Tests/CriticalPathSmokeTests.cs"             # 测试探针 key
)

hits="$(grep -rn -E "DateTime\.UtcNow|DateTimeOffset\.UtcNow" src/yzh-core src/certplatform-api 2>/dev/null \
        | grep -v "/bin/" | grep -v "/obj/" \
        | grep -vE ':[0-9]+:[[:space:]]*(//|///|\*)' || true)"

violations=""
count=0
while IFS= read -r line; do
  [ -z "$line" ] && continue
  allowed=0
  for w in "${WHITELIST[@]}"; do
    case "$line" in
      *"$w"*) allowed=1; break ;;
    esac
  done
  if [ "$allowed" -eq 0 ]; then
    violations="${violations}${line}"$'\n'
    count=$((count + 1))
  fi
done <<< "$hits"

if [ "$count" -eq 0 ]; then
  echo "✅ 铁律十通过：无白名单外的 DateTime.UtcNow 持久化写法"
  exit 0
fi

echo "❌ 铁律十违规：$count 处 DateTime.UtcNow（应为 DateTime.Now；若确为内部机制，请加入本脚本白名单并在 AGENTS.md 说明）"
echo "------------------------------------------------------------"
printf '%s' "$violations"
echo "------------------------------------------------------------"
exit 1
