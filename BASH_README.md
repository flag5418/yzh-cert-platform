# 后端服务管理脚本

## 脚本说明

### 1. run-backend.sh - 主脚本
编译并运行后端服务。

**使用方法：**
```bash
./run-backend.sh          # 编译并运行（默认）
./run-backend.sh build    # 只编译不运行
./run-backend.sh run      # 只运行不编译（需要先编译过）
```

### 2. restart-backend.sh - 快速重启
停止已运行的服务，重新编译并启动。

**使用方法：**
```bash
./restart-backend.sh
```

### 3. stop-backend.sh - 停止服务
停止后端服务。

**使用方法：**
```bash
./stop-backend.sh
```

## 服务信息

- **服务端口**: 9991
- **服务地址**: http://localhost:9991
- **Swagger 地址**: http://localhost:9991/swagger

## 前置条件

确保已安装 .NET 8 SDK。可以通过以下命令检查：
```bash
dotnet --version
```

## 常见问题

### 端口被占用
如果端口 9991 被占用，脚本会提示是否终止占用进程。选择 `y` 即可。

### 编译失败
检查错误信息，通常是代码语法错误或缺少依赖。修复后重新运行脚本。

### 服务无法启动
1. 检查数据库连接是否正常
2. 检查配置文件 `appsettings.json`
3. 查看控制台输出的错误信息
