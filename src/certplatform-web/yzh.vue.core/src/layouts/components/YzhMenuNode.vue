<script setup lang="ts">
/**
 * YzhMenuNode —— 侧边栏菜单节点的**递归渲染单元**（2026-10-02，框架层改造）
 *
 * ★ 为什么要有这个组件（框架层改造准入 · 理由）：
 *   `YzhAppLayout.vue` 原先在模板里硬编码「一级 `el-sub-menu` + 二级 `el-menu-item`」，
 *   **无递归**。而后端 `MenuController.BuildTree` 与前端 `useMenuTree` 都支持 N 级，
 *   于是菜单树第 3 级及以下的节点被**静默丢弃**（DOM 里根本没有，页面上表现为「菜单不见了」）。
 *   2026-10-02 后台菜单改为「业务管理 › 基础资料 › 认证机构管理」三级结构时暴露。
 *
 * ★ 同时修掉一个静默错跳：
 *   原模板写 `:index="menu.url || String(menu.id)"` —— 目录型菜单（`Url = NULL`）被误当叶子时，
 *   `url` 为空 → `index` 退化成 `Id` → `router` 模式下跳到 `/system/243` 之类的**不存在的路由**。
 *   本组件改为：**有子节点一律走 `el-sub-menu`（标题不可点）**；
 *   **无 Url 的叶子一律渲染为 `disabled`，绝不生成可跳转 index**。
 *
 * ★ 契约不变（准入 · 不破坏既有契约）：
 *   - 叶子 `index` 仍是 `menu.url`，`el-menu :router` 行为不变；
 *   - 子菜单 index 改用 `menu.code`（稳定业务键）而非 `String(menu.id)`，仅影响 el-menu 内部 key；
 *   - 菜单数据、路由表、ApiResponse 信封一概不动。
 *
 * ⚠️ prop 刻意命名为 `menu` 而非 `node`：
 *   守卫 **R2** 按变量名正则 `/\bnode\.(code|children|...)\b/` 拦截「核心 TreeNode 必须 PascalCase」
 *   的违规读取。本组件操作的是 `SysMenu`（**已登记例外**：菜单 DTO 为 camelCase，
 *   见 `api/system/menu.ts` 的 `toMenu()` 归一化），与 TreeNode 无关，故避开该变量名。
 */
import { formatMenuIcon } from '../../utils/menu'
import type { SysMenu } from '../../api/system/menu'

defineOptions({ name: 'YzhMenuNode' })

const props = defineProps<{
  /** 菜单节点（children 由 useMenuTree 已归一化为 camelCase 的树） */
  menu: SysMenu
}>()

/** 是否有下级 —— 有则渲染成可展开分组，绝不渲染成叶子 */
function hasChildren(menu: SysMenu): boolean {
  return Array.isArray(menu.children) && menu.children.length > 0
}

/** 叶子是否有可跳转地址 */
function isNavigable(menu: SysMenu): boolean {
  return typeof menu.url === 'string' && menu.url !== ''
}
</script>

<template>
  <!-- 有下级：分组容器（标题不可点，天然规避 Url 为空的错跳） -->
  <el-sub-menu v-if="hasChildren(props.menu)" :index="props.menu.code || String(props.menu.id)">
    <template #title>
      <el-icon v-if="props.menu.icon">
        <component :is="formatMenuIcon(props.menu.icon)" />
      </el-icon>
      <span>{{ props.menu.menuName }}</span>
    </template>
    <YzhMenuNode
      v-for="child in props.menu.children"
      :key="child.code || child.id"
      :menu="child"
    />
  </el-sub-menu>

  <!-- 叶子且有 Url：正常菜单项（router 模式，index 即路径） -->
  <el-menu-item
    v-else-if="isNavigable(props.menu)"
    :index="props.menu.url"
  >
    <el-icon v-if="props.menu.icon">
      <component :is="formatMenuIcon(props.menu.icon)" />
    </el-icon>
    <span>{{ props.menu.menuName }}</span>
  </el-menu-item>

  <!-- 叶子但无 Url：脏数据兜底。渲染为禁用项，绝不退化成 index=Id 去跳不存在的路由 -->
  <el-menu-item v-else disabled>
    <el-icon v-if="props.menu.icon">
      <component :is="formatMenuIcon(props.menu.icon)" />
    </el-icon>
    <span>{{ props.menu.menuName }}</span>
  </el-menu-item>
</template>
