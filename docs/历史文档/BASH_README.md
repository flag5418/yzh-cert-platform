# 后端服务管理脚本

## 脚本说明

### 1. run-backend.sh - 主脚本
编译并**后台运行**后端服务（nohup，日志/PID 落盘，命令立即返回）。

**使用方法：**
```bash
./run-backend.sh              # 编译并后台运行（默认）
./run-backend.sh build        # 只编译不运行
./run-backend.sh run          # 只运行不编译（需要先编译过）
./run-backend.sh status       # 查看运行状态
```

### 2. restart-backend.sh - 快速重启（推荐）
停止已运行的服务（**按进程名过滤 dotnet/VOL.WebApi 直接关闭**），重新编译并后台启动。

**使用方法：**
```bash
./restart-backend.sh
```

### 3. stop-backend.sh - 停止服务
**按进程名过滤 dotnet / VOL.WebApi 相关进程直接关闭**（SIGTERM → SIGKILL），端口 9992 仅作兜底，不会误杀其他服务。

**使用方法：**
```bash
./stop-backend.sh
```

## 服务信息

- **服务端口**: 9992
- **服务地址**: http://localhost:9992
- **Swagger 地址**: http://localhost:9992/swagger
- **日志文件**: `/tmp/vol_backend_9992.log`
- **PID 文件**: `/tmp/vol_backend_9992.pid`

## 停止策略（重要）

停止后端**不依赖端口**，而是按进程名过滤：

```bash
# 主手段：按进程名过滤 VOL.WebApi / dotnet run 项目进程
pgrep -f "VOL\.WebApi|dotnet run.*VOL\.WebApi"
```

这样在「端口已释放但进程还在退出」「进程未监听端口」等场景也能可靠关闭；
只有进程名匹配不到时才用 `lsof -ti:9992` 兜底排查。

## 前置条件

确保已安装 .NET 8 SDK。可以通过以下命令检查：
```bash
dotnet --version
```

## 常见问题

### 端口被占用
如果端口 9992 被非后端进程占用，`run-backend.sh` 会报错并提示占用进程 PID，需先处理占用进程。

### 编译失败
检查错误信息，通常是代码语法错误或缺少依赖。修复后重新运行脚本。

### 服务无法启动
1. 检查数据库连接是否正常（MySQL 容器 `yzh-mysql`）
2. 检查配置文件 `appsettings.json`
3. 查看日志：`tail -50 /tmp/vol_backend_9992.log`
