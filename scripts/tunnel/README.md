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

# scripts/tunnel/ — 公网临时访问（Cloudflare Quick Tunnel）

> **作用**：把本机开发环境的前端临时暴露到公网，供他人演示/验收，无需域名与云服务器。
> **适用阶段**：演示、联调、异地验收。**不用于正式生产**。

---

## 1. 一键使用

```bash
# 开启（两个前端都开）
./scripts/tunnel/start-tunnel.sh

# 只开其中一个
./scripts/tunnel/start-tunnel.sh admin

# 查看状态
./scripts/tunnel/status-tunnel.sh

# 关闭
./scripts/tunnel/stop-tunnel.sh
```

启动后终端会打印形如 `https://xxxx-yyyy.trycloudflare.com` 的地址，直接发给需要访问的人即可。

> 首次执行会自动下载 `cloudflared`（约 20MB）到 `scripts/tunnel/bin/`，之后复用。

---

## 2. 访问链路（为什么只暴露前端）

```
外网浏览器
   │ HTTPS
   ▼
Cloudflare 边缘 ──隧道──▶ 本机 Vite dev server (9990 / 9991)
                              │  /api 相对路径
                              ▼
                         Vite proxy（本机回环）
                              ▼
                         后端 YZH.Core.Web  127.0.0.1:9992
                              │ SDK
                              ▼
                         MySQL 3307 / Redis 6380 / MinIO 9000
```

**结论：后端与数据层无需、也不应对外暴露。**

| 服务 | 端口 | 是否暴露 | 原因 |
|------|------|---------|------|
| cert-admin 前端 | 9990 | ✅ 暴露 | 浏览器需要加载页面 |
| cert-auditor 前端 | 9991 | ✅ 暴露 | 同上 |
| 后端 API | 9992 | ❌ 不暴露 | 前端以相对路径 `/api` 调用，由 Vite proxy 在本机转发 |
| MySQL | 3307 | ❌ 不暴露 | 后端「出站」连接，仅走本机回环 |
| Redis | 6380 | ❌ 不暴露 | 同上 |
| MinIO | 9000 / 9001 | ❌ 不暴露 | 文件经后端流式代理（`DownloadAsync`），无预签名 URL 外链 |

> 后端连接数据库/缓存/对象存储属于**服务端本地出站连接**，与隧道无关，
> 隧道不会也不该影响它——隧道只负责把**入站 HTTP** 映射到公网。

---

## 3. 前置条件

1. **前端已在运行**：`scripts/frontend/start.sh admin start`（auditor 同理）。
2. **`vite.config.ts` 已配置 `allowedHosts`**（已内置）：
   ```ts
   server: {
     allowedHosts: ['.trycloudflare.com'],
   }
   ```
   > Vite ≥ 5.4.12 会校验请求的 `Host` 头，白名单外的域名一律返回
   > `403 Blocked request`。这是外网访问失败最常见的原因。
   > **修改该配置后必须重启前端才生效。**
3. 后端 9992 已在运行：`scripts/backend/run-backend.sh`。

---

## 4. 常见问题

| 现象 | 原因 | 处理 |
|------|------|------|
| 打开地址显示 `Blocked request. This host is not allowed.` | 前端未重启，`allowedHosts` 未生效 | 重启前端（`scripts/frontend/start.sh admin restart`） |
| 页面能开，接口全 502/失败 | 后端 9992 没起来 | `scripts/backend/run-backend.sh status` |
| 页面热更新报错（WebSocket） | HMR 走隧道不稳定 | 不影响功能，忽略即可 |
| 地址打不开 / `trycloudflare` 无法解析 | 隧道进程已退出 | `./status-tunnel.sh` 查看，重新 `start-tunnel.sh` |
| 换网络后地址变了 | Quick Tunnel 地址是随机的 | 正常现象，重新启动会得到新地址 |

---

## 5. 安全须知

- 隧道一旦开启，**任何拿到地址的人都能访问你的开发环境**，请：
  - 仅使用测试数据，避免真实业务数据与个人信息；
  - 演示结束后立即执行 `./scripts/tunnel/stop-tunnel.sh`；
  - 不要把 `3307 / 6380 / 9000` 一起开到公网（本脚本已刻意不暴露）。
- 隧道进程退出即失效，不对系统做任何持久化改动。

---

## 6. 升级路径（后续需要固定地址时）

Quick Tunnel 地址随机且不稳定，若要长期固定：
1. 准备一个域名并托管到 Cloudflare；
2. `cloudflared tunnel login && cloudflared tunnel create yzh-demo`；
3. 配置 `config.yml` 指向 `localhost:9990 / 9991`，用 named tunnel 替换本脚本的 `--url` 模式。

此阶段仍建议只暴露前端，后端与数据层保持本机。
*（内容由AI生成，仅供参考）*
