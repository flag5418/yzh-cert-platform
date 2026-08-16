# docker/ — Docker 编排与运维脚本

> **版本**：V1.0 | **日期**：2026-08-16 | **状态**：成熟态
>
> **作用**：本项目 Docker 编排配置（compose.yml）与配套运维脚本。仅启动本项目所需服务（MySQL + Redis + MinIO + LibreOffice），不影响其他项目。

---

## 1. 脚本作用

| 脚本 | 作用 | 用法 |
|------|------|------|
| `start.sh` | 启动本项目全部容器（MySQL + Redis + MinIO 等） | `cd docker && bash start.sh` |
| `stop.sh` | 停止本项目容器（仅停本项目，不影响其他项目） | `cd docker && bash stop.sh` |
| `restart.sh` | 重启本项目容器 | `cd docker && bash restart.sh` |
| `status.sh` | 查看本项目容器运行状态 | `cd docker && bash status.sh` |

---

## 2. 与 compose.yml 的绑定关系

- 本目录所有脚本均以 `compose.yml` 为编排依据（脚本内使用 `SCRIPT_DIR` 自定位，`cd "$SCRIPT_DIR"` 后执行 `docker compose` 命令）；
- `.env` / `.env.example`：环境变量配置（端口、密码等），`compose.yml` 引用；
- 各服务配置目录（`mysql/`、`redis/`、`minio/`、`libreoffice/`）存放对应容器的配置文件/初始化脚本。

---

## 3. 维护约定

1. **脚本保持原地**：本目录脚本与 compose.yml 强绑定，**不得移入 scripts/**；
2. 新增容器/服务时：同步修改 `compose.yml` + 更新本 README 脚本作用表；
3. 修改端口/密码等参数：改 `.env`，不要硬编码进脚本；
4. 脚本命名统一 kebab-case（`start.sh` / `stop.sh` / `restart.sh` / `status.sh`）。

---

## 4. 服务端口速查

| 服务 | 端口 | 容器 |
|------|------|------|
| MySQL | 3307 | yzh-mysql |
| Redis | 6380 | yzh-redis |
| MinIO | 9000/9001 | yzh-minio |
