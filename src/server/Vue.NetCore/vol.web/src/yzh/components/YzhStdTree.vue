<template>
  <div class="yzh-std-tree">
    <!-- 搜索框 -->
    <div class="tree-search">
      <el-input
        v-model="searchText"
        placeholder="搜索..."
        size="small"
        clearable
        :prefix-icon="IconSearch"
      />
    </div>

    <!-- 树容器 -->
    <div class="tree-body">
      <div v-for="org in filteredTree" :key="org.id" class="tree-group">
        <!-- 机构 -->
        <div class="tree-node level-0" @click="toggleExpand(org)">
          <el-icon class="tree-toggle" :class="{ expanded: org.expanded }">
            <IconForward />
          </el-icon>
          <el-icon class="tree-icon org"><IconOfficeBuilding /></el-icon>
          <span class="tree-label">{{ org.label }}</span>
          <el-badge
            v-if="org.children?.length"
            :value="org.children.length"
            type="info"
          />
        </div>

        <!-- 标准 -->
        <template v-if="org.expanded && org.children">
          <template v-for="std in org.children" :key="std.id">
            <div class="tree-node level-1" @click="toggleExpand(std)">
              <el-icon class="tree-toggle" :class="{ expanded: std.expanded }">
                <IconForward />
              </el-icon>
              <el-icon class="tree-icon standard"><IconFile /></el-icon>
              <span class="tree-label">{{ std.label }}</span>
              <el-badge
                v-if="std.children?.length"
                :value="std.children.length"
                type="info"
              />
            </div>

            <!-- 阶段 -->
            <div
              v-for="phase in std.children"
              :key="phase.id"
              class="tree-node level-2"
              :class="{ active: selectedId === phase.id }"
              @click="handleSelect(phase, std, org)"
            >
              <el-icon class="tree-toggle" style="visibility: hidden">
                <IconForward />
              </el-icon>
              <el-icon class="tree-icon phase"><IconCalendar /></el-icon>
              <span class="tree-label">{{ phase.label }}</span>
              <el-tag
                v-if="badgeField && phase[badgeField]"
                size="small"
                type="success"
                class="node-badge"
              >
                {{ phase[badgeField] }}
              </el-tag>
            </div>
          </template>
        </template>
      </div>

      <!-- 空状态 -->
      <div v-if="!filteredTree.length" class="tree-empty">
        <el-empty description="暂无数据" :image-size="60" />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, getCurrentInstance } from 'vue'
import {
  IconForward, IconSearch, IconFile, IconCalendar, IconOfficeBuilding
} from '@/yzh/icons'

const props = defineProps({
  /** 标题 */
  title: { type: String, default: '目录结构' },
  /** 接口地址，默认使用 organization-tree */
  apiUrl: { type: String, default: '/api/standard-directory/organization-tree' },
  /** 角标字段名（如 ruleCount, tplCount），不传则不显示 */
  badgeField: { type: String, default: '' },
  /** 是否自动加载 */
  autoLoad: { type: Boolean, default: true },
})

const emit = defineEmits([
  /** 选中阶段节点时触发，返回 { phase, standard, org } */
  'select',
  /** 树加载完成 */
  'loaded',
])

const { proxy } = getCurrentInstance()
const searchText = ref('')
const treeData = ref([])
const selectedId = ref('')

// 过滤搜索
const filteredTree = computed(() => {
  if (!searchText.value) return treeData.value
  const kw = searchText.value.toLowerCase()
  return treeData.value
    .map(org => {
      const stdMatched = (org.children || []).filter(std => {
        if (std.label?.toLowerCase().includes(kw)) return true
        return (std.children || []).some(p => p.label?.toLowerCase().includes(kw))
      })
      if (stdMatched.length === 0 && !org.label?.toLowerCase().includes(kw)) return null
      return { ...org, expanded: true, children: stdMatched }
    })
    .filter(Boolean)
})

// 加载树数据
async function loadTree() {
  try {
    const res = await proxy.http.get(props.apiUrl, null, false)
    const raw = res?.Data || res?.data || []
    treeData.value = raw.map(org => ({
      ...org,
      expanded: true,
      children: (org.children || []).map(std => ({
        ...std,
        expanded: false,
        children: std.children || []
      }))
    }))
    emit('loaded', treeData.value)
  } catch (e) {
  }
}

// 展开/折叠
function toggleExpand(node) {
  node.expanded = !node.expanded
}

// 选中阶段
function handleSelect(phase, std, org) {
  selectedId.value = phase.id
  emit('select', {
    phase,
    standard: std,
    org,
    // 便捷字段
    orgCode: org.cbCode || org.id,
    stdCode: std.stdCode || std.code || '',
    standardCode: phase.standardCode || std.standardCode,
    phaseCode: phase.phaseCode,
    phaseName: phase.phaseName || phase.label,
  })
}

// 暴露方法
defineExpose({
  reload: loadTree,
  clearSelection: () => { selectedId.value = '' },
})

onMounted(() => {
  if (props.autoLoad) loadTree()
})

// 外部 apiUrl 变化时重新加载
watch(() => props.apiUrl, () => {
  if (props.autoLoad) loadTree()
})
</script>

<style scoped lang="less">
.yzh-std-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: #fff;
}

.tree-search {
  padding: 16px;
  border-bottom: 1px solid #f1f5f9;

  :deep(.el-input__wrapper) {
    background: #f8fafc !important;
    border: 1px solid #e2e8f0 !important;
    box-shadow: none !important;
    border-radius: 12px !important;
    height: 40px !important;
    padding: 0 16px !important;

    &.is-focus {
      background: #fff !important;
      border-color: var(--yzh-color-primary) !important;
      box-shadow: 0 0 0 2px rgba(47, 84, 235, 0.1) !important;
    }
  }
}

.tree-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 8px;

  /* 滚动条美化 */
  &::-webkit-scrollbar {
    width: 6px;
  }
  &::-webkit-scrollbar-thumb {
    background: #e2e8f0;
    border-radius: 3px;
  }
  &::-webkit-scrollbar-track {
    background: transparent;
  }
}

.tree-group {
  margin-bottom: 4px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 16px;
  cursor: pointer;
  border-radius: 12px;
  margin-bottom: 2px;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  user-select: none;

  &:hover {
    background: #f1f5ff;
    .tree-toggle { color: var(--yzh-color-primary); }
  }

  &.level-0 {
    font-weight: 800;
    color: #0f172a;
    font-size: 15px;
  }

  &.level-1 {
    margin-left: 20px;
    font-weight: 700;
    color: #334155;
    font-size: 14px;
  }

  &.level-2 {
    margin-left: 40px;
    font-weight: 500;
    color: #64748b;
    font-size: 14px;

    &.active {
      background: #f0f5ff;
      color: var(--yzh-color-primary);
      font-weight: 800;
      box-shadow: inset 4px 0 0 var(--yzh-color-primary);
    }
  }
}

.tree-toggle {
  font-size: 14px;
  color: #94a3b8;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;

  &.expanded {
    transform: rotate(90deg);
    color: var(--yzh-color-primary);
  }
}

.tree-icon {
  font-size: 18px;
  flex-shrink: 0;
  opacity: 0.9;

  &.org { color: #2f54eb; }
  &.standard { color: #10b981; }
  &.phase { color: #f59e0b; }
}

.tree-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  letter-spacing: 0.2px;
}

:deep(.el-badge) {
  .el-badge__content {
    background: #f1f5f9 !important;
    color: #64748b !important;
    border: none !important;
    font-weight: 700 !important;
    height: 18px !important;
    line-height: 18px !important;
    padding: 0 6px !important;
    border-radius: 9px !important;
  }
}

.node-badge {
  background: rgba(16, 185, 129, 0.1) !important;
  color: #10b981 !important;
  border: none !important;
  font-weight: 800 !important;
  border-radius: 8px !important;
}

.tree-empty {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
