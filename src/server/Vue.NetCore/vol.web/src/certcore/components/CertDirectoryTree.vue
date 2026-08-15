<template>
  <div class="cert-directory-tree">
    <div class="cert-directory-tree__header">
      <span class="cert-directory-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-directory-tree__search">
      <el-select
        v-model="ruleFilter"
        size="small"
        class="cert-directory-tree__rule-filter"
        placeholder="规则状态"
      >
        <el-option label="全部" value="all" />
        <el-option label="已配置规则" value="configured" />
        <el-option label="未配置规则" value="none" />
        <el-option label="配置失败" value="failed" />
      </el-select>
      <el-input
        v-model="filterText"
        placeholder="搜索文档..."
        size="small"
        clearable
        prefix-icon="Search"
      />
    </div>

    <!-- 队列锁定提示条 -->
    <div v-if="activeQueue?.exists" class="cert-directory-tree__queue-lock-bar">
      <el-icon class="is-spinning"><IconLoading /></el-icon>
      <span class="lock-text">队列「{{ activeQueue.queueName || activeQueue.queueCode }}」执行中，以下文件暂不可操作</span>
      <el-tag size="small" type="danger" class="lock-badge">{{ lockCount }} 个资源被锁定</el-tag>
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

            <!-- 规则状态标签（明显可筛选：已配置/未配置/失败） -->
            <span
              v-if="showRuleStatus && data.type === 'file'"
              class="cert-directory-tree__rule-tag"
              :class="`is-${data.ruleStatus || 'none'}`"
              :title="ruleStatusTitle(data.ruleStatus)"
            >{{ ruleStatusText(data.ruleStatus) }}</span>
            <!-- 队列锁定指示器 -->
            <el-tooltip
              v-if="isLocked(data.fileCode)"
              :content="`队列 ${queueLockMap[data.fileCode]} 处理中，不可操作`"
              placement="right"
            >
              <el-icon class="cert-directory-tree__lock-icon"><IconLoading /></el-icon>
            </el-tooltip>
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
import { computed, ref, watch, onMounted, onUnmounted } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhEmptyState, IconFolderOpen, IconLoading } from '@/yzh'
import { useYzhQueue } from '@/yzh/composables/useYzhQueue'
import CertConvertBadge from './CertConvertBadge.vue'
import { useFileTree } from '../composables/useFileTree'
import { CertTreeIcon, CERT_FILE_TYPE_COLOR } from '../icons'

const props = defineProps({
  title: { type: String, default: '文件目录' },
  mode: { type: String, default: 'file' }, // 'file' | 'folder'
  showConvertBadge: { type: Boolean, default: true },
  showRuleStatus: { type: Boolean, default: true },
  filterable: { type: Boolean, default: false },
  selectedFile: { type: Object, default: null },
  // 运行中队列的锁定状态 map：{ fileCode: queueCode }
  queueLockMap: { type: Object, default: () => ({}) },
  // 正在上传中的文件编码集合（本地上传阶段，尚未入 yzh_queue）
  uploadingFileCodes: { type: Set, default: () => new Set() },
  // 当前目录的 busy 状态（队列执行中或本地上传中）
  isBusy: { type: Boolean, default: false }
})

const emit = defineEmits(['select', 'stage-load', 'update:selectedFile'])

const treeRef = ref(null)
const filterText = ref('')
const ruleFilter = ref('all')

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
const RULE_STATUS_TEXT = {
  none: '未配置',
  configured: '已配置',
  failed: '失败'
}
const ruleStatusTitle = (status) => RULE_STATUS_TITLE[status] || ''
const ruleStatusText = (status) => RULE_STATUS_TEXT[status] || ''

/* ===== 搜索 + 规则状态过滤 ===== */
// 规则状态过滤：文件节点按自身状态匹配；文件夹/阶段节点保留「子树中存在匹配文件」的祖先链
const matchRuleStatus = (data, node) => {
  const filter = ruleFilter.value
  if (!filter || filter === 'all') return true
  if (data.type === 'file') return data.ruleStatus === filter
  // 非文件节点：递归检查子树是否有匹配的文件
  const walk = (n) => {
    if (!n?.childNodes?.length) return false
    return n.childNodes.some((child) => {
      const d = child.data
      if (d.type === 'file') return d.ruleStatus === filter
      return walk(child)
    })
  }
  return walk(node)
}

const filterNode = (value, data, node) => {
  if (!matchRuleStatus(data, node)) return false
  if (!value) return true
  return (data.name || '').toLowerCase().includes(value.toLowerCase())
}
const applyFilter = () => {
  treeRef.value?.filter(filterText.value || '')
}
watch(filterText, applyFilter)
watch(ruleFilter, applyFilter)

/* ===== 交互 ===== */
/* ===== 队列锁定 ===== */
const queueLockMap = ref({})
const activeQueue = ref(null)

const lockCount = computed(() => Object.keys(queueLockMap.value).length)
const isLocked = (fileCode) => !!queueLockMap.value[fileCode]
const isUploading = (fileCode) => props.uploadingFileCodes?.has(fileCode) ?? false

/** 收集所有已加载阶段的文件编码 */
const collectFileCodes = () => {
  const codes = []
  const walk = (nodes) => {
    for (const n of nodes || []) {
      if (n.type === 'file' && n.fileCode) codes.push(n.fileCode)
      if (n.children) walk(n.children)
    }
  }
  walk(fileTreeData.value)
  return codes
}

const { getActiveQueue, getFileLockStatus } = useYzhQueue()

/** 查询当前目录的运行中队列 */
const refreshActiveQueue = async () => {
  const dc = activeDirectoryCode.value
  if (!dc) { activeQueue.value = null; return }
  try {
    activeQueue.value = await getActiveQueue(dc)
  } catch {}
}

/** 轮询更新锁定状态 */
let lockPollTimer = null
const refreshLockStatus = async () => {
  const codes = collectFileCodes()
  if (codes.length === 0) { queueLockMap.value = {}; return }
  try {
    queueLockMap.value = await getFileLockStatus(codes)
  } catch {}
}

const startLockPolling = () => {
  if (lockPollTimer) return
  lockPollTimer = setInterval(refreshLockStatus, 5000)
}
const stopLockPolling = () => {
  if (lockPollTimer) { clearInterval(lockPollTimer); lockPollTimer = null }
}

// 当前展开阶段的 directoryCode（用于查询锁定）
const activeDirectoryCode = computed(() => {
  for (const org of fileTreeData.value || []) {
    for (const std of org.children || []) {
      for (const phase of std.children || []) {
        if (phase._loaded && phase.directoryCode) return phase.directoryCode
      }
    }
  }
  return null
})

// 监听阶段加载完成，自动查询队列状态
watch(() => fileTreeData.value, async (data) => {
  if (!data) return
  let hasNewLoaded = false
  const walk = (nodes) => {
    for (const n of nodes || []) {
      if (n.type === 'stage' && n._loaded) hasNewLoaded = true
      if (n.children) walk(n.children)
    }
  }
  walk(data)
  if (hasNewLoaded) {
    await refreshActiveQueue()
    if (activeQueue.value?.exists) {
      refreshLockStatus()
      startLockPolling()
    } else {
      queueLockMap.value = {}
      stopLockPolling()
    }
  }
}, { immediate: true, deep: true })

onUnmounted(() => {
  stopLockPolling()
})

const onNodeClick = (data, node) => {
  if (data.type === 'file' && props.mode === 'file') {
    const fc = data.fileCode
    if (isLocked(fc)) {
      const qc = queueLockMap.value[fc]
      emit('lock-warning', { fileCode: fc, fileName: data.name, queueCode: qc })
      return
    }
    if (isUploading(fc)) {
      emit('lock-warning', { fileCode: fc, fileName: data.name, queueCode: 'uploading' })
      return
    }
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

/* ===== 队列事件联动：队列终态后自动刷新已加载的阶段文件 =====
 * 队列执行期间文件 IsValid=0（提取规则页隐藏），完成后恢复可见。
 * 若当前已展开某阶段，列表是旧数据，监听 queue-progress 事件自动重拉。
 */
const refreshLoadedStages = async () => {
  const stages = []
  const walk = (nodes) => {
    for (const n of nodes || []) {
      if (n.type === 'stage' && n._loaded) stages.push(n)
      if (n.children && n.children.length) walk(n.children)
    }
  }
  walk(fileTreeData.value)
  for (const stage of stages) {
    try {
      await loadStageFiles(stage)
    } catch (e) {
      // 刷新失败不打断其它阶段
    }
  }
}

const onQueueProgress = (e) => {
  const data = e?.detail
  if (!data) return
  // 仅终态事件触发刷新（完成/失败/取消），进行中进度不刷
  const status = data.data?.status || ''
  if (['completed', 'failed', 'cancelled'].includes(status)) {
    refreshLoadedStages()
  }
}

onMounted(() => {
  window.addEventListener('queue-progress', onQueueProgress)
})
onUnmounted(() => {
  window.removeEventListener('queue-progress', onQueueProgress)
})

/* 供父页面主动刷新（如重试失败转换后立即隐藏/显示文件） */
defineExpose({ refresh: refreshLoadedStages })
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
  display: flex;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.cert-directory-tree__rule-filter {
  width: 110px;
  flex-shrink: 0;
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

/* 队列锁定提示条 */
.cert-directory-tree__queue-lock-bar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-shrink: 0;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  background: var(--yzh-color-danger-light-9, #fef0f0);
  border-bottom: 1px solid var(--yzh-color-danger-light-7, #fde2e2);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-danger, #f56c6c);
}
.cert-directory-tree__queue-lock-bar .is-spinning {
  animation: spin 1s linear infinite;
}
.cert-directory-tree__queue-lock-bar .lock-text {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cert-directory-tree__queue-lock-bar .lock-badge {
  flex-shrink: 0;
}

/* 队列锁定图标 */
.cert-directory-tree__lock-icon {
  font-size: 14px;
  color: var(--yzh-color-danger, #f56c6c);
  flex-shrink: 0;
  animation: spin 1s linear infinite;
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

/* 规则状态标签（明显可见 + 可筛选） */
.cert-directory-tree__rule-tag {
  flex-shrink: 0;
  font-size: 11px;
  line-height: 1;
  padding: 2px 6px;
  border-radius: 3px;
  color: var(--cert-color-rule-none-text, #909399);
  background: var(--cert-color-rule-none-bg, #f0f2f5);
  border: 1px solid var(--cert-color-rule-none, #c0c4cc);
}

.cert-directory-tree__rule-tag.is-configured {
  color: var(--cert-color-rule-configured-text, #529b2e);
  background: var(--cert-color-rule-configured-bg, #f0f9eb);
  border-color: var(--cert-color-rule-configured, #67c23a);
}

.cert-directory-tree__rule-tag.is-failed {
  color: var(--cert-color-rule-failed-text, #c45656);
  background: var(--cert-color-rule-failed-bg, #fef0f0);
  border-color: var(--cert-color-rule-failed, #f56c6c);
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
