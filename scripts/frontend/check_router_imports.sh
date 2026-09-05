#!/usr/bin/env bash
# 校验 src/router/*.js 中所有 @/xxx 动态 import 路径是否能在 src/ 下解析
# 用途：开发模式下 Vite import-analysis 会在 transform 阶段验证路径，
#      任何 MISSING 都会导致 /src/router/index.js 返回 500。
# 用法：bash scripts/frontend/check_router_imports.sh
set -e

cd "$(dirname "$0")/../../src/server/Vue.NetCore/vol.web"

MISSING=0
grep -hoE "import\(['\"]@/[^'\"]+['\"]\)" src/router/*.js \
  | sed -E "s/import\(['\"]@\/([^'\"]+)['\"]\)/\1/" \
  | sort -u \
  | while read p; do
      found=0
      for ext in "" ".vue" ".js" ".ts" "/index.vue" "/index.js" "/index.ts"; do
        [ -e "src/${p}${ext}" ] && found=1 && break
      done
      if [ $found -eq 0 ]; then
        echo "MISSING: @$p"
      fi
    done

echo "---"
echo "校验完成。若上方无 MISSING 行则路由全部可解析。"
