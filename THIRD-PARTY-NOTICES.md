# THIRD-PARTY-NOTICES

本项目使用的第三方依赖及其许可证清单：

## 后端依赖

| 依赖 | 许可证 | 备注 |
|---|---|---|
| Microsoft.EntityFrameworkCore 8.x | MIT | ORM |
| Pomelo.EntityFrameworkCore.MySql | MIT | MySQL 提供程序 |
| Autofac / Autofac.Extensions.DependencyInjection | MIT | DI 容器 |
| AutoMapper | MIT | 对象映射 |
| Newtonsoft.Json | MIT | JSON 序列化 |
| Quartz.NET | Apache-2.0 | 定时任务 |
| NPOI | Apache-2.0 | Office 文档读写 |
| EPPlus.Core 1.5.4 | LGPL | Excel 导出（勿升级至商业授权版） |
| Dapper | MIT | 轻量 ORM |
| Minio .NET | Apache-2.0 | 对象存储客户端 |
| CSRedisCore | MIT | Redis 客户端 |
| SkiaSharp | MIT | 图像处理 |
| Yitter.IdGenerator | MIT | ID 生成器 |
| Swashbuckle.AspNetCore | MIT | Swagger 文档 |
| System.Linq.Dynamic.Core | MIT | 动态 LINQ |

## 前端依赖

| 依赖 | 许可证 | 备注 |
|---|---|---|
| Vue 3 | MIT | 框架 |
| Vite | MIT | 构建工具 |
| TypeScript | Apache-2.0 | 语言 |
| Element Plus | MIT | admin 端 UI |
| Naive UI | MIT | auditor 端 UI |
| Pinia | MIT | 状态管理 |
| Vue Router | MIT | 路由 |
| axios | MIT | HTTP 客户端 |
| @logicflow/core | MIT | 工作流流程图 |

## 特别说明

- **vol 框架**（cq-panda/Vue.NetCore）：MIT License，Copyright (c) 2020 283591387@qq.com jxx，详见主 LICENSE 文件
- **EPPlus.Core**：使用 1.5.4 版本（LGPL），切勿升级至 5.x（商业授权）
- 若启用达梦/Oracle 国产化适配，需另行评估厂商 EULA
