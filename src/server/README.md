# src/server/ — 后端 API

> .NET 8 + Vol 框架，统一后端服务。

## 技术栈

- .NET 8 ASP.NET Core WebAPI
- Entity Framework Core (MySQL)
- Autofac 依赖注入
- JWT 认证
- Redis 缓存

## 项目结构

```
server/Vue.NetCore/vol.api/
├── VOL.Core/         框架核心层（不改）
├── VOL.Entity/       实体定义
├── VOL.Sys/          业务系统层（⭐ 主编码区）
│   └── Services/System/Partial/  扩展业务逻辑
├── VOL.WebApi/       Web API 入口
│   ├── appsettings.json          数据库连接配置
│   └── Properties/launchSettings.json  端口设置
└── DB/               数据库脚本（mysql/ 等）
```

## 配置要点

- **数据库**: MySQL 8.0 @ `127.0.0.1:3307`，库名 `yzh_cert_platform`
- **缓存**: Redis 7 @ `127.0.0.1:6380`
- **API 端口**: `http://localhost:9992`

配置入口：`VOL.WebApi/appsettings.json` → `Connection` 节点

## 关键词

`server` `后端` `API` `.NET` `Vol` `EF Core` `MySQL` `Redis` `JWT`
