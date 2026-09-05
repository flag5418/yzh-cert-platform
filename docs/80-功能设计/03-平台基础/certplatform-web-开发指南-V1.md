# certplatform-web 开发指南

> **版本**：V1.0 | **日期**：2026-09-05 | **状态**：开发中

---

## 一、项目结构

```
src/certplatform-web/
├── package.json                    ← workspace 根（npm install 后失效，已改用 symlink）
├── yzh.vue.core/                   ← 核心组件库（业务无关）
│   ├── src/
│   │   ├── components/
│   │   │   ├── table/              ← YzhTable（Schema 驱动表格）
│   │   │   ├── form/               ← YzhForm（Schema 驱动表单）
│   │   │   ├── layout/             ← YzhPageLayout, YzhSearchBar, YzhToolbar, YzhPagination
│   │   │   └── ui/                 ← YzhEmptyState, YzhStatusBadge, YzhCard
│   │   ├── api/
│   │   │   └── client.ts           ← YzhApiClient（统一 HTTP 封装）
│   │   ├── composables/
│   │   │   ├── useTable.ts
│   │   │   └── useAuth.ts
│   │   ├── types/
│   │   │   └── index.ts            ← Page, PageParams, ApiResponse
│   │   └── index.ts                ← 统一导出
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── share/                          ← 业务共享层（跨角色复用）
│   ├── src/
│   │   ├── components/
│   │   │   ├── CertDirectoryTree.vue
│   │   │   ├── CertConvertBadge.vue
│   │   │   ├── CertPageHeader.vue
│   │   │   └── CertStatusBar.vue
│   │   ├── composables/
│   │   │   ├── useDirectoryApi.ts
│   │   │   ├── useFileTree.ts
│   │   │   └── usePolling.ts
│   │   ├── api/
│   │   │   ├── certification-body.ts
│   │   │   ├── iso-standard.ts
│   │   │   ├── iso-clause.ts
│   │   │   ├── cert-stage.ts
│   │   │   ├── enterprise.ts
│   │   │   ├── system-user.ts
│   │   │   ├── system-role.ts
│   │   │   ├── system-dept.ts
│   │   │   ├── system-menu.ts
│   │   │   ├── system-dict.ts
│   │   │   ├── system-log.ts
│   │   │   ├── prompt-template.ts
│   │   │   ├── skill.ts
│   │   │   └── ai-usage.ts
│   │   ├── types/
│   │   │   └── cert.ts             ← ISOStandard, CertificationBody, ISOClause 等
│   │   ├── utils/
│   │   │   ├── format.ts
│   │   │   ├── download.ts
│   │   │   └── convertStatus.ts
│   │   └── index.ts
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── admin/                          ← 管理员端（端口 9990，Element Plus）
│   ├── src/
│   │   ├── main.ts                 ← 入口（注册 Element Plus 图标）
│   │   ├── App.vue
│   │   ├── router/index.ts         ← 路由配置
│   │   ├── store/auth.ts           ← Pinia 状态
│   │   ├── layouts/
│   │   │   ├── AdminLogin.vue      ← 登录页（蓝色渐变品牌区 + 白色表单区）
│   │   │   └── AdminLayout.vue     ← 主布局（左侧菜单 + 顶部栏）
│   │   ├── pages/
│   │   │   ├── system/             ← 系统管理（user/role/dept/dict/menu/log）
│   │   │   ├── cert/               ← 认证业务（iso-standard/iso-clause/cert-stage/cert-body/enterprise）
│   │   │   └── business/           ← 业务管理（prompt-template/skill-manage/ai-usage-monitor 等）
│   │   └── assets/css/main.css     ← 全局设计令牌
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   └── index.html
│
└── auditor/                        ← 审核员端（端口 9991）
    └── （结构类似 admin）
```

---

## 二、启动方式

### 方法1：使用脚本（推荐）
```bash
# 管理员端
cd src/certplatform-web/admin
./start.sh

# 审核员端
cd src/certplatform-web/auditor
./start.sh
```

### 方法2：直接命令
```bash
cd src/certplatform-web/admin
export PATH="/opt/homebrew/bin:$PATH"
node node_modules/.bin/vite --host 127.0.0.1 --port 9990
```

### 访问地址
- 管理员端：http://127.0.0.1:9990/
- 审核员端：http://127.0.0.1:9991/
- 后端 API：http://127.0.0.1:9992/（自动代理）

---

## 三、开发规范

### 3.1 新建页面模板
```vue
<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getXXXPage } from '@share/api/xxx'
import type { Xxx } from '@share/types/cert'

const columns: YzhTableColumn<Xxx>[] = [
  { prop: 'code', label: '编码', width: 150 },
  { prop: 'name', label: '名称', minWidth: 200 }
]

const searchFields: SearchField[] = [
  { prop: 'code', label: '编码', type: 'text' }
]

async function loadData(params: PageParams) {
  return getXXXPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
```

### 3.2 导入路径规范
```ts
// 核心组件
import { YzhTable } from '@yzh-core/components/table'
import { YzhForm } from '@yzh-core/components/form'
import { YzhPageLayout } from '@yzh-core/components/layout'

// API
import { getXxxPage } from '@share/api/xxx'

// 类型
import type { Xxx } from '@share/types/cert'

// 组件
import { CertDirectoryTree } from '@share/components'
```

### 3.3 禁止事项
- ❌ 使用 `vue.vue.core/api` 路径（应使用 `@yzh-core/api/client`）
- ❌ 使用 `bootstrap-icons`（已移除，改用 Element Plus 图标）
- ❌ 使用 `const` 声明响应式对象后赋值（应使用 `let`）
- ❌ 新页面使用 `view-grid`、`VolBox`、`VolForm`

---

## 四、当前进度

### 已完成（14个页面）
| 类别 | 页面 | 状态 |
|------|------|------|
| 系统管理 | 用户/角色/部门/字典/菜单/日志 | ✅ 已完成 |
| 认证业务 | ISO标准/条款/阶段/机构/企业 | ✅ 已完成 |
| 业务管理 | AI费用监控/Prompt模板/Skill管理 | ✅ 已完成 |

### 骨架完成（7个页面）
| 页面 | 状态 |
|------|------|
| 标准目录管理 | ⚠️ 骨架完成 |
| 文档提取规则 | ⚠️ 骨架完成 |
| NC规则配置 | ⚠️ 骨架完成 |
| 报告章节定义 | ⚠️ 骨架完成 |
| 系统参数配置 | ⚠️ 骨架完成 |
| 队列监控 | ⚠️ 骨架完成 |
| 文件上传 | ⚠️ 骨架完成 |

---

## 五、待解决问题

### 5.1 构建错误
- ✅ 已修复 `bootstrap-icons` 依赖问题
- ✅ 已修复 `@yzh-core/api` 路径问题
- ✅ 已修复 TypeScript 类型错误（`let` vs `const`）

### 5.2 后续工作
1. 后端 API 路由对齐（待后端重构完成后）
2. 骨架页面业务逻辑填充
3. 审核员端页面迁移
4. 企业端子前端搭建

---

## 六、依赖管理

### 当前方案
- node_modules 通过 symlink 复用 vol.web 的依赖
- 位置：`src/server/Vue.NetCore/vol.web/node_modules/`
- 各子项目通过 symlink 指向共享依赖

### 如需安装新依赖
```bash
cd src/certplatform-web/admin
npm install <package-name> --save
```

---

## 七、相关文档

- 重构方案：`docs/80-功能设计/03-平台基础/certplatform-web-前端重构-V1.md`
- 启动指南：`docs/80-功能设计/03-平台基础/certplatform-web-启动指南-V1.md`
- 迁移对照表：`docs/历史文档/前端功能迁移对照表-V1.1.md`
- 项目全局规则：`项目全局规则.md`（V2.4）

---

*创建时间：2026-09-05*
*最后更新：2026-09-05*
