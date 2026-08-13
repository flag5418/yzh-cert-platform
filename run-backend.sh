#!/bin/bash

# 标准目录管理后端编译和运行脚本
# 使用方法: 
#   ./run-backend.sh          # 编译并运行
#   ./run-backend.sh build    # 只编译不运行
#   ./run-backend.sh run      # 只运行不编译

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 项目路径
PROJECT_DIR="/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.api"
WEB_API_DIR="$PROJECT_DIR/VOL.WebApi"
PORT=9992

# 打印带颜色的消息
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 编译项目
build_project() {
    print_info "开始编译后端项目..."
    cd "$PROJECT_DIR"
    
    # 清理之前的编译结果
    print_info "清理旧的编译文件..."
    dotnet clean --nologo -v q 2>/dev/null
    
    # 编译项目
    print_info "编译中..."
    dotnet build --nologo
    
    if [ $? -eq 0 ]; then
        print_info "编译成功!"
        return 0
    else
        print_error "编译失败!"
        return 1
    fi
}

# 运行项目
run_project() {
    print_info "启动后端服务..."
    print_info "服务地址: http://localhost:$PORT"
    print_info "Swagger 地址: http://localhost:$PORT/swagger"
    print_info "按 Ctrl+C 停止服务"
    echo ""
    
    cd "$WEB_API_DIR"
    
    # 使用 dotnet run 启动项目
    # --no-build: 跳过编译（假设已经编译过）
    # --urls: 指定监听地址和端口
    dotnet run --no-build --urls "http://0.0.0.0:$PORT"
}

# 检查端口是否被占用
check_port() {
    if lsof -Pi :$PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
        print_warn "端口 $PORT 已被占用!"
        
        # 非交互模式（如使用 restart-backend.sh）
        if [ ! -t 0 ]; then
            print_info "终止占用端口 $PORT 的进程..."
            kill $(lsof -t -i:$PORT) 2>/dev/null
            sleep 1
            print_info "进程已终止"
            return 0
        fi
        
        # 交互模式
        echo "是否要终止占用该端口的进程? (y/n)"
        read -r response
        if [[ "$response" =~ ^[Yy]$ ]]; then
            print_info "终止占用端口 $PORT 的进程..."
            kill $(lsof -t -i:$PORT) 2>/dev/null
            sleep 1
            print_info "进程已终止"
        else
            print_error "请手动释放端口 $PORT 后重试"
            exit 1
        fi
    fi
}

# 主函数
main() {
    echo "========================================="
    echo "    标准目录管理后端服务"
    echo "========================================="
    echo ""
    
    # 检查项目目录是否存在
    if [ ! -d "$PROJECT_DIR" ]; then
        print_error "项目目录不存在: $PROJECT_DIR"
        exit 1
    fi
    
    # 检查端口
    check_port
    
    # 根据参数执行操作
    case "${1:-all}" in
        build)
            build_project
            ;;
        run)
            run_project
            ;;
        all|"")
            if build_project; then
                run_project
            fi
            ;;
        *)
            echo "用法: $0 [build|run|all]"
            echo "  build - 只编译不运行"
            echo "  run   - 只运行不编译"
            echo "  all   - 编译并运行 (默认)"
            exit 1
            ;;
    esac
}

# 捕获 Ctrl+C 信号
trap '\nprint_info "服务已停止"\nexit 0' INT

# 执行主函数
main "$@"
