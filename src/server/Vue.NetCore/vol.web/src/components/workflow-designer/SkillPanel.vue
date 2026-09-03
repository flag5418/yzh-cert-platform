<template>
  <div class="skill-panel">
    <div class="panel-title">节点库</div>

    <!-- 搜索框（置顶） -->
    <div class="skill-search">
      <el-input v-model="searchText" placeholder="搜索节点..." clearable size="small" />
    </div>

    <!-- 统一滚动区域：特殊节点 + 动态 Skill -->
    <div class="skill-scroll">
      <!-- 控制流节点（内置） -->
      <div v-if="specialNodesForPanel.length > 0" class="skill-category">
        <div class="category-header">
          <span class="cat-dot" style="background: #64748b"></span>
          <span class="category-name">控制流节点</span>
          <span class="category-count">{{ specialNodesForPanel.length }}</span>
        </div>
        <div class="skill-list">
          <div
            v-for="sp in specialNodesForPanel"
            :key="sp.classCode"
            class="skill-panel-item"
            draggable="true"
            @dragstart="onDragStart($event, sp)"
            @click="addNode(sp)"
          >
            <div class="skill-dot" :style="{ background: sp.color }">
              <el-icon v-if="sp.classCode === 'start'"><IconPlay /></el-icon>
              <el-icon v-else-if="sp.classCode === 'end'"><IconSwitchButton /></el-icon>
              <el-icon v-else-if="sp.classCode === 'branch'"><IconShare /></el-icon>
              <el-icon v-else-if="sp.classCode === 'ai_node'"><IconCpu /></el-icon>
              <span v-else>{{ sp.className.charAt(0) }}</span>
            </div>
            <span class="skill-name">{{ sp.className }}</span>
          </div>
        </div>
      </div>

      <!-- Skill 分类（动态） -->
      <div class="skill-categories">
        <div v-for="cat in filteredCategories" :key="cat.categoryCode" class="skill-category">
          <div class="category-header" @click="cat.collapsed = !cat.collapsed">
            <span class="cat-dot" :style="{ background: cat.color || '#409EFF' }"></span>
            <span class="category-name">{{ cat.categoryName }}</span>
            <span class="category-count">{{ cat.skills.length }}</span>
          </div>
          <div v-show="!cat.collapsed" class="skill-list">
            <div
              v-for="skill in cat.skills"
              :key="skill.skillCode"
              class="skill-panel-item"
              draggable="true"
              @dragstart="onDragStart($event, skill)"
              @click="addNode(skill)"
            >
              <div class="skill-dot" :style="{ background: cat.color || '#409EFF' }">
                {{ skill.skillName.charAt(0) }}
              </div>
              <span class="skill-name">{{ skill.skillName }}</span>
            </div>
          </div>
        </div>
        <div v-if="!filteredCategories.length" class="panel-empty">
          暂无 Skill（请在 Skill 管理中维护）
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { SPECIAL_NODES } from '@/views/cert/Standard/WorkflowDesigner/specialNodes.js'
import { IconCpu, IconPlay, IconShare, IconSwitchButton } from '@/yzh/icons'
import { computed, ref } from 'vue'

const props = defineProps({
  /** api/skill/query-nodes 返回的启用 Skill（含 category） */
  skills: { type: Array, default: () => [] },
  /** api/skill-category/list 返回的分类（含颜色/图标/排序） */
  categories: { type: Array, default: () => [] }
})

const emit = defineEmits(['add-node'])

const searchText = ref('')

// 特殊节点从统一元数据导入
// 排除 start（自动创建）和 loop（已废弃）
const specialNodesForPanel = SPECIAL_NODES.filter(
  (n) => n.classCode !== 'start' && n.classCode !== 'loop'
)

const categoryState = ref({})

// 分类 → 该分类下 skills（保留分类颜色）
const categoryMap = computed(() => {
  const map = {}
  for (const c of props.categories) map[c.categoryCode] = c
  const result = []
  const groups = {}
  for (const s of props.skills) {
    const code = s.category || '_default'
    if (!groups[code]) {
      groups[code] = {
        categoryCode: code,
        categoryName: map[code]?.categoryName || code,
        color: map[code]?.color || '#409EFF',
        collapsed: false,
        skills: []
      }
    }
    groups[code].skills.push(s)
  }
  for (const code of Object.keys(groups)) {
    result.push(groups[code])
  }
  result.sort((a, b) => {
    const ao = props.categories.find((c) => c.categoryCode === a.categoryCode)?.sortOrder ?? 99
    const bo = props.categories.find((c) => c.categoryCode === b.categoryCode)?.sortOrder ?? 99
    return ao - bo
  })
  return result
})

const filteredCategories = computed(() => {
  if (!searchText.value) return categoryMap.value
  const term = searchText.value.toLowerCase()
  return categoryMap.value
    .map((cat) => ({
      ...cat,
      skills: cat.skills.filter(
        (s) =>
          (s.skillName || '').toLowerCase().includes(term) ||
          (s.skillCode || '').toLowerCase().includes(term)
      )
    }))
    .filter((cat) => cat.skills.length > 0)
})

function onDragStart(event, item) {
  event.dataTransfer.setData('nodeData', JSON.stringify(item))
}

function addNode(item) {
  emit('add-node', { ...item })
}
</script>

<style scoped lang="less">
.skill-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: #fff;
}
.panel-title {
  padding: 24px 20px;
  font-size: 18px;
  font-weight: 800;
  border-bottom: 1px solid #f1f5f9;
  flex-shrink: 0;
  color: var(--yzh-color-text-primary);
}
.skill-search {
  padding: 16px 20px;
  border-bottom: 1px solid #f1f5f9;
  flex-shrink: 0;
}
.skill-scroll {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
  padding: 12px 0;
}
.special-section {
  padding: 0 0 16px 0;
  border-bottom: 1px solid #f1f5f9;
}
.section-label {
  font-size: 12px;
  color: #94a3b8;
  margin: 0 20px 12px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 1px;
}
.skill-categories {
  padding: 16px 0;
}
.category-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 20px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 700;
  color: var(--yzh-color-text-regular);
  transition: all 0.2s;
}
.category-header:hover {
  background: #f8fafc;
}
.cat-dot {
  width: 10px;
  height: 10px;
  border-radius: 3px;
  display: inline-block;
}
.category-count {
  margin-left: auto;
  font-size: 12px;
  color: #cbd5e1;
  background: #f1f5f9;
  padding: 2px 8px;
  border-radius: 10px;
}

.skill-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}
.skill-dot {
  width: 12px;
  height: 12px;
  border-radius: 4px;
  display: inline-block;
  flex-shrink: 0;
}
.skill-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--yzh-color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.skill-code {
  font-size: 11px;
  color: #94a3b8;
  font-family: monospace;
}
.panel-empty {
  padding: 40px 20px;
  text-align: center;
  color: #cbd5e1;
  font-size: 14px;
}
</style>
