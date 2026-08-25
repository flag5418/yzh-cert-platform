# @deprecated - Vol 框架基础组件

> **此目录已废弃**，请使用 `@/yzh/components/` 中的组件替代。

## 替代方案

| 原组件 | 替代组件 | 说明 |
|--------|----------|------|
| `VolTable` | `YzhDataTable` | 数据表格 |
| `VolForm` | `YzhFormGrid` + `YzhFormField` | 表单 |
| `VolBox` | `YzhEditDialog` | 编辑弹窗 |
| `VolHeader` | `YzhToolbar` | 工具栏 |
| `VolElementMenu` | Element Plus 原生 `el-menu` | 菜单 |

## 迁移指南

1. 新页面请使用 `@/yzh/` 中的组件
2. 旧页面逐步迁移，优先迁移高频使用的页面
3. 此目录将在所有页面迁移完成后删除

## 状态

- 创建时间：Vol 框架初始版本
- 废弃时间：2024年
- 计划删除：所有页面迁移到 `@/yzh/` 后
