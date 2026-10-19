---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_568af391bf0b11f197eb525400393706
    ReservedCode1: FKodHiwuNkBpRM89FLDQpGRS9ubtHofQSThasBsVkzJcqs+bOqp3C36sWX7ptT7sa5CdGP44Txd3Y5f7D0XjB+PyP0Pdi4bYg+o9xz8E8RSo1pxfNwSJc+acUJMdZaFbEuusFu7YBN+1eD2NcxNvFM+oiOZxEKjVxKHZzLo8IDDclJlA+OU8ViqFDH8=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_568af391bf0b11f197eb525400393706
    ReservedCode2: FKodHiwuNkBpRM89FLDQpGRS9ubtHofQSThasBsVkzJcqs+bOqp3C36sWX7ptT7sa5CdGP44Txd3Y5f7D0XjB+PyP0Pdi4bYg+o9xz8E8RSo1pxfNwSJc+acUJMdZaFbEuusFu7YBN+1eD2NcxNvFM+oiOZxEKjVxKHZzLo8IDDclJlA+OU8ViqFDH8=
---

# scripts/tunnel/ — 公网访问（Cloudflare Tunnel）

> **作用**：把本机开发环境的前端临时暴露到公网，供他人演示/验收，无需域名与云服务器。
> **适用阶段**：演示、联调、异地验收。**不用于正式生产**。
> **★ 2026-10-19 起最佳实践**：优先暴露 `docker/web/` 的**生产构建容器**（见 `scripts/deploy/deploy-web.sh`），
> 而非 Vite dev server —— 跨境链路上 dev 模式请求数高一个数量级（详见 §0）。

---

## 0. 为什么用「生产构建容器」而不是 dev server

外网用户 → Cloudflare（免费版走**美西边缘**，日志可见 `location=lax/sjc`）→ 本机，RTT 通常 150–300ms+。

| 方式 | 首屏需请求的文件数 | 说明 |
|------|------|------|
| Vite dev server | **~275 个源码模块**（admin 89 + core 103 + share 83） | 每个模块一次跨境往返，首屏可达数十秒 |
| 生产构建（nginx） | **~87 个产物文件**，且 gzip 后 2.8MB→~0.75MB、带 hash 可长缓存 | 请求数、传输字节双双下降 |

> 结论：**先把 dev server 换成生产构建，再谈隧道**。这一步不依赖域名，收益最大。

---

## 1. 一键使用

```bash
# 发布（构建镜像 + 起容器 + 保活隧道），并打印固定公网地址
./scripts/deploy/deploy-web.sh

# 只保活隧道（不重新构建/发布）
./scripts/deploy/deploy-web.sh --tunnel-only

# 直接操作隧道
./scripts/tunnel/start-tunnel.sh            # 两个都开（幂等：已存活则不重启）
./scripts/tunnel/start-tunnel.sh admin
./scripts/tunnel/status-tunnel.sh
./scripts/tunnel/stop-tunnel.sh
```

启动后终端会打印形如 `https://xxxx-yyyy.trycloudflare.com` 的地址，直接发给需要访问的人即可。

> 首次执行会自动下载 `cloudflared`（约 20MB）到 `scripts/tunnel/bin/`，之后复用。

**默认目标端口（容器生产构建）**：admin=`10990` / auditor=`10991`。
临时要映射 dev server，用环境变量覆盖：

```bash
TUNNEL_ADMIN_PORT=9990 TUNNEL_AUDITOR_PORT=9991 ./scripts/tunnel/start-tunnel.sh
```

---

## 1.1 地址稳定性（不频繁重启 Cloudflare）

`start-tunnel.sh` 是**幂等**的：

- 隧道进程存活且**目标端口一致** → **不重启**，公网地址保持不变（`scripts/deploy/deploy-web.sh` 反复执行也安全）；
- 只有**目标端口变化**时才重启一次（例如首次从 dev `9990` 切到容器 `10990`），属一次性切换；
- 传输协议可用 `TUNNEL_PROTOCOL` 覆盖（默认 `quic`；跨境丢包严重时可试 `http2`）。

> Quick Tunnel 的地址与**进程生命周期**绑定：只要进程不停、目标端口不变，重启「容器/前端」也**不会**换地址。
> 进程退出（重启机器/手动 stop）后地址才会变。

---

## 2. 访问链路（为什么只暴露前端）

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

> 若用 `TUNNEL_*_PORT=9990/9991` 覆盖为 dev 模式，则上一步换成「Vite dev server + Vite proxy」，其余不变。

**结论：后端与数据层无需、也不应对外暴露。**

| 服务 | 端口 | 是否暴露 | 原因 |
|------|------|---------|------|
| cert-admin 生产构建 | 10990 | ✅ 暴露 | 浏览器需要加载页面 |
| cert-auditor 生产构建 | 10991 | ✅ 暴露 | 同上 |
| 后端 API | 9992 | ❌ 不暴露 | 前端以相对路径 `/api` 调用，由 nginx（或 Vite proxy）在本机转发 |
| MySQL | 3307 | ❌ 不暴露 | 后端「出站」连接，仅走本机回环 |
| Redis | 6380 | ❌ 不暴露 | 同上 |
| MinIO | 9000 / 9001 | ❌ 不暴露 | 文件经后端流式代理（`DownloadAsync`），无预签名 URL 外链 |

> 后端连接数据库/缓存/对象存储属于**服务端本地出站连接**，与隧道无关，
> 隧道不会也不该影响它——隧道只负责把**入站 HTTP** 映射到公网。

---

## 3. 前置条件

1. **生产构建容器已发布**：`./scripts/deploy/deploy-web.sh`（或手动 `docker compose -f docker/web/compose.yml up -d --build`）。
   若临时映射 dev server，则改为 `scripts/frontend/start.sh admin start`（auditor 同理）并覆盖 `TUNNEL_*_PORT`。
2. **`vite.config.ts` 已配置 `allowedHosts`**（仅 dev 模式需要，已内置）：
   ```ts
   server: {
     allowedHosts: ['.trycloudflare.com'],
   }
   ```
   > Vite ≥ 5.4.12 会校验请求的 `Host` 头，白名单外的域名一律返回
   > `403 Blocked request`。这是 **dev 模式**外网访问失败最常见的原因。
   > **修改该配置后必须重启前端才生效。**（容器 nginx 不校验 Host，无此问题。）
3. 后端 9992 已在运行：`scripts/backend/run-backend.sh`。

---

## 4. 常见问题

| 现象 | 原因 | 处理 |
|------|------|------|
| 打开地址显示 `Blocked request. This host is not allowed.` | （dev 模式）前端未重启，`allowedHosts` 未生效 | 重启前端（`scripts/frontend/start.sh admin restart`） |
| 页面能开，接口全 502/失败 | 后端 9992 没起来 | `scripts/backend/run-backend.sh status` |
| 页面能开但接口 502，且是容器模式 | 容器内 nginx 连不到宿主后端 | 确认 `extra_hosts: host.docker.internal` 生效；确认后端监听 9992 |
| 首屏特别慢（数十秒） | 隧道映射的是 **dev server**（~275 个模块） | 改用生产构建容器：`./scripts/deploy/deploy-web.sh` |
| 页面热更新报错（WebSocket） | dev 模式 HMR 走隧道不稳定 | 不影响功能；生产构建容器无此问题 |
| 地址打不开 / `trycloudflare` 无法解析 | 隧道进程已退出 | `./status-tunnel.sh` 查看，重新 `start-tunnel.sh` |
| 换网络后地址变了 | Quick Tunnel 地址与进程生命周期绑定 | 正常现象；保持进程存活即可稳定（见 §1.1） |
| 隧道日志反复 `timeout`/`no recent network activity` | 跨境 UDP 丢包导致 QUIC 抖动 | 试 `TUNNEL_PROTOCOL=http2` 重启一次隧道 |

---

## 5. 安全须知

- 隧道一旦开启，**任何拿到地址的人都能访问你的开发环境**，请：
  - 仅使用测试数据，避免真实业务数据与个人信息；
  - 演示结束后立即执行 `./scripts/tunnel/stop-tunnel.sh`；
  - 不要把 `3307 / 6380 / 9000` 一起开到公网（本脚本已刻意不暴露）。
- 隧道进程退出即失效，不对系统做任何持久化改动。

---

## 6. 升级路径（需要**永久固定**地址时）

Quick Tunnel 地址与进程生命周期绑定，进程重启即变。若需**永久固定**：
1. 准备一个域名并托管到 Cloudflare；
2. `cloudflared tunnel login && cloudflared tunnel create yzh-demo`；
3. 配置 `config.yml` 指向容器端口 `127.0.0.1:10990 / 10991`，用 named tunnel 替换本脚本的 `--url` 模式。

> ⚠️ 域名 + Named Tunnel 解决的是「地址固定 + 可加缓存规则」，**不解决**「中国 → 美西边缘」的跨境延迟。
> 若国内访问仍慢，需另配**国内中继**（轻量云 + frp/nps），本机仍可作为源站。

此阶段仍建议只暴露前端，后端与数据层保持本机。
*（内容由AI生成，仅供参考）*
