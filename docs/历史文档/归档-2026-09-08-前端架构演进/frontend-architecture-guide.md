# 前端架构与代码存放规范

> **版本**：V1.0 | **日期**：2026-08-25 | **状态**：成熟态
>
> 本文档定义前端代码的架构分层、模块划分、代码存放规则，确保后续开发不再乱放代码。

---

## 一、架构总览

### 1.1 分层架构

```
views/           → 业务页面（仅本项目使用）
certcore/        → 业务通用层（仅本项目使用）
yzh/             → 架构层（可跨项目复用）
components/      → Vol 框架遗留组件（@deprecated）
```

### 1.2 模块职责

| 模块 | 性质 | 职责 | 可复用性 |
|------|------|------|----------|
| `yzh/` | 架构层 | CRUD 组件、生命周期、状态管理 | ✅ 跨项目复用 |
| `certcore/` | 业务层 | 认证业务通用组件和方法 | ❌ 项目专属 |
| `views/` | 页面层 | 具体业务页面 | ❌ 项目专属 |
| `components/basic/` | Vol 遗留 | Vol 框架基础组件 | ⚠️ @deprecated |
| `components/VolProvider/` | Vol 遗留 | Vol 框架 Provider | ⚠️ @deprecated |
| `components/workflow/` | Vol 遗留 | 旧版工作流 | ⚠️ @deprecated |
| `components/workflow-designer/` | 新版 | 新版工作流设计器 | ✅ 可复用 |

---

## 二、yzh/ 架构层结构

### 2.1 目录结构

```
yzh/
├── index.ts                统一出口
├── README.md               使用文档
├── types/                  类型定义（纯 TS）
│   ├── index.ts
│   ├── YZHEntitySchema.ts  实体 Schema + Action 常量
│   ├── YZHLifecycles.ts    13+ 生命周期 Hook 类型
│   ├── YZHPageProps.ts     Props 类型
│   └── YZHConfigLoader.ts  V3.0 配置加载器
├── core/                   核心逻辑（纯 TS，无 Vue 依赖）
│   ├── YZHBaseApiClient.ts 泛型 HTTP 客户端
│   ├── YZHEditGuard.ts     保存/删除前置校验
│   ├── YZHRowDiff.ts       行级增量更新算法
│   ├── YZHPageLifecycle.ts 生命周期接口
│   └── YZHConfigLoader.ts  V3.0 配置加载器
├── components/             Vue 组件（基于 Element Plus）
│   ├── YzhCrudV3.vue       V3.0 核心 CRUD 组件
│   ├── YzhCrudTable.vue    V2.0 核心 CRUD 组件
│   ├── YzhDataTable.vue    数据表格
│   ├── YzhEditDialog.vue   编辑弹窗
│   ├── YzhToolbar.vue      工具栏
│   ├── YzhSearchBar.vue    搜索栏
│   ├── YzhPagination.vue   分页
│   ├── YzhFormField.vue    表单字段
│   ├── YzhFormGrid.vue     表单网格
│   ├── YzhTreeTable.vue    左树右表
│   ├── YzhStdTree.vue      机构-标准-阶段树
│   ├── YzhOrgLink.vue      机构关联
│   ├── YzhFolderUpload/    文件夹上传
│   └── ui/                 基础 UI 组件
├── composables/            Vue 3 Composables
│   ├── useYZHEditMode.ts   编辑模式状态机
│   ├── useYZHIncrementSync.ts 增量同步
│   └── useYzhQueue.js      队列中心
├── views/
│   └── QueueMonitor/       队列监控页面
├── store/                  状态管理
├── styles/                 样式
├── icons/                  图标管理
└── presets/                预设配置
```

### 2.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| CRUD 组件 | `yzh/components/` | `Yzh{Feature}.vue` |
| Composables | `yzh/composables/` | `useYzh{Feature}.ts` |
| 类型定义 | `yzh/types/` | `YZH{Feature}.ts` |
| 核心逻辑 | `yzh/core/` | `YZH{Feature}.ts` |
| 样式 | `yzh/styles/` | `yzh.css` |
| 图标 | `yzh/icons/` | `index.ts` |
| 预设配置 | `yzh/presets/` | `{Feature}.ts` |

### 2.3 组件开发规范

```vue
<template>
  <!-- 使用 Element Plus 原生组件 -->
  <el-table :data="tableData">
    <el-table-column prop="name" title="名称" />
  </el-table>
</template>

<script setup lang="ts">
// 使用 yzh/ 提供的类型和工具
import { ref } from 'vue'
import type { YZHPageProps } from '@/yzh/types'
import { YZHBaseApiClient } from '@/yzh/core'

// 定义 Props
const props = defineProps<YZHPageProps>()

// 使用 Composables
import { useYZHEditMode } from '@/yzh/composables'
const { isEditing, startEdit, cancelEdit } = useYZHEditMode()
</script>

<style scoped>
/* 使用 yzh/ 提供的设计令牌 */
@import '@/yzh/styles/yzh.css';
</style>
```

---

## 三、certcore/ 业务层结构

### 3.1 目录结构

```
certcore/
├── index.js                统一出口
├── README.md               使用文档
├── components/             业务组件
│   ├── CertDirectoryTree.vue   目录树（机构→标准→阶段→文件夹→文件）
│   ├── CertConvertBadge.vue    转换状态徽标
│   ├── CertStatusBar.vue       底部状态栏
│   ├── CertPageHeader.vue      页面标题栏
│   └── CertDocPreview.vue      文档预览（规划中）
├── composables/            业务 Composables
│   ├── useFileTree.js          树数据转换/懒加载
│   ├── useDirectoryApi.js      标准目录 API 封装
│   └── usePolling.js           轮询
├── utils/                  业务工具
│   ├── format.js               格式化工具
│   ├── api.js                  API 响应解包
│   ├── download.js             文件下载
│   └── convertStatus.js        转换状态映射
├── styles/                 业务样式
│   └── cert-tokens.css         业务扩展令牌
└── icons/                  业务图标
    └── index.js                业务图标映射
```

### 3.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| 业务组件 | `certcore/components/` | `Cert{Feature}.vue` |
| 业务 Composables | `certcore/composables/` | `use{Feature}.js` |
| 业务工具 | `certcore/utils/` | `{feature}.js` |
| 业务样式 | `certcore/styles/` | `cert-tokens.css` |
| 业务图标 | `certcore/icons/` | `index.js` |

### 3.3 组件开发规范

```vue
<template>
  <!-- 使用 certcore/ 提供的业务组件 -->
  <CertDirectoryTree @node-click="handleNodeClick" />
</template>

<script setup lang="ts">
// 使用 certcore/ 提供的工具
import { CertDirectoryTree, useFileTree, formatFileSize } from '@/certcore'
import { ref } from 'vue'

// 使用业务 Composables
const { treeData, loadNode } = useFileTree()

// 处理节点点击
const handleNodeClick = (node: any) => {
  console.log('[CertDirectory] 🌲 节点点击:', node)
}
</script>

<style scoped>
/* 使用 yzh/ 和 certcore/ 提供的设计令牌 */
@import '@/yzh/styles/yzh.css';
@import '@/certcore/styles/cert-tokens.css';
</style>
```

---

## 四、views/ 业务页面结构

### 4.1 目录结构

```
views/
├── cert/                   认证业务页面
│   ├── ISOStandard/        ISO 标准管理
│   │   ├── ISOStandard.vue
│   │   └── options.js
│   ├── Enterprise/         企业管理
│   ├── Standard/           标准管理
│   ├── CertApplication/    认证申请
│   ├── Link/               关联管理
│   ├── AuditTask/          审核任务
│   ├── CertificationBody/  认证机构
│   ├── ISOClause/          ISO 条款
│   └── Base/               基础页面
├── sys/                    系统管理
├── mes/                    MES 模块
├── builder/                代码生成器
├── signalR/                SignalR 页面
└── formDraggable/          表单拖拽
```

### 4.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| 业务页面 | `views/cert/{Feature}/` | `{Feature}.vue` |
| 页面配置 | `views/cert/{Feature}/` | `options.js` |
| 系统页面 | `views/sys/{Feature}/` | `{Feature}.vue` |
| 扩展页面 | `extension/cert/` | `{Feature}.jsx` |

### 4.3 页面开发规范

```vue
<template>
  <!-- 使用 yzh/ 提供的 CRUD 组件 -->
  <YzhCrudV3
    ref="crudTable"
    :schema="schema"
    :options="viewOptions"
    :lifecycles="lifecycles"
  >
    <template #toolbarLeft="{ selectedRow }">
      <!-- 业务按钮 -->
    </template>
  </YzhCrudV3>
</template>

<script setup lang="ts">
// 使用 yzh/ 提供的组件和类型
import { ref, markRaw } from 'vue'
import { YzhCrudV3 } from '@/yzh/index'
import type { YZHPageProps } from '@/yzh/types'
import viewOptions from './options.js'

// 定义 Schema
const schema = Object.freeze({
  keyField: 'Id',
  keyType: 'number',
  defaultSortField: 'CreateDate',
  defaultSortOrder: 'desc',
  controllerName: 'ISOStandard',
})

// 定义生命周期
const lifecycles = markRaw({
  onLoadAfter(rows) { return rows },
  onAddSaveBefore(main) { return true },
})
</script>
```

---

## 五、组件引用规范

### 5.1 引用优先级

```typescript
// 1. 优先使用 yzh/ 架构层组件
import { YzhCrudV3, YzhDataTable, YzhEditDialog } from '@/yzh'

// 2. 其次使用 certcore/ 业务层组件
import { CertDirectoryTree, CertConvertBadge } from '@/certcore'

// 3. 最后使用 components/ 遗留组件（@deprecated）
import { VolTable, VolForm } from '@/components/basic'
```

### 5.2 样式引用顺序

```css
/* 1. 先加载 yzh/ 架构层样式 */
@import '@/yzh/styles/yzh.css';

/* 2. 再加载 certcore/ 业务层样式 */
@import '@/certcore/styles/cert-tokens.css';

/* 3. 最后加载页面级样式 */
<style scoped>
/* 页面级样式 */
</style>
```

---

## 六、新功能开发检查清单

### 6.1 开发前

- [ ] 确认功能属于哪个业务模块（cert/sys/mes）
- [ ] 确认是否需要新的通用组件（提升到 yzh/ 或 certcore/）
- [ ] 确认是否需要扩展 yzh/ 架构能力

### 6.2 开发中

- [ ] 页面放在 `views/cert/{Feature}/`
- [ ] 通用组件放在 `yzh/components/`（架构级）或 `certcore/components/`（业务级）
- [ ] Composables 放在 `yzh/composables/`（架构级）或 `certcore/composables/`（业务级）
- [ ] 工具函数放在 `yzh/core/`（架构级）或 `certcore/utils/`（业务级）
- [ ] 使用正确的命名空间和引用路径

### 6.3 开发后

- [ ] 编译通过（0 errors）
- [ ] 更新相关 README 文档
- [ ] 如有新组件，更新 yzh/index.ts 或 certcore/index.js 导出

---

## 七、常见错误对照表

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `Failed to resolve import '@/yzh/xxx'` | 路径错误 | 检查 yzh/ 目录结构 |
| `Failed to resolve import '@/certcore/xxx'` | 路径错误 | 检查 certcore/ 目录结构 |
| `Cannot find module '@/components/xxx'` | 使用了旧组件 | 改为使用 yzh/ 或 certcore/ 组件 |
| 样式不生效 | 样式引用顺序错误 | 按 §5.2 规范调整引用顺序 |
| 组件不显示 | 路由未配置 | 检查 router/ 目录 |

---

## 八、与后端架构的对应关系

| 前端模块 | 后端模块 | 说明 |
|----------|----------|------|
| `yzh/` | `YZH.Core` | 架构层，可跨项目复用 |
| `certcore/` | `VOL.CERT` | 业务层，仅本项目使用 |
| `views/cert/` | `VOL.WebApi/Controllers/CertPlatform/` | 业务页面对应 Controller |
| `extension/cert/` | `VOL.WebApi/Controllers/CertPlatform/Partial/` | 业务扩展对应 Partial Controller |

---

**文档版本**：V1.0  
**创建时间**：2026-08-25  
**最后更新**：2026-08-25  
**更新内容**：初始版本，定义前端架构分层、模块划分、代码存放规则
