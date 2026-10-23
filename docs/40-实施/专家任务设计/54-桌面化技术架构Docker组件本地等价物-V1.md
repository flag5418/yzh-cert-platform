# 桌面化技术架构：Docker 组件的本地等价物（V1）

> 2026-10-23 ｜ 问题：docker 中部署的东西如何打包进 exe
> 现有 docker 栈（compose.yml 实证）：MySQL 8.0（3307）· Redis 7（6380，1GB）· MinIO（9000/9001）· LibreOffice（CJK 字体自建镜像）· anydoc（node:22 + @firecrawl/anydoc）
> 核心裁决：**桌面产品是新产品，不是 docker 的重新打包**；逐组件换本地等价物，复用纯函数层，不移植平台全量代码。
> ⚠️ **评审见 `56-54号桌面化技术架构评审-V1.md`（2026-10-23）**：方向成立，但 P0（SqlSugar 非 EF Core / Pandoc 不等价 / 离线与零上传口径 / 手机采集安全 / 签名更新备份许可）未修前，**不得按本文工时排期**。

## 一、组件映射表

| Docker 组件 | 桌面等价物 | 工作量 | 说明 |
|---|---|---|---|
| MySQL 8.0 | **SQLite**（EF Core 换 provider） | 中 | 桌面产品只建自己的表（疑点/证据池/检查单/企业档案/标准模型缓存），⛔ 不移植 60+ 表平台 schema；新代码禁写 provider 特定 SQL |
| Redis 7 | 进程内 `IMemoryCache`＋本地磁盘 | 低 | 单用户无分布式缓存需求；提示词/Markdown 缓存落本地文件即可 |
| MinIO | 本地文件目录 | 低 | 存储抽象换本地 provider：`%APPDATA%\<产品名>\files\`，按 企业/项目/疑点 分子目录 |
| LibreOffice | 安装器内置便携版 | 低-中 | CJK 字体问题已在 docker 镜像解决过一次，同样配方搬到便携版；**体积大头 ~350MB**；.doc 老格式只有它能可靠转 |
| anydoc（node） | **Pandoc 单二进制**（~30MB）或 .NET 原生解析 | 中 | Pandoc 无运行时依赖、可随安装器分发；docx/xlsx→md 质量足够；PDF→文本走 pdftotext 同类方案 |
| API（.NET） | **self-contained 发布**（含运行时） | 低 | `dotnet publish -r win-x64 --self-contained`；目标机器零依赖 |
| Vue 前端 | 构建产物内嵌 Kestrel `wwwroot` | 低 | 单进程同时服务 API 与 UI；⛔ 不为桌面产品保留三端分离 |

## 二、云地分离架构（承 53 号文档）

```
┌──────────────── 本地 exe（全功能离线）────────────────┐
│ UI(SPA) + API(Kestrel) + SQLite + files/ + 规则包缓存  │
│ 文档解析：LibreOffice + Pandoc + OCR(本地)              │
│ AI 调用：BYOK 直连 或 云端代理(充值用户)                │
└───────────┬──────────────────────┬───────────────────┘
            │ HTTPS                │ HTTPS
     激活校验/规则包下载      匿名决策统计回传(opt-in)
            └────────── 云端轻服务(复用现有 API 新端点组) ─┘
```

- 本地不依赖任何 docker 组件；云只做授权、规则同步、统计——**业务数据零上传**；
- 纯离线用户：手动导入规则更新包（zip）即可，功能不残。

## 三、最小骨架步骤（按序）

1. **新宿主项目**（现有方案加 `CertPlatform.Desktop` 或独立方案）：引用可复用层（`Shared/Fill`、`Shared/Office` 纯函数部分），⛔ 不引 Auditor/Admin 业务层；
2. 数据层：SQLite＋EF Core migrations，首次启动自动建库于 `%APPDATA%`；
3. 前端：新建单页工作台（疑点裁定台/双栏预填/采集），build 后拷入 `wwwroot`；
4. 发布：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`（注意：native 库单文件有限制，**别执着于真单 exe，专业工具用安装器是正常形态**）；
5. 安装器（Inno Setup）：exe＋LibreOffice 便携版＋Pandoc＋OCR 数据＋中文字体；
6. 云端轻服务：激活（机器码绑定）、规则包（版本化+签名）、统计回传（仅元数据）。

## 四、隐藏工作量（必须单独排期，最易低估）

| 项 | 方案 | 风险 |
|---|---|---|
| OCR 本地化 | Tesseract＋chi_sim（~50MB）或 `Windows.Media.Ocr`（Win10/11 内置，零体积） | 扫描件（校准台账、GBT 国标）是证据入口的硬需求；Windows 内置方案省 50MB 但依赖系统语言包 |
| LibreOffice 便携版 | 从现有 docker 镜像的配方移植（CJK 字体） | 安装包体积 ~350MB，可做“完整版/精简版”两档下载 |
| 规则包格式 | 版本号＋内容签名＋增量；本地缓存最近 N 版 | 签名私钥管理；更新失败要能回滚上一版 |
| 激活服务 | 机器码→激活码；换机解绑流程 | 客服成本（单人开发者要设计自助解绑，⛔ 不做人工审核） |
| 安装包体积 | 总计约 500-550MB | OCR/LibreOffice 可做可选组件降体积 |

## 五、边界（防范围蔓延）

- 现有平台**继续 docker 部署不动**（企业侧 SaaS＋机构侧），桌面产品是并行的第二产品形态；
- 桌面产品⛔ 不移植：权限体系、多租户、消息队列、三端分离、MinIO 分片——单用户场景全部不需要；
- 桌面产品✅ 复用：Shared 纯函数层（填写/填充/判定逻辑）、条款-表单解析逻辑、文档解析管线（改造为本地进程内调用，去掉队列）。
