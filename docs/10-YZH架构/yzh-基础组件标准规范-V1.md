# yzh 基础组件标准规范

> **版本**：V1.0 | **日期**：2026-08-12 | **状态**：成熟态（待实施）
>
> **定位**：定义 `src/yzh/components/ui/` 下标准组件的使用规范，确保项目 UI 代码一致性与可维护性。对齐 vidlang `ui-components-standard-V1.0.md` 模式。

---

## 一、概述与设计原则

### 1.1 设计原则

- **渐进式统一**：新代码必须使用标准组件，旧代码按迁移优先级逐步替换
- **语义化优先**：组件命名与参数清晰表达设计意图
- **令牌驱动**：组件样式一律取 `--yzh-*` CSS 变量，禁止硬编码色值/间距/圆角
- **barrel 导入**：统一通过 `yzh/components/ui/index.js` 导入，禁止单独 import 子文件

### 1.2 组件清单

| 组件 | 文件 | 用途 | 优先级 |
|------|------|------|--------|
| YzhBaseCard | YzhBaseCard.vue | 通用卡片容器 | ⭐⭐⭐ 必须 |
| YzhTitledCard | YzhTitledCard.vue | 带标题卡片 | ⭐⭐⭐ 必须 |
| YzhEmptyState | YzhEmptyState.vue | 空数据占位 | ⭐⭐⭐ 必须 |
| YzhStatusBadge | YzhStatusBadge.vue | 状态标签 | ⭐⭐ 推荐 |

---

## 二、YzhBaseCard 基础卡片

### 2.1 用途

统一卡片容器，支持 4 种视觉变体，替换全部手写"白底+圆角+边框"容器。

### 2.2 变体

| 变体 | 视觉 | 适用场景 |
|------|------|---------|
| `default` | 白底 + 轻微阴影 | 一般内容容器 |
| `outlined` | 白底 + 细边框 + 无阴影 | 表单区域、信息展示、三栏面板 |
| `elevated` | 白底 + 双层柔和阴影 | 可点击卡片、悬浮卡片 |
| `filled` | 自定义背景色 | 强调区域、品牌色背景 |

### 2.3 核心 Props

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| variant | String | 'default' | default/outlined/elevated/filled |
| padding | String | '--yzh-space-4' | 内边距（CSS 变量值） |
| margin | String | '' | 外边距 |
| borderRadius | String | '--yzh-radius-sm' | 圆角 |
| backgroundColor | String | '' | 覆盖背景色 |
| shadow | String | '' | 覆盖阴影 |
| showBorder | Boolean | false | 是否显示边框 |

### 2.4 使用边界

✅ 应该使用：设置分组、表单容器、统计卡片、三栏面板、任何"白底+圆角"容器

❌ 不应该使用：高度定制化业务卡片（内部有复杂布局的）、需要特殊交互的区域

---

## 三、YzhTitledCard 带标题卡片

### 3.1 用途

在 YzhBaseCard 基础上增加标题行，适用于设置分组、统计面板。

### 3.2 Props

| 参数 | 类型 | 说明 |
|------|------|------|
| title | String | 标题（必填） |
| subtitle | String | 副标题 |
| leadingIcon | Component | 标题前图标（经 YzhIcon 传入） |
| actions | Slot | 标题后操作区 |
| variant / padding / ... | 同 YzhBaseCard | 透传 |

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
| icon | Component | 必填 | 图标（经 YzhIcon 传入） |
| title | String | 必填 | 标题 |
| description | String | '' | 描述 |
| actionLabel | String | '' | 操作按钮文案 |
| compact | Boolean | false | 紧凑模式 |
| iconSize | Number | 48 | 图标大小 |
| iconColor | String | '--yzh-color-text-secondary' | 图标颜色 |

---

## 五、YzhStatusBadge 状态徽章

### 5.1 用途

状态标签组件，4 个语义类型，替换手写 `<el-tag>` 颜色逻辑与 emoji/文本字符状态。

### 5.2 语义类型

| type | 背景 | 文字 | 图标 | 适用场景 |
|------|------|------|------|---------|
| success | success-light-9 | success | YzhIcon.success | 成功/已配置 |
| warning | warning-light-9 | warning | YzhIcon.warning | 待处理/转换中 |
| danger | danger-light-9 | danger | YzhIcon.error | 失败 |
| info | info-light-9 | info | YzhIcon.info | 未配置/提示 |

### 5.3 Props

| 参数 | 类型 | 说明 |
|------|------|------|
| type | String | success/warning/danger/info |
| icon | Component | 覆盖默认图标 |
| size | String | small / default |

---

## 六、开发规范

### 6.1 Import 规范

```js
// ✅ 正确
import { YzhBaseCard, YzhEmptyState } from '@/yzh/components/ui'

// ❌ 错误（不要单独导入子文件）
import YzhBaseCard from '@/yzh/components/ui/YzhBaseCard.vue'
```

### 6.2 新页面开发检查清单

- [ ] 卡片容器是否使用 YzhBaseCard / YzhTitledCard？
- [ ] 空状态是否使用 YzhEmptyState？
- [ ] 状态标签是否使用 YzhStatusBadge？（禁止 emoji / 文本字符当状态）
- [ ] 图标是否经 YzhIcon 语义命名访问？（禁止直接 import 图标库）
- [ ] 样式是否引用 `@/yzh/styles/yzh.css` 令牌变量？（禁止硬编码色值/间距）
- [ ] 是否通过 barrel（index.js）统一导入？

### 6.3 旧代码迁移优先级

1. **P0 立即替换**：手写空状态 → YzhEmptyState（投入产出比最高）
2. **P1 逐步替换**：手写卡片容器 → YzhBaseCard（相关重构时顺带处理）
3. **P2 新代码强制**：新页面必须使用标准组件
4. **P3 保持不变**：高度定制化业务组件

---

## 七、版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| V1.0 | 2026-08-12 | 初始版本，定义 YzhBaseCard/YzhTitledCard/YzhEmptyState/YzhStatusBadge 规范 |

---

**维护者**：AI Coding Assistant
**审核状态**：待实施
*（内容由AI生成，仅供参考）*
