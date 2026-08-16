# scripts/ — 全局与工具性脚本

> **版本**：V1.0 | **日期**：2026-08-16 | **状态**：成熟态
>
> **作用**：项目全局性/工具性脚本的统一存放处，按用途分子目录。功能性脚本（含测试）不存放于此，遵循「就近原则」随功能文档存放（见 §3 存放规范）。

---

## 1. 存放规范

**脚本分两级存储**：

| 级别 | 存放位置 | 适用脚本 | 示例 |
|------|---------|---------|------|
| 全局/工具性 | `scripts/` 按用途分子目录 | 后端服务管理、数据库、前端代码生成、MinIO 存储、通用工具、代码生成 | `scripts/backend/run-backend.sh` |
| 功能性（含测试） | 所属功能文档同目录 `test/` 子目录 | 与某个具体功能强绑定的测试/验证脚本 | `80-功能设计/01-系统管理/xxx功能/test/test_xxx.py` |

**禁止行为**：
- ❌ 向项目根目录散落脚本（历史教训：根目录曾散落 9 个脚本导致混乱）；
- ❌ 将功能性测试脚本放入 `scripts/`（应就近放功能目录 `test/`）。

**子目录职责**：

| 子目录 | 职责 |
|--------|------|
| `backend/` | 后端服务管理（编译/启动/重启/停止） |
| `db/` | 数据库脚本（SQL 清洗、迁移、PascalCase 转换等） |
| `frontend/` | 前端代码脚本（Vue 文件批量更新等） |
| `storage/` | MinIO/存储相关脚本 |
| `generate/` | 代码/实体生成脚本 |
| `tools/` | 通用工具脚本（目录检查等） |

---

## 2. 脚本作用表

### 2.1 backend/ — 后端服务管理

| 脚本 | 作用 | 用法 |
|------|------|------|
| `run-backend.sh` | 编译并后台运行后端（nohup，日志/PID 落盘，命令立即返回） | `./run-backend.sh`（默认）<br>`./run-backend.sh build`（只编译）<br>`./run-backend.sh run`（只运行）<br>`./run-backend.sh status`（查状态） |
| `restart-backend.sh` | 快速重启：按进程名停止 → 重新编译 → 后台启动 | `./restart-backend.sh` |
| `stop-backend.sh` | 停止服务：按进程名过滤 dotnet/VOL.WebApi 关闭（SIGTERM → SIGKILL），端口 9992 仅作兜底 | `./stop-backend.sh` |

**服务信息**：
- 服务端口：9992；服务地址：http://localhost:9992
- Swagger：http://localhost:9992/swagger
- 日志文件：`/tmp/vol_backend_9992.log`；PID 文件：`/tmp/vol_backend_9992.pid`

**停止策略（重要）**：停止后端不依赖端口，按进程名过滤：

```bash
pgrep -f "VOL\.WebApi|dotnet run.*VOL\.WebApi"
```

只有进程名匹配不到时才用 `lsof -ti:9992` 兜底排查。

**前置条件**：已安装 .NET 8 SDK（`dotnet --version` 验证）。

**常见问题**：
- 端口被占用：`run-backend.sh` 会报错并提示占用进程 PID，先处理占用进程；
- 编译失败：检查语法错误或缺失依赖，修复后重跑；
- 服务无法启动：① 检查 MySQL 容器 `yzh-mysql` 是否正常；② 检查 `appsettings.json`；③ 查看日志 `tail -50 /tmp/vol_backend_9992.log`。

### 2.2 db/ — 数据库脚本

| 脚本 | 作用 | 用法 |
|------|------|------|
| `clean_sql.py` | 清洗 SQL 文件（整理 CREATE TABLE 语句） | `python3 clean_sql.py <sql文件>` |
| `convert_sql_to_pascal.py` | SQL 字段名转 PascalCase | `python3 convert_sql_to_pascal.py <sql文件>` |
| `fix_sql_v2.py` | 批量修复 SQL（版本 2） | `python3 fix_sql_v2.py <sql文件>` |
| `fix_sql_v3.py` | 批量修复 SQL（版本 3，列名统一 PascalCase） | `python3 fix_sql_v3.py <sql文件>` |
| `run_db_migration.py` | 执行数据库迁移脚本 | `python3 run_db_migration.py` |

### 2.3 frontend/ — 前端代码脚本

| 脚本 | 作用 | 用法 |
|------|------|------|
| `update_cert_vue_files.py` | 批量更新 cert 相关 Vue 文件 | `python3 update_cert_vue_files.py` |
| `update_cert_vue_files_v2.py` | 批量更新 cert 相关 Vue 文件（版本 2） | `python3 update_cert_vue_files_v2.py` |
| `update_frontend_options.py` | 更新前端下拉选项配置 | `python3 update_frontend_options.py` |
| `wrap_vue_slots.py` | 处理 Vue 模板插槽包装 | `python3 wrap_vue_slots.py` |

### 2.4 storage/ — 存储脚本

| 脚本 | 作用 | 用法 |
|------|------|------|
| `clear_minio_and_db.py` | 清空 MinIO 与数据库关联数据（谨慎使用） | `python3 clear_minio_and_db.py` |
| `upload_files_to_minio.py` | 上传文件至 MinIO | `python3 upload_files_to_minio.py` |

### 2.5 generate/ — 代码生成

| 脚本 | 作用 | 用法 |
|------|------|------|
| `generate_entities.sh` | 生成后端实体类（bash） | `./generate_entities.sh` |
| `generate_entities.ps1` | 生成后端实体类（PowerShell，Windows 用） | `powershell -File generate_entities.ps1` |
| `check-coding-standards.sh` | 检查代码规范符合性 | `./check-coding-standards.sh` |

### 2.6 tools/ — 通用工具

| 脚本 | 作用 | 用法 |
|------|------|------|
| `check_tree.py` | 输出项目目录树/检查目录结构 | `python3 check_tree.py` |

---

## 3. 编写规范

1. **命名**：`.sh` 用 kebab-case（如 `run-backend.sh`）；`.py` 用 snake_case（如 `run_db_migration.py`）；
2. **自定位路径**：脚本必须使用 `SCRIPT_DIR` / `PROJECT_DIR` 自定位，禁止依赖调用时 cwd；
3. **头部注释**：每个脚本头部必须写注释：作用 / 用法 / 依赖 / 维护人；
4. **同步更新**：新增或修改脚本必须同步更新本 README 脚本作用表；
5. **版本后缀**：`V2`/`V3` 后缀仅在确有必要（同功能多版本并存）时使用，语义化；
6. **就近原则**：功能性测试脚本不放入 scripts/，就近放功能目录 `test/` 子目录，命名 `test_功能名.sh/py`。

---

## 4. 历史

| 日期 | 变更内容 |
|------|---------|
| 2026-08-16 | 创建：根目录 9 个散落脚本归位（3 个 backend + 1 个 tools），scripts/ 原 14 个脚本按 db/frontend/storage/generate 分类归位；并入原根目录 BASH_README.md 内容（backend 章节）；删除 5 个 test_ai_* 一次性脚本（详见 80-功能设计/README.md 变更记录） |
