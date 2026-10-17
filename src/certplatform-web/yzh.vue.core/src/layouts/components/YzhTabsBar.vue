<template>
  <div v-if="tabs.length > 0" class="yzh-tabs">
    <el-tabs
      :model-value="activePath"
      type="card"
      class="yzh-tabs__list"
      @tab-click="onTabClick"
      @tab-remove="onTabRemove"
    >
      <el-tab-pane
        v-for="tab in tabs"
        :key="tab.path"
        :name="tab.path"
        :label="tab.title"
        :closable="tab.closable"
      />
    </el-tabs>

    <el-dropdown class="yzh-tabs__actions" @command="onCommand">
      <el-button text type="primary">
        批量关闭
        <el-icon class="yzh-tabs__caret"><ArrowDown /></el-icon>
      </el-button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item command="others">关闭其他</el-dropdown-item>
          <el-dropdown-item command="right">关闭右侧</el-dropdown-item>
          <el-dropdown-item command="all">关闭全部</el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </div>
</template>

<script setup lang="ts">
import { watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown } from '@element-plus/icons-vue'
import type { TabsPaneContext } from 'element-plus'
import { useTabs } from '../../composables/useTabs'
import { useMenuTree } from '../../composables/useMenuTree'

/**
 * 多标签页栏 —— 解决「离开一个页面后想回去，只能从菜单重新找一遍」
 *
 * 【铁律】① 同一 path **不重复**（`open` 幂等）② 支持**批量关闭**（其他 / 右侧 / 全部）
 * 【标题来源】菜单树按 path 匹配 `menuName` —— 与 `YzhAppLayout` 的面包屑**同源同算法**
 *   （同一份 `useMenuTree` 菜单树、同一个「按 `url === path` 递归查找」写法），
 *   ⛔ 不另建一套标题映射（否则改菜单名会出现两处不一致）。
 *   ⚠️ 唯一差别：面包屑在**按 menuTag 过滤后**的树上找，本组件在**全量树**上找
 *   （本组件拿不到宿主的 menuTag；全量树是超集 ⇒ 只会更全，不会更少）。
 * 【⛔ 不控制缓存】标签只管导航；页面缓存保持「缓存所有访问过的页面」现状。
 *
 * 【为什么在 layouts/components/ 而非 components/layout/】
 *   本组件依赖 `vue-router`（`useRoute`/`useRouter`），属**布局内部件**；
 *   守卫 R1 要求 `components/` 下原子组件零领域依赖 ⇒ 与 `YzhMenuNode.vue` 同处。
 */
const route = useRoute()
const router = useRouter()
const { tabs, activePath, open, close, closeOthers, closeRight, closeAll, retitle } = useTabs()
const { menus } = useMenuTree()

/** 从菜单树按 path 找标题（与面包屑同一实现口径） */
function findTitle(path: string, list: typeof menus.value = menus.value): string | null {
  for (const m of list) {
    if (m.url === path) return m.menuName
    if (m.children?.length) {
      const found = findTitle(path, m.children)
      if (found) return found
    }
  }
  return null
}

/** 路由变化 ⇒ 打开（或激活）标签；菜单未加载时先用 path 占位，稍后由下方 watch 回填 */
watch(
  () => route.path,
  (path) => {
    open(path, findTitle(path) ?? path)
  },
  { immediate: true },
)

/** 菜单是异步加载的 ⇒ 到位后把占位标题回填成菜单名 */
watch(
  () => menus.value,
  () => {
    tabs.value.forEach((t) => {
      const title = findTitle(t.path)
      if (title) retitle(t.path, title)
    })
  },
)

function onTabClick(pane: TabsPaneContext) {
  const path = String(pane.paneName ?? '')
  if (path && path !== route.path) void router.push(path)
}

function onTabRemove(name: string | number) {
  const next = close(String(name))
  if (next) void router.push(next)
}

function onCommand(command: string) {
  const current = route.path
  if (command === 'others') {
    closeOthers(current)
  } else if (command === 'right') {
    closeRight(current)
  } else if (command === 'all') {
    void router.push(closeAll())
  }
}
</script>

<style scoped>
.yzh-tabs {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2);
  padding: 0 var(--yzh-space-6);
  background: var(--yzh-color-bg-container);
  border-bottom: 1px solid var(--yzh-color-border-light);
}

.yzh-tabs__list {
  flex: 1;
  min-width: 0;
}

.yzh-tabs__actions {
  flex: none;
}

.yzh-tabs__caret {
  margin-left: var(--yzh-space-1);
}

/* 收掉 el-tabs 默认的外边距与底部横线 —— 标签栏自带下边框 */
.yzh-tabs__list :deep(.el-tabs__header) {
  margin: 0;
}

.yzh-tabs__list :deep(.el-tabs__nav-wrap)::after {
  height: 0;
}
</style>
