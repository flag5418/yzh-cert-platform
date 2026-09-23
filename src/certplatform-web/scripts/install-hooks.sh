#!/bin/bash
#
# 安装 git pre-commit 钩子（前端架构守卫）
#
# 背景：.git/hooks/ 不受版本控制，直接写进去无法随仓库分发。
#       本脚本把钩子内容固化在仓库内，一键安装到 .git/hooks/pre-commit。
#
# 用法：
#   bash src/certplatform-web/scripts/install-hooks.sh
#   或 npm run hooks:install   （在 src/certplatform-web 下）
#
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
GIT_DIR="$(git -C "$SCRIPT_DIR" rev-parse --git-dir 2>/dev/null)"
if [ -z "$GIT_DIR" ]; then
    echo "[ERROR] 未找到 git 仓库，请在仓库内执行" >&2
    exit 1
fi

# git rev-parse --git-dir 可能返回相对路径，转成绝对路径
case "$GIT_DIR" in
    /*) ;;
    *) GIT_DIR="$(cd "$SCRIPT_DIR" && cd "$GIT_DIR" && pwd)" ;;
esac

HOOKS_DIR="$GIT_DIR/hooks"
HOOK="$HOOKS_DIR/pre-commit"
mkdir -p "$HOOKS_DIR"

if [ -f "$HOOK" ] && ! grep -q "certplatform-web/scripts/guards.mjs" "$HOOK"; then
    BACKUP="$HOOK.bak.$(date +%Y%m%d%H%M%S)"
    cp "$HOOK" "$BACKUP"
    echo "[WARN] 已存在其他 pre-commit 钩子，已备份到: $BACKUP"
fi

cat > "$HOOK" << 'HOOK_EOF'
#!/bin/bash
#
# 由 src/certplatform-web/scripts/install-hooks.sh 生成 —— 请勿手改，改脚本后重装
#
# 前端架构守卫：违规即拒绝提交。
#   查看详情：node src/certplatform-web/scripts/guards.mjs
#   确需跳过：git commit --no-verify
#
REPO_ROOT="$(git rev-parse --show-toplevel)"
GUARD="$REPO_ROOT/src/certplatform-web/scripts/guards.mjs"
[ -f "$GUARD" ] || exit 0

# 解析 node：优先 PATH，回退到 WorkBuddy 托管版本
NODE_BIN="$(command -v node 2>/dev/null || true)"
if [ -z "$NODE_BIN" ]; then
    for cand in \
        "$HOME/.workbuddy-ai/binaries/node/versions/22.22.2-2/bin/node" \
        /opt/homebrew/bin/node \
        /usr/local/bin/node
    do
        [ -x "$cand" ] && { NODE_BIN="$cand"; break; }
    done
fi
[ -z "$NODE_BIN" ] && exit 0   # 无 node 时不阻断提交

if ! "$NODE_BIN" "$GUARD"; then
    echo ""
    echo "❌ 前端架构守卫未通过，提交已阻止。"
    echo "   修复后重试；确需跳过：git commit --no-verify"
    exit 1
fi
HOOK_EOF

chmod +x "$HOOK"
echo "[INFO] 已安装 pre-commit 钩子: $HOOK"
