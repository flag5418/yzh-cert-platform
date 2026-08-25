---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_a2b615a6a02a11f1a54f525400f8a581
    ReservedCode1: 5G6t0Wp7BM8c3OU/bVWlyZmH0eSX5i/GejWxY8E6GlkcGjEcO/FvwEuygODw5wDqnvY8Wfs3MJ4SJ/5C0OwvrtEyKnhJlWcfKt0/2hO/NHX5rHpDSoOf2JPFo/eUOeehd5O+w0wgW3PCEPiwNDeR1Zh+DQo4km5Y5s1JnPKURkLpfdT4fQgL3Q+cQmo=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_a2b615a6a02a11f1a54f525400f8a581
    ReservedCode2: 5G6t0Wp7BM8c3OU/bVWlyZmH0eSX5i/GejWxY8E6GlkcGjEcO/FvwEuygODw5wDqnvY8Wfs3MJ4SJ/5C0OwvrtEyKnhJlWcfKt0/2hO/NHX5rHpDSoOf2JPFo/eUOeehd5O+w0wgW3PCEPiwNDeR1Zh+DQo4km5Y5s1JnPKURkLpfdT4fQgL3Q+cQmo=
---

# anydoc 容器运行方式

> **版本**：V1.0 | **日期**：2026-08-25 | **状态**：成熟态
>
> **作用**：anydoc 文档转 Markdown 转换容器（Word/PPT/Excel/PDF/EPUB/CSV 等 14 种格式 → GFM Markdown）的搭建、启动、调用与故障排查说明。配套 docker/start.sh、docker/status.sh 使用。

---

## 1. 容器概览

| 项 | 值 |
|------|------|
| 容器名 | `yzh-anydoc` |
| 基础镜像 | `node:22-slim`（无官方 Docker 镜像，自建） |
| 软件包 | `@firecrawl/anydoc` v0.2.3（npm 全局安装，二进制走 npm optionalDependencies 分发） |
| 镜像名 | `docker-anydoc`（compose 构建产物） |
| 挂载目录 | 宿主 `./anydoc/tmp` ↔ 容器 `/tmp/anydoc` |
| 网络 | `yzh-net`（yzh-network） |
| 端口 | 无（不对外暴露端口，通过 `docker exec` 调用） |
| 内存占用 | 约 8MB（实测） |
| 转换速度 | 毫秒级（官方中位 <5ms） |

---

## 2. 构建与启动

### 2.1 首次构建（含加速说明）

```bash
cd docker

# 基础镜像拉取加速（Docker Hub 直连慢时先走国内镜像源再 tag）
docker pull docker.m.daocloud.io/library/node:22-slim
docker tag docker.m.daocloud.io/library/node:22-slim node:22-slim

# 构建镜像（npm 默认走 npmmirror 国内源；海外可覆盖 --build-arg NPM_REGISTRY=https://registry.npmjs.org/）
docker compose build anydoc
```

### 2.2 启动 / 停止 / 状态

```bash
# 启动（若已配置在 start.sh，则整体启动时自动拉起）
docker compose up -d anydoc

# 停止
docker compose stop anydoc   # 或整体 ./stop.sh

# 查看状态
docker compose ps anydoc     # 或 ./status.sh（已纳入）
```

> 已同步进 `start.sh`（[5/5] 启动）、`stop.sh`（整体停止）、`status.sh`（状态/资源/连接信息）。

---

## 3. 调用方式（核心）

### 3.1 基本转换：输出到 stdout

```bash
docker exec yzh-anydoc anydoc /tmp/anydoc/文件.docx
```

### 3.2 输出到文件

```bash
docker exec yzh-anydoc anydoc /tmp/anydoc/文件.docx -o /tmp/anydoc/文件.md
```

### 3.3 从 stdin 读取（无文件签名格式需显式指定，如 CSV）

```bash
cat 数据.csv | docker exec -i yzh-anydoc anydoc - --format csv
```

### 3.4 宿主文件进入容器

把待转换文件放入 `docker/anydoc/tmp/`（容器内对应 `/tmp/anydoc/`），转换产物同样落在该目录、宿主可直接读取：

```bash
cp /path/原始文件.docx docker/anydoc/tmp/
docker exec yzh-anydoc anydoc /tmp/anydoc/原始文件.docx -o /tmp/anydoc/结果.md
# 结果文件：docker/anydoc/tmp/结果.md
```

### 3.5 退出码约定

| 退出码 | 含义 |
|--------|------|
| 0 | 转换成功 |
| 1 | 文档无法转换（如扫描版 PDF 无文本层） |
| 2 | 使用错误（参数不对） |

---

## 4. 支持格式与限制

- **支持**：doc / docx / docm / ppt / pptx / xls / xlsx / xlsb / odt / ods / odp / rtf / epub / csv / pdf（共 14+ 种）→ GFM Markdown
- **限制**：扫描版 PDF（无文本层）返回 Unsupported（退出码 1），需走 MinerU 云端 OCR 分支；不内置 OCR
- **中文**：实测中文 Word/Excel 表格转换完整，无乱码

---

## 5. 与项目集成的对接方式

- **方案 A（当前推荐）**：`docker exec yzh-anydoc anydoc <容器内文件>`，.NET 侧通过 Process 调用；文件经 `docker/anydoc/tmp` 挂载卷交换
- **方案 B（备选）**：在 anydoc 容器外再包一层 HTTP 服务（POST /convert），.NET 走 HTTP 调用，隔离文件系统细节

> 对接 .NET 的 IFileExtractor 落地计划见 `docs/20-架构决策/技术研究-文档解析开源方案与轻量部署-V1.md`。

---

## 6. 故障排查

| 现象 | 处理 |
|------|------|
| `docker exec` 报 command not found | 镜像未构建成功，先 `docker compose build anydoc` |
| 拉取 node:22-slim 超时 | 走 2.1 节国内镜像源 + tag 方案 |
| 转换报 Unsupported / 退出码 1 | 文件为扫描版 PDF 或格式不受支持，转走 OCR 分支 |
| 宿主看不到转换产物 | 确认文件在 `docker/anydoc/tmp/`（而非别的目录），容器内使用 `/tmp/anydoc/` 前缀 |
| npm 安装慢 | 已默认 npmmirror；必要时在 Dockerfile 中调整 `ARG NPM_REGISTRY` |

---

## 7. 维护约定

1. 升级 anydoc：修改 `docker/anydoc/Dockerfile` 中 npm 包版本后 `docker compose build anydoc && docker compose up -d anydoc`；
2. 不改容器名/挂载目录时，仅需改 compose.yml 与 Dockerfile；
3. 本目录 `tmp/` 为运行时挂载点，不提交 Git（如已跟踪需加 .gitignore）。
*（内容由AI生成，仅供参考）*
