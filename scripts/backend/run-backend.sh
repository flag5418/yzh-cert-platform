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

# 项目路径（★ 自定位：禁止硬编码绝对路径，见 scripts/README.md §2 脚本职责铁律）
PROJECT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
WEB_API_DIR="$PROJECT_DIR/src/yzh-core/YZH.Core.Web"
SOLUTION_FILE="$PROJECT_DIR/CertPlatform.sln"
PORT=9992

# dotnet 解析：PATH 优先，其次 $HOME/.dotnet（本机 dotnet 常不在 PATH）
resolve_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
    elif [ -x "$HOME/.dotnet/dotnet" ]; then
        echo "$HOME/.dotnet/dotnet"
    else
        echo ""
    fi
}
DOTNET_BIN="${DOTNET_BIN:-$(resolve_dotnet)}"
if [ -z "$DOTNET_BIN" ]; then
    echo "[ERROR] 未找到 dotnet，请设置 DOTNET_BIN 或把 dotnet 加入 PATH"
    exit 1
fi
# ★ 运行日志落项目内 Logs/（2026-10-17 纠偏）：
#   原为 /tmp/vol_backend_9992.log 且以 'w' 截断打开 ⇒ ① 重启即清空历史 ② /tmp 会被系统清理
#   ⇒ 500 异常一旦发生且后端被重启排障，堆栈永远丢失（无法事后分析）。
#   现改为内容根 Logs/backend-YYYYMMDD.log + 追加写入：重启只追加、按天分文件，可长期留存分析。
#   口径：运行日志（排障用）≠ 审计日志（Logs/Audit/*.log + sys_log 表，追责用），两者互不替代。
LOG_DIR="$WEB_API_DIR/Logs"
LOG_FILE="$LOG_DIR/backend-$(date +%Y%m%d).log"
PID_FILE="/tmp/vol_backend_9992.pid"
mkdir -p "$LOG_DIR"

print_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
print_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
print_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ─────────────────────────────────────────────────────────────────────────────
# ★★ 必须把「本机地址」排除出代理，否则 MinIO 全线失败（2026-10-06 实测）
#
# 机理：.NET 在 Unix 上 `HttpClient.DefaultProxy` 会读 `HTTP_PROXY`/`HTTPS_PROXY`
#       环境变量；MinIO SDK 走 HttpClient ⇒ 本机 9000 的请求会被送去代理。
# 症状：预览 / 下载 / 转换产物读写**全部**失败，而错误信息是
#       「文件不存在」「源文件读取失败（对象不存在或存储不可用）」——
#       **看起来像数据缺失，实际是 `Connection refused (127.0.0.1:<代理端口>)`**。
#       （代理端口每次会话都不同，所以后端一旦继承了旧会话的代理端口，
#         那个端口早就没了 ⇒ 必然 connection refused。）
# 判据：日志里出现 `Minio.MinioClient.BucketExistsAsync` + `Connection refused (127.0.0.1:xxxxx)`
#       ⇒ 就是本条，⛔ 不要去查 MinIO 对象或数据库。
#
# ⚠️ 只**追加** NO_PROXY、⛔ 不要 unset 代理 —— 外网调用仍需要代理。
#    但「国内 AI 服务（DashScope/阿里云）」⛔ 不需要代理，且代理端口动态变化
#    ⇒ 一旦端口失效，语义分析会被静默拒绝（报「全部 N 个标准分析失败」）。
#    2026-10-14 实测：追加 dashscope.aliyuncs.com 后直连 378ms 成功。
# ─────────────────────────────────────────────────────────────────────────────
export NO_PROXY="127.0.0.1,localhost,::1,0.0.0.0,dashscope.aliyuncs.com${NO_PROXY:+,$NO_PROXY}"
export no_proxy="$NO_PROXY"

# 编译项目
build_project() {
    print_info "开始编译后端项目... (dotnet: $DOTNET_BIN)"
    cd "$PROJECT_DIR"
    "$DOTNET_BIN" build "$SOLUTION_FILE" --nologo
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
    pgrep -f "YZH\.Core\.Web" >/dev/null 2>&1
}

# 后台运行项目
run_project() {
    # 已在运行则直接提示
    if is_running; then
        print_warn "后端已在运行（进程: $(pgrep -f 'YZH\.Core\.Web' | tr '\n' ' ')）"
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
    # 必须显式指定项目：YZH.Core.Web 目录下存在多个 .csproj 时 dotnet run 会报 MSB1011
    python3 -c "
import os, subprocess, sys
pid = os.fork()
if pid == 0:
    os.setsid()
    log = open('$LOG_FILE', 'a')
    p = subprocess.Popen(['$DOTNET_BIN', 'run', '--project', 'YZH.Core.Web.csproj', '--no-build',
                          '--urls', 'http://0.0.0.0:$PORT'],
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
        print_info "后端运行中: PID $(pgrep -f 'YZH\.Core\.Web' | tr '\n' ' '), 端口 :$PORT"
        print_info "今日运行日志: $LOG_FILE"
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
