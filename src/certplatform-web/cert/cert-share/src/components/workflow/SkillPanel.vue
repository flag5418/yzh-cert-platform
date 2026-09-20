<template>
  <div class="skill-panel">
    <div class="panel-title">节点库</div>
    <div class="skill-search"><el-input v-model="searchText" placeholder="搜索节点..." clearable size="small" /></div>
    <div class="skill-scroll">
      <div v-if="specialNodesForPanel.length > 0" class="skill-category">
        <div class="category-header"><span class="cat-dot" style="background: #64748b"></span><span class="category-name">控制流节点</span><span class="category-count">{{ specialNodesForPanel.length }}</span></div>
        <div class="skill-list">
          <div v-for="sp in specialNodesForPanel" :key="sp.classCode" class="skill-panel-item" draggable="true" @dragstart="onDragStart($event, sp)" @click="addNode(sp)">
            <div class="skill-dot" :style="{ background: sp.color }">
              <el-icon v-if="sp.classCode === 'start'"><VideoPlay /></el-icon>
              <el-icon v-else-if="sp.classCode === 'end'"><SwitchButton /></el-icon>
              <el-icon v-else-if="sp.classCode === 'branch'"><Share /></el-icon>
              <el-icon v-else-if="sp.classCode === 'ai_node'"><Cpu /></el-icon>
              <span v-else>{{ (sp.className || '?').charAt(0) }}</span>
            </div>
            <span class="skill-name">{{ sp.className }}</span>
          </div>
        </div>
      </div>
      <div class="skill-categories">
        <div v-for="cat in filteredCategories" :key="cat.categoryCode" class="skill-category">
          <div class="category-header" @click="cat.collapsed = !cat.collapsed">
            <span class="cat-dot" :style="{ background: cat.color || '#409EFF' }"></span>
            <span class="category-name">{{ cat.categoryName }}</span>
            <span class="category-count">{{ cat.skills.length }}</span>
          </div>
          <div v-show="!cat.collapsed" class="skill-list">
            <div v-for="skill in cat.skills" :key="skill.skillCode" class="skill-panel-item" draggable="true" @dragstart="onDragStart($event, skill)" @click="addNode(skill)">
              <div class="skill-dot" :style="{ background: cat.color || '#409EFF' }">{{ (skill.skillName || skill.Name || '?').charAt(0) }}</div>
              <span class="skill-name">{{ skill.skillName }}</span>
            </div>
          </div>
        </div>
        <div v-if="!filteredCategories.length" class="panel-empty">暂无 Skill（请在 Skill 管理中维护）</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { SPECIAL_NODES } from '@share/composables/workflow/specialNodes'
import { Cpu, VideoPlay, Share, SwitchButton } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'

const props = defineProps<{ skills?: any[]; categories?: any[] }>()
const emit = defineEmits<{ 'add-node': [item: any] }>()
const searchText = ref('')
const specialNodesForPanel = SPECIAL_NODES.filter(n => n.classCode !== 'start' && n.classCode !== 'loop')

const categoryMap = computed(() => {
  const map: Record<string, any> = {}; for (const c of (props.categories || [])) map[c.categoryCode] = c
  const groups: Record<string, any> = {}
  for (const s of (props.skills || [])) {
    const code = s.category || '_default'
    if (!groups[code]) groups[code] = { categoryCode: code, categoryName: map[code]?.categoryName || code, color: map[code]?.color || '#409EFF', collapsed: false, skills: [] }
    groups[code].skills.push(s)
  }
  const result = Object.values(groups)
  result.sort((a, b) => (props.categories?.find((c: any) => c.categoryCode === a.categoryCode)?.sortOrder ?? 99) - (props.categories?.find((c: any) => c.categoryCode === b.categoryCode)?.sortOrder ?? 99))
  return result
})

const filteredCategories = computed(() => {
  if (!searchText.value) return categoryMap.value
  const term = searchText.value.toLowerCase()
  return categoryMap.value.map((cat: any) => ({ ...cat, skills: cat.skills.filter((s: any) => (s.skillName || '').toLowerCase().includes(term) || (s.skillCode || '').toLowerCase().includes(term)) })).filter((cat: any) => cat.skills.length > 0)
})

function onDragStart(event: DragEvent, item: any) { event.dataTransfer?.setData('nodeData', JSON.stringify(item)) }
function addNode(item: any) { emit('add-node', { ...item }) }
</script>

<style scoped lang="less">
.skill-panel { display: flex; flex-direction: column; height: 100%; overflow: hidden; background: #fff; }
.panel-title { padding: 24px 20px; font-size: 18px; font-weight: 800; border-bottom: 1px solid #f1f5f9; flex-shrink: 0; color: var(--yzh-color-text-primary); }
.skill-search { padding: 16px 20px; border-bottom: 1px solid #f1f5f9; flex-shrink: 0; }
.skill-scroll { flex: 1; overflow-y: auto; min-height: 0; padding: 12px 0; }
.skill-categories { padding: 16px 0; }
.category-header { display: flex; align-items: center; gap: 10px; padding: 12px 20px; cursor: pointer; font-size: 14px; font-weight: 700; color: var(--yzh-color-text-regular); transition: all 0.2s; &:hover { background: #f8fafc; } }
.cat-dot { width: 10px; height: 10px; border-radius: 3px; display: inline-block; }
.category-count { margin-left: auto; font-size: 12px; color: #cbd5e1; background: #f1f5f9; padding: 2px 8px; border-radius: 10px; }
.skill-dot { width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 14px; font-weight: 700; flex-shrink: 0; }
.skill-name { font-size: 12px; font-weight: 600; color: var(--yzh-color-text-primary); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; text-align: center; max-width: 100%; }
.panel-empty { padding: 40px 20px; text-align: center; color: #cbd5e1; font-size: 14px; }
.skill-panel-item { display: flex; flex-direction: column; align-items: center; gap: 6px; padding: 12px 8px; cursor: pointer; border-radius: 8px; transition: all 0.15s; border: 1px solid transparent; &:hover { background: #f0f7ff; border-color: #bfdbfe; } }
.skill-list { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; padding: 0 12px; }
.skill-dot { width: 36px; height: 36px; border-radius: 10px; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 14px; font-weight: 700; flex-shrink: 0; }
</style>
