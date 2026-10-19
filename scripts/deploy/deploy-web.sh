#!/bin/bash
#
# deploy-web.sh — 一键：把最新前端源码打包成 Docker 镜像并发布，同时保活公网隧道
#
# 【作用】
#   1) 用 docker/web/Dockerfile 对前端源码做**生产构建**（admin / auditor），
#      打成 nginx 镜像并启动容器（宿主 127.0.0.1:10990 / 10991，不占用 dev 端口）；
#   2) 确保 Cloudflare 隧道存活：**隧道已在运行则不重启**（公网地址保持不变）。
#
# 【为什么这样就快】
#   外网走的是跨境链路（CF 美西边缘）。dev server 会按需请求 ~275 个源码模块，
#   生产构建只有 ~87 个可压缩/长缓存的产物文件 —— 首屏请求数量级下降。
#
# 【用法】
#   ./scripts/deploy/deploy-web.sh                # 打包并发布 admin + auditor，并保活隧道
#   ./scripts/deploy/deploy-web.sh admin          # 只处理 admin
#   ./scripts/deploy/deploy-web.sh auditor        # 只处理 auditor
#   ./scripts/deploy/deploy-web.sh --build-only   # 只构建镜像，不重启容器、不碰隧道
#   ./scripts/deploy/deploy-web.sh --tunnel-only   # 只确保隧道存活（不构建、不重启容器）
#   ./scripts/deploy/deploy-web.sh --no-tunnel     # 构建+发布，但不碰隧道
#
# 【前置】
#   · OrbStack / Docker 已运行；
#   · 后端已启动（scripts/backend/run-backend.sh），否则页面能开但 /api 会 502；
#   · 首次切换端口时隧道会重启一次（dev 9990 -> 容器 10990），之后地址保持不变。
#
# 【维护人】wangqingquan
# 【安全约束】只绑 127.0.0.1；不暴露后端 9992 与数据层；无任何数据库操作。
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
COMPOSE_FILE="$PROJECT_DIR/docker/web/compose.yml"
TUNNEL_SH="$PROJECT_DIR/scripts/tunnel/start-tunnel.sh"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1" >&2; }

# role -> compose 服务名 / 宿主端口
svc_of()  { case "$1" in admin) echo web-admin ;; auditor) echo web-auditor ;; *) echo "" ;; esac; }
port_of() { case "$1" in admin) echo 10990 ;; auditor) echo 10991 ;; *) echo "" ;; esac; }

# ---------------- 参数解析 ----------------
ROLES=()
DO_BUILD=1
DO_UP=1
DO_TUNNEL=1
for arg in "$@"; do
  case "$arg" in
    admin|auditor) ROLES+=("$arg") ;;
    --build-only)  DO_UP=0; DO_TUNNEL=0 ;;
    --tunnel-only) DO_BUILD=0; DO_UP=0 ;;
    --no-tunnel)   DO_TUNNEL=0 ;;
    -h|--help)     sed -n '2,30p' "$0"; exit 0 ;;
    *) error "未知参数: $arg（可选 admin|auditor|--build-only|--tunnel-only|--no-tunnel）"; exit 1 ;;
  esac
done
[ ${#ROLES[@]} -eq 0 ] && ROLES=(admin auditor)

# ---------------- 环境检查 ----------------
if ! docker info >/dev/null 2>&1; then
  error "Docker 未运行，请先启动 OrbStack"
  exit 1
fi
[ -f "$COMPOSE_FILE" ] || { error "缺少编排文件: $COMPOSE_FILE"; exit 1; }

# ---------------- 1. 构建镜像 ----------------
if [ "$DO_BUILD" -eq 1 ]; then
  for r in "${ROLES[@]}"; do
    svc="$(svc_of "$r")"
    info "构建镜像 $r（生产构建，首次或改动依赖较慢）..."
    docker compose -f "$COMPOSE_FILE" build "$svc"
  done
fi

# ---------------- 2. 启动容器 ----------------
if [ "$DO_UP" -eq 1 ]; then
  SERVICES=()
  for r in "${ROLES[@]}"; do SERVICES+=("$(svc_of "$r")"); done
  info "启动容器: ${SERVICES[*]} ..."
  docker compose -f "$COMPOSE_FILE" up -d "${SERVICES[@]}"

  for r in "${ROLES[@]}"; do
    port="$(port_of "$r")"
    ok=0
    for i in $(seq 1 30); do
      if lsof -nP -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
        info "$r 容器就绪: http://127.0.0.1:$port（耗时 ${i}s）"
        ok=1; break
      fi
      sleep 1
    done
    [ "$ok" -eq 0 ] && error "$r 容器 30s 内未监听 :$port，请查看: docker logs yzh-web-$r"
  done
fi

# ---------------- 3. 后端可达性体检（不阻塞）----------------
if ! lsof -nP -iTCP:9992 -sTCP:LISTEN -t >/dev/null 2>&1; then
  warn "后端 9992 未监听 —— 页面能开，但 /api 会 502。请先: $PROJECT_DIR/scripts/backend/run-backend.sh"
fi

# ---------------- 4. 保活隧道（不重启已存活隧道）----------------
if [ "$DO_TUNNEL" -eq 1 ]; then
  info "确保隧道存活（已运行且目标一致则不重启，地址不变）..."
  for r in "${ROLES[@]}"; do
    "$TUNNEL_SH" "$r" || warn "$r 隧道建立失败，详见 scripts/tunnel/run/${r}.log"
  done
fi

# ---------------- 5. 结果 ----------------
echo ""
echo "=========================================================="
echo "  发布完成"
echo "=========================================================="
for r in "${ROLES[@]}"; do
  port="$(port_of "$r")"
  case "$r" in
    admin)   label="管理端 cert-admin  " ;;
    auditor) label="审核端 cert-auditor" ;;
    *)       label="$r" ;;
  esac
  echo "  $label  本机 http://127.0.0.1:$port"
done
if [ -f "$PROJECT_DIR/scripts/tunnel/run/tunnel-urls.txt" ]; then
  echo "----------------------------------------------------------"
  echo "  公网地址（隧道不重启则保持不变）："
  grep -E '^\s*(admin|auditor)' "$PROJECT_DIR/scripts/tunnel/run/tunnel-urls.txt" | sed 's/^/    /' || true
fi
echo "  隧道状态: $PROJECT_DIR/scripts/tunnel/status-tunnel.sh"
echo "=========================================================="
echo ""
