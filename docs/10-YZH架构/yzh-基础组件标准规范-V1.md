# yzh 基础组件标准规范

> **版本**：V1.1 | **日期**：2026-10-03 | **状态**：**已按代码实测修正**（V1.0 描述的 `YzhBaseCard`/`YzhTitledCard`/`YzhIcon` 三个组件**实际不存在**，代码只有 `YzhCard`/`YzhEmptyState`/`YzhStatusBadge` 三个）
>
> **目录路径勘误**：V1.0 写的 `src/yzh/components/ui/` 是旧结构；实际为 **`yzh.vue.core/src/components/ui/`**，导出 barrel = `components/ui/index.ts`。
>
> **定位**：定义 `src/yzh/components/ui/` 下标准组件的使用规范，确保项目 UI 代码一致性与可维护性。对齐 vidlang `ui-components-standard-V1.0.md` 模式。

---

## 一、概述与设计原则

### 1.1 设计原则

- **渐进式统一**：新代码必须使用标准组件，旧代码按迁移优先级逐步替换
- **语义化优先**：组件命名与参数清晰表达设计意图
- **令牌驱动**：组件样式一律取 `--yzh-*` CSS 变量（**必带兜底** `var(--yzh-*, <字面量>)`），禁止硬编码色值/间距/圆角 —— 映射表与守卫见 **[`25-样式规范与硬编码治理-V1.md`](./25-样式规范与硬编码治理-V1.md)**（`npm run guard` 的 R18/R19 拦截）
- **barrel 导入**：统一通过 `yzh/components/ui/index.js` 导入，禁止单独 import 子文件

### 1.2 组件清单

| 组件 | 文件 | 用途 | 优先级 |
|------|------|------|--------|
| **YzhCard** | `ui/YzhCard.vue` | 卡片容器（`title` + `#header`/`#footer` 插槽） | ⭐⭐⭐ 必须 |
| **YzhEmptyState** | `ui/YzhEmptyState.vue` | 空数据占位 | ⭐⭐⭐ 必须 |
| **YzhStatusBadge** | `ui/YzhStatusBadge.vue` | 状态徽章（success/warning/danger/info） | ⭐⭐ 推荐 |

> ⛔ **不存在**：`YzhBaseCard`、`YzhTitledCard`、`YzhIcon`（V1.0 幻影组件，勿 import）。
> 图标统一用 `@element-plus/icons-vue` 组件，经 `YzhStatusBadge`/`YzhEmptyState` 的 `icon` prop 传入。

---

## 二、YzhCard 卡片容器

### 2.1 用途

统一卡片容器，替换全部手写"白底+圆角+边框"容器。

### 2.2 实际 Props（★ 以 `YzhCard.vue` 为准，V1.0 的 `variant`/`padding`/`margin`/`borderRadius`/`backgroundColor`/`shadow`/`showBorder` **均不存在**）

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| title | String | `''` | 标题；有值或有 `#header` 插槽才渲染头部分隔条 |

### 2.3 插槽

| 插槽 | 用途 |
|------|------|
| `header` | 覆盖 `title` 文本（放操作按钮也在这） |
| 默认 | 卡片主体 |
| `footer` | 底部分隔区（有内容才渲染） |

### 2.4 样式（令牌驱动）

```css
.yzh-card            { background: var(--yzh-color-bg-card, #fff);
                       border: 1px solid var(--yzh-color-border-light, #ebeef5); }
.yzh-card__header    { padding: 16px 20px; font-size: 14px; font-weight: 600;
                       color: var(--yzh-color-text-primary, #303133); }
.yzh-card__footer    { background: var(--yzh-color-bg-subtle, #fafafa); }
```

### 2.5 使用边界

✅ 应该使用：设置分组、表单容器、统计卡片、三栏面板、任何"白底+圆角"容器

❌ 不应该使用：高度定制化业务卡片（内部有复杂布局的）、需要特殊交互的区域

---

## 三、（已并入 §二）

> **V1.0 的「YzhTitledCard」不存在** —— 带标题卡片 = `YzhCard` + `title` / `#header` 插槽，无独立组件。

---

## 四、YzhEmptyState 空状态

### 4.1 用途

标准空数据占位组件，替换全部手写空态。

### 4.2 三种模式

| 模式 | 说明 | 适用场景 |
|------|------|---------|
| 默认 | 居中显示 | 全屏空状态 |
| compact | 无居中包裹 | 列表内部（配合 flex 布局） |
| iconBackground | 图标外圆形容器 | 文件夹/文件列表空态 |

### 4.3 Props

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| icon | Component | 必填 | 图标组件（`@element-plus/icons-vue` 导入后传入，**无 YzhIcon**） |
| title | String | 必填 | 标题 |
| description | String | '' | 描述 |
| actionLabel | String | '' | 操作按钮文案 |
| compact | Boolean | false | 紧凑模式 |
| iconSize | Number | 48 | 图标大小 |
| iconColor | String | `'var(--yzh-color-text-secondary)'` | 图标颜色（已令牌化） |
| onAction | Function | null | `actionLabel` 按钮点击回调（不传则按钮不渲染） |
| iconBackgroundColor | String | '' | 传入则渲染图标外圆形容器背景色 |
| iconBackgroundPadding | String | '20px' | 圆形容器内边距 |

---

## 五、YzhStatusBadge 状态徽章

### 5.1 用途

状态标签组件，4 个语义类型，替换手写 `<el-tag>` 颜色逻辑与 emoji/文本字符状态。

### 5.2 语义类型

| type | 背景 | 文字 | 图标 | 适用场景 |
|------|------|------|------|---------|
| success | success-light-9 | success | 无默认图标（`icon` prop 传入 `CircleCheckFilled`） | 成功/已配置 |
| warning | warning-light-9 | warning | 无默认图标（`WarningFilled`） | 待处理/转换中 |
| danger | danger-light-9 | danger | 无默认图标（`CircleCloseFilled`） | 失败 |
| info | info-light-9 | info | 无默认图标（`InfoFilled`） | 未配置/提示 |

### 5.3 Props

| 参数 | 类型 | 说明 |
|------|------|------|
| type | String | success/warning/danger/info |
| icon | Component | 覆盖默认图标 |
| size | String | small / default |

---

## 六、开发规范

### 6.1 Import 规范

```ts
// ✅ 正确 —— barrel
import { YzhCard, YzhEmptyState, YzhStatusBadge } from '@yzh-core/components/ui'

// ✅ 也可从包根导出（yzh.vue.core/src/index.ts:37）
import { YzhCard } from '@yzh-core'

// ❌ 错误（幻影组件，文件不存在）
import { YzhBaseCard } from '@yzh-core/components/ui'
```

### 6.2 新页面开发检查清单

- [ ] 卡片容器是否使用 `YzhCard`？
- [ ] 空状态是否使用 YzhEmptyState？
- [ ] 状态标签是否使用 YzhStatusBadge？（禁止 emoji / 文本字符当状态）
- [ ] 图标是否从 `@element-plus/icons-vue` 取并经 `icon` prop 传入？（禁止 emoji/文本字符当状态）
- [ ] 样式是否一律 `var(--yzh-*, 兜底)`？（裸 hex/裸 px 由 `npm run guard` 的 R18 拦截）
- [ ] 是否通过 barrel `components/ui/index.ts` 统一导入？

### 6.3 旧代码迁移优先级

1. **P0 立即替换**：手写空状态 → YzhEmptyState（投入产出比最高）
2. **P1 逐步替换**：手写卡片容器 → `YzhCard`（相关重构时顺带处理）
3. **P2 新代码强制**：新页面必须使用标准组件
4. **P3 保持不变**：高度定制化业务组件

---

## 七、版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| V1.0 | 2026-08-12 | 初始版本（含 3 个**不存在**的组件名，见 V1.1 勘误） |
| V1.1 | 2026-10-03 | 按代码实测勘误：删除幻影组件 YzhBaseCard/YzhTitledCard/YzhIcon，改为 `YzhCard`（title + 3 插槽）；路径改 `yzh.vue.core/src/components/ui/`；补令牌 + R18/R19 守卫链接 |

---

**维护者**：AI Coding Assistant
**审核状态**：已按 2026-10-03 代码实测修正
*（内容由AI生成，仅供参考）*
