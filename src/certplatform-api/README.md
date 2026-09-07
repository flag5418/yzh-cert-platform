# Cert.Platform - 认证平台业务系统

认证平台后端业务项目，按角色分子文件夹组织代码。

## 项目结构

```
certplatform-api/
│
├── Cert.Platform.Api/           ← 主启动项目
│   ├── Program.cs               ← 入口：UseYzhCore + UseYzhPipeline
│   ├── CertDbContext.cs         ← 数据库上下文
│   ├── Controllers/             ← 公共控制器
│   └── EntityConfigs/             ← 公共EntityConfig配置
│
├── Cert.Platform.Entity/        ← 所有业务实体
│   ├── Cert/                    ← 认证机构/标准/条款
│   ├── Audit/                   ← 审核任务
│   ├── Wf/                      ← 工作流定义/模板
│   └── Sys/                     ← 系统实体(机构/角色/用户)
│
├── Admin/                       ← 管理员端子模块
│   ├── Controllers/             ← 管理员端API
│   ├── Services/                ← 管理员端业务服务
│   └── EntityConfigs/             ← 管理员端表格/表单配置
│
├── Auditor/                     ← 审核员端子模块
│   ├── Controllers/             ← 审核员端API
│   ├── Services/                ← 审核员端业务服务
│   └── EntityConfigs/             ← 审核员端表格/表单配置
│
└── Enterprise/                  ← 企业端子模块
    ├── Controllers/             ← 企业端API
    ├── Services/                ← 企业端业务服务
    └── EntityConfigs/             ← 企业端表格/表单配置
```

## 业务项目约定

1. **角色隔离**：不同角色的文件在各自文件夹下，互不影响
2. **EntityConfig**：每个页面的表格/表单配置放在对应角色的 `EntityConfigs` 目录
3. **控制器继承**：所有 CRUD 控制器继承 `BaseController<T>`
4. **实体集中**：所有实体统一在 `Cert.Platform.Entity` 项目中管理
