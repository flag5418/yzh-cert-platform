# certplatform-web 前端重构方案

> **版本**：V1.0 | **日期**：2026-09-05 | **状态**：实施中
>
> **目标**：将 vol.web 单体前端重构为多端架构，按角色隔离 admin/auditor/enterprise，核心组件独立为 yzh.vue.core，业务共享层为 share/。

---

## 一、背景与目标

### 1.1 现状问题
- vol.web 单体项目，所有角色代码混在一起
- 两套组件并存（YzhCrudTable + YzhTable），维护困难
- API 封装双轨制（手写 ts + extension 自动生成 jsx）
- 无法独立发布各角色端

### 1.2 重构目标
1. **角色隔离**：admin/auditor/enterprise 独立项目，可独立部署
2. **核心抽离**：yzh.vue.core 作为业务无关的底层能力层
3. **共享复用**：share/ 提供跨角色业务组件和 API
4. **组件统一**：淘汰 YzhCrudTable 和 Vol 遗留组件，统一使用 YzhTable + YzhForm

---

## 二、目标结构

```
certplatform-web/                          ← 工作区根目录（npm workspace）
│
├── yzh.vue.core/                          ← 核心模块（业务无关，独立发布）
│   ├── src/
│   │   ├── components/
│   │   │   ├── table/                     ← YzhTable（Schema 驱动）
│   │   │   ├── form/                      ← YzhForm
│   │   │   ├── layout/                    ← YzhPageContainer / Toolbar / SearchBar / Pagination
│   │   │   └── ui/                        ← YzhEmpty / YzhStatusBadge / YzhCard
│   │   ├── api/
│   │   │   ├── client.ts                  ← YzhApiClient（fetch 封装）
│   │   │   ├── auth.ts                    ← 认证工具
│   │   │   └── types.ts                   ← 通用类型
│   │   ├── composables/
│   │   │   ├── useTable.ts
│   │   │   ├── useForm.ts
│   │   │   └── useAuth.ts
│   │   ├── types/                         ← Page / ApiResponse 等通用类型
│   │   ├── utils/
│   │   └── index.ts                       ← 统一导出
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── share/                                 ← 项目共享层（业务相关，跨角色复用）
│   ├── src/
│   │   ├── components/                    ← 业务组件（原 certcore/）
│   │   │   ├── CertDirectoryTree.vue
│   │   │   ├── CertConvertBadge.vue
│   │   │   ├── CertPageHeader.vue
│   │   │   └── CertStatusBar.vue
│   │   ├── composables/                   ← 业务 Composables
│   │   │   ├── useDirectoryApi.ts
│   │   │   ├── useFileTree.ts
│   │   │   └── usePolling.ts
│   │   ├── api/                           ← 业务 API
│   │   │   ├── certification-body.ts
│   │   │   ├── iso-standard.ts
│   │   │   ├── iso-clause.ts
│   │   │   ├── cert-stage.ts
│   │   │   ├── enterprise.ts
│   │   │   └── system-*.ts
│   │   ├── types/                         ← 业务类型定义
│   │   ├── utils/
│   │   └── index.ts
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── admin/                                 ← 管理员端
│   ├── src/
│   │   ├── app/                           ← 应用入口
│   │   │   ├── main.ts
│   │   │   ├── App.vue
│   │   │   ├── router/index.ts
│   │   │   └── store/auth.ts              ← Pinia
│   │   ├── layouts/
│   │   │   ├── AdminLogin.vue             ← 管理员登录页
│   │   │   ├── AdminLayout.vue            ← 管理员首页布局
│   │   │   └── AdminSidebar.vue           ← 侧边栏
│   │   ├── pages/
│   │   │   ├── system/                    ← 系统管理
│   │   │   │   ├── user/
│   │   │   │   ├── role/
│   │   │   │   ├── dept/
│   │   │   │   ├── dict/
│   │   │   │   └── menu/
│   │   │   └── cert/                      ← 体系认证
│   │   │       ├── iso-standard/
│   │   │       ├── iso-clause/
│   │   │       ├── cert-stage/
│   │   │       └── cert-body/
│   │   ├── api/                           ← 管理员端专属 API（可选）
│   │   └── assets/
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── auditor/                               ← 审核员端
│   ├── src/
│   │   ├── app/
│   │   ├── layouts/
│   │   │   ├── AuditorLogin.vue           ← 审核员登录页（不同样式）
│   │   │   └── AuditorLayout.vue          ← 审核员首页布局
│   │   ├── pages/
│   │   │   └── workspace/
│   │   └── assets/
│   ├── package.json
│   └── vite.config.ts
│
└── enterprise/                            ← 企业端（Phase 2+）
    └── （待建）
```

---

## 三、npm workspace 配置

### 3.1 根 package.json
```json
{
  "name": "certplatform-web",
  "private": true,
  "workspaces": [
    "yzh.vue.core",
    "share",
    "admin",
    "auditor"
  ]
}
```

### 3.2 子包依赖关系
```json
// admin/package.json
{
  "dependencies": {
    "yzh.vue.core": "workspace:*",
    "share": "workspace:*"
  }
}

// auditor/package.json
{
  "dependencies": {
    "yzh.vue.core": "workspace:*",
    "share": "workspace:*"
  }
}
```

---

## 四、实施阶段

### Phase 1: 搭建 yzh.vue.core 核心模块
- [ ] 创建目录结构
- [ ] 迁移 YzhTable.vue（从 src/yzh/components/table/）
- [ ] 迁移 YzhForm.vue（从 src/yzh/components/form/）
- [ ] 迁移布局组件（YzhPageContainer, YzhToolbar, YzhSearchBar, YzhPagination）
- [ ] 迁移 UI 组件（YzhEmptyState, YzhStatusBadge, YzhCard）
- [ ] 迁移 YzhApiClient（从 src/yzh/api/client.ts）
- [ ] 迁移 Composables（useTable, useForm, useAuth）
- [ ] 配置 package.json（exports 字段支持 tree-shaking）
- [ ] 验证独立编译

### Phase 2: 迁移 share 共享层
- [ ] 创建目录结构
- [ ] 迁移业务组件（CertDirectoryTree, CertConvertBadge, CertPageHeader, CertStatusBar）
- [ ] 迁移业务 Composables（useDirectoryApi, useFileTree, usePolling）
- [ ] 迁移业务 API（certification-body, iso-standard, iso-clause, cert-stage, enterprise, system-*.ts）
- [ ] 配置 package.json
- [ ] 验证可被 admin/auditor 引用

### Phase 3: 重构 admin 端
- [ ] 创建目录结构
- [ ] 配置 vite.config.ts（path alias + proxy）
- [ ] 迁移 pages/system/ 页面
- [ ] 迁移 pages/business/ 页面
- [ ] 编写 AdminLogin.vue
- [ ] 编写 AdminLayout.vue + AdminSidebar.vue
- [ ] 配置路由（router/index.ts）
- [ ] 配置 Pinia store（store/auth.ts）
- [ ] 验证独立启动（端口 9990）

### Phase 4: 重构 auditor 端
- [ ] 迁移现有 src/auditor/ 结构
- [ ] 编写 AuditorLogin.vue
- [ ] 编写 AuditorLayout.vue
- [ ] 验证独立启动（端口 9991）

### Phase 5: 清理与验收
- [ ] 删除已迁移的旧代码（yzh/、certcore/、extension/）
- [ ] 更新 AGENTS.md
- [ ] 更新项目全局规则.md
- [ ] 验证各端独立启动和构建
- [ ] 验收测试

---

## 五、关键决策记录

| 决策 | 说明 |
|------|------|
| 前后端 GridConfig 对接 | 延后，前端先用现有手写 API |
| vol.web 保留 | 保留历史版本，不删除 |
| YzhCrudTable 处理 | 业务迁移完成后删除 |
| 登录页独立 | 各端 layouts/ 独立实现 |
| 企业端时机 | Phase 2+ 实施 |

---

## 六、风险与缓解

| 风险 | 影响 | 缓解方案 |
|------|------|---------|
| workspace 配置复杂 | 中 | 分阶段验证，先完成 yzh.vue.core |
| 旧代码引用路径变更 | 中 | 使用 path alias 过渡 |
| 组件功能缺失 | 高 | MVP 版本覆盖 80% 场景 |
| 样式回归 | 中 | 保留原有 CSS，逐步迁移 |

---

*文档创建时间：2026-09-05*
*最后更新：2026-09-05*
