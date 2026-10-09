<script setup lang="ts">
/**
 * 标准管理（体系 → 族 → 版本/条款）
 *
 * ★ 遵循 YZH 架构：
 * - 左树：体系(字典固定) → 族(可增删改)
 * - 右表：
 *     · 未选中族 → 空提示
 *     · 选中族   → 版本列表（dataLoader 驱动，FamilyCode 过滤）
 *     · 选中版本 → 条款树（YzhTreeTable + 行内操作）
 * - 族 CRUD：弹窗表单（不走内核 add/edit/delete，独立实现）
 * - 条款 CRUD：弹窗表单 + 行内操作
 *
 * 参考样板：cert-stage（TreeTableCore + dataLoader 模式）
 */
import { Delete, Plus, RefreshRight } from '@element-plus/icons-vue'
import {
  YzhEmptyState,
  YzhDialog,
  YzhStatusBadge,
  resolveStatusBadge,
  YzhTreeTableLayout,
  YzhTreeTable,
  YzhTable,
  type TreeNode,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, nextTick, onMounted, ref } from 'vue'
import { StandardManageLogic } from './logic'
import type { FamilyRow, VersionRow } from './logic'
import type { YzhAction } from '@yzh-core'

const logic = new StandardManageLogic()
const versionTableRef = ref()
const clauseTreeRef = ref()
const treeTableRef = ref()
const selectedRows = ref<any[]>([])

/**
 * 版本表函数式 ref：挂载/卸载时同步给内核（`logic.setTableRef`）。
 *
 * ★ 必要性：切族时右表**不会**重新挂载（同一 v-else-if 分支）⇒ 必须由内核
 *   触发 `refresh()` 重跑 dataLoader；若内核拿不到引用，`refreshTable()` 会
 *   回落到 `loadPageWithTree()`，对 CertStandardFamily 发 ParentCode 过滤 → 400。
 */
function setVersionTableRef(el: any): void {
  versionTableRef.value = el
  logic.setTableRef(el)
}

// ── 计算属性 ──
const hasSelection = computed(() => selectedRows.value.length > 0)
const panelMode = computed(() => logic.panelMode.value)
const currentVersion = computed(() => logic.currentVersion.value)
const categoryLabel = (value: unknown): string => {
  const v = String(value ?? '')
  if (!v) return '—'
  return (logic.categoryMap.value as Map<string, string>).get(v) ?? v
}

// ── 族操作 ──
function handleAddFamily() {
  logic.openAddFamily()
}

async function handleBatchDeleteVersions() {
  const rows = (versionTableRef.value as any)?.getSelectionRows?.() ?? []
  if (rows.length === 0) { ElMessage.warning('请先勾选要删除的版本'); return }
  await logic.batchDeleteVersions(rows as VersionRow[])
  selectedRows.value = []
  versionTableRef.value?.clearSelection?.()
}

// ── 版本操作 ──
function handleAddVersion() {
  logic.openAddVersionDialog()
}

function handleVersionRowAction(key: string, row: VersionRow) {
  if (key === 'edit') logic.openEditVersionDialog(row)
  else if (key === 'delete') void logic.deleteVersion(row)
  else if (key === 'toggle-valid') void logic.toggleVersionValid(row)
}

// ── 树节点操作（族节点 ⋯ 下拉菜单） ──
function nodeActions(node: TreeNode): YzhAction[] {
  const extra: Record<string, any> = node.Extra ?? {}
  if (extra._level !== 1) return []  // 仅族节点显示
  const isValid = extra.IsValid ?? 1
  return [
    { key: 'edit', text: '编辑' },
    { key: 'toggle-valid', text: isValid === 1 ? '禁用' : '启用' },
    { key: 'delete', text: '删除', danger: true },
  ]
}

async function handleNodeAction(action: string, node: TreeNode) {
  const extra: Record<string, any> = node.Extra ?? {}
  if (extra._level !== 1) return
  const family: FamilyRow = {
    Code: node.Code,
    FamilyNo: extra.FamilyNo ?? '',
    FamilyName: extra.FamilyName ?? '',
    Category: extra.Category ?? '',
    Sort: extra.Sort,
    IsValid: extra.IsValid ?? 1,
  }
  if (action === 'edit') {
    logic.openEditFamily(family)
  } else if (action === 'delete') {
    await logic.deleteFamily(family)
  } else if (action === 'toggle-valid') {
    await logic.toggleFamilyValid(family)
  }
}

// ── 条款操作 ──
async function handleAddClause() {
  const ok = await logic.openAddClause()
  if (!ok) ElMessage.warning('请先选择版本')
}

async function handleEditClause(row: any) {
  await logic.openEditClause(row)
}

async function handleDeleteClause(row: any) {
  await logic.deleteClause(row)
}

async function handleBatchDeleteClauses() {
  if (selectedRows.value.length === 0) return
  await logic.batchDeleteClauses(selectedRows.value)
  selectedRows.value = []
  clauseTreeRef.value?.clearSelection?.()
}

function handleClauseRowAction(action: string, row: any) {
  if (action === 'edit') handleEditClause(row)
  else if (action === 'delete') handleDeleteClause(row)
}

// ── 初始化 ──
onMounted(async () => {
  await logic.init()
  // 注入 treeTableRef，使内核 appendNode/removeNode 可局部刷新
  nextTick(() => {
    logic.setTreeTableRef(treeTableRef.value)
  })
})
</script>

<template>
  <div class="sm-page">
    <YzhTreeTableLayout
      ref="treeTableRef"
      :tree-data="logic.treeData"
      :tree-width="280"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      :node-actions="nodeActions"
      @tree-node-click="(node: TreeNode) => logic.onNodeClick(node)"
      @tree-node-action="handleNodeAction"
    >
      <template #treeFooter>
        <el-button type="primary" :icon="Plus" @click="handleAddFamily" style="width:100%">新增族</el-button>
      </template>

      <template #default>
        <div class="sm-page__content">
          <!-- 空态 -->
          <div v-if="!panelMode" class="sm-page__empty">
            <YzhEmptyState title="未选择族" description="请点击左侧「族」节点查看版本列表" />
          </div>

          <!--
            族选中：版本列表（YzhTable + dataLoader）

            ★ 列定义两铁律（2026-10-08 修复）：
            1. 要自定渲染的列必须 slot:true，否则 YzhTable 渲裸值（quality / 1）—— 插槽名 column-{prop}
            2. ⛔ 不要手写 { prop: 'actions' } 列：YzhTable 的 showDynamicActionColumn 见到
               actions 列会抑制动态操作列 ⇒ row-action-buttons 永不渲染
            （注：Vue 内联表达式里禁止 // 注释，故本注释置于元素外）
          -->
          <div v-else-if="panelMode === 'family'" class="sm-page__table-area">
            <YzhTable
              :ref="setVersionTableRef"
              :columns="[
                { prop: 'StandardCode', label: '标准编号', width: 140, sortable: true },
                { prop: 'StandardName', label: '标准名称', minWidth: 200, showOverflowTooltip: true },
                { prop: 'VersionYear', label: '年份', width: 80, sortable: true, align: 'center' },
                { prop: 'Category', label: '体系', width: 120, slot: true },
                { prop: 'IsValid', label: '状态', width: 90, align: 'center', slot: true },
              ]"
              :data-loader="logic.dataLoader.bind(logic)"
              :show-pagination="false"
              :selectable="true"
              :row-action-buttons="(row: VersionRow) => [
                { key: 'edit', text: '编辑', type: 'primary' },
                { key: 'toggle-valid', text: (row.IsValid ?? 1) === 1 ? '禁用' : '启用', type: 'default' },
                { key: 'delete', text: '删除', type: 'danger' },
              ]"
              row-key="Code"
              select-mode="multiple"
              @selection-change="(v: any[]) => selectedRows = v"
              @row-action="handleVersionRowAction"
            >
              <template #toolbar-left>
                <el-button type="primary" :icon="Plus" @click="handleAddVersion">新增</el-button>
                <el-button type="danger" :icon="Delete" :disabled="!hasSelection"
                  @click="handleBatchDeleteVersions">批量删除</el-button>
                <el-button type="default" :icon="RefreshRight" @click="versionTableRef?.refresh?.()">刷新</el-button>
              </template>
              <template #column-Category="{ row }">
                {{ categoryLabel(row.Category) }}
              </template>
              <template #column-IsValid="{ row }">
                <YzhStatusBadge
                  v-if="resolveStatusBadge(row, 'IsValid')"
                  :type="resolveStatusBadge(row, 'IsValid')?.type"
                  :text="resolveStatusBadge(row, 'IsValid')?.text"
                  size="small"
                />
              </template>
            </YzhTable>
          </div>

          <!-- 版本选中：条款树 -->
          <div v-else-if="panelMode === 'version'" class="sm-page__table-area">
            <YzhTreeTable
              ref="clauseTreeRef"
              :columns="[
                { prop: 'ClauseNumber', label: '条款编号', width: 100, sortable: true },
                { prop: 'Title', label: '条款标题', minWidth: 200, showOverflowTooltip: true },
                { prop: 'IsValid', label: '状态', width: 80, align: 'center', slot: true },
              ]"
              :data-loader="() => logic.loadClauses(currentVersion?.Code || '').then(() => ({ rows: logic.clauses.value, total: logic.clauses.value.length }))"
              :search-fields="[]"
              :selectable="true"
              :row-action-buttons="[{ key: 'edit', text: '编辑', type: 'primary' }, { key: 'delete', text: '删除', type: 'danger' }]"
              :allow-add-child="true"
              :tree-default-expand-all="true"
              row-key="Code"
              select-mode="multiple"
              @selection-change="(v: any[]) => selectedRows = v"
              @row-action="handleClauseRowAction"
              @toolbar-action="async (key: string) => { if (key === 'add-child') await handleAddClause(); }"
            >
              <template #toolbar-left>
                <el-button type="primary" :icon="Plus" @click="handleAddClause">新增顶级条款</el-button>
                <el-button type="danger" :icon="Delete" :disabled="!hasSelection"
                  @click="handleBatchDeleteClauses">批量删除</el-button>
                <el-button type="default" :icon="RefreshRight" @click="clauseTreeRef?.refresh?.()">刷新</el-button>
              </template>
              <template #column-IsValid="{ row }">
                <YzhStatusBadge
                  v-if="resolveStatusBadge(row, 'IsValid')"
                  :type="resolveStatusBadge(row, 'IsValid')?.type"
                  :text="resolveStatusBadge(row, 'IsValid')?.text"
                  size="small"
                />
              </template>
            </YzhTreeTable>
          </div>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 族表单弹窗 -->
    <YzhDialog
      v-model="logic.formVisible.value"
      :title="logic.formMode.value === 'add' ? '新增族' : '编辑族'"
      width="600px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
    >
      <el-form :model="logic.formData.value" label-width="100px" :disabled="logic.formSubmitting.value">
        <el-form-item label="族编号" required>
          <el-input v-model="logic.formData.value.FamilyNo" placeholder="如：iso9000" />
        </el-form-item>
        <el-form-item label="族名称" required>
          <el-input v-model="logic.formData.value.FamilyName" placeholder="如：ISO 9000 质量管理体系族" />
        </el-form-item>
        <el-form-item label="所属体系" required>
          <el-select v-model="logic.formData.value.Category" placeholder="选择体系" style="width:100%">
            <el-option v-for="c in (logic.categoryMap.value as Map<string,string>)" :key="c[0]" :label="c[1]" :value="c[0]" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="logic.formData.value.Sort" :min="0" :step="1" style="width:100%" />
        </el-form-item>
        <el-form-item label="说明">
          <el-input v-model="logic.formData.value.Description" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="logic.formData.value.IsValid" :active-value="1" :inactive-value="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button type="default" @click="logic.formVisible.value = false">取消</el-button>
        <el-button type="primary" :loading="logic.formSubmitting.value" @click="logic.submitFamilyForm()">确定</el-button>
      </template>
    </YzhDialog>

    <!-- 版本表单弹窗 -->
    <YzhDialog
      v-model="logic.versionDialogVisible.value"
      :title="logic.versionMode.value === 'add' ? '新增版本' : '编辑版本'"
      width="600px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
    >
      <el-form :model="logic.versionFormData.value" label-width="100px" :disabled="logic.versionSubmitting.value">
        <el-form-item label="标准编号" required>
          <el-input v-model="logic.versionFormData.value.StandardCode" placeholder="如：iso9001（不含年份）" />
        </el-form-item>
        <el-form-item label="标准名称" required>
          <el-input v-model="logic.versionFormData.value.StandardName" placeholder="如：质量管理体系" />
        </el-form-item>
        <el-form-item label="版本年份" required>
          <el-input-number v-model="logic.versionFormData.value.VersionYear" :min="1900" :max="2100" style="width:100%" />
        </el-form-item>
        <el-form-item label="体系">
          <el-input :value="categoryLabel(logic.versionFormData.value.Category)" disabled />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="logic.versionFormData.value.Sort" :min="0" :step="1" style="width:100%" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="logic.versionFormData.value.IsValid" :active-value="1" :inactive-value="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button type="default" @click="logic.versionDialogVisible.value = false">取消</el-button>
        <el-button type="primary" :loading="logic.versionSubmitting.value" @click="logic.submitVersionForm()">确定</el-button>
      </template>
    </YzhDialog>

    <!-- 条款表单弹窗 -->
    <YzhDialog
      v-model="logic.clauseDialogVisible.value"
      :title="logic.clauseMode.value === 'add' ? '新增条款' : '编辑条款'"
      width="600px"
      :close-on-click-modal="false"
      :destroy-on-close="false"
    >
      <el-form :model="logic.clauseFormData.value" label-width="100px" :disabled="logic.clauseSubmitting.value">
        <el-form-item label="条款编号" required>
          <el-input v-model="logic.clauseFormData.value.ClauseNumber" placeholder="如：1、1.1、1.1.1" />
        </el-form-item>
        <el-form-item label="条款标题" required>
          <el-input v-model="logic.clauseFormData.value.Title" placeholder="条款标题" />
        </el-form-item>
        <el-form-item label="上级条款">
          <el-select
            v-model="logic.clauseFormData.value.ParentCode"
            placeholder="留空为顶级"
            filterable
            clearable
            style="width:100%"
          >
            <el-option
              v-for="opt in logic.parentOptions.value"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="logic.clauseFormData.value.SortOrder" :min="0" :step="1" style="width:100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button type="default" @click="logic.clauseDialogVisible.value = false">取消</el-button>
        <el-button type="primary" :loading="logic.clauseSubmitting.value" @click="logic.submitClauseForm()">确定</el-button>
      </template>
    </YzhDialog>
  </div>
</template>

<style scoped>
.sm-page {
  height: 100%;
  box-sizing: border-box;
  overflow: hidden;
}

.sm-page__content {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  overflow: hidden;
}

.sm-page__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 200px;
}

.sm-page__table-area {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sm-page__table-area :deep(.el-table) {
  flex: 1;
  overflow: auto;
}
</style>
