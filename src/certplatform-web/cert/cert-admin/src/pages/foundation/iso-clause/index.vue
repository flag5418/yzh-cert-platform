<script setup lang="ts">
/**
 * 条款管理（左树右表）
 *
 * - 左树：**体系（分类） → 族 → 标准** 三层，**全只读**（不可增删改）
 * - 右表：仅当选中「标准」节点时加载条款树；否则显示空提示
 * - 条款操作（增 / 删 / 改 / 批量删除 / 行内新增下级）仍可用 —— 但只作用于已选中的标准
 *
 * ★ 布局裁决（2026-11-05）：与 iso-standard（/cert/iso-standard）同构。
 *   注：标准的增删改在「标准管理」/cert/standard-manage 独立页面完成。
 *
 * ★ 选择判定唯一入口 = `logic.isSelectableNode()`（点击守卫 + 空态 v-if 共用）。
 */
import { Delete, Plus, RefreshRight, Upload } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import {
  YzhDialog,
  YzhFormDialog,
  YzhEmptyState,
  YzhStatusBadge,
  resolveStatusBadge,
  YzhTreeTableLayout,
  YzhTreeTable,
  YzhTable,
  unwrapOk,
  type TreeNode,
} from '@yzh-core'
import { buildAcceptAttribute } from '@share/constants/upload-file-policy'
import { computed, onMounted, ref } from 'vue'
import {
  applyStandardClauses,
  previewStandardClauses,
  uploadStandardMarkdown,
  type StandardSourcePreview,
} from '@share/api/cert/iso-standard'
import { ISOClauseLogic } from './logic'

// 静默第三方库 ResizeObserver polyfill 的 startTime 错误（VM809:2，同 iso-standard）
window.addEventListener('error', (event) => {
  if (event.message?.includes("Cannot read properties of undefined (reading 'startTime')")) {
    event.preventDefault()
    return true
  }
})

const logic = new ISOClauseLogic()

const treeTableRef = ref()
const tableRef = ref()

/**
 * 函数式 ref：挂载/卸载时同步给内核。
 *
 * ★ 必要性：右表在 `v-if` 分支里 —— 首次选中标准前**根本不渲染**，
 *   `onMounted + nextTick` 一次性注册拿到的恒是 `undefined`，
 *   此后 `refresh()` / 启停 / 显示已禁用 / 提交后刷新全走 `_tableRef?.refresh()`
 *   静默 no-op。范式同 `useTreeTable` / standard-manage / iso-standard。
 */
function setTreeTableRef(el: any): void {
  treeTableRef.value = el
  logic.setTreeTableRef(el)
}

function setTableRef(el: any): void {
  tableRef.value = el
  logic.setTableRef(el)
}

const rowActionButtons = computed(() => logic.rowActions)

const dialogTitle = computed(() =>
  logic.dialogMode.value === 'add' ? '新增条款' : '编辑条款',
)

// ========================================================
// ★ 标准原文 → Markdown → 条款（2026-11-10）
//   Markdown 是知识层事实源：条款生成、文档内容提取都读它。
// ========================================================

const fileInput = ref<HTMLInputElement>()
const uploadLoading = ref(false)
const previewLoading = ref(false)
const applyLoading = ref(false)
const sourceDialog = ref(false)
const overwrite = ref(false)
const sourcePreview = ref<StandardSourcePreview | null>(null)

/** 当前选中的标准（左侧标准节点 Code = cert_iso_standard.Code） */
const selectedStdCode = computed(() =>
  logic.isSelectableNode(logic.selectedNode) ? (logic.selectedNode?.Code ?? '') : '',
)
const selectedStdName = computed(() => {
  const node: any = logic.selectedNode
  if (!logic.isSelectableNode(node)) return ''
  return String(node?.Extra?.StandardName ?? node?.Name ?? '')
})
const previewColumns = [
  { prop: 'ClauseNumber', label: '条款号', width: 110 },
  { prop: 'Title', label: '标题', minWidth: 240, showOverflowTooltip: true },
  { prop: 'Description', label: '正文字数', width: 100, align: 'center' as const },
]

const mdSummary = computed(() => {
  const p = sourcePreview.value
  if (!p) return ''
  return `原文 ${p.MarkdownChars} 字 · 解析 ${p.ParsedCount} 条 · 库内已有 ${p.ExistingCount} 条 · ${
    p.Parser === 'llm' ? '模型兜底' : '按标题编号'
  }`
})

function pickStandardFile(): void {
  if (!selectedStdCode.value) {
    ElMessage.warning('请先在左侧选择标准')
    return
  }
  fileInput.value?.click()
}

async function onStandardFileChange(e: Event): Promise<void> {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  uploadLoading.value = true
  try {
    const res = await uploadStandardMarkdown(file, selectedStdCode.value)
    const data = unwrapOk(res as any, '上传失败')
    sourcePreview.value = (data ?? null) as StandardSourcePreview | null
    overwrite.value = false
    sourceDialog.value = true
    ElMessage.success(res.message || '已转换并解析')
  } catch (err: any) {
    ElMessage.error(err?.message || '上传失败')
  } finally {
    uploadLoading.value = false
  }
}

async function openPreview(): Promise<void> {
  if (!selectedStdCode.value) {
    ElMessage.warning('请先在左侧选择标准')
    return
  }
  previewLoading.value = true
  try {
    const res = await previewStandardClauses(selectedStdCode.value)
    sourcePreview.value = unwrapOk(res, '解析失败') as StandardSourcePreview
    overwrite.value = false
    sourceDialog.value = true
  } catch (err: any) {
    ElMessage.error(err?.message || '解析失败')
  } finally {
    previewLoading.value = false
  }
}

async function confirmApply(): Promise<void> {
  if (!sourcePreview.value) return
  applyLoading.value = true
  try {
    const res = await applyStandardClauses(sourcePreview.value.StandardCode, overwrite.value)
    unwrapOk(res, '写入失败')
    ElMessage.success(res.message || '条款已写入')
    sourceDialog.value = false
    await logic.refresh()
  } catch (err: any) {
    ElMessage.error(err?.message || '写入失败')
  } finally {
    applyLoading.value = false
  }
}

// ========================================================
// 树节点点击：仅标准节点触发条款加载（判定唯一入口 = isSelectableNode）
// ========================================================

async function handleNodeClick(node: TreeNode) {
  if (!logic.isSelectableNode(node)) {
    // 点击体系（虚拟）/族节点：清空选择，右侧回到空提示
    logic.selectedNode = null
    return
  }
  await logic.onNodeClick(node)
}

onMounted(async () => {
  // refs 由模板函数式 ref 在子组件挂载时注入（早于本 onMounted），
  // 这里只做 init —— init 末尾若自动选中节点，right table refresh 才拿得到引用。
  await logic.init()
})
</script>

<template>
  <div class="clause-page">
    <!-- ★ 标准原文：上传即转 Markdown 存档，再按原文生成条款 -->
    <div class="clause-page__topbar">
      <div class="topbar-info">
        <span class="topbar-label">标准原文</span>
        <span v-if="selectedStdName" class="topbar-std">{{ selectedStdName }}</span>
        <span v-else class="topbar-hint">先在左侧选择标准</span>
        <span v-if="mdSummary" class="topbar-meta">{{ mdSummary }}</span>
      </div>
      <div class="topbar-actions">
        <input
          ref="fileInput"
          type="file"
          class="topbar-file"
          :accept="buildAcceptAttribute(['document', 'pdf'])"
          @change="onStandardFileChange"
        />
        <el-button
          type="primary"
          :icon="Upload"
          :loading="uploadLoading"
          :disabled="!selectedStdCode"
          @click="pickStandardFile"
        >
          上传标准文件
        </el-button>
        <el-button
          type="default"
          :loading="previewLoading"
          :disabled="!selectedStdCode"
          @click="openPreview"
        >
          按原文生成条款
        </el-button>
      </div>
    </div>

    <YzhTreeTableLayout
      :ref="setTreeTableRef"
      :tree-data="logic.treeData"
      :tree-width="320"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="logic.nodeActions"
      :get-action-label="(action: string, node: TreeNode) => logic.getNodeActionLabel(action, node)"
      @tree-node-click="handleNodeClick"
    >
      <!-- 树底部：无操作按钮（左树全只读） -->

      <template #default>
        <div class="clause-page__content">
          <!-- 未选中标准时：空提示（判定与点击守卫共用 isSelectableNode） -->
          <div v-if="!logic.isSelectableNode(logic.selectedNode)" class="clause-page__empty">
            <YzhEmptyState
              title="未选择标准"
              description="请从左侧选择标准节点以查看和编辑条款"
            />
          </div>

          <!-- 已选中标准：条款树形表格 -->
          <YzhTreeTable
            v-else
            :ref="setTableRef"
            :columns="logic.columns as any"
            :data-loader="logic.dataLoader.bind(logic)"
            :search-fields="logic.searchFields as any"
            :selectable="true"
            :row-action-buttons="rowActionButtons"
            select-mode="multiple"
            row-key="Code"
            :children-field="'Children'"
            :allow-add-child="true"
            @selection-change="logic.onSelectionChange"
            @row-action="logic.onRowAction"
          >
            <!-- 状态列 -->
            <template #column-IsValid="{ row }">
              <YzhStatusBadge
                v-if="resolveStatusBadge(row, 'IsValid')"
                :type="resolveStatusBadge(row, 'IsValid')?.type"
                :text="resolveStatusBadge(row, 'IsValid')?.text"
                size="small"
              />
            </template>

            <!-- 工具栏左侧 -->
            <template #toolbar-left>
              <el-button type="primary" :icon="Plus" @click="logic.addClauseWithToast()">
                新增顶级条款
              </el-button>
              <el-button type="danger" :icon="Delete" @click="logic.batchDeleteClausesWithConfirm()">
                批量删除
              </el-button>
              <el-button type="default" :icon="RefreshRight" @click="logic.refresh()">
                刷新
              </el-button>
            </template>

            <!-- 工具栏右侧：显示已禁用开关 -->
            <template #toolbar-right>
              <div class="toolbar-switch">
                <span class="toolbar-switch__label">显示已禁用</span>
                <el-switch
                  :model-value="logic.showDisabled.value"
                  @change="logic.toggleShowDisabled()"
                />
              </div>
            </template>
          </YzhTreeTable>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 条款解析预览弹窗：确认后才写库 -->
    <YzhDialog
      v-model="sourceDialog"
      title="按标准原文生成条款"
      width="760px"
      top="4vh"
      confirm-text="写入条款"
      :confirm-loading="applyLoading"
      append-to-body
      @confirm="confirmApply"
    >
      <div v-if="sourcePreview" class="src-dialog">
        <div class="src-dialog__meta">
          <span>{{ sourcePreview.StandardName }}</span>
          <span>Markdown {{ sourcePreview.MarkdownChars }} 字</span>
          <span>解析 {{ sourcePreview.ParsedCount }} 条</span>
          <span>库内已有 {{ sourcePreview.ExistingCount }} 条</span>
        </div>
        <!-- ★ 2026-11-11：操作栏放到表格**上方**并吸顶。
             原来放在表格下方 + 依赖 dialog footer，短屏/缩放时整条被顶出视口，
             用户只看到表格和复选框、看不到“写入条款”。 -->
        <div class="src-dialog__actions">
          <el-checkbox v-model="overwrite" class="src-dialog__overwrite">
            先清空该标准现有条款，再按本次解析结果重建（上传错标准/换版时使用）
          </el-checkbox>
          <el-button type="primary" :loading="applyLoading" @click="confirmApply">写入条款</el-button>
        </div>
        <YzhTable
          :columns="previewColumns as any"
          :data="sourcePreview.Clauses as any"
          :show-pagination="false"
          :height="220"
          :toolbar="false"
          select-mode="none"
          row-key="ClauseNumber"
          no-padding
        >
          <template #column-Description="{ row }">
            {{ (row.Description || '').length }}
          </template>
        </YzhTable>
      </div>

      <template #footer>
        <el-button type="default" @click="sourceDialog = false">取消</el-button>
        <el-button type="primary" :loading="applyLoading" @click="confirmApply">写入条款</el-button>
      </template>
    </YzhDialog>

    <!-- 条款新增/编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      :title="dialogTitle"
      width="600px"
      :fields="logic.formFields as any"
      :loading="logic.submitting.value"
      :cols="logic.formLayoutCols as any"
      :destroy-on-close="false"
      @submit="logic.submitClauseFormWithToast()"
    />
  </div>
</template>

<style scoped>
.clause-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.clause-page__topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  flex-wrap: wrap;
}

.topbar-info {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  min-width: 0;
}

.topbar-label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.topbar-std {
  font-size: var(--yzh-font-size-base, 14px);
  color: var(--yzh-color-text-primary, #303133);
  font-weight: 600;
}

.topbar-hint {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.topbar-meta {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.topbar-actions {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.topbar-file {
  display: none;
}

.src-dialog {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
}

.src-dialog__meta {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.src-dialog__overwrite {
  font-size: var(--yzh-font-size-sm, 13px);
}

.src-dialog__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  position: sticky;
  top: 0;
  z-index: 2;
  padding: var(--yzh-space-2, 8px) 0;
  background: var(--yzh-color-bg-container, #fff);
  border-bottom: 1px solid var(--yzh-color-border, #ebeef5);
}

.clause-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.clause-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 200px;
}

.toolbar-switch {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.toolbar-switch__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}
</style>
