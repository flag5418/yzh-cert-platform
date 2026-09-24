<template>
  <div class="yzh-home">
    <!-- 欢迎区 -->
    <el-card shadow="never" class="yzh-home__welcome">
      <div class="welcome-row">
        <div class="welcome-text">
          <h2 class="welcome-greeting">{{ greeting }}，{{ displayName }}</h2>
          <p class="welcome-sub">{{ todayText }} · 欢迎回来</p>
        </div>
        <div class="welcome-badge">YZH</div>
      </div>
    </el-card>

    <!-- 快捷入口（读当前用户菜单树，按 menuTag 分流） -->
    <div class="yzh-home__entries">
      <h3 class="entries-title">快捷入口</h3>
      <div v-if="quickEntries.length" class="entries-grid">
        <router-link
          v-for="entry in quickEntries"
          :key="entry.code"
          :to="entry.url!"
          class="entry-card"
        >
          <el-icon class="entry-icon" :size="22">
            <component :is="formatMenuIcon(entry.icon)" />
          </el-icon>
          <span class="entry-name">{{ entry.menuName }}</span>
        </router-link>
      </div>
      <el-empty v-else description="暂无可用入口" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useAuthState } from '../../composables/useAuthState'
import { useMenuTree } from '../../composables/useMenuTree'
import { filterMenuTreeByTag, formatMenuIcon } from '../../utils/menu'

/**
 * 默认占位首页（系统底座默认件）
 *
 * 定位（D-3）：欢迎语 + 菜单快捷入口 + 版本感 —— **无业务图表**（业务仪表盘归宿主，覆盖 '/' 即可）。
 * createYzhRoutes 缺省把 '/' 指到本页；宿主可 per-path 手写替换。
 */

const props = withDefaults(defineProps<{
  /** 菜单 Tag 分流（与 YzhAppLayout 保持一致） */
  menuTag?: string
}>(), {
  menuTag: 'admin'
})

const { userInfo } = useAuthState()
const { menus, loadMenus } = useMenuTree()

const displayName = computed(() => userInfo.value?.UserTrueName || userInfo.value?.UserName || '用户')

const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 6) return '夜深了'
  if (h < 12) return '早上好'
  if (h < 14) return '中午好'
  if (h < 18) return '下午好'
  return '晚上好'
})

const todayText = computed(() => {
  const d = new Date()
  const week = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()]
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日 星期${week}`
})

/** 快捷入口：按 menuTag 分流后的全部带 URL 菜单（≤2 层拍平） */
const quickEntries = computed(() => {
  const out: { code: string; menuName: string; url?: string; icon?: string }[] = []
  const walk = (list: ReturnType<typeof filterMenuTreeByTag>) => {
    for (const m of list) {
      if (m.url && m.url !== '/') out.push(m)
      if (m.children?.length) walk(m.children)
    }
  }
  walk(filterMenuTreeByTag(menus.value, props.menuTag))
  return out
})

onMounted(() => {
  loadMenus()
})
</script>

<style scoped>
.yzh-home {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.yzh-home__welcome :deep(.el-card__body) {
  padding: 28px 32px;
}

.welcome-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.welcome-greeting {
  margin: 0 0 8px;
  font-size: 22px;
  font-weight: 700;
  color: #1e293b;
}

.welcome-sub {
  margin: 0;
  font-size: 13px;
  color: #64748b;
}

.welcome-badge {
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  font-weight: 700;
  color: #fff;
  background: linear-gradient(135deg, var(--yzh-color-primary, #2563eb), var(--yzh-color-primary-light, #3b82f6));
  border-radius: 14px;
  letter-spacing: 1px;
}

.entries-title {
  margin: 0 0 14px;
  font-size: 15px;
  font-weight: 600;
  color: #334155;
}

.entries-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 14px;
}

.entry-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 18px 16px;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  text-decoration: none;
  transition: all 0.2s;
}

.entry-card:hover {
  border-color: var(--yzh-color-primary, #2563eb);
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.12);
  transform: translateY(-2px);
}

.entry-icon {
  color: var(--yzh-color-primary, #2563eb);
  flex-shrink: 0;
}

.entry-name {
  font-size: 14px;
  color: #334155;
  font-weight: 500;
}
</style>
