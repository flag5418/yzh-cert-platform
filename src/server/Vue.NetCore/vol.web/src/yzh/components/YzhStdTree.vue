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
    console.error('[YzhStdTree] 加载树失败:', e)
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
}

.tree-search {
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
}

.tree-body {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.tree-group {
  margin-bottom: 2px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  cursor: pointer;
  font-size: 13px;
  transition: background 0.2s;
  user-select: none;

  &:hover {
    background: #f5f7fa;
  }

  &.level-0 {
    font-weight: 600;
    color: #303133;
  }

  &.level-1 {
    padding-left: 28px;
    font-weight: 500;
    color: #606266;
  }

  &.level-2 {
    padding-left: 52px;
    color: #909399;

    &.active {
      background: #ecf5ff;
      color: #409eff;
      border-right: 3px solid #409eff;
    }
  }
}

.tree-toggle {
  font-size: 12px;
  color: #c0c4cc;
  transition: transform 0.2s;

  &.expanded {
    transform: rotate(90deg);
  }
}

.tree-icon {
  font-size: 14px;
  flex-shrink: 0;

  &.org { color: #409eff; }
  &.standard { color: #67c23a; }
  &.phase { color: #e6a23c; }
}

.tree-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-badge {
  margin-left: 4px;
  transform: scale(0.85);
}

.tree-empty {
  display: flex;
  justify-content: center;
  padding: 20px 0;
}
</style>
