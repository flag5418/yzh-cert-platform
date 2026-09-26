# scripts/ — 脚本职责与存放规范

> **版本**：V2.0 | **日期**：2026-09-24 | **状态**：living（活文档，须与脚本同步）
>
> **作用**：定义全部脚本的**职责边界**、存放位置与编写规范。
> **V2.0 变更**：新增 §0「脚本职责铁律」（用户 2026-09-24 明确要求）；修正 §2 作用表为实测现状；新增 §4 `db/` 历史脚本分类。

---

## 0. ★ 脚本职责铁律（最高优先级）

> 动机（用户原话）：「**sh 只是一个简单的 netcore 的启动命令，不允许它还单独执行数据库相关的操作**」。
> 脚本是**编排层**，不是**逻辑层**。编排层一旦承载逻辑，就会变成不可维护、不可审计的黑盒。

| # | 铁律 | 违反症状 | 机器可查 |
|---|------|---------|---------|
| **B1** | **单一职责**：一个脚本只做一件事。文件名必须能说清它做什么；做不到就拆。 | 脚本越长越没人敢改；`rebuild_db.sh` 曾同时干 DROP/CREATE/DDL/seed/回填/校验 6 件事 | 人工评审 |
| **B2** | **启动脚本只启动**：`backend/`、`frontend/`、`docker/` 下的启停脚本**不得出现任何数据库操作**（连接、DDL、DML、读写校验）。 | 启动失败原因被数据库错误掩盖；重建/改数据被误当成"启动的一部分" | `grep -E 'mysql\|DELETE FROM\|ALTER TABLE' scripts/backend/*.sh` 须为 0 |
| **B3** | **SQL 一律外置**：`.sh` 内**禁止内嵌 SQL**（含 heredoc 与 `mysql -e "SELECT ..."`）。DDL/DML 放 `.sql` 文件，脚本只负责"按序喂进去"；需传参时用 `sed` 替换 `.sql` 里的 `__PLACEHOLDER__`，**不要拼 SQL 字符串**。 | 同一段 SQL 在 `.sh` 和 `.sql` 里各存一份 → **两处不同步**（`rebuild_db.sh` 与 `post_fix.sql` 曾完全重复） | `grep -nE '(SELECT\|DELETE FROM\|INSERT INTO\|ALTER TABLE\|CREATE TABLE\|DROP (DATABASE\|TABLE))' scripts/**/*.sh` 须为 0（会话级 `SET NAMES` 用 `--init-command` 表达，**不算**内嵌 SQL） |
| **B4** | **只读与写分离**：校验脚本放 `db/verify/`（只读，可随时跑）；写操作放 `db/fix/`、`db/fixture/`（显式调用，需确认）。 | 校验脚本顺手改了数据；"看一眼"变成"改一下" | 目录归属评审 |
| **B5** | **自定位路径**：用 `SCRIPT_DIR` / `PROJECT_DIR` 推导，**禁止硬编码绝对路径**。 | 换机器/换目录即失效；仓库路径写死在脚本里 | `grep -n '/Volumes/\|/Users/' scripts/**/*.sh` |
| **B6** | **危险操作必须自锁**：会删数据的脚本必须校验目标（前缀/白名单），不符即拒绝执行。 | 误删业务数据 | 见 `db/fixture/cleanup_dict_test_data.sh` 的 `__T__` 前缀校验 |
| **B7** | **失败要响**：校验类步骤失败必须 `exit 1`，**不允许只打印警告后继续**。 | 「重建成功」但数据不合规 → 静默回退（本项目已踩） | 人工评审 |

### 编排 vs 逻辑：一条判据

```
脚本可以做的：判断前置条件、按序调用、传参、检查退出码、打印结果
脚本不可以做的：内联 SQL、内联大段业务逻辑、把"启动"和"改数据"混在一起
```

### 启动流程的三段独立（互不越界）

```
① docker/start.sh     仅启动依赖容器（MySQL/Redis/MinIO/LibreOffice/anydoc）
② scripts/backend/*   仅编译 + 启停 .NET Core 后端   ← ⛔ 不得碰数据库
③ scripts/frontend/*  仅启停 Vite 前端
```

数据库的建表/迁移/修正/重建是**独立流程**，由 `scripts/db/` 下脚本**显式调用**，永不隐含在 ①②③ 中。

---

## 1. 存放规范

**脚本分两级存储**：

| 级别 | 存放位置 | 适用脚本 |
|------|---------|---------|
| 全局/工具性 | `scripts/` 按用途分子目录 | 后端服务管理、数据库、前端、存储、代码生成、通用工具 |
| 功能性（含测试） | 所属功能目录 `test/` 子目录 | 与某个具体功能强绑定的测试/验证脚本 |

**禁止行为**：
- ❌ 向项目根目录散落脚本（历史教训：根目录曾散落 9 个脚本）；
- ❌ 把功能性测试脚本放进 `scripts/`（应就近放功能目录 `test/`）。

**子目录职责**：

| 子目录 | 职责 | 允许的副作用 |
|--------|------|-------------|
| `backend/` | 后端服务管理（编译/启动/重启/停止/冒烟） | ⛔ 无数据库操作 |
| `frontend/` | 前端启停/构建/路由检查/引用审计 | ⛔ 无数据库操作 |
| `db/verify/` | ★ **只读**校验（命名规范、软删除语义等） | ⛔ 只读 |
| `db/fix/` | 一次性修正脚本（命名修正等） | 写，需显式调用 |
| `db/fixture/` | 测试夹具的建/清（带自锁） | 写，需显式调用 |
| `db/`（根） | 迁移 SQL、一次性 DDL/DML（历史，见 §4） | 写 |
| `generate/` | 代码/实体生成 | 无 |
| `storage/` | MinIO/存储相关 | 无 |
| `tools/` | 通用工具（目录树、冻结检查、文档迁移） | 无 |
| `docker/`（仓库根） | 依赖容器启停/状态 | ⛔ 无数据库操作 |

---

## 2. 脚本作用表（实测，2026-09-24）

### 2.1 backend/ — 后端服务管理（⛔ 零数据库操作）

| 脚本 | 作用 | 用法 |
|------|------|------|
| `run-backend.sh` | 编译 + 后台运行后端（setsid 脱离会话，日志/PID 落盘） | `./run-backend.sh`｜`build`｜`run`｜`status` |
| `restart-backend.sh` | 停止 → 重新编译 → 后台启动 | `./restart-backend.sh` |
| `stop-backend.sh` | 按进程名过滤 `YZH.Core.Web` 关闭（SIGTERM → SIGKILL），端口 9992 仅兜底 | `./stop-backend.sh` |
| `smoke_test.sh` | 冒烟检查（curl 探后端/前端/路由） | `./smoke_test.sh` |
| `test-dictionary-api.sh` | 字典模块接口端到端测试（写临时数据，**调用 `db/verify` + `db/fixture`** 做核对与清理） | `./test-dictionary-api.sh` |
| `test-doc-extraction-api.sh` | 文档提取接口端到端测试 | `./test-doc-extraction-api.sh` |

**服务信息**：端口 9992｜http://localhost:9992｜Swagger `/swagger`｜日志 `/tmp/vol_backend_9992.log`｜PID `/tmp/vol_backend_9992.pid`

**dotnet 解析**：`run-backend.sh` 按 `PATH` → `$HOME/.dotnet/dotnet` 顺序查找；可用环境变量 `DOTNET_BIN` 覆盖。

**停止策略**：不依赖端口，按进程名过滤 `pgrep -f "YZH\.Core\.Web"`；匹配不到才用 `lsof -ti:9992` 兜底。

### 2.2 db/ — 数据库脚本

> ★ **库的获取方式（2026-09-25 定）**：本地 / 测试环境一律用**正式库备份 + 恢复**，**不再提供「从零重建」脚本**。
> 原 `db/rebuild/`（`rebuild_db.sh` + `schema_before.sql` 等 7 文件）**已删除** —— 其 schema 快照是旧版本，
> 每次重建会把历史命名修正**冲回 snake_case**（「浪费一周」的根因），且该编排无实际使用者。
> 备份 / 恢复命令见 `docs/20-体系认证/.../数据库运维` 或直接 `docker exec yzh-mysql mysqldump / mysql < backup.sql`。

| 脚本 | 作用 | 用法 |
|------|------|------|
| `verify/verify_naming.sql` | ★ **只读**命名规范校验（铁律七 + 铁律九） | `docker exec -i yzh-mysql mysql ... < verify/verify_naming.sql` |
| `verify/count_baseline.sql` | ★ **只读**重建后基础计数（表/视图/Code 空值/菜单/接口） | `mysql ... < verify/count_baseline.sql` |
| `verify/export_menu_urls.sql` + `sync_menu_urls.sh` | ★ **只读**导出菜单路由契约快照 → `menu-urls.tsv`（供前端守卫 **R12** 比对） | `./verify/sync_menu_urls.sh` |
| `verify/verify_dict_softdelete.sh` + `.sql` | ★ **只读**字典软删除语义核对 | `./verify_dict_softdelete.sh <DictCode> <ItemCode>` |
| `fixture/cleanup_dict_test_data.sh` + `.sql` | 测试夹具清理（物理删除 `__T__` 前缀行，带自锁） | `./cleanup_dict_test_data.sh <DictCode> <ItemCode>` |
| `fix/fix-column-naming-2026-09-24.sql` | 一次性命名修正（86 列改名 + 26 表删 `enable` + `sys_api` 迁移 + 遗留表处置） | 见文件头；**执行前先备份** |
| `verify/verify-enterprise-org-tree.sh` + `.sql` | ★ **只读**专家端「企业信息」机构树一致性诊断（7 段，含「挂人」门禁提示） | `./verify/verify-enterprise-org-tree.sh` |
| `verify/enterprise-org-gate.sql` | ★ **只读**修复前置门禁（**单行 8 计数**，供修复脚本机器判定 + 复核） | 由 `fix/fix-enterprise-org-tree-*.sh` 调用 |
| `fix/fix-enterprise-org-tree-2026-09-26.sh` + `.sql` | 一次性收敛企业机构树（L3「企业信息」文件夹 + L4 企业节点、软删幽灵/重复节点）｜★ **B6 四道自锁**：库名白名单 / 需 `--apply` / 门禁拒绝「有人员挂在企业节点上」/ 自动备份到 `db/backup/` | `./fix/fix-enterprise-org-tree-2026-09-26.sh`（演练）｜`… --apply`（执行） |
| ⚠️ 以上 3 个 `.sh` 为 2026-09-26 新建，**需 `chmod +x`**；未加执行位时用 `bash <路径>` 调用 | | |
| `verify/verify_phase_migration.sql` | ★ **只读**认证阶段定义迁移验证查询 | 由 `run_phase_migration.sh` 调用 |
| `run_phase_migration.sh` | 认证阶段定义一次性迁移（**已执行完毕**，保留备查） | `./run_phase_migration.sh` |

**连接方式**：`docker exec -i yzh-mysql mysql -uroot -p*** --default-character-set=utf8mb4 yzh_cert_platform`
**密码**：`docker/.env` → `Yzh123456.`

### 2.3 frontend/ — 前端脚本

| 脚本 | 作用 | 用法 |
|------|------|------|
| `start.sh` | 启动/停止/重启/查看前端服务 | `./start.sh admin start`｜`auditor stop`｜`all status` |
| `build.sh` | 构建前端（admin/auditor/all） | `./build.sh all` |
| `check_router_imports.sh` | 检查路由导入 | `./check_router_imports.sh` |
| `ref-audit.mjs` | 引用审计（找出引用不存在符号的死代码） | `node ref-audit.mjs` |

**各端 dev 端口**：admin 9990｜auditor 9991｜enterprise 9993。

### 2.4 generate/ / storage/ / tools/

| 脚本 | 作用 |
|------|------|
| `generate/generate_entities.sh` `.ps1` | 生成后端实体类（历史脚本，供参考） |
| `generate/check-coding-standards.sh` | 代码规范检查（历史脚本，供参考） |
| `storage/clear_minio_and_db.py` | 清空 MinIO 与数据库关联数据（⚠️ 谨慎） |
| `storage/upload_files_to_minio.py` | 上传文件至 MinIO |
| `tools/check_tree.py` | 输出目录树 / 检查目录结构 |
| `tools/freeze-check.sh` | 冻结检查（`src/old/` 禁改校验） |
| `tools/docs-migration/` | 文档体系重构一次性脚本（已执行完毕，保留备查） |

### 2.5 仓库根 docker/ — 依赖容器

| 脚本 | 作用 |
|------|------|
| `docker/start.sh` | 启动 MySQL 3307 / Redis 6380 / MinIO 9000+9001 / LibreOffice / anydoc，并等待就绪 |
| `docker/stop.sh` | 停止本项目容器（不影响其他项目） |
| `docker/restart.sh` | 重启容器 |
| `docker/status.sh` | 查看容器状态 |

---

## 3. 编写规范

1. **命名**：`.sh` 用 kebab-case（`run-backend.sh`）；`.py` 用 snake_case（`db_tool.py`）；SQL 用 `YYYYMMDD_动词_对象_V1.sql` 或 `动词-对象-日期.sql`。
2. **自定位路径**：必须用 `SCRIPT_DIR` / `PROJECT_DIR` 自定位（铁律 B5）。
3. **头部注释**：必须写「作用 / 用法 / 依赖 / 维护人」；写操作脚本还须写「安全约束」。
4. **同步更新**：新增或修改脚本必须同步更新本 README §2 作用表。
5. **`set -euo pipefail`**：写操作与编排类脚本必须启用；测试类脚本至少 `set -uo pipefail`。
6. **就近原则**：功能性测试脚本不放入 `scripts/`，放功能目录 `test/`。
7. **版本后缀**：`V1`/`V2` 仅在确有必要（同功能多版本并存）时使用。

---

## 4. `scripts/db/` 历史脚本分类（115 个文件）

> ⚠️ 这里是**一次性脚本堆积区**，不是可复用能力。使用前先读本表定位。

| 分类 | 文件（模式） | 处置 |
|------|-------------|------|
| **① 在建/在用** | `verify/`、`fix/`、`fixture/`、`run_phase_migration.sh`、`cert_phase_definition_setup.sql`、`create_queue_tables.sql`、`add_dictionary_tree_fields_V1.sql`、`dual_key_design_V1.sql`、`unify_collation` 系列、`views/*.sql` | 保留 |
| **② 已执行完毕、仅备查** | `2026091x_*.sql` 系列（snake→Pascal 改名、加列、seed）、`create_*.sql`、`add_*.sql`、`fix_*.sql` | 保留但**不得重跑**（多为非幂等或已过期） |
| **③ 已被取代** | `unify_audit_columns_V1_final{,2,3,4}.sql`、`20260921_snake_to_pascal_final_V{1,2,3}.sql`、`fix_sql_v2/v3.py` | 待归档（D-E5） |
| **④ ⛔ 反向/失效，禁止使用** | `fix_yzh_columns_to_snake_case.sql`（**方向与铁律七相反**，**已删除**） | ~~D-E3 ✅~~ |
| **⑤ 散落的一次性核查脚本** | `check_*.py`、`verify_*.py`、`test_*.py`、`cleanup_*.py`、`disable_*.py`、`db_tool.py`、`db_verify.py`、`restart_backend.py` | ~~D-E5 ✅~~ 已归档至 `db/archive/2026-09/`（24 文件） |

> ★ 已被取代的脚本（`unify_audit_columns_V1_final{,2,3,4}.sql`、`fix_sql_v2/v3.py`）一并归档至 `db/archive/2026-09/`。

---

## 5. 历史

| 日期 | 变更内容 |
|------|---------|
| **2026-09-26** | **V2.4**：新增专家端企业机构树三件套（`verify/verify-enterprise-org-tree.{sh,sql}`、`verify/enterprise-org-gate.sql`、`fix/fix-enterprise-org-tree-2026-09-26.{sh,sql}`）。**B6 自锁首次落到「库名白名单 + 前置门禁 + 自动备份」三层**（此前只有 `__T__` 前缀校验一种形态）；`.sql` 内**去掉硬编码 `USE`**（改由调用方给库名，否则白名单自锁形同虚设）；修正 `verify-enterprise-org-tree.sql` 中「不涉及列 vs 列关联」的**错误声明**（实际存在跨表关联，依赖全库统一 collation） |
| **2026-09-25** | **V2.3**：**D-E1~D-E5 全部处置** —— 执行 `fix-column-naming-2026-09-24.sql`（命名修正 87 列 + DROP 25 表 enable + sys_api.Enable→IsValid + sys_log.Enable DROP + 7 张 Vol 表整表 DROP）；代码同步 5 处（SysApi : IIsValid + ApiSyncService + RoleApiController + SysApi.json + 前端 api 页）；删除反向脚本 `fix_yzh_columns_to_snake_case.sql`；归档 24 个历史脚本至 `db/archive/2026-09/` |
| **2026-09-25** | **V2.2**：**删除 `db/rebuild/`（7 文件）** —— 库获取方式定为「正式库备份 + 恢复」，不再提供从零重建；根因是 `schema_before.sql` 旧快照会把历史命名修正冲回 snake_case（「浪费一周」根因）。**D-E4 由此关闭**（不再需要修 schema 单一真相源） |
| **2026-09-24** | **V2.1**：**B3 落实到底** —— `rebuild_db.sh` 残留的 3 处内嵌 SQL（DROP/CREATE、SET NAMES、基础计数）全部外置为 `.sql`（`rebuild/00_drop_create_db.sql`、`verify/count_baseline.sql`），其中 `SET NAMES` 改用 `--init-command` 表达（会话参数不算内嵌 SQL）；`verify_dict_softdelete` / `cleanup_dict_test_data` 的 SQL 外置为同名 `.sql`（占位符 + `sed` 注入）；`run_phase_migration.sh` 去内嵌 SQL + 去硬编码 OrbStack 绝对路径 + 去掉 `set -e` 与 `$?` 的矛盾；**B3 检测命令升级**（覆盖 `mysql -e "SELECT ..."` 形态）。新增菜单路由契约快照 `verify/export_menu_urls.sql` + `sync_menu_urls.sh`（服务前端守卫 **R12**） |
| **2026-09-24** | **V2.0**：新增 §0 脚本职责铁律 B1–B7；`rebuild_db.sh` 去除内嵌 SQL（改调 `post_fix.sql`）+ 新增强制命名校验；新增 `db/verify/`、`db/fixture/`；`test-dictionary-api.sh` 剥离内联 SQL；删除重复脚本 `db/run_migration.sh`；`run-backend.sh` 去硬编码绝对路径 |
| 2026-09-12 | 收编 `docs/` 根目录下 5 个文档迁移脚本至 `tools/docs-migration/` |
| 2026-09-11 | 迁移历史项目至 `src/old/`；后端脚本指向 `src/yzh-core/YZH.Core.Web` |
| 2026-08-16 | 创建：根目录 9 个散落脚本归位；scripts/ 原 14 个脚本按 db/frontend/storage/generate 分类 |
