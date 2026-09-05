# Vol 后台管理全面重写方案

> **版本**：V1.0 | **创建日期**：2026-09-04
>
> 基于新架构（YzhCrudV3 配置驱动）全面重写 Vol 后台管理系统。

---

## 一、重写目标

| 维度 | 旧架构 | 新架构 |
|------|--------|--------|
| 前端核心组件 | `<view-grid>` (26文件/793行) | `<YzhCrudV3>` (配置驱动) |
| 配置方式 | options.js + .vue + extension.jsx 三文件 | 实体特性反射 → 一个JSON配置 |
| 后端Service基类 | `ServiceBase` (无校验/脱敏) | `CertServiceBase` (校验/钩子/脱敏) |
| API接口风格 | ViewGrid内部封装 | 标准RESTful (CRUD) |
| 代码复用率 | 每页150-800行 | 每页<50行 |

---

## 二、重写范围

### Phase 1：基础设施层（优先）
1. **http.js** → 统一API调用方式，去掉XMLHttpRequest
2. **store** → 精简状态管理
3. **路由** → 清理重复路由，统一到 viewGird.js
4. **布局 Index.vue** → 保持现有布局，仅清理依赖

### Phase 2：系统管理页面（核心）
用 YzhCrudV3 重写以下 6 个系统管理页面：

| 页面 | 旧文件 | 重写后 | 行数变化 |
|------|--------|--------|----------|
| 用户管理 | Sys_User.vue (157行) + options.js (78行) | UserManagement.vue (~40行) | -75% |
| 角色管理 | Sys_Role.vue (109行) + options.js (53行) | RoleManagement.vue (~40行) | -63% |
| 菜单设置 | Sys_Menu.vue (824行) 自定义 | MenuManagement.vue (~200行) | -75% |
| 数据字典 | Sys_Dictionary.vue (481行) + options.js (78行) | DictionaryManagement.vue (~80行) | -83% |
| 组织架构 | Sys_Department.vue (198行) + options.js (58行) | DepartmentManagement.vue (~50行) | -75% |
| 操作日志 | Sys_Log.vue (88行) | LogManagement.vue (~30行) | -66% |

### Phase 3：后端适配
1. 为系统管理实体添加 YZH 特性注解
2. 确保 CertServiceBase 兼容系统管理 Service

---

## 三、实施步骤

### Step 1：清理基础设施
- 清理 viewGird.js 旧路由
- 统一 http.js（移除 XMLHttpRequest）
- 精简 store（移除 test 等无用方法）

### Step 2：为系统管理实体添加 YZH 特性
为以下实体添加 `[YZHPage]`/`[YZHColumn]`/`[YZHForm]`/`[YZHSearch]` 特性：
- Sys_User
- Sys_Role
- Sys_Menu
- Sys_Dictionary
- Sys_Department

### Step 3：重写系统管理页面
每个页面使用 YzhCrudV3 组件，通过 entity-name 加载配置。

### Step 4：特殊页面处理
- **Sys_Menu**：菜单管理需要树形结构+权限配置，不能完全用配置驱动，保留部分自定义
- **Permission**：权限分配页面保留自定义实现

### Step 5：联调测试
- 启动后端 + 前端
- 逐个页面验证 CRUD 功能

---

## 四、注意事项

1. **保留现有布局**：Index.vue 布局结构保持不变，只清理内部依赖
2. **保留现有 API**：后端 Controller 接口不变，前端调用方式统一
3. **渐进式迁移**：先重写系统管理页面，业务页面后续单独处理
4. **菜单管理特殊处理**：树形结构+权限配置复杂度高，保留自定义但重构代码
