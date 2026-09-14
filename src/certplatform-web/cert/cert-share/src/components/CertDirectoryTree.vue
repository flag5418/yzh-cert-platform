<template>
  <div class="cert-directory-tree">
    <div class="cert-directory-tree__header">
      <span class="cert-directory-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-directory-tree__search">
      <el-input v-model="filterText" placeholder="搜索文件夹/文件..." clearable size="small" />
    </div>

    <div v-loading="loading" class="cert-directory-tree__content">
      <el-tree
        ref="treeRef"
        :data="fileTreeData"
        :props="treeProps"
        :highlight-current="true"
        :expand-on-click-node="false"
        :filter-node-method="filterNode"
        node-key="id"
        @node-expand="onNodeExpand"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="cert-directory-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="cert-directory-tree__node-icon">
              <component :is="getNodeIconComponent(data)" />
            </el-icon>
            <span class="cert-directory-tree__node-label">{{ data.name }}</span>
            <CertConvertBadge v-if="showConvertBadge && data.type === 'file' && data.convertStatus" :status="data.convertStatus" />
          </div>
        </template>
      </el-tree>

      <el-empty v-if="!loading && fileTreeData.length === 0" description="暂无目录数据" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import {
  Folder,
  Document,
  OfficeBuilding,
  Collection,
} from '@element-plus/icons-vue'
import { useFileTree, type TreeNode } from '../composables/useFileTree'
import CertConvertBadge from './CertConvertBadge.vue'

defineProps<{
  title?: string
  filterable?: boolean
  showConvertBadge?: boolean
}>()

const emit = defineEmits<{
  select: [node: TreeNode]
  'node-click': [node: TreeNode]
}>()

const { fileTreeData, loading, loadTree, loadStageFiles, loadFolderFiles } = useFileTree()
const treeRef = ref()
const filterText = ref('')
const selectedId = ref<string | number | null>(null)

const treeProps = {
  children: 'children',
  label: 'name',
}

/** 根据节点类型返回图标组件 */
function getNodeIconComponent(data: TreeNode) {
  const iconMap: Record<string, any> = {
    organization: OfficeBuilding,
    standard: Collection,
    stage: Collection,
    folder: Folder,
    file: Document,
  }
  return iconMap[data.type] || Document
}

function isActive(data: TreeNode) {
  return selectedId.value === data.id
}

/** 懒加载：展开节点时加载子节点 */
function onNodeExpand(data: TreeNode) {
  if (data.type === 'stage' && !data._loaded) {
    loadStageFiles(data)
  } else if (data.type === 'folder' && !data._loaded) {
    loadFolderFiles(data)
  }
}

function onNodeClick(data: TreeNode) {
  selectedId.value = data.id
  emit('node-click', data)
  if (data.type === 'file') {
    emit('select', data)
  }
}

function filterNode(value: string, data: TreeNode) {
  if (!value) return true
  return data.name.toLowerCase().includes(value.toLowerCase())
}

watch(filterText, (val) => {
  treeRef.value?.filter(val)
})

onMounted(() => {
  loadTree()
})
</script>

<style scoped>
.cert-directory-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.cert-directory-tree__header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  font-weight: 600;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.cert-directory-tree__search {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.cert-directory-tree__content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.cert-directory-tree__node {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.cert-directory-tree__node-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.cert-directory-tree__node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.cert-directory-tree__node.is-active .cert-directory-tree__node-label {
  color: var(--el-color-primary);
  font-weight: 500;
}
</style>
