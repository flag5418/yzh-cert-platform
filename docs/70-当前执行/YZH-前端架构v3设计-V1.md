# YZH 前端架构 v3 设计

> **版本**：V1.0 | **日期**：2026-08-12 | **状态**：成熟态（待实施）
>
> **定位**：基于 vidlang 组件控制管理模式，对 YZH 前端框架进行扩容，并建立认证平台项目级通用层 `certcore`。本文档是后续全部前端改造的宪法级依据。

---

## 一、背景与目标

### 1.1 现状问题

| # | 问题 | 证据 |
|---|------|------|
| 1 | 12 个 CertPlatform 页面存在 3 种"语言"（旧 Vol view-grid / YZH / 手写自定义页） | 全量页面盘点 |
| 2 | 5 个手写页布局约定互不统一（absolute 定位 / padding 16 / padding 20 / 三栏拼板） | DirectoryManager vs DocExtractionRule vs PromptTemplate |
| 3 | 图标 3 套体系混用（emoji / 文本字符 ○✓✕ / el-icon） | DocExtractionRule 实测 |
| 4 | 同一功能重复实现：目录树 3 处各写一套、下载/格式化/响应解包全站散落 | 代码盘点 |
| 5 | yzh 框架仅覆盖 CRUD 基类，缺设计令牌、图标管理、基础组件库 | yzh/README 目录清单 |
| 6 | 无统一样式 token，色值/间距每页手写 | grep 结果 |

### 1.2 目标

1. 扩容 yzh 框架：设计令牌体系 + 统一图标管理 + 基础组件库（**多项目可剥离复用**）
2. 建立 `certcore` 项目级通用层（**认证业务域专用**）
3. 统一认证平台视觉语言：灰底 + 白卡片 + 16px 间隙 + 4px 圆角 + 全 el-icon
4. 目录树等高频业务件组件化，供规则定义 / 报告内容定义等后续页面复用

---

## 二、参考模式：vidlang 组件控制管理（取经结论）

> 来源：`/Volumes/Expand/wangqingquan/Documents/work/study/flutter/vidlang`
> 核心文件：`lib/theme/`（design_tokens + app_colors/app_spacing/app_radius/app_shadows/app_typography/app_icons）、`lib/components/ui/`（ui_components.dart barrel + BaseCard/EmptyState/Badge）、`docs/developer/design/ui-components-standard-V1.0.md`

| # | vidlang 机制 | 具体做法 | 迁移到 vol.web |
|---|-------------|---------|---------------|
| 1 | **设计令牌分层** | theme/ 下颜色/尺寸/圆角/阴影/字体分文件，design_tokens.dart 统一出口 | yzh/styles/ 分 css + yzh.css 出口 |
| 2 | **图标集中管理** | app_icons.dart 语义化命名全部图标，换图标库只改一处 | yzh/icons/index.js |
| 3 | **基础组件库 + barrel** | components/ui/ + ui_components.dart 统一 export，禁单独 import 子文件 | yzh/components/ui/ + index.js |
| 4 | **组件标准文档** | 组件清单/参数表/使用边界/迁移优先级 P0~P3/新页面检查清单 | 配套规范文档 |
| 5 | **渐进式统一** | 新代码强制标准组件，旧代码按优先级顺带迁移 | 迁移路线 |

---

## 三、分层架构

```
src/
├── yzh/                            # ★ 框架级：多项目可整体剥离（架构扩容）
│   ├── styles/                     # 设计令牌体系（新增）
│   │   ├── yzh-colors.css
│   │   ├── yzh-spacing.css
│   │   ├── yzh-radius.css
│   │   ├── yzh-shadows.css
│   │   ├── yzh-typography.css
│   │   └── yzh.css                 # 统一出口
│   ├── icons/
│   │   └── index.js                # 统一图标管理（新增）
│   ├── components/
│   │   ├── ui/                     # 基础组件库（新增）
│   │   │   ├── YzhBaseCard.vue
│   │   │   ├── YzhTitledCard.vue
│   │   │   ├── YzhEmptyState.vue
│   │   │   ├── YzhStatusBadge.vue
│   │   │   └── index.js            # barrel 出口
│   │   ├── YzhCrudTable.vue        # 已有
│   │   ├── YzhTreeTable.vue        # 已有
│   │   └── ...                     # 已有
│   ├── core/ composables/ types/ presets/ store/   # 已有，不动
│   └── index.ts
│
├── certcore/                       # ★ 项目级：认证业务域通用（新建）
│   ├── styles/
│   │   └── cert-tokens.css         # 业务扩展 token
│   ├── icons/
│   │   └── index.js                # 业务图标映射（文件类型/状态）
│   ├── components/
│   │   ├── CertDirectoryTree.vue   # ★ 目录树（全局复用核心）
│   │   ├── CertDocPreview.vue      # ★ 文档预览
│   │   ├── CertConvertBadge.vue    # 转换状态徽标
│   │   ├── CertStatusBar.vue       # 底部状态栏
│   │   ├── CertPageHeader.vue      # 页面标题栏
│   │   └── index.js                # barrel 出口
│   ├── composables/
│   │   ├── useFileTree.js          # 树数据转换/懒加载/目录编码
│   │   ├── useDirectoryApi.js      # 标准目录 API 封装
│   │   └── usePolling.js           # 轮询
│   ├── utils/
│   │   ├── format.js               # formatFileSize / formatDate
│   │   ├── api.js                  # API 响应解包
│   │   ├── download.js             # blob 下载 + 文件名解析
│   │   └── convertStatus.js        # 转换状态映射
│   └── index.js
│
└── views/cert/{模块}/              # 业务页面：只写差异
```

### 3.1 分层边界（写入 yzh/README 与 certcore/README）

| 层 | 归属 | 依赖 | 可剥离性 |
|----|------|------|---------|
| `yzh/` | 前端框架 | 仅 Element Plus | ✅ 复制即用，支撑其他项目 |
| `certcore/` | 认证业务域 | yzh + 业务 API | 仅本项目 |
| `views/cert/*` | 页面 | yzh + certcore | 只写差异 |

### 3.2 命名原则

- 框架级组件：`Yzh*` 前缀
- 项目级组件：`Cert*` 前缀
- 业务页面：`views/cert/{模块}/`，与 `certcore/` 通过目录名区分

---

## 四、yzh 样式令牌体系

### 4.1 yzh-colors.css（对齐 vidlang app_colors.dart）

```css
:root {
  /* 品牌色（对齐 Vol 主题主色） */
  --yzh-color-primary: #409eff;
  --yzh-color-primary-light-3: #79bbff;
  --yzh-color-primary-light-5: #a0cfff;
  --yzh-color-primary-light-7: #c6e2ff;
  --yzh-color-primary-light-9: #ecf5ff;
  --yzh-color-primary-dark-2: #337ecc;

  /* 语义色 */
  --yzh-color-success: #67c23a;
  --yzh-color-success-light-9: #f0f9eb;
  --yzh-color-warning: #e6a23c;
  --yzh-color-warning-light-9: #fdf6ec;
  --yzh-color-danger: #f56c6c;
  --yzh-color-danger-light-9: #fef0f0;
  --yzh-color-info: #909399;
  --yzh-color-info-light-9: #f4f4f5;

  /* 中性色（文字层级，对齐 vidlang textPrimary/Secondary/Weak） */
  --yzh-color-text-primary: #303133;
  --yzh-color-text-regular: #606266;
  --yzh-color-text-secondary: #909399;
  --yzh-color-text-disabled: #c0c4cc;
  --yzh-color-text-placeholder: #a8abb2;

  /* 表面层级 */
  --yzh-color-bg-page: #f5f7fa;        /* 页面灰底 */
  --yzh-color-bg-card: #ffffff;        /* 卡片白 */
  --yzh-color-bg-hover: #f5f7fa;       /* 悬停 */
  --yzh-color-bg-active: #ecf5ff;      /* 选中 */

  /* 边框 */
  --yzh-color-border: #e4e7ed;
  --yzh-color-border-light: #ebeef5;
  --yzh-color-border-lighter: #f0f0f0;
}
```

### 4.2 yzh-spacing.css（对齐 vidlang app_spacing.dart，4px 网格）

```css
:root {
  /* 基础档位 */
  --yzh-space-1: 4px;
  --yzh-space-2: 8px;
  --yzh-space-3: 12px;
  --yzh-space-4: 16px;
  --yzh-space-5: 20px;
  --yzh-space-6: 24px;
  --yzh-space-8: 32px;
  --yzh-space-12: 48px;

  /* 语义档位 */
  --yzh-space-page: var(--yzh-space-4) var(--yzh-space-6);  /* 页面留白 16/24 */
  --yzh-space-gap: var(--yzh-space-4);                      /* 卡片间隙 16 */
  --yzh-space-card-pad: var(--yzh-space-4);                 /* 卡片内边距 */
  --yzh-space-header-pad: var(--yzh-space-3) var(--yzh-space-5);
  --yzh-space-section-gap: var(--yzh-space-6);              /* 区块间距 */
}
```

### 4.3 yzh-radius.css（对齐 vidlang app_radius.dart）

```css
:root {
  --yzh-radius-none: 0;
  --yzh-radius-xs: 2px;       /* 小标签 */
  --yzh-radius-sm: 4px;       /* 卡片/按钮（认证平台基线） */
  --yzh-radius-md: 6px;       /* 输入框/表格 */
  --yzh-radius-lg: 8px;       /* 弹窗/大卡片 */
  --yzh-radius-xl: 12px;      /* 大弹窗 */
  --yzh-radius-full: 9999px;  /* 圆点/头像 */
}
```

### 4.4 yzh-shadows.css

```css
:root {
  --yzh-shadow-none: none;
  --yzh-shadow-sm: 0 1px 4px rgba(0, 0, 0, 0.04);    /* 卡片 */
  --yzh-shadow-md: 0 2px 12px rgba(0, 0, 0, 0.06);   /* 悬浮卡片 */
  --yzh-shadow-lg: 0 6px 24px rgba(0, 0, 0, 0.10);   /* 弹窗 */
}
```

### 4.5 yzh-typography.css

```css
:root {
  --yzh-font-size-xs: 12px;
  --yzh-font-size-sm: 13px;
  --yzh-font-size-md: 14px;
  --yzh-font-size-lg: 16px;
  --yzh-font-size-xl: 18px;
  --yzh-font-weight-regular: 400;
  --yzh-font-weight-medium: 500;
  --yzh-font-weight-bold: 600;
  --yzh-line-height-base: 1.6;
}
```

### 4.6 yzh.css 统一出口（对齐 design_tokens.dart）

```css
@import './yzh-colors.css';
@import './yzh-spacing.css';
@import './yzh-radius.css';
@import './yzh-shadows.css';
@import './yzh-typography.css';

:root {
  /* 断点（响应式） */
  --yzh-bp-sm: 768px;
  --yzh-bp-md: 1280px;
  --yzh-bp-lg: 1600px;

  /* z-index 层级（对齐 vidlang zIndex map） */
  --yzh-z-dropdown: 100;
  --yzh-z-sticky: 200;
  --yzh-z-fixed: 300;
  --yzh-z-modal: 500;
  --yzh-z-notification: 800;

  /* 动画时长 */
  --yzh-duration-fast: 0.15s;
  --yzh-duration-normal: 0.3s;
  --yzh-duration-slow: 0.5s;
}
```

**引用规则**：业务页面只 `@import '@/yzh/styles/yzh.css'`，禁止单独引用子文件（对齐 barrel 规则）。

---

## 五、统一图标管理

### 5.1 yzh/icons/index.js（对齐 vidlang app_icons.dart）

```js
import {
  ArrowLeft, ArrowRight, Menu, Close,
  Plus, Delete, Edit, Search, Refresh, Download, Upload, Save,
  CopyDocument, Folder, FolderOpened, Document, DocumentChecked,
  Check, CircleCheck, Close as CloseBold, Warning, InfoFilled, Clock, Loading,
  MagicStick, ChatDotRound, QuestionFilled, Setting,
} from '@element-plus/icons-vue'

/** YZH 统一图标表：页面禁止直接 import 图标库，一律经此语义命名访问 */
export const YzhIcon = {
  /* 导航 */
  back: ArrowLeft, forward: ArrowRight, menu: Menu, close: Close,
  /* 操作 */
  add: Plus, delete: Delete, edit: Edit, search: Search, refresh: Refresh,
  download: Download, upload: Upload, save: Save, copy: CopyDocument,
  /* 文件 */
  folder: Folder, folderOpen: FolderOpened, file: Document, fileChecked: DocumentChecked,
  /* 状态 */
  success: Check, error: CloseBold, warning: Warning, info: InfoFilled,
  loading: Loading, pending: Clock, help: QuestionFilled, setting: Setting,
  /* AI */
  analyze: MagicStick, prompt: ChatDotRound,
}
```

**规则**：
1. 页面 `<el-icon><YzhIcon.back /></el-icon>`，**禁止** `import { ArrowLeft } from '@element-plus/icons-vue'`
2. 换图标 / 换图标库：只改 `yzh/icons/index.js` 一个文件
3. 语义命名必须表意（back 而非 arrow-left 业务无关名可保留，但页面层必须语义化）

### 5.2 certcore/icons/index.js（业务扩展）

```js
import { YzhIcon } from '@/yzh'
import { Files, Picture, VideoCamera, ... } from '@element-plus/icons-vue'

/** 文件扩展名 → 图标映射 */
export const CertFileIcon = { ... }
/** 文件扩展名 → 类型色 */
export const CertFileTypeColor = { ... }
```

---

## 六、基础组件库（yzh/components/ui）

| Vue 组件 | vidlang 对应 | 设计要点 |
|---------|-------------|---------|
| `YzhBaseCard` | BaseCard | variant: default/outlined/elevated/filled；padding/margin/borderRadius 默认取 `--yzh-*` 变量，不硬编码色值 |
| `YzhTitledCard` | TitledCard | title + subtitle + leadingIcon + actions 插槽 |
| `YzhEmptyState` | EmptyState | 默认（居中）/ compact / iconBackground 三模式；title/description/action |
| `YzhStatusBadge` | Badge | type: success/warning/danger/info 语义子类，颜色取 yzh-colors |

统一出口 `yzh/components/ui/index.js`（barrel）：**页面禁单独 import 子文件**。

---

## 七、certcore 项目级通用层

### 7.1 CertDirectoryTree（★ 复用核心）

```vue
<CertDirectoryTree
  v-model:selectedFile="file"         /* 选中文件 */
  mode="file"                         /* 'file' | 'folder' 选择模式 */
  :show-convert-badge="true"          /* 转换状态徽标 */
  :show-rule-status="true"            /* 规则状态点 */
  filterable                          /* 树内搜索 */
  @stage-load="onStageLoad"           /* 阶段懒加载完成 */
/>
```

- 内部组合：`useFileTree` + YzhIcon + CertConvertBadge + 规则状态点
- 数据契约：`{ id, name, type: organization|standard|stage|folder|file, fileCode, storagePath, convertedStoragePath, convertStatus, ruleStatus }`

### 7.2 CertDocPreview

从 `DocExtractionRule/components/DocPreview.vue` 提升，props 不变（`file`），下载按钮常驻 header。

### 7.3 CertConvertBadge

`{ status: pending|converting|converted|failed }` → YzhIcon + 语义色。

### 7.4 certcore/utils

| 文件 | 导出 | 消灭的重复 |
|------|------|-----------|
| format.js | formatFileSize / formatDate | DirectoryManager + DocExtractionRule 各一份 |
| api.js | unwrap(res) 响应解包 | 全站散落 |
| download.js | downloadFile(url, name) blob 下载 + Content-Disposition 文件名 | 两处手写 |
| convertStatus.js | convertStatusMap / convertStatusType | FileTree + uploadStatus 两套 |

---

## 八、与 vidlang 对照验证（完备性检查）

| vidlang 已有 | 本设计对应 | 状态 |
|-------------|-----------|------|
| design_tokens.dart（tokens+断点+zIndex+动画） | yzh.css | 设计完成 |
| app_icons.dart（语义化图标集中管理） | yzh/icons/index.js | 设计完成 |
| components/ui + barrel | yzh/components/ui + index.js | 设计完成 |
| ui-components-standard 规范文档 | 《yzh-基础组件标准规范-V1.md》 | 待编写 |
| 业务扩展（PngIcons/ResourceIcons） | certcore/icons | 设计完成 |

---

## 九、落地路线与验收

| 阶段 | 内容 | 验收标准 |
|------|------|---------|
| P1 | yzh/styles + yzh/icons + yzh/components/ui | 静态检查 0 报错；tokens 变量可用 |
| P2 | certcore 骨架 + utils + composables + 通用组件 | 静态检查 0 报错；组件可实例化 |
| P3 | 全面改造现有 cert 页面 | 页面接入 tokens/图标/组件；无 emoji 图标残留；布局统一 |

**代码红线（沿用项目全局规则）**：
- 不改 Vol 框架源码 / .jsx
- 不引入新依赖（纯 Element Plus + 现有栈）
- 改完只做静态 Diagnostics，不跑测试框架

---

**文档版本**：V1.0
**创建时间**：2026-08-12
**更新内容**：基于 vidlang 组件控制管理模式的 YZH 前端架构扩容 + certcore 项目级通用层设计
*（内容由AI生成，仅供参考）*
