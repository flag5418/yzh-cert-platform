#!/bin/bash

# 停止后端服务
# 核心策略：按进程名过滤 dotnet / VOL.WebApi 相关进程直接关闭（SIGTERM → SIGKILL），
#           端口 9992 仅作兜底（防止进程名匹配不到的僵尸监听）。
# 用法: ./stop-backend.sh

PORT=9992
APP_NAME="VOL.WebApi"

print_info() { echo -e "\033[0;32m[INFO]\033[0m $1"; }
print_warn() { echo -e "\033[1;33m[WARN]\033[0m $1"; }
print_error() { echo -e "\033[0;31m[ERROR]\033[0m $1"; }

echo "========================================="
echo "    停止后端服务 ($APP_NAME @ :$PORT)"
echo "========================================="

# 1) 按进程名过滤（主手段）：匹配 VOL.WebApi 可执行 / dotnet run 项目进程
#    注意 pgrep -f 会匹配完整命令行，脚本自身不含 APP_NAME，无自匹配风险
PIDS=$(pgrep -f "VOL\.WebApi|dotnet run.*VOL\.WebApi" 2>/dev/null | grep -v "^$$\$" | tr '\n' ' ')

    # 1.1) PID 文件兜底：run-backend.sh 用 os.setsid 启动时，dotnet run 父进程命令行
    #      可能不含 VOL.WebApi（--urls 方式），从 PID 文件补齐父进程一起关闭
    if [ -f /tmp/vol_backend_9992.pid ]; then
        PIDFILE_PID=$(cat /tmp/vol_backend_9992.pid 2>/dev/null)
        if [ -n "$PIDFILE_PID" ] && kill -0 $PIDFILE_PID 2>/dev/null; then
            PIDS="$PIDS $PIDFILE_PID"
        fi
    fi
    PIDS=$(echo $PIDS | tr ' ' '\n' | sort -u | tr '\n' ' ')

if [ -n "$PIDS" ]; then
    print_info "发现后端进程: $PIDS"
    print_info "发送 SIGTERM..."
    kill $PIDS 2>/dev/null
    sleep 2

    # 检查是否还有残留（SIGTERM 未生效则升级 SIGKILL）
    REMAIN=$(pgrep -f "VOL\.WebApi|dotnet run.*VOL\.WebApi" 2>/dev/null | grep -v "^$$\$" | tr '\n' ' ')
    if [ -n "$REMAIN" ]; then
        print_warn "SIGTERM 未完全退出，强制 SIGKILL: $REMAIN"
        kill -9 $REMAIN 2>/dev/null
        sleep 1
    fi
    print_info "进程已全部关闭"
else
    print_info "未发现运行中的后端进程（进程名匹配）"
fi

# 2) 端口兜底：若 9992 仍被监听，可能是进程名匹配不到的残留，提示但不误杀其他服务
if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    print_warn "端口 $PORT 仍被占用: $(lsof -Pi :$PORT -sTCP:LISTEN -t | tr '\n' ' ')"
    print_warn "若确认是后端残留，可执行: kill -9 \$(lsof -t -i:$PORT)"
else
    print_info "端口 $PORT 已释放"
fi

# 3) 清理 PID 文件
rm -f /tmp/vol_backend_9992.pid

print_info "停止完成"
