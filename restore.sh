#!/bin/bash
set -e

PROJECT_DIR="/Volumes/Expand/wangqingquan/Documents/work/体系认证平台"

echo "=== 1. Git 记录恢复 ==="
cd "$PROJECT_DIR"
echo "当前分支: $(git branch --show-current)"
echo "Remote: $(git remote get-url origin)"
echo ""
echo "Git 状态:"
git status --short
echo ""

echo "=== 2. 重启 Docker 后端 ==="
cd "$PROJECT_DIR/docker"
echo "停止旧容器..."
bash stop.sh 2>/dev/null || docker compose down 2>/dev/null || true
echo ""
echo "启动 Docker..."
bash start.sh 2>/dev/null || docker compose up -d 2>/dev/null || true
echo ""
echo "Docker 状态:"
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null || echo "docker 未响应，请手动 'cd docker && bash start.sh'"
echo ""

echo "=== 3. 重启前端 ==="
cd "$PROJECT_DIR"
echo "清理旧 Vite 进程..."
lsof -ti :9990 2>/dev/null | xargs kill 2>/dev/null || true
lsof -ti :9991 2>/dev/null | xargs kill 2>/dev/null || true
lsof -ti :9993 2>/dev/null | xargs kill 2>/dev/null || true
sleep 1
echo "安装前端依赖..."
cd "$PROJECT_DIR/src/certplatform-web"
[ -d node_modules ] || npm install
echo ""
echo "启动前端..."
cd "$PROJECT_DIR"
bash scripts/frontend/start.sh all start
echo ""

echo "=== 4. 验证 ==="
sleep 3
echo ""
echo "服务状态:"
curl -s -o /dev/null -w "  前端 admin(9990): %{http_code}\n" http://127.0.0.1:9990/ 2>/dev/null || echo "  前端 admin: 无法连接"
curl -s -o /dev/null -w "  前端 auditor(9991): %{http_code}\n" http://127.0.0.1:9991/ 2>/dev/null || echo "  前端 auditor: 无法连接"
curl -s -o /dev/null -w "  前端 enterprise(9993): %{http_code}\n" http://127.0.0.1:9993/ 2>/dev/null || echo "  前端 enterprise: 无法连接"
curl -s -o /dev/null -w "  后端 (9992): %{http_code}\n" http://127.0.0.1:9992/ 2>/dev/null || echo "  后端: 无法连接"
echo ""
echo "=== 恢复完成 ==="
