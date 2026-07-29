# 映智汇认证审核管理系统

> yzh-cert-platform · 四川映智汇信息技术有限公司

## 项目概述

面向认证行业的数字化审核管理平台，覆盖 ISO 体系认证从企业建档、审核任务分派、资料预审、审计复核到报告生成与 NC 管理的全流程。

## 技术栈

| 层 | 技术 |
|---|------|
| 后端 | .NET 8 + Vol 框架 (ASP.NET Core WebAPI + EF Core) |
| 后台管理 | Vue 3 + TypeScript + Element Plus + Vite |
| 审核员端 | Vue 3 + TypeScript + Element Plus + Vite |
| 数据库 | MySQL 8.0 |
| 缓存 | Redis 7 |
| 容器化 | Docker Compose (OrbStack) |

## 端口规划

| 服务 | 端口 | 说明 |
|------|------|------|
| 后端 API | **9992** | Vol WebApi |
| 后台管理前端 | **9990** | Vol admin |
| 审核员前端 | **9991** | Auditor web |
| MySQL | **3307** | yzh-mysql |
| Redis | **6380** | yzh-redis |

## 快速开始

```bash
# 1. 启动数据库和缓存
cd docker && bash start.sh

# 2. 导入数据库表结构（首次）
docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform \
  < src/server/Vue.NetCore/DB/mysql/mysql表结构与表数据.sql

# 3. 启动后端
cd src/server/Vue.NetCore/vol.api
dotnet run --project VOL.WebApi

# 4. 启动审核员前端
cd src/auditor
npm run dev
```

## 目录结构

```
├── docker/           Docker Compose 配置（MySQL + Redis）
├── docs/             项目文档（按 00-60 分层）
├── scripts/          运维脚本
├── src/
│   ├── server/       .NET 8 后端
│   ├── admin/        后台管理前端
│   ├── auditor/      审核员前端
│   └── auditor-app/  移动端（预留）
└── .gitignore
```

## 文档

所有设计文档位于 `docs/`，按分层索引。入口：`docs/项目全局规则-V1.md`
