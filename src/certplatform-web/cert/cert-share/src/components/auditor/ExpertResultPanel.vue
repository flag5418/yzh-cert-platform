<script setup lang="ts">
/**
 * 专家结果面板（NC 检查结果 / 报告结论 **共用**）
 *
 * ★ 为什么共用一个组件：两个菜单的**形态完全同构**（左树右表 + 勾选审批 + 单行修改 + 导出），
 *   差异只有 3 个字段（结论字段名、生成方式列、导出列头）。拆成两份代码 = 改一处漏一处。
 *   这与项目里「NC 与报告规则刻意共用 `WorkflowDesigner.vue`」是同一个决策。
 *
 * 版式（用户 2026-09-30 裁决）：
 *   左树：**企业 → 阶段 → 任务**（恒 3 级，标准不进树）
 *   右表：结论列表 —— **勾选批量审批 / 单行直接修改 / 整体列表导出**
 *   ⛔ **点企业或阶段节点 → 右表留空，不发请求**（必须选到任务）
 *
 * ★ 左树一次取全（`Full: true`）：工作区数据量是个位数量级，
 *   一次取回比懒加载少 N×M 次往返，也让「点企业/阶段不发请求」这条规则没有网络竞态。
 *
 * ★ 数据来源全部是**结果表**（`cert_expert_nc_result` / `cert_expert_report_result`），
 *   不是实体层 —— 结果表才是「多轮次」的载体（同一检查项可跑 15 轮）。
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { Page, PageParams, YzhTableColumn, YzhTreeNode } from '@yzh-core'
import { YzhTable, YzhTree, confirmOrFalse } from '@yzh-core'
import {
  acknowledgeResults,
  exportResults,
  getResultHistory,
  getResultList,
  getResultTree,
  modifyResult,
  type ResultHistoryRow,
  type ResultRow,
  type ResultTreeNode,
  type ResultType,
} from '@share/api/auditor/expert-result'
import {
  AUTO_STATUS_MAP,
  AUTO_STATUS_TAG,
  CONFORMITY_MAP,
  CONFORMITY_OPTIONS,
  CONFORMITY_TAG,
  GENERATION_MODE_TAG,
  REVIEW_STATUS_MAP,
  REVIEW_STATUS_TAG,
  SEVERITY_MAP,
  SEVERITY_OPTIONS,
  SEVERITY_TAG,
  SKIP_CATEGORY_MAP,
} from '@share/constants/expert-task'
import { formatDateTime } from '@share/utils/format'

const props = defineProps<{ type: ResultType }>()

const route = useRoute()
const router = useRouter()

const isReport = computed(() => props.type === 'report')
const menuLabel = computed(() => (isReport.value ? '报告结论' : 'NC 检查结果'))

// ══════════════════════════════════════════════════════════════════════
// 一、左树
// ══════════════════════════════════════════════════════════════════════

const treeData = ref<ResultTreeNode[]>([])
const treeLoading = ref(false)
const selectedTask = ref<{ Code: string; Name: string } | null>(null)
/** 当前选中的节点类型（enterprise / stage / task）—— 决定右表是否加载 */
const selectedNodeType = ref<string>('')

async function loadTree() {
  treeLoading.value = true
  try {
    treeData.value = await getResultTree(props.type)
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载结果树失败')
  } finally {
    treeLoading.value = false
  }
}

/**
 * 节点点击。
 *
 * ⛔ 点企业 / 阶段 → 右表留空且**不发请求**（用户裁决：「不显示，必须选到任务」）。
 */
function onNodeClick(node: YzhTreeNode) {
  const type = String(node.NodeType ?? '')
  selectedNodeType.value = type
  if (type === 'task') {
    selectedTask.value = { Code: node.Code, Name: node.Name }
    void reloadList()
  } else {
    selectedTask.value = null
    rows.value = []
    total.value = 0
    selectedRows.value = []
    // ★ 必须显式 refresh：`rows` 只是本组件的镜像，YzhTable 有自己的内部数据副本。
    //   不刷新的话，先点任务、再点企业/阶段时，右表会**残留上一个任务的结论** ——
    //   违背「点企业/阶段不显示」这条裁决。
    void reloadList()
  }
}

// ══════════════════════════════════════════════════════════════════════
// 二、右表（结论列表）
// ══════════════════════════════════════════════════════════════════════

const rows = ref<ResultRow[]>([])
const total = ref(0)
const listLoading = ref(false)
const selectedRows = ref<ResultRow[]>([])
const reviewFilter = ref<string>('')

const baseColumns: YzhTableColumn<ResultRow>[] = [
  { prop: 'StandardName', label: '标准', width: 150 },
  { prop: 'ClauseNumber', label: '条款', width: 100 },
  { prop: 'ItemName', label: '检查项 / 章节', minWidth: 220, showOverflowTooltip: true },
  { prop: 'RoundNo', label: '轮次', width: 70, align: 'center' },
  { prop: 'AutoStatus', label: '自动结果', width: 120, slot: true },
  { prop: 'ConclusionLabel', label: '结论', width: 110, slot: true },
  { prop: 'Severity', label: '严重度', width: 110, slot: true },
  { prop: 'SourceLabel', label: '结论来源', width: 110, slot: true },
  { prop: 'ReviewStatus', label: '复核状态', width: 100, slot: true },
  { prop: 'ResponsibleDept', label: '责任部门', width: 110, slot: true },
  { prop: 'CreateTime', label: '生成时间', width: 170, slot: true },
  { prop: 'ReviewName', label: '复核人', width: 100 },
  // ★ 2026-09-30：数据不足的行给一个直达补录清单的入口（否则专家只能自己找菜单）
  { prop: 'GapAction', label: '', width: 100, slot: true },
]

/** 报告专用列：生成方式三档（自动生成 / 引用上轮 / 人工撰写） */
const reportColumns: YzhTableColumn<ResultRow>[] = [
  ...baseColumns.slice(0, 4),
  { prop: 'GenerationMode', label: '生成方式', width: 120, slot: true },
  ...baseColumns.slice(4),
]

const columns = computed(() => (isReport.value ? reportColumns : baseColumns))

// ★ 2026-09-30：区分「数据不足，未检查」与「执行失败」。
//   前者是【无法判定】（引用的字段/表格没值），专家能通过补录解决；
//   后者是系统/配置问题，专家补录也解决不了。UI 上必须一眼分开。
const DATA_GAP_CATEGORIES = new Set(['data_gap', 'data_gap_skipped'])

function isDataGap(row: ResultRow): boolean {
  return !!row.SkipCategory && DATA_GAP_CATEGORIES.has(row.SkipCategory)
}

/** 直达该任务的补录清单页 */
function gotoGapFill(row: ResultRow) {
  if (!row.TaskCode) return
  router.push({ name: 'TaskDetail', params: { code: row.TaskCode }, query: { tab: 'gaps' } })
}

/** 列表数据源：YzhTable 的分页/排序由组件驱动，这里只透传给后端 */
async function dataLoader(params: PageParams): Promise<Page<ResultRow>> {
  if (!selectedTask.value) return { rows: [], total: 0 }
  listLoading.value = true
  try {
    const res = await getResultList({
      Type: props.type,
      TaskCode: selectedTask.value.Code,
      ReviewStatus: reviewFilter.value || null,
      Page: params.page ?? 1,
      PageSize: params.rows ?? 20,
    })
    rows.value = res.Rows ?? []
    total.value = res.Total ?? 0
    return { rows: rows.value, total: total.value }
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载结论列表失败')
    return { rows: [], total: 0 }
  } finally {
    listLoading.value = false
  }
}

const tableRef = ref<any>(null)

async function reloadList() {
  await tableRef.value?.refresh()
}

function onSelectionChange(list: ResultRow[]) {
  selectedRows.value = list
}

// ══════════════════════════════════════════════════════════════════════
// 三、审批（勾选批量认可）
// ══════════════════════════════════════════════════════════════════════

const acking = ref(false)

async function onAcknowledge() {
  const picked = selectedRows.value.filter((r) => r.Code)
  if (picked.length === 0) {
    ElMessage.warning('请先勾选要认可的结论（未生成结果的项无法认可）')
    return
  }
  const ok = await confirmOrFalse(
    `将认可 ${picked.length} 条结论，认可后复核状态变为「已认可」。\n\n确定？`,
    '批量认可',
    { confirmButtonText: '确定认可' },
  )
  if (!ok) return
  acking.value = true
  try {
    const n = await acknowledgeResults(props.type, picked.map((r) => r.Code))
    ElMessage.success(`已认可 ${n} 条结论`)
    tableRef.value?.clearSelection?.()
    await reloadList()
  } finally {
    acking.value = false
  }
}

// ══════════════════════════════════════════════════════════════════════
// 四、修改（单行直接修改）
// ══════════════════════════════════════════════════════════════════════

const editVisible = ref(false)
const editSaving = ref(false)
const editing = ref<ResultRow | null>(null)

const editForm = ref({
  Conformity: '',
  Severity: '',
  ContentText: '',
  EvidenceRef: '',
  ReviewRemark: '',
})

function openEdit(row: ResultRow) {
  if (!row.Code) {
    ElMessage.warning('该检查项还没有生成结果，暂不能修改（请先执行任务）')
    return
  }
  editing.value = row
  editForm.value = {
    Conformity: row.Conformity ?? '',
    Severity: row.Severity ?? '',
    ContentText: row.ContentText ?? '',
    EvidenceRef: row.EvidenceRef ?? '',
    ReviewRemark: row.ReviewRemark ?? '',
  }
  editVisible.value = true
}

async function saveEdit() {
  const row = editing.value
  if (!row) return
  editSaving.value = true
  try {
    await modifyResult({
      Type: props.type,
      Code: row.Code,
      Conformity: isReport.value ? null : editForm.value.Conformity || null,
      Severity: isReport.value ? null : editForm.value.Severity || null,
      ContentText: editForm.value.ContentText || null,
      EvidenceRef: isReport.value ? null : editForm.value.EvidenceRef || null,
      ReviewRemark: editForm.value.ReviewRemark || null,
    })
    ElMessage.success('已保存修改')
    editVisible.value = false
    await reloadList()
  } finally {
    editSaving.value = false
  }
}

// ══════════════════════════════════════════════════════════════════════
// 五、历史轮次（多轮下钻）
// ══════════════════════════════════════════════════════════════════════

const historyVisible = ref(false)
const historyLoading = ref(false)
const historyRows = ref<ResultHistoryRow[]>([])
const historyItemName = ref('')

const historyColumns: YzhTableColumn<ResultHistoryRow>[] = [
  { prop: 'RoundNo', label: '轮次', width: 70, align: 'center' },
  { prop: 'CreateTime', label: '时间', width: 170, slot: true },
  { prop: 'AutoStatus', label: '自动结果', width: 110, slot: true },
  { prop: 'Conformity', label: '结论', width: 100, slot: true },
  { prop: 'Severity', label: '严重度', width: 110, slot: true },
  { prop: 'ContentText', label: '正文 / 描述', minWidth: 260, showOverflowTooltip: true },
  { prop: 'ReviewStatus', label: '复核状态', width: 100, slot: true },
  { prop: 'ReviewName', label: '复核人', width: 100 },
  { prop: 'ReviewTime', label: '复核时间', width: 170, slot: true },
]

/** 历史轮次表格实例（★ 必须显式 refresh —— 数据是异步填的，表格挂载时还是空数组） */
const historyTableRef = ref<any>(null)

async function openHistory(row: ResultRow) {
  historyItemName.value = row.ItemName
  historyVisible.value = true
  historyLoading.value = true
  historyRows.value = []
  try {
    historyRows.value = await getResultHistory(props.type, row.ItemCode)
    await nextTick()
    historyTableRef.value?.refresh?.()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载历史轮次失败')
  } finally {
    historyLoading.value = false
  }
}

async function historyLoader(params: PageParams): Promise<Page<ResultHistoryRow>> {
  const page = params.page ?? 1
  const size = params.rows ?? 20
  return {
    rows: historyRows.value.slice((page - 1) * size, page * size),
    total: historyRows.value.length,
  }
}

// ══════════════════════════════════════════════════════════════════════
// 六、导出（整体列表）
// ══════════════════════════════════════════════════════════════════════

const exporting = ref(false)

async function onExport() {
  if (!selectedTask.value) {
    ElMessage.warning('请先在左侧选到具体任务，再导出')
    return
  }
  exporting.value = true
  try {
    const stamp = new Date().toISOString().slice(0, 19).replace(/[-:T]/g, '')
    await exportResults(
      { Type: props.type, TaskCode: selectedTask.value.Code },
      `${menuLabel.value}_${selectedTask.value.Name}_${stamp}.csv`,
    )
    ElMessage.success('导出已开始下载')
  } finally {
    exporting.value = false
  }
}

// ══════════════════════════════════════════════════════════════════════
// 七、行按钮
// ══════════════════════════════════════════════════════════════════════

const rowActions = computed(() => (_row: ResultRow) => [
  // 历史轮次恒可点（同一检查项可跑多轮；只跑过 1 轮时也值得看那一轮的自动判定原文）
  { key: 'edit', text: isReport.value ? '修改正文' : '修改结论', type: 'primary' as const },
  { key: 'history', text: '历史轮次', type: 'info' as const },
])

async function onRowAction(key: string, row: ResultRow) {
  if (key === 'edit') openEdit(row)
  else if (key === 'history') await openHistory(row)
}

// ══════════════════════════════════════════════════════════════════════
// 八、初始化
// ══════════════════════════════════════════════════════════════════════

/** 从任务详情「看结果」跳过来时带的 `?task=xxx` —— 自动定位到该任务 */
async function autoSelectFromQuery() {
  const code = route.query.task
  if (typeof code !== 'string' || !code) return
  for (const ent of treeData.value) {
    for (const stage of ent.Children ?? []) {
      const hit = (stage.Children ?? []).find((t) => t.Code === code)
      if (hit) {
        selectedNodeType.value = 'task'
        selectedTask.value = { Code: hit.Code, Name: hit.Name }
        await reloadList()
        return
      }
    }
  }
}

onMounted(async () => {
  await loadTree()
  await autoSelectFromQuery()
})

// 两个菜单复用同一组件：类型变化必须整页重载（路由复用时组件不重建）
watch(
  () => props.type,
  async () => {
    selectedTask.value = null
    selectedNodeType.value = ''
    rows.value = []
    total.value = 0
    reviewFilter.value = ''
    await loadTree()
  },
)
</script>

<template>
  <div class="er">
    <!-- ════ 左侧：企业 → 阶段 → 任务 ════ -->
    <div class="er__tree">
      <div class="er__tree-head">
        <span class="er__tree-title">{{ menuLabel }}</span>
        <el-button link size="small" :loading="treeLoading" @click="loadTree">刷新</el-button>
      </div>
      <div v-loading="treeLoading" class="er__tree-body">
        <YzhTree
          :data="treeData"
          node-key="Code"
          default-expand-all
          searchable
          search-placeholder="搜索企业 / 阶段 / 任务"
          @node-click="onNodeClick"
        />
      </div>
      <div class="er__tree-foot">
        <span>点「任务」节点查看结果</span>
      </div>
    </div>

    <!-- ════ 右侧：结论列表 ════ -->
    <div class="er__main">
      <div class="er__bar">
        <div class="er__bar-left">
          <template v-if="selectedTask">
            <span class="er__bar-title">{{ selectedTask.Name }}</span>
            <el-tag size="small" type="info" effect="plain">共 {{ total }} 条结论</el-tag>
          </template>
          <span v-else class="er__bar-empty">
            {{
              selectedNodeType === ''
                ? '请先在左侧选择「任务」节点'
                : '已选中「' + (selectedNodeType === 'enterprise' ? '企业' : '阶段') + '」节点 —— 请继续展开到具体任务'
            }}
          </span>
        </div>
        <div class="er__bar-right">
          <el-select
            v-model="reviewFilter"
            size="small"
            placeholder="复核状态"
            clearable
            style="width: 130px"
            :disabled="!selectedTask"
            @change="reloadList"
          >
            <el-option label="待审批" value="pending_review" />
            <el-option label="已认可" value="reviewed" />
            <el-option label="已修改" value="modified" />
            <el-option label="未复核" value="not_started" />
          </el-select>
          <el-button
            type="primary"
            size="small"
            :loading="acking"
            :disabled="!selectedTask || selectedRows.length === 0"
            @click="onAcknowledge"
          >
            认可勾选（{{ selectedRows.length }}）
          </el-button>
          <el-button size="small" :loading="exporting" :disabled="!selectedTask" @click="onExport">
            导出列表
          </el-button>
        </div>
      </div>

      <div class="er__table">
        <YzhTable
          ref="tableRef"
          :columns="columns"
          :data-loader="dataLoader"
          :row-action-buttons="rowActions"
          :page-size="20"
          select-mode="multiple"
          row-key="ItemCode"
          :empty-text="selectedTask ? '该任务下暂无结论（可能还没执行）' : '请先选择具体任务'"
          @selection-change="onSelectionChange"
          @row-action="onRowAction"
        >
          <!-- 自动结果
               ★ 2026-09-30：缺数据（SkipCategory='data_gap'）要【一眼可辨】且能直达补录页 ——
               「执行失败」与「数据不足」在专家眼里都是"没结果"，但处理方式完全不同：
               前者报 bug，后者去补录。 -->
          <template #column-AutoStatus="{ row }">
            <el-tooltip
              v-if="isDataGap(row)"
              placement="top"
            >
              <template #content>
                <div><strong>缺失必要数据</strong>（不是不符合，是无法判定）</div>
                <div v-if="row.SkipReason" style="margin-top:4px">{{ row.SkipReason }}</div>
                <div style="margin-top:4px">点击「去补录」补充数据后重跑</div>
              </template>
              <el-tag type="danger" size="small" effect="dark">
                数据不足，未检查
              </el-tag>
            </el-tooltip>
            <el-tag
              v-else
              :type="AUTO_STATUS_TAG[row.AutoStatus] ?? 'info'"
              size="small"
              effect="plain"
            >
              {{ AUTO_STATUS_MAP[row.AutoStatus] ?? row.AutoStatus }}
            </el-tag>
          </template>

          <!-- ★ 数据不足 → 直达补录清单 -->
          <template #column-GapAction="{ row }">
            <el-button
              v-if="isDataGap(row)"
              size="small"
              type="danger"
              plain
              @click.stop="gotoGapFill(row)"
            >
              去补录
            </el-button>
          </template>

          <!-- 结论（专家结论优先，为空回落到自动结果的人话） -->
          <template #column-ConclusionLabel="{ row }">
            <el-tag
              v-if="row.Conformity"
              :type="CONFORMITY_TAG[row.Conformity] ?? 'info'"
              size="small"
            >
              {{ row.ConclusionLabel }}
            </el-tag>
            <span v-else>{{ row.ConclusionLabel }}</span>
          </template>

          <!-- 严重度 -->
          <template #column-Severity="{ row }">
            <el-tag
              v-if="row.Severity || row.AutoSeverity"
              :type="SEVERITY_TAG[(row.Severity ?? row.AutoSeverity)!] ?? 'info'"
              size="small"
              effect="plain"
            >
              {{ SEVERITY_MAP[(row.Severity ?? row.AutoSeverity)!] ?? (row.Severity ?? row.AutoSeverity) }}
            </el-tag>
            <span v-else>—</span>
          </template>

          <!-- 结论来源：自动 / 人工修改（辅助系统不替代正式报告的显式证据） -->
          <template #column-SourceLabel="{ row }">
            <el-tag
              size="small"
              :type="row.IsModified ? 'primary' : 'info'"
              effect="plain"
              disable-transitions
            >
              {{ row.SourceLabel }}
            </el-tag>
          </template>

          <!-- 复核状态 -->
          <template #column-ReviewStatus="{ row }">
            <el-tag :type="REVIEW_STATUS_TAG[row.ReviewStatus] ?? 'info'" size="small">
              {{ REVIEW_STATUS_MAP[row.ReviewStatus] ?? row.ReviewStatus }}
            </el-tag>
          </template>

          <!-- 责任部门：D-E 列先加、数据先空 -->
          <template #column-ResponsibleDept="{ row }">
            {{ row.ResponsibleDept || '—' }}
          </template>

          <!-- 报告：生成方式三档 -->
          <template #column-GenerationMode="{ row }">
            <el-tag
              v-if="row.GenerationMode"
              :type="GENERATION_MODE_TAG[row.GenerationMode] ?? 'info'"
              size="small"
              effect="plain"
            >
              {{ row.GenerationMode }}
            </el-tag>
            <span v-else>—</span>
          </template>

          <template #column-CreateTime="{ row }">
            {{ formatDateTime(row.CreateTime) }}
          </template>

          <template #toolbar-left>
            <span v-if="selectedTask" class="er__hint">
              <template v-if="selectedRows.length === 0">
                勾选多行 → 「认可勾选」批量审批；单行「修改」直接改结论
              </template>
              <template v-else>已勾选 {{ selectedRows.length }} 条</template>
            </span>
          </template>
        </YzhTable>
      </div>
    </div>

    <!-- ════ 修改弹窗 ════ -->
    <el-dialog
      v-model="editVisible"
      :title="isReport ? '修改章节正文' : '修改 NC 结论'"
      width="720px"
      append-to-body
    >
      <div v-if="editing" class="er__edit-ctx">
        <div class="er__edit-ctx-row">
          <span class="er__edit-label">标准</span><span>{{ editing.StandardName }}</span>
          <span class="er__edit-label">检查项</span><span>{{ editing.ItemName }}</span>
        </div>
        <div class="er__edit-ctx-row">
          <span class="er__edit-label">轮次</span><span>第 {{ editing.RoundNo }} 轮</span>
          <span class="er__edit-label">自动结果</span>
          <span>
            {{ AUTO_STATUS_MAP[editing.AutoStatus] ?? editing.AutoStatus }}
            <template v-if="editing.AutoSeverity">
              / {{ SEVERITY_MAP[editing.AutoSeverity] ?? editing.AutoSeverity }}
            </template>
          </span>
        </div>
        <div v-if="editing.SkipCategory" class="er__edit-ctx-row er__edit-ctx-row--warn">
          <span class="er__edit-label">跳过原因</span>
          <span>{{ SKIP_CATEGORY_MAP[editing.SkipCategory] ?? editing.SkipCategory }}</span>
        </div>
        <div v-if="editing.AutoDescription" class="er__edit-ctx-row er__edit-ctx-row--auto">
          <span class="er__edit-label">自动判定说明</span>
          <span>{{ editing.AutoDescription }}</span>
        </div>
      </div>

      <el-form label-width="96px" class="er__edit-form">
        <template v-if="!isReport">
          <el-form-item label="结论">
            <el-radio-group v-model="editForm.Conformity">
              <el-radio-button v-for="o in CONFORMITY_OPTIONS" :key="o.value" :value="o.value">
                {{ o.label }}
              </el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="严重度">
            <el-select v-model="editForm.Severity" clearable placeholder="请选择" style="width: 240px">
              <el-option v-for="o in SEVERITY_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="不符合描述">
            <el-input v-model="editForm.ContentText" type="textarea" :rows="5" maxlength="4000" />
          </el-form-item>
          <el-form-item label="客观证据">
            <el-input
              v-model="editForm.EvidenceRef"
              type="textarea"
              :rows="3"
              maxlength="2000"
              placeholder="引用的企业资料 / 现场记录等"
            />
          </el-form-item>
        </template>

        <template v-else>
          <el-form-item label="章节正文">
            <el-input v-model="editForm.ContentText" type="textarea" :rows="12" maxlength="20000" />
          </el-form-item>
          <el-alert
            type="warning"
            :closable="false"
            show-icon
            title="正文为纯文本（D19）"
            description="章节正文按纯文本保存与导出，不使用富文本。系统生成的内容仅为参照，专家须自行核对。"
            class="er__edit-tip"
          />
        </template>

        <el-form-item label="复核备注">
          <el-input v-model="editForm.ReviewRemark" maxlength="500" placeholder="选填" />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="editSaving" @click="saveEdit">保存</el-button>
      </template>
    </el-dialog>

    <!-- ════ 历史轮次 ════ -->
    <el-dialog v-model="historyVisible" :title="`历史轮次 — ${historyItemName}`" width="1000px" append-to-body>
      <div class="er__history">
        <YzhTable
          ref="historyTableRef"
          :columns="historyColumns"
          :data-loader="historyLoader"
          :show-pagination="false"
          :toolbar="false"
          empty-text="暂无历史轮次"
        >
          <template #column-CreateTime="{ row }">
            {{ row.CreateTime ? formatDateTime(row.CreateTime) : '—' }}
          </template>
          <template #column-AutoStatus="{ row }">
            <el-tag :type="AUTO_STATUS_TAG[row.AutoStatus ?? 'none'] ?? 'info'" size="small" effect="plain">
              {{ AUTO_STATUS_MAP[row.AutoStatus ?? 'none'] ?? row.AutoStatus }}
            </el-tag>
          </template>
          <template #column-Conformity="{ row }">
            {{ row.Conformity ? CONFORMITY_MAP[row.Conformity] ?? row.Conformity : '—' }}
          </template>
          <template #column-Severity="{ row }">
            {{ row.Severity ? SEVERITY_MAP[row.Severity] ?? row.Severity : '—' }}
          </template>
          <template #column-ReviewStatus="{ row }">
            <el-tag :type="REVIEW_STATUS_TAG[row.ReviewStatus ?? ''] ?? 'info'" size="small">
              {{ REVIEW_STATUS_MAP[row.ReviewStatus ?? ''] ?? row.ReviewStatus }}
            </el-tag>
          </template>
          <template #column-ReviewTime="{ row }">
            {{ row.ReviewTime ? formatDateTime(row.ReviewTime) : '—' }}
          </template>
        </YzhTable>
      </div>
    </el-dialog>
  </div>
</template>

<style scoped>
.er {
  height: 100%;
  display: flex;
  background: var(--yzh-color-bg-container, #fff);
  border-radius: 4px;
  overflow: hidden;
}

/* ──── 左树 ──── */

.er__tree {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}

.er__tree-head {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.er__tree-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.er__tree-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.er__tree-foot {
  flex-shrink: 0;
  padding: 8px 12px;
  border-top: 1px solid var(--el-border-color-lighter);
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* ──── 右表 ──── */

.er__main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.er__bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.er__bar-left {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.er__bar-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.er__bar-empty {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.er__bar-right {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.er__table {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.er__hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* ──── 修改弹窗 ──── */

.er__edit-ctx {
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
  padding: 10px 12px;
  margin-bottom: 14px;
  font-size: 13px;
  line-height: 1.9;
}

.er__edit-ctx-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.er__edit-ctx-row--warn {
  color: var(--el-color-warning);
}

.er__edit-ctx-row--auto {
  color: var(--el-text-color-secondary);
}

.er__edit-label {
  color: var(--el-text-color-secondary);
  margin-right: 2px;
}

.er__edit-label + span {
  margin-right: 16px;
}

.er__edit-tip {
  margin-bottom: 14px;
}

.er__edit-form :deep(.el-textarea__inner) {
  font-family: inherit;
}

.er__history {
  height: 480px;
}
</style>
