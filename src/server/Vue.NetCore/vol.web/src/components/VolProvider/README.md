# @deprecated - Vol 框架 Provider

> **此目录已废弃**，请使用 `@/yzh/` 中的状态管理方案替代。

## 替代方案

| 原组件 | 替代方案 | 说明 |
|--------|----------|------|
| `VolProvider.js` | Vue 3 Composition API | 状态管理 |
| `VolStoreCache.js` | `@/yzh/store/` | 缓存管理 |
| `VolPermission.js` | `@/yzh/core/` | 权限控制 |

## 迁移指南

1. 新页面请使用 Vue 3 Composition API
2. 旧页面逐步迁移
3. 此目录将在所有页面迁移完成后删除

## 状态

- 创建时间：Vol 框架初始版本
- 废弃时间：2024年
- 计划删除：所有页面迁移到 `@/yzh/` 后
