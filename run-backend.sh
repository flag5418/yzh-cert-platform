#!/bin/bash

# 标准目录管理后端编译和运行脚本（后台运行版）
# 使用方法:
#   ./run-backend.sh          # 编译并后台运行
#   ./run-backend.sh build    # 只编译不运行
#   ./run-backend.sh run      # 只运行不编译（需已编译过）
#   ./run-backend.sh status   # 查看运行状态

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# 项目路径
PROJECT_DIR="/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api"
WEB_API_DIR="$PROJECT_DIR/VOL.WebApi"
PORT=9992
LOG_FILE="/tmp/vol_backend_9992.log"
PID_FILE="/tmp/vol_backend_9992.pid"

print_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
print_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# 编译项目
build_project() {
    print_info "开始编译后端项目..."
    cd "$PROJECT_DIR"
    dotnet build --nologo
    if [ $? -eq 0 ]; then
        print_info "编译成功!"
        return 0
    else
        print_error "编译失败!"
        return 1
    fi
}

# 检查是否已在运行（按进程名）
is_running() {
    pgrep -f "VOL\.WebApi" >/dev/null 2>&1
}

# 后台运行项目
run_project() {
    # 已在运行则直接提示
    if is_running; then
        print_warn "后端已在运行（进程: $(pgrep -f 'VOL\.WebApi' | tr '\n' ' ')）"
        print_warn "如需重启请执行: ./restart-backend.sh"
        return 1
    fi

    # 端口被非本后端进程占用
    if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1 && ! is_running; then
        print_error "端口 $PORT 被其他进程占用: $(lsof -Pi :$PORT -sTCP:LISTEN -t | tr '\n' ' ')"
        print_error "请先处理占用进程后重试"
        return 1
    fi

    print_info "后台启动后端服务..."
    print_info "服务地址: http://localhost:$PORT"
    print_info "Swagger 地址: http://localhost:$PORT/swagger"
    print_info "日志文件: $LOG_FILE"
    print_info "PID 文件: $PID_FILE"

    cd "$WEB_API_DIR"

    # 关键：用 python os.setsid() 让进程脱离当前会话（等价于 Linux setsid）
    # 避免 run_terminal_command 等执行环境在命令返回后清理子进程把后端一起带走
    # （nohup 只防 SIGHUP，挡不住执行环境对子进程的清理）
    python3 -c "
import os, subprocess, sys
pid = os.fork()
if pid == 0:
    os.setsid()
    log = open('$LOG_FILE', 'w')
    p = subprocess.Popen(['dotnet', 'run', '--no-build', '--urls', 'http://0.0.0.0:$PORT'],
                         stdout=log, stderr=log, stdin=subprocess.DEVNULL)
    with open('$PID_FILE', 'w') as f:
        f.write(str(p.pid))
    os._exit(0)
"

    print_info "已启动，等待就绪..."

    # 等待端口就绪（最多 60 秒）
    for i in $(seq 1 60); do
        if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
            print_info "服务已就绪 (耗时 ${i}s): http://localhost:$PORT"
            return 0
        fi
        # 进程若已退出，提前报错
        if ! kill -0 $(cat $PID_FILE) 2>/dev/null; then
            print_error "进程异常退出，查看日志: $LOG_FILE"
            tail -20 "$LOG_FILE"
            return 1
        fi
        sleep 1
    done

    print_warn "等待 60s 仍未就绪，请查看日志: $LOG_FILE"
    return 1
}

# 查看状态
show_status() {
    if is_running; then
        print_info "后端运行中: PID $(pgrep -f 'VOL\.WebApi' | tr '\n' ' '), 端口 :$PORT"
    else
        print_info "后端未运行"
    fi
}

# 主函数
main() {
    echo "========================================="
    echo "    标准目录管理后端服务"
    echo "========================================="
    echo ""

    case "${1:-all}" in
        build)   build_project ;;
        run)     run_project ;;
        status)  show_status ;;
        all|"")
            if build_project; then
                run_project
            fi
            ;;
        *)
            echo "用法: $0 [build|run|all|status]"
            echo "  build  - 只编译不运行"
            echo "  run    - 只运行不编译（后台）"
            echo "  all    - 编译并后台运行 (默认)"
            echo "  status - 查看运行状态"
            exit 1
            ;;
    esac
}

main "$@"
