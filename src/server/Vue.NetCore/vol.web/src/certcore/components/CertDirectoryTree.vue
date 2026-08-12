<template>
  <div class="cert-directory-tree">
    <div class="cert-directory-tree__header">
      <span class="cert-directory-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-directory-tree__search">
      <el-input
        v-model="filterText"
        placeholder="搜索..."
        size="small"
        clearable
        prefix-icon="Search"
      />
    </div>

    <div v-loading="loading" class="cert-directory-tree__content">
      <el-tree
        ref="treeRef"
        :data="fileTreeData"
        :props="defaultProps"
        :filter-node-method="filterNode"
        :highlight-current="true"
        :expand-on-click-node="false"
        node-key="id"
        :current-node-key="selectedFile?.id"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="cert-directory-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="cert-directory-tree__node-icon" :class="`is-${data.type}`">
              <component :is="treeIcon(data)" />
            </el-icon>
            <span class="cert-directory-tree__node-label">{{ data.name }}</span>

            <!-- 转换状态徽标 -->
            <CertConvertBadge
              v-if="showConvertBadge && data.type === 'file' && data.convertStatus"
              :status="data.convertStatus"
            />

            <!-- 规则状态点 -->
            <span
              v-if="showRuleStatus && data.type === 'file'"
              class="cert-directory-tree__rule-dot"
              :class="`is-${data.ruleStatus || 'none'}`"
              :title="ruleStatusTitle(data.ruleStatus)"
            />
          </div>
        </template>
      </el-tree>

      <YzhEmptyState
        v-if="!loading && fileTreeData.length === 0"
        :icon="IconFolderOpen"
        title="暂无目录"
        description="请先配置标准目录"
        compact
      />
    </div>
  </div>
</template>

<script setup>
/**
 * CertDirectoryTree —— 认证目录树（全局复用核心）
 * 机构 → 标准 → 阶段（懒加载）→ 文件夹 → 文件
 *
 * 供 DocExtractionRule / 规则定义 / 报告内容定义等页面复用
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhEmptyState, IconFolderOpen } from '@/yzh'
import CertConvertBadge from './CertConvertBadge.vue'
import { useFileTree } from '../composables/useFileTree'
import { CertTreeIcon, CERT_FILE_TYPE_COLOR } from '../icons'

const props = defineProps({
  title: { type: String, default: '文件目录' },
  mode: { type: String, default: 'file' }, // 'file' | 'folder'
  showConvertBadge: { type: Boolean, default: true },
  showRuleStatus: { type: Boolean, default: true },
  filterable: { type: Boolean, default: false },
  selectedFile: { type: Object, default: null }
})

const emit = defineEmits(['select', 'stage-load', 'update:selectedFile'])

const treeRef = ref(null)
const filterText = ref('')

const { fileTreeData, loading, loadTree, loadStageFiles } = useFileTree()

const defaultProps = { children: 'children', label: 'name' }

/* ===== 节点图标 ===== */
const treeIcon = (data) => {
  if (data.type === 'file') {
    const ext = (data.name || '').split('.').pop().toLowerCase()
    return CertTreeIcon[data.type] || CertTreeIcon.file
  }
  return CertTreeIcon[data.type] || CertTreeIcon.folder
}

const fileColor = (name) => {
  const ext = (name || '').split('.').pop().toLowerCase()
  return CERT_FILE_TYPE_COLOR[ext] || CERT_FILE_TYPE_COLOR.default
}

const isActive = (data) => props.selectedFile && props.selectedFile.id === data.id

/* ===== 规则状态 ===== */
const RULE_STATUS_TITLE = {
  none: '未制定规则',
  configured: '已制定规则',
  failed: '制定规则失败'
}
const ruleStatusTitle = (status) => RULE_STATUS_TITLE[status] || ''

/* ===== 搜索过滤 ===== */
const filterNode = (value, data) => {
  if (!value) return true
  return (data.name || '').toLowerCase().includes(value.toLowerCase())
}
watch(filterText, (val) => {
  treeRef.value?.filter(val)
})

/* ===== 交互 ===== */
const onNodeClick = (data, node) => {
  if (data.type === 'file' && props.mode === 'file') {
    emit('select', data)
    emit('update:selectedFile', data)
  } else if (data.type === 'stage') {
    handleStageClick(data, node)
  } else {
    node.expanded = !node.expanded
  }
}

const handleStageClick = async (data, node) => {
  if (data._loaded) {
    node.expanded = !node.expanded
    return
  }
  if (data._loading) return
  data._loading = true
  try {
    await loadStageFiles(data)
    emit('stage-load', data)
  } catch (e) {
    ElMessage.error('加载文件列表失败')
    console.error('[CertDirectoryTree] 加载阶段文件失败:', e)
  } finally {
    data._loading = false
  }
  node.expanded = !node.expanded
}

/* ===== 初始化 ===== */
watch(() => props.filterable, () => {}, { immediate: true })
loadTree()
</script>

<style scoped>
.cert-directory-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.cert-directory-tree__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-shrink: 0;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-weight: var(--yzh-font-weight-bold, 600);
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--yzh-color-text-primary, #303133);
}

.cert-directory-tree__search {
  flex-shrink: 0;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.cert-directory-tree__content {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: var(--yzh-space-2, 8px);
}

.cert-directory-tree__node {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.cert-directory-tree__node-label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.cert-directory-tree__node.is-active .cert-directory-tree__node-label {
  color: var(--yzh-color-primary, #409eff);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.cert-directory-tree__node-icon {
  font-size: 16px;
  color: var(--yzh-color-text-secondary, #909399);
  flex-shrink: 0;
}

.cert-directory-tree__node-icon.is-organization { color: var(--cert-color-org, #e6a23c); }
.cert-directory-tree__node-icon.is-standard { color: var(--cert-color-standard, #409eff); }
.cert-directory-tree__node-icon.is-stage { color: var(--cert-color-stage, #67c23a); }
.cert-directory-tree__node-icon.is-folder { color: var(--cert-color-folder, #fac858); }
.cert-directory-tree__node-icon.is-file { color: var(--cert-color-file-default, #909399); }

/* 规则状态点（纯 CSS，不用字符/emoji） */
.cert-directory-tree__rule-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
  background: var(--cert-color-rule-none, #c0c4cc);
}

.cert-directory-tree__rule-dot.is-configured {
  background: var(--cert-color-rule-configured, #67c23a);
}

.cert-directory-tree__rule-dot.is-failed {
  background: var(--cert-color-rule-failed, #f56c6c);
}

/* Element Tree 微调 */
.cert-directory-tree :deep(.el-tree-node__content) {
  height: 36px;
  border-radius: var(--yzh-radius-sm, 4px);
  margin: 2px 0;
  transition: background-color var(--yzh-duration-fast, 0.15s);
}

.cert-directory-tree :deep(.el-tree-node__content:hover) {
  background: var(--yzh-color-bg-hover, #f5f7fa);
}

.cert-directory-tree :deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: var(--yzh-color-bg-active, #ecf5ff);
}

.cert-directory-tree :deep(.el-tree-node__expand-icon) {
  color: var(--yzh-color-text-secondary, #909399);
}
</style>
