<template>
  <div class="cert-directory-tree">
    <div class="cert-directory-tree__header">
      <span class="cert-directory-tree__title">{{ title }}</span>
      <slot name="header-actions" />
    </div>

    <div v-if="filterable" class="cert-directory-tree__search">
      <el-select
        v-if="showRuleStatus"
        v-model="ruleFilter"
        size="small"
        class="cert-directory-tree__rule-filter"
      >
        <el-option label="全部" value="all" />
        <el-option label="已配置规则" value="configured" />
        <el-option label="未配置规则" value="none" />
        <el-option label="配置失败" value="failed" />
      </el-select>
      <el-input v-model="filterText" placeholder="搜索文档..." clearable size="small" />
    </div>

    <div v-loading="loading" class="cert-directory-tree__content">
      <el-tree
        v-if="fileTreeData.length > 0"
        ref="treeRef"
        :data="fileTreeData"
        :props="treeProps"
        :highlight-current="true"
        :expand-on-click-node="false"
        :filter-node-method="filterNode"
        :default-expanded-keys="defaultExpandedKeys"
        node-key="Code"
        @node-expand="onNodeExpand"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <div class="cert-directory-tree__node" :class="{ 'is-active': isActive(data) }">
            <el-icon class="cert-directory-tree__node-icon">
              <component :is="getNodeIconComponent(data)" />
            </el-icon>
            <span class="cert-directory-tree__node-label">{{ data.Name }}</span>
            <CertConvertBadge v-if="showConvertBadge && data.Type === 'file' && data.ConvertStatus" :status="data.ConvertStatus" />
            <span
              v-if="showRuleStatus && data.Type === 'file'"
              class="cert-directory-tree__rule-tag"
              :class="`is-${data.RuleStatus || 'none'}`"
            >{{ RULE_STATUS_TEXT[data.RuleStatus || 'none'] }}</span>
          </div>
        </template>
      </el-tree>

      <el-empty v-if="!loading && fileTreeData.length === 0" description="暂无目录数据" :image-size="80" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Folder,
  Document,
  OfficeBuilding,
  Collection,
} from '@element-plus/icons-vue'
import { useFileTree, type TreeNode } from '../composables/useFileTree'
import CertConvertBadge from './CertConvertBadge.vue'

const props = defineProps<{
  title?: string
  filterable?: boolean
  showConvertBadge?: boolean
  /** 显示文件规则状态标签（已配置/未配置/配置失败）并启用规则筛选 */
  showRuleStatus?: boolean
  defaultExpandedKeys?: (string | number)[]
}>()

const RULE_STATUS_TEXT: Record<string, string> = {
  none: '未配置',
  configured: '已配置',
  failed: '配置失败'
}

const emit = defineEmits<{
  select: [node: TreeNode]
  'node-click': [node: TreeNode]
}>()

const { fileTreeData, loading, defaultExpandedKeys, loadTree, loadStageFiles } = useFileTree()
const treeRef = ref()
const filterText = ref('')
const ruleFilter = ref<'all' | 'configured' | 'none' | 'failed'>('all')
const selectedCode = ref<string | number | null>(null)

/** fileCode → 规则状态（由 configured-rules 接口推导；保存后可通过 refresh() 重建） */
const ruleStatusMap = ref<Record<string, 'configured' | 'failed'>>({})

function resolveRuleStatus(fileCode: string): 'configured' | 'none' | 'failed' {
  return ruleStatusMap.value[fileCode] || 'none'
}

/** 拉取已配置规则并刷新已有文件节点的状态标签 */
async function loadRuleStatuses() {
  if (!props.showRuleStatus) return
  try {
    const { getConfiguredRules } = await import('../api/workflow/doc-extraction-rule')
    const res = await getConfiguredRules()
    const map: Record<string, 'configured' | 'failed'> = {}
    for (const r of res?.data || []) {
      const code = r.standardFileCode || (r as any).fileCode
      if (!code) continue
      map[code] = r.isValid === false ? 'failed' : 'configured'
    }
    ruleStatusMap.value = map
  } catch {
    // 规则状态获取失败不影响目录浏览
  }
  applyRuleStatusToLoaded()
}

/** 把规则状态回写到树中已加载的文件节点（保持懒加载结构不变） */
function applyRuleStatusToLoaded() {
  const walk = (nodes: TreeNode[]) => {
    for (const n of nodes || []) {
      if (n.Type === 'file') {
        n.RuleStatus = resolveRuleStatus(String(n.FileCode || n.Code))
      } else if (n.Children?.length) {
        walk(n.Children)
      }
    }
  }
  walk(fileTreeData.value)
}

const treeProps = {
  children: 'Children',
  label: 'Name',
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
  return iconMap[data.Type] || Document
}

function isActive(data: TreeNode) {
  return selectedCode.value === data.Code
}

async function onNodeExpand(data: TreeNode) {
  if (data._loaded) return
  if (data.Type === 'stage') {
    await loadStageFiles(data)
    treeRef.value?.updateKeyChildren(data.Code, data.Children || [])
  } else if (data.Type === 'folder') {
    const folderCode = data.FolderCode
    if (!folderCode) return
    const { getFiles } = await import('../composables/useDirectoryApi')
    try {
      const files = await getFiles(folderCode)
      const fileNodes = (files || []).map((file: any) => ({
        Code: file.FileCode || file.fileCode,
        Name: file.FileName || file.fileName,
        Type: 'file' as const,
        FileCode: file.FileCode || file.fileCode,
        DirectoryCode: data.DirectoryCode,
        Raw: file,
        ConvertStatus: file.ConvertStatus || file.convertStatus,
        RuleStatus: resolveRuleStatus(String(file.FileCode || file.fileCode)),
      }))
      data._loaded = true
      // 先移除占位子节点，再追加真实文件节点
      const tree = treeRef.value
      if (tree) {
        const node = tree.getNode(data.Code)
        if (node) {
          // 移除所有现有子节点
          const children = node.childNodes?.slice() || []
          for (const child of children) {
            tree.remove(child)
          }
          // 追加文件节点
          for (const fn of fileNodes) {
            tree.append(fn, node)
          }
        }
      }
    } catch (e: any) {
      ElMessage.error('加载文件失败：' + (e?.message || ''))
    }
  }
}

function onNodeClick(data: TreeNode) {
  selectedCode.value = data.Code
  emit('node-click', data)
  if (data.Type === 'file') {
    emit('select', data)
  }
}

function filterNode(value: string, data: TreeNode) {
  const textOk = !value || (data.Name || '').toLowerCase().includes(value.toLowerCase())
  // 父节点（机构/标准/阶段/文件夹）始终保留，否则子树会被整体隐藏
  if (data.Type !== 'file') return true
  if (ruleFilter.value !== 'all') {
    return textOk && (data.RuleStatus || 'none') === ruleFilter.value
  }
  return textOk
}

watch([filterText, ruleFilter], ([val]) => {
  treeRef.value?.filter(val)
})

/** 重新加载规则状态并重绘（保存规则后调用，使状态标签立即更新） */
async function refresh() {
  await loadRuleStatuses()
  treeRef.value?.filter(filterText.value)
}

defineExpose({ refresh, loadRuleStatuses })

onMounted(async () => {
  await loadTree()
  await loadRuleStatuses()
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
  display: flex;
  gap: 8px;
}

.cert-directory-tree__rule-filter {
  width: 120px;
  flex-shrink: 0;
}

.cert-directory-tree__rule-tag {
  flex-shrink: 0;
  font-size: 11px;
  line-height: 18px;
  padding: 0 6px;
  border-radius: 2px;
  border: 1px solid transparent;
}
.cert-directory-tree__rule-tag.is-none {
  color: #909399;
  background: #f4f4f5;
  border-color: #e9e9eb;
}
.cert-directory-tree__rule-tag.is-configured {
  color: #529b2e;
  background: #f0f9eb;
  border-color: #e1f3d8;
}
.cert-directory-tree__rule-tag.is-failed {
  color: #c45656;
  background: #fef0f0;
  border-color: #fde2e2;
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
