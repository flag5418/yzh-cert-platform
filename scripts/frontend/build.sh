#!/bin/bash
# 前端构建脚本
# 用法: ./scripts/frontend/build.sh [admin|auditor|all]

set -e

PROJECT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
CERTPLATFORM_DIR="$PROJECT_DIR/src/certplatform-web"

# 构建前端
build() {
  local role=$1
  local dir="$CERTPLATFORM_DIR/$role"
  
  if [[ ! -d "$dir" ]]; then
    echo "错误: $role 目录不存在: $dir"
    exit 1
  fi
  
  echo "构建 $role 端..."
  cd "$dir"
  export PATH="/opt/homebrew/bin:$PATH"
  node node_modules/.bin/vite build
  echo "$role 端构建完成: $dir/dist/"
}

# 主入口
role=${1:-all}

case "$role" in
  admin)
    build "admin"
    ;;
  auditor)
    build "auditor"
    ;;
  all)
    build "admin"
    build "auditor"
    ;;
  *)
    echo "未知角色: $role"
    echo "用法: $0 [admin|auditor|all]"
    exit 1
    ;;
esac
