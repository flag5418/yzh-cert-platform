#!/bin/bash
# 前端启停脚本
# 用法: ./scripts/frontend/start.sh [admin|auditor|all] [start|stop|restart|status]
#
# 示例:
#   ./scripts/frontend/start.sh admin start    # 启动管理员端
#   ./scripts/frontend/start.sh auditor stop   # 停止审核员端
#   ./scripts/frontend/start.sh all status     # 检查所有前端状态

set -e

PROJECT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
CERTPLATFORM_DIR="$PROJECT_DIR/src/certplatform-web"

# 端口映射（兼容 bash 3.2，不使用关联数组）
get_port() {
  case "$1" in
    admin)      echo 9990 ;;
    auditor)    echo 9991 ;;
    enterprise) echo 9993 ;;
    *)          echo "" ;;
  esac
}

# 启动前端
start() {
  local role=$1
  local port=$(get_port "$role")
  local dir="$CERTPLATFORM_DIR/cert/cert-$role"
  
  if [[ ! -d "$dir" ]]; then
    echo "错误: $role 目录不存在: $dir"
    exit 1
  fi
  
  if [[ ! -f "$dir/package.json" ]]; then
    echo "错误: $role package.json 不存在: $dir"
    exit 1
  fi
  
  echo "启动 $role 端 (端口 $port)..."
  cd "$dir"
  export PATH="/opt/homebrew/bin:$PATH"
  nohup npm run dev > "/tmp/vite_${role}_${port}.log" 2>&1 &
  echo "启动中，日志: /tmp/vite_${role}_${port}.log"
  
  # 等待端口就绪（最多 30 秒）
  for i in $(seq 1 30); do
    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1; then
      echo "$role 端已就绪 (耗时 ${i}s): http://127.0.0.1:$port"
      return 0
    fi
    sleep 1
  done
  echo "启动超时，请查看日志: /tmp/vite_${role}_${port}.log"
}

# 停止前端
stop() {
  local role=$1
  local port=$(get_port "$role")
  
  local pid
  pid=$(lsof -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || echo "")
  if [[ -z "$pid" ]]; then
    echo "$role 端未运行"
    return
  fi
  
  echo "停止 $role 端 (PID: $pid)..."
  kill "$pid" 2>/dev/null || true
  sleep 1
}

# 重启前端
restart() {
  local role=$1
  stop "$role"
  sleep 1
  start "$role"
}

# 检查状态
status() {
  local role=$1
  local port=$(get_port "$role")
  
  local pid
  pid=$(lsof -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || echo "")
  if [[ -n "$pid" ]]; then
    echo "$role 端运行中 (PID: $pid, 端口: $port)"
    echo "访问地址: http://127.0.0.1:$port"
  else
    echo "$role 端未运行"
  fi
}

# 主入口
role=${1:-all}
action=${2:-start}

case "$role" in
  admin)
    case "$action" in
      start)   start "admin" ;;
      stop)    stop "admin" ;;
      restart) restart "admin" ;;
      status)  status "admin" ;;
      *)       echo "未知操作: $action"; exit 1 ;;
    esac
    ;;
  auditor)
    case "$action" in
      start)   start "auditor" ;;
      stop)    stop "auditor" ;;
      restart) restart "auditor" ;;
      status)  status "auditor" ;;
      *)       echo "未知操作: $action"; exit 1 ;;
    esac
    ;;
  enterprise)
    case "$action" in
      start)   start "enterprise" ;;
      stop)    stop "enterprise" ;;
      restart) restart "enterprise" ;;
      status)  status "enterprise" ;;
      *)       echo "未知操作: $action"; exit 1 ;;
    esac
    ;;
  all)
    case "$action" in
      start)
        start "admin" &
        start "auditor" &
        start "enterprise" &
        wait
        ;;
      stop)
        stop "admin"
        stop "auditor"
        stop "enterprise"
        ;;
      restart)
        stop "admin"
        stop "auditor"
        stop "enterprise"
        sleep 1
        start "admin" &
        start "auditor" &
        start "enterprise" &
        wait
        ;;
      status)
        status "admin"
        status "auditor"
        status "enterprise"
        ;;
      *)
        echo "未知操作: $action"; exit 1 ;;
    esac
    ;;
  *)
    echo "未知角色: $role"
    echo "用法: $0 [admin|auditor|enterprise|all] [start|stop|restart|status]"
    exit 1
    ;;
esac
