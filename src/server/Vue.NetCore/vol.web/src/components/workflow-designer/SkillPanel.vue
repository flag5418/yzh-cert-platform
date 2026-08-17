<template>
  <div class="skill-panel">
    <div class="panel-title">节点库</div>

    <!-- 特殊节点（内置，不落表） -->
    <div class="special-section">
      <div class="section-label">控制流节点</div>
      <div
        v-for="sp in specialNodes"
        :key="sp.nodeType"
        class="skill-item"
        draggable="true"
        @dragstart="onDragStart($event, sp)"
        @click="addNode(sp)"
      >
        <span class="skill-dot" :style="{ background: sp.color }"></span>
        <span class="skill-name">{{ sp.title }}</span>
        <span class="skill-code">{{ sp.nodeType }}</span>
      </div>
    </div>

    <!-- Skill 分类（动态） -->
    <div class="skill-search">
      <el-input v-model="searchText" placeholder="搜索 Skill..." clearable size="small" />
    </div>
    <div class="skill-categories">
      <div
        v-for="cat in filteredCategories"
        :key="cat.categoryCode"
        class="skill-category"
      >
        <div class="category-header" @click="cat.collapsed = !cat.collapsed">
          <span class="cat-dot" :style="{ background: cat.color || '#409EFF' }"></span>
          <span class="category-name">{{ cat.categoryName }}</span>
          <span class="category-count">{{ cat.skills.length }}</span>
        </div>
        <div v-show="!cat.collapsed" class="skill-list">
          <div
            v-for="skill in cat.skills"
            :key="skill.skillCode"
            class="skill-item"
            draggable="true"
            @dragstart="onDragStart($event, skill)"
            @click="addNode(skill)"
          >
            <span class="skill-dot" :style="{ background: cat.color || '#409EFF' }"></span>
            <span class="skill-name">{{ skill.skillName }}</span>
            <span class="skill-code">{{ skill.skillCode }}</span>
          </div>
        </div>
      </div>
      <div v-if="!filteredCategories.length" class="panel-empty">暂无 Skill（请在 Skill 管理中维护）</div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({
  /** api/skill/list-active 返回的启用 Skill（含 category） */
  skills: { type: Array, default: () => [] },
  /** api/skill-category/list 返回的分类（含颜色/图标/排序） */
  categories: { type: Array, default: () => [] }
})

const emit = defineEmits(['add-node'])

const searchText = ref('')

// 特殊节点：start / end / logic（V1.2 内置，端口契约与表注册一致，不落表）
const specialNodes = [
  { nodeType: 'start', title: '开始', color: '#67C23A' },
  { nodeType: 'end', title: '结束', color: '#F56C6C' },
  { nodeType: 'logic', title: '逻辑判断', color: '#E6A23C' }
]

const categoryState = ref({})

function catKey(code) { return code || '_default' }

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
  // 按分类 sort_order 排序
  result.sort((a, b) => {
    const ao = props.categories.find(c => c.categoryCode === a.categoryCode)?.sortOrder ?? 99
    const bo = props.categories.find(c => c.categoryCode === b.categoryCode)?.sortOrder ?? 99
    return ao - bo
  })
  return result
})

const filteredCategories = computed(() => {
  if (!searchText.value) return categoryMap.value
  const term = searchText.value.toLowerCase()
  return categoryMap.value
    .map(cat => ({
      ...cat,
      skills: cat.skills.filter(s =>
        (s.skillName || '').toLowerCase().includes(term) ||
        (s.skillCode || '').toLowerCase().includes(term)
      )
    }))
    .filter(cat => cat.skills.length > 0)
})

function onDragStart(event, item) {
  event.dataTransfer.setData('nodeData', JSON.stringify(item))
}

function addNode(item) {
  emit('add-node', { ...item })
}
</script>

<style scoped lang="less">
.skill-panel { display: flex; flex-direction: column; height: 100%; overflow: hidden; }
.panel-title { padding: 10px 12px; font-size: 14px; font-weight: 600; border-bottom: 1px solid #f0f0f0; }
.special-section { padding: 8px 12px; border-bottom: 1px solid #f0f0f0; }
.section-label { font-size: 12px; color: #909399; margin-bottom: 6px; }
.skill-search { padding: 8px 12px; border-bottom: 1px solid #f0f0f0; }
.skill-categories { flex: 1; overflow-y: auto; padding: 4px 0; }
.category-header { display: flex; align-items: center; gap: 6px; padding: 6px 12px; cursor: pointer; font-size: 13px; font-weight: 600; color: #606266; }
.cat-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; }
.category-count { margin-left: auto; font-size: 11px; color: #c0c4cc; }
.skill-item { display: flex; align-items: center; gap: 6px; padding: 6px 12px 6px 24px; cursor: pointer; font-size: 13px; color: #606266; transition: background .15s; }
.skill-item:hover { background: #ecf5ff; }
.skill-dot { width: 8px; height: 8px; border-radius: 50%; display: inline-block; flex-shrink: 0; }
.skill-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.skill-code { font-size: 11px; color: #c0c4cc; }
.panel-empty { padding: 20px; text-align: center; color: #c0c4cc; font-size: 12px; }
</style>
