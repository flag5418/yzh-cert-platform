<template>
  <div class="cert-directory-tree">
    <div class="cert-directory-tree__header">
      <span class="cert-directory-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-directory-tree__search">
      <el-select v-model="ruleFilter" size="small" placeholder="规则状态">
        <el-option label="全部" value="all" />
        <el-option label="已配置规则" value="configured" />
        <el-option label="未配置规则" value="none" />
      </el-select>
      <el-input v-model="filterText" placeholder="搜索文档..." clearable />
    </div>

    <div v-loading="loading" class="cert-directory-tree__content">
      <el-tree
        ref="treeRef"
        :data="fileTreeData"
        :props="{ children: 'children', label: 'name' }"
        :highlight-current="true"
        :expand-on-click-node="false"
        node-key="id"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="cert-directory-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="cert-directory-tree__node-icon">{{ getNodeIcon(data) }}</el-icon>
            <span class="cert-directory-tree__node-label">{{ data.name }}</span>
            <CertConvertBadge v-if="showConvertBadge && data.type === 'file' && data.convertStatus" :status="data.convertStatus" />
          </div>
        </template>
      </el-tree>
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useFileTree } from '../composables/useFileTree'
import CertConvertBadge from './CertConvertBadge.vue'

const props = defineProps({
  title: { type: String, default: '文件目录' },
  filterable: { type: Boolean, default: false },
  showConvertBadge: { type: Boolean, default: true }
})

const emit = defineEmits(['select'])

const { fileTreeData, loading, loadTree } = useFileTree()
const treeRef = ref(null)
const filterText = ref('')
const ruleFilter = ref('all')

function getNodeIcon(data) {
  const icons = { organization: '🏢', standard: '📋', stage: '📌', folder: '📁', file: '📄' }
  return icons[data.type] || '📄'
}

function isActive(data) {
  return false
}

function onNodeClick(data) {
  if (data.type === 'file') {
    emit('select', data)
  }
}

watch(filterText, () => {
  treeRef.value?.filter(filterText.value)
})

loadTree()
</script>

<style scoped>
.cert-directory-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.cert-directory-tree__header {
  padding: 12px 16px;
  border-bottom: 1px solid #ebeef5;
  font-weight: 600;
}

.cert-directory-tree__search {
  padding: 8px 16px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  gap: 8px;
}

.cert-directory-tree__content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.cert-directory-tree__node {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cert-directory-tree__node-icon {
  font-size: 16px;
}

.cert-directory-tree__node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
