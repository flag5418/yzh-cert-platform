# 映智汇体系认证平台（yzh-cert-platform）

> ISO 体系认证全流程管理平台（建档 → 任务分派 → 预审 → 复核 → 报告 → NC）

## 版权信息

```
Copyright (c) 2024-2026 四川映智汇信息技术有限公司
Contact: flag5418@126.com / 13348947810
```

本项目基于 MIT 开源协议的项目 [cq-panda/Vue.NetCore](https://github.com/cq-panda/Vue.NetCore) 进行二次开发，完整许可声明见 [LICENSE](./LICENSE) 文件。

## 技术栈

| 层 | 技术 |
|---|---|
| 后端 | .NET 8 + YZH 框架内核（EF Core 8 / Autofac / Quartz） |
| 前端（admin） | Vue 3 + TypeScript + Vite + Element Plus |
| 前端（auditor） | Vue 3 + TypeScript + Naive UI |
| 数据库 | MySQL 8.0 |
| 缓存 | Redis 7 |
| 对象存储 | MinIO |
| 容器 | Docker Compose（OrbStack） |

## 工程结构

```
src/server/
├── Vue.NetCore/
│   ├── vol.api/
│   │   ├── YZH.Core/      框架内核（EF/服务基类/缓存/安全）
│   │   ├── YZH.Entity/    实体定义
│   │   ├── YZH.Sys/       系统域（用户/角色/菜单）
│   │   ├── YZH.WebApi/    Web 入口
│   │   ├── Cert.Platform/ 认证业务域
│   │   └── YZH.Builder/   代码生成器
│   └── vol.web/           admin 前端
├── YZH-Framework/         自研框架层（迁移中，合并入 YZH.Core）
└── auditor/               审核员前端
```

## 端口规划

| 服务 | 端口 |
|---|---|
| 后端 API | 9992 |
| 后台管理前端 | 9990 |
| 审核员前端 | 9991 |
| MySQL | 3307 |
| Redis | 6380 |
| MinIO | 9000 + 9001 |

## 快速开始

详见 `docs/00-工程体系/README.md`。

## 合规声明

- 本软件包含基于 MIT 许可的第三方开源代码，详见 [NOTICE.md](./NOTICE.md)
- 第三方依赖许可证清单见 [THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md)
