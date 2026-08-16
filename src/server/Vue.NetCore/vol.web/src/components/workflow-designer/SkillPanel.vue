<template>
  <div class="skill-panel">
    <div class="panel-title">Skill 节点库</div>
    <div class="skill-search">
      <el-input v-model="searchText" placeholder="搜索 Skill..." clearable size="small" />
    </div>
    <div class="skill-categories">
      <div
        v-for="cat in filteredCategories"
        :key="cat.key"
        class="skill-category"
      >
        <div class="category-header" @click="cat.collapsed = !cat.collapsed">
          <span class="category-icon">{{ cat.icon }}</span>
          <span class="category-name">{{ cat.name }}</span>
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
            <span class="skill-icon">{{ skill.icon }}</span>
            <span class="skill-name">{{ skill.skillName }}</span>
            <span class="skill-code">{{ skill.skillCode }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({
  skills: { type: Array, default: () => [] }
})

const emit = defineEmits(['add-node'])

const searchText = ref('')
const categories = ref([
  { key: 'data_access', name: '数据获取', icon: '📥', collapsed: false, skills: [] },
  { key: 'data_process', name: '数据处理', icon: '🔢', collapsed: false, skills: [] },
  { key: 'ai_judge', name: 'AI 判断', icon: '🧠', collapsed: false, skills: [] },
  { key: 'ai_generate', name: 'AI 生成', icon: '✍️', collapsed: false, skills: [] },
  { key: 'output', name: '结果输出', icon: '📤', collapsed: false, skills: [] }
])

const skillIcons = {
  get_field: '📋', get_table: '📊', compare: '⚖️', date_diff: '📅',
  text_merge: '🔗', llm_judge: '🤖', llm_generate: '✨',
  create_nc: '⚠️', save_result: '💾', assemble_text: '📝'
}

const skillCategories = {
  get_field: 'data_access', get_table: 'data_access',
  compare: 'data_process', date_diff: 'data_process', text_merge: 'data_process',
  llm_judge: 'ai_judge',
  llm_generate: 'ai_generate',
  create_nc: 'output', save_result: 'output', assemble_text: 'output'
}

// 将后端 Skill 数据映射到分类
const mappedSkills = computed(() => {
  const result = {}
  for (const skill of props.skills) {
    const cat = skillCategories[skill.skillCode] || 'data_process'
    if (!result[cat]) result[cat] = []
    result[cat].push({
      ...skill,
      icon: skillIcons[skill.skillCode] || '⚙️'
    })
  }
  return result
})

// 更新分类
for (const cat of categories.value) {
  cat.skills = mappedSkills.value[cat.key] || []
}

const filteredCategories = computed(() => {
  if (!searchText.value) return categories.value
  const term = searchText.value.toLowerCase()
  return categories.value.map(cat => ({
    ...cat,
    skills: cat.skills.filter(s =>
      s.skillName.toLowerCase().includes(term) ||
      s.skillCode.toLowerCase().includes(term)
    )
  })).filter(cat => cat.skills.length > 0)
})

function onDragStart(event, skill) {
  event.dataTransfer.setData('skillCode', skill.skillCode)
  event.dataTransfer.setData('skillName', skill.skillName)
}

function addNode(skill) {
  emit('add-node', skill)
}
</script>


