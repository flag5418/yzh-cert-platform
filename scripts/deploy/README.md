# scripts/deploy/ — 前端一键发布（生产构建 → Docker → 公网）

> **版本**：V1.0 | **日期**：2026-10-19 | **状态**：living（活文档，须与脚本同步）
>
> **作用**：把**最新前端源码**一键做生产构建、打进 nginx 镜像并发布为常驻容器，同时**保活**公网隧道。
> **一句话**：改完前端 → `./scripts/deploy/deploy-web.sh` → 刷新公网地址即可见到新版（地址不变）。

---

## 0. 为什么是「生产构建容器」而不是 dev server

外网访问链路：`浏览器 → Cloudflare 边缘（免费版走美西）→ 本机`，RTT 常 150–300ms+。

| 方式 | 首屏请求文件数 | 说明 |
|------|------|------|
| Vite dev server | **~275 个源码模块**（admin 89 + core 103 + share 83） | 每个模块一次跨境往返，首屏可达数十秒 |
| 生产构建（本文） | **~87 个产物文件**，gzip 后 2.8MB→~0.75MB，带 hash 可长缓存 | 请求数、传输字节双双下降 |

> **先换生产构建，再谈隧道**。这一步不依赖域名，收益最大。

---

## 1. 一键发布（最常用）

```bash
# 打包并发布 admin + auditor，并保活隧道；结束打印公网地址
./scripts/deploy/deploy-web.sh
```

改完前端代码后**重复执行同一条命令即可**：镜像重建、容器替换，**隧道不重启、公网地址不变**。

---

## 2. 参数

| 参数 | 作用 |
|------|------|
| `admin` / `auditor` | 只处理指定端（可同时给多个；默认两个都处理） |
| `--build-only` | 只构建镜像，**不**重启容器、**不**碰隧道 |
| `--tunnel-only` | 只确保隧道存活（不构建、不重启容器） |
| `--no-tunnel` | 构建 + 发布容器，但**不碰**隧道 |
| `-h` / `--help` | 打印用法 |

常见组合：

```bash
./scripts/deploy/deploy-web.sh admin            # 只发管理端
./scripts/deploy/deploy-web.sh --build-only     # 只出镜像（预构建/排查）
./scripts/deploy/deploy-web.sh --tunnel-only    # 隧道挂了只补隧道
./scripts/deploy/deploy-web.sh --no-tunnel      # 只发容器，隧道另行管理
```

---

## 3. 它做了什么（4 步）

1. **构建镜像**：`docker compose -f docker/web/compose.yml build`（内部 `npm ci` + `vite build`）。
2. **启动容器**：`up -d`，宿主绑定 `127.0.0.1:10990`（admin）/ `127.0.0.1:10991`（auditor）。
3. **后端体检**：探测 `9992` 是否监听，未起则告警（页面能开但 `/api` 会 502）。
4. **保活隧道**：调用 `scripts/tunnel/start-tunnel.sh`（**幂等**：目标端口一致就复用，不重启）；结束打印公网地址。

```
外网浏览器
   │ HTTPS
   ▼
Cloudflare 边缘 ──隧道──▶ 本机 nginx 容器 (10990 / 10991)   ← docker/web/ 生产构建
                              │  /api 相对路径
                              ▼
                        nginx 反代（host.docker.internal）
                              ▼
                         后端 YZH.Core.Web  127.0.0.1:9992
                              │ SDK
                              ▼
                         MySQL 3307 / Redis 6380 / MinIO 9000
```

---

## 4. 端口与隔离（不影响本地开发）

| 用途 | 端口 | 谁在跑 |
|------|------|--------|
| **本地开发（dev）** | 9990 / 9991 | `scripts/frontend/start.sh`（Vite dev server） |
| **对外发布（生产构建）** | **10990 / 10991** | 本文：`docker/web/` nginx 容器 |
| 后端 API | 9992 | `scripts/backend/run-backend.sh`（宿主机，**不对外暴露**） |

- 发布端口刻意避开 dev 端口 → **本地开发不受影响**；
- 容器只绑 `127.0.0.1`，不对局域网/公网暴露；外网仅经隧道；
- 后端与 MySQL/Redis/MinIO **一律不对外暴露**（前端同源 `/api` 由 nginx 本机转发）。

---

## 5. 地址稳定性（不频繁重启 Cloudflare）

- `deploy-web.sh` → `start-tunnel.sh` 是**幂等**的：隧道存活且**目标端口一致** → **不重启**；
- 重新发布前端**不会**改变公网地址（目标端口始终 10990/10991）；
- 仅当**目标端口变化**时重启一次（如首次从 dev 9990 切到容器 10990）；
- 地址与**隧道进程生命周期**绑定：进程退出（重启机器 / 手动 stop）才会换地址。

---

## 6. 相关文件

| 文件 | 作用 |
|------|------|
| `scripts/deploy/deploy-web.sh` | 本文档的一键发布脚本 |
| `docker/web/Dockerfile` | 多阶段：生产构建 → nginx |
| `docker/web/nginx/{admin,auditor}.conf` | 站点配置：gzip / 长缓存 / SPA 回退 / `/api` 反代（含 WS） |
| `docker/web/compose.yml` | 编排 `web-admin`(10990) / `web-auditor`(10991) |
| `scripts/tunnel/` | 隧道启停/状态（保活入口） |
| `scripts/frontend/` | 本地 dev server 启停（与发布互不影响） |

---

## 7. 常见问题

| 现象 | 原因 | 处理 |
|------|------|------|
| 构建报 `ECONNRESET` | 容器直连 `registry.npmjs.org` 不稳 | 已在 Dockerfile 内置国内源；仍失败可 `--build-arg NPM_REGISTRY=...` |
| 页面能开，接口 502 | 后端 9992 未起 | `./scripts/backend/run-backend.sh` |
| 发版后页面还是旧版 | 浏览器缓存了 `index.html` | 已设 `no-cache`；强刷一次即可 |
| 容器起不来 | 端口 10990/10991 被占 | `lsof -nP -iTCP:10990` 排查；或 `docker compose -f docker/web/compose.yml down` 后重发 |
| 想确认隧道地址 | — | `./scripts/tunnel/status-tunnel.sh` 或看 `scripts/tunnel/run/tunnel-urls.txt` |

---

## 8. 维护约定

1. 本目录脚本只做**编排**：构建 / 起容器 / 调隧道；**无任何数据库操作**（遵循 `scripts/README.md` §0 铁律 B2/B3）。
2. 端口、镜像名、角色等**参数集中在** `deploy-web.sh` 顶部与 `docker/web/compose.yml`，改端口须两处同步。
3. 新增发布端（如 enterprise）时：同步 `deploy-web.sh` 的 `svc_of`/`port_of` 与 `docker/web/compose.yml`。
4. 脚本自定位路径（`SCRIPT_DIR` / `PROJECT_DIR`），**禁止硬编码绝对路径**（铁律 B5）。
5. 默认只暴露前端；**严禁**在本目录脚本中加入暴露 9992 / 3307 / 6380 / 9000 的逻辑。

---

## 9. 与「隧道」「本地开发」的边界（职责三段）

```
① docker/web/（本文）   前端生产构建发布 → 对外服务   ← 外网访问用
② scripts/frontend/      本地 Vite dev server          ← 本地开发用（9990/9991）
③ scripts/backend/       后端 .NET 服务                 ← 两者共用（9992）
```
