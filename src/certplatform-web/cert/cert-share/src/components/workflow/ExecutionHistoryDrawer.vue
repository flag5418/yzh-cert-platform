<!--
  ExecutionHistoryDrawer.vue — 工作流测试历史抽屉（共享层，阶段四新增）

  设计约束（项目「工作流规则页的抽象设计」）：本组件必须建在 cert-share 共享层，
  让 NC 规则设置与报告规则设置**自动同时获得**该能力；绝不在两个页面各写一遍。

  数据来源（后端 WorkflowTestController 只读端点）：
    POST /api/Admin/Workflow/test/history            列表（四层模型第一层）
    GET  /api/Admin/Workflow/test/detail/{taskCode}  展开时按需加载四层聚合详情

  为什么用「展开行懒加载」而不是「点开跳新页」：
  一次测试的四层数据（task/item/path/node）通常几十行，抽屉内直接展开比跳转更省事；
  但列表可能有上百条，所以详情**只在展开时请求**，避免一次性拉全量。

  「节点复用」怎么读：路径表 `ReusedCount` 是 DB 层的唯一证据
  （节点表 wf_node_execution 已按 NodeId 去重，IsReused 恒为 0 属预期行为）。
-->
<template>
  <YzhDrawer
    :model-value="modelValue"
    :title="drawerTitle"
    size="1080px"
    direction="rtl"
    :show-footer="false"
    @update:model-value="(v: boolean) => $emit('update:modelValue', v)"
    @open="onOpen"
  >
    <div class="history-drawer">
      <!-- ===== 筛选条 ===== -->
      <div class="filter-bar">
        <el-select v-model="filters.TestScope" placeholder="测试范围" size="small" clearable style="width: 130px">
          <el-option label="整流（FULL）" value="FULL" />
          <el-option label="单节点（NODE）" value="NODE" />
          <el-option label="AI 节点（AI_NODE）" value="AI_NODE" />
        </el-select>
        <el-select v-model="filters.TaskStatus" placeholder="状态" size="small" clearable style="width: 120px">
          <el-option label="已完成" value="completed" />
          <el-option label="失败" value="failed" />
          <el-option label="执行中" value="executing" />
        </el-select>
        <el-input v-model="filters.RuleCode" placeholder="规则编码" size="small" clearable style="width: 220px" />
        <el-button size="small" type="primary" :loading="loading" @click="reload">
          <el-icon><Search /></el-icon> 查询
        </el-button>
        <el-button size="small" @click="resetFilters">重置</el-button>
      </div>

      <!-- ===== 列表（R6：YzhTable；展开列 = type:'expand' + #expand 插槽） ===== -->
      <YzhTable
        ref="tableRef"
        :columns="historyColumns"
        :data-loader="dataLoader"
        :toolbar="false"
        :page-size="20"
        size="small"
        row-key="TaskCode"
        class="history-table"
        empty-text="暂无测试历史"
        @expand-change="onExpandChange"
      >
        <!-- 展开行：四层详情（懒加载 + 缓存） -->
        <template #expand="{ row }">
          <div class="detail-wrapper">
            <div v-if="detailLoading === row.TaskCode" class="detail-loading">加载中…</div>
            <template v-else-if="details[row.TaskCode]">
              <div class="detail-summary">
                <span>ItemCode: <code>{{ details[row.TaskCode]?.Items?.[0]?.ItemCode || '—' }}</code></span>
                <span v-if="details[row.TaskCode]!.RuleContext?.RuleName">规则: {{ details[row.TaskCode]!.RuleContext!.RuleName }}</span>
                <span v-if="details[row.TaskCode]!.RuleContext?.SeverityIfViolated">严重级别: {{ details[row.TaskCode]!.RuleContext!.SeverityIfViolated }}</span>
                <span>企业: {{ row.EnterpriseCode || '—' }}</span>
                <span>阶段: {{ row.PhaseCode || '—' }}</span>
                <span v-if="row.ErrorMessage" class="error-text">错误: {{ row.ErrorMessage }}</span>
              </div>

              <!-- 路径层 -->
              <div class="detail-section">
                <div class="detail-section-title">路径执行（{{ details[row.TaskCode]?.Paths?.length || 0 }} 条）</div>
                <YzhTable
                  :columns="pathColumns"
                  :data="details[row.TaskCode]!.Paths"
                  :toolbar="false"
                  :show-pagination="false"
                  size="small"
                  row-key="PathIndex"
                  empty-text="暂无路径"
                >
                  <template #column-PathIndex="{ row: p }">{{ p.PathIndex + 1 }}</template>
                  <template #column-PathStatus="{ row: p }">
                    <el-tag :type="p.Status === 'completed' ? 'success' : 'danger'" size="small">{{ p.Status }}</el-tag>
                  </template>
                  <template #column-PathDuration="{ row: p }">{{ p.DurationMs }}ms</template>
                  <template #column-PathReused="{ row: p }">
                    <el-tag v-if="p.ReusedCount > 0" type="info" size="small">{{ p.ReusedCount }}</el-tag>
                    <span v-else class="muted">—</span>
                  </template>
                  <template #column-NodeIds="{ row: p }"><span class="mono">{{ (p.NodeIds || []).join(' → ') }}</span></template>
                  <template #column-PathOutput="{ row: p }">
                    <span v-if="p.ErrorMessage" class="error-text">{{ p.FailedAtNodeId }}: {{ p.ErrorMessage }}</span>
                    <el-popover v-else-if="p.Output" placement="left" :width="440" trigger="click">
                      <template #reference><el-button link size="small">查看输出</el-button></template>
                      <pre class="json-block">{{ formatJson(p.Output) }}</pre>
                    </el-popover>
                    <span v-else class="muted">—</span>
                  </template>
                </YzhTable>
              </div>

              <!-- 节点层 -->
              <div class="detail-section">
                <div class="detail-section-title">节点执行（{{ details[row.TaskCode]?.Nodes?.length || 0 }} 个，已按 NodeId 去重）</div>
                <YzhTable
                  :columns="nodeColumns"
                  :data="details[row.TaskCode]!.Nodes"
                  :toolbar="false"
                  :show-pagination="false"
                  size="small"
                  row-key="NodeId"
                  empty-text="暂无节点"
                >
                  <template #column-Node="{ row: n }">
                    <div class="node-cell">
                      <span class="mono node-title">{{ n.NodeTitle || n.NodeId }}</span>
                      <span class="node-desc">{{ n.NodeDescription || '' }}</span>
                    </div>
                  </template>
                  <template #column-NodeStatus="{ row: n }">
                    <el-tag :type="n.ExecStatus === 'completed' ? 'success' : 'danger'" size="small">{{ n.ExecStatus }}</el-tag>
                  </template>
                  <template #column-NodeDuration="{ row: n }">{{ n.ExecutionTimeMs }}ms</template>
                  <template #column-Timing="{ row: n }">
                    <span class="muted">{{ shortTime(n.StartedAt) }} → {{ shortTime(n.CompletedAt) }}</span>
                  </template>
                  <template #column-NodeOutput="{ row: n }">
                    <span v-if="n.ErrorMessage" class="error-text">{{ n.ErrorMessage }}</span>
                    <el-popover v-else-if="n.Output" placement="left" :width="440" trigger="click">
                      <template #reference><el-button link size="small">查看输出</el-button></template>
                      <pre class="json-block">{{ formatJson(n.Output) }}</pre>
                    </el-popover>
                    <span v-else class="muted">—</span>
                  </template>
                </YzhTable>
              </div>
            </template>
            <div v-else class="detail-loading">无详情数据</div>
          </div>
        </template>

        <template #column-CreateTime="{ row }">{{ formatTime(row.CreateTime) }}</template>
        <template #column-Scope="{ row }">
          <el-tag size="small" :type="scopeTagType(row.TestScope)">{{ row.TestScope }}</el-tag>
        </template>
        <template #column-Status="{ row }">
          <el-tag size="small" :type="row.TaskStatus === 'completed' ? 'success' : 'danger'">{{ row.TaskStatus }}</el-tag>
        </template>
        <template #column-Duration="{ row }">{{ row.DurationMs ?? '—' }}ms</template>
        <template #column-TaskCode="{ row }"><span class="mono muted">{{ row.TaskCode }}</span></template>
      </YzhTable>
    </div>
  </YzhDrawer>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhDrawer, YzhTable, type Page, type PageParams, type YzhTableColumn } from '@yzh-core'
import {
  getTaskDetail,
  getTaskHistory,
  approveNode,
  type TaskExecutionDetail,
  type TaskHistoryItem,
} from '@share/api/workflow/execution'

const props = withDefaults(defineProps<{
  modelValue: boolean
  /** 预置的规则编码筛选（NC / 报告规则页会传入当前选中叶子节点的编码） */
  ruleCode?: string
  /** 预置的测试范围筛选 */
  testScope?: string
}>(), { ruleCode: '', testScope: '' })

defineEmits<{ 'update:modelValue': [boolean] }>()
// 显式暴露给 template（vue-tsc noUnusedLocals 无法识别模板引用）
defineExpose({ nodeApprovalStatus, showApprovalDialog, handleApprove })

const drawerTitle = computed(() => '测试历史')

/** 表格实例（重查/重置走它的 refresh，分页状态由组件自己管） */
const tableRef = ref<any>(null)
/** 查询按钮 loading（YzhTable 内部 loading 不对外暴露） */
const loading = ref(false)

const filters = reactive({
  TestScope: props.testScope || '',
  TaskStatus: '',
  RuleCode: props.ruleCode || '',
})

// ========================================================
// 列定义
// ★ 无字段的展示列必须给 `slotKey`（插槽名 = column-${slotKey}）；
//   只写 label 不给 slotKey 会渲染成空白列。
// ========================================================

/** 主表：一次测试任务一行，展开看四层详情 */
const historyColumns: YzhTableColumn<TaskHistoryItem>[] = [
  { type: 'expand', width: 40 },
  { slotKey: 'CreateTime', label: '时间', width: 160, slot: true },
  { slotKey: 'Scope', label: '范围', width: 90, align: 'center', slot: true },
  { slotKey: 'Status', label: '状态', width: 90, align: 'center', slot: true },
  { prop: 'RuleCode', label: '规则编码', minWidth: 180, showOverflowTooltip: true },
  { slotKey: 'Duration', label: '耗时', width: 80, align: 'right', slot: true },
  { prop: 'PathCount', label: '路径', width: 64, align: 'center' },
  { prop: 'NodeCount', label: '节点', width: 64, align: 'center' },
  { slotKey: 'TaskCode', label: 'TaskCode', minWidth: 240, slot: true },
]

/** 展开层一：路径执行 */
const pathColumns: YzhTableColumn[] = [
  { slotKey: 'PathIndex', label: '路径', width: 70, align: 'center', slot: true },
  { slotKey: 'PathStatus', label: '状态', width: 90, align: 'center', slot: true },
  { slotKey: 'PathDuration', label: '耗时', width: 80, align: 'right', slot: true },
  { slotKey: 'PathReused', label: '复用', width: 70, align: 'center', slot: true },
  { slotKey: 'NodeIds', label: '节点链', minWidth: 260, slot: true },
  { slotKey: 'PathOutput', label: '失败点 / 输出', minWidth: 200, slot: true },
]

/** 展开层二：节点执行（NodeType / NodeTitle 是真字段，其余走插槽） */
const nodeColumns: YzhTableColumn[] = [
  { slotKey: 'Node', label: '节点', minWidth: 160, slot: true },
  { prop: 'NodeType', label: '类型', width: 90 },
  { prop: 'NodeTitle', label: '名称', minWidth: 120 },
  { slotKey: 'NodeStatus', label: '状态', width: 80, align: 'center', slot: true },
  { slotKey: 'NodeDuration', label: '耗时', width: 80, align: 'right', slot: true },
  { slotKey: 'Timing', label: '时序', width: 150, slot: true },
  { slotKey: 'NodeOutput', label: '输出 / 错误', minWidth: 200, slot: true },
]/** TaskCode → 四层详情（展开时懒加载并缓存） */
const details = reactive<Record<string, TaskExecutionDetail>>({})
const detailLoading = ref<string | null>(null)

function onOpen() {
  // 每次打开都同步一次外部传入的筛选，并刷新列表
  filters.RuleCode = props.ruleCode || filters.RuleCode
  if (props.testScope) filters.TestScope = props.testScope
  reload()
}

/**
 * YzhTable 数据加载器：分页/页码全部由组件驱动，这里只把筛选条件透给后端。
 *
 * ⛔ 失败**不要**自己吞掉并 `return { rows: [] }` —— 那会把「查询失败」渲染成
 *    「暂无数据」；直接 throw，由 YzhTable 走错误态（保留上次数据 + 提示）。
 */
async function dataLoader(params: PageParams): Promise<Page<TaskHistoryItem>> {
  loading.value = true
  try {
    const res = await getTaskHistory({
      Page: params.page,
      PageSize: params.rows,
      RuleCode: filters.RuleCode || undefined,
      TestScope: filters.TestScope || undefined,
      TaskStatus: filters.TaskStatus || undefined,
      TaskType: 'TEST',
    })
    return { rows: res?.Items || [], total: res?.TotalCount || 0 }
  } finally {
    loading.value = false
  }
}

/**
 * 重新查询。
 *
 * ★ 首次打开时表格还没挂载（`el-drawer` 懒渲染），此时 `tableRef` 为 null ⇒
 *   什么都不做，等表格挂载后由它自己的 `onMounted` 用**已同步好的 filters** 加载。
 *   ⛔ 别在这里无脑 `refresh()`，会打出两次相同请求。
 */
function reload() {
  tableRef.value?.refresh()
}

function resetFilters() {
  filters.TestScope = ''
  filters.TaskStatus = ''
  filters.RuleCode = ''
  reload()
}

/** 展开行时按需加载详情（已缓存则不再请求） */
async function onExpandChange(row: TaskHistoryItem, expandedRows: TaskHistoryItem[]) {
  const isExpanding = (expandedRows || []).some(r => r.TaskCode === row.TaskCode)
  if (!isExpanding || details[row.TaskCode]) return

  detailLoading.value = row.TaskCode
  try {
    details[row.TaskCode] = await getTaskDetail(row.TaskCode)
  } catch (e: any) {
    ElMessage.error('详情加载失败: ' + (e?.message || e))
  } finally {
    detailLoading.value = null
  }
}

function scopeTagType(scope: string) {
  return scope === 'FULL' ? 'success' : scope === 'AI_NODE' ? 'warning' : 'info'
}

function formatTime(t?: string) {
  if (!t) return '—'
  const d = new Date(t)
  if (Number.isNaN(d.getTime())) return t
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
}

/** 节点级时序只显示时分秒 —— 完整日期在「时间」列已有，重复展示反而难读 */
function shortTime(t?: string) {
  if (!t) return '—'
  const d = new Date(t)
  if (Number.isNaN(d.getTime())) return t
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
}

const approveState = reactive<Record<string, { status: string; comment: string; confidence: number }>>({})

function nodeApprovalStatus(taskCode: string, nodeId: string): string {
  return details[taskCode]?.NodeApprovals?.[nodeId]?.ApprovalStatus || 'pending'
}

function showApprovalDialog(row: TaskHistoryItem, nodeId: string, nodeData: any, reject = false): void {
  ElMessageBox.prompt(reject ? '请填写驳回意见' : '请填写审批意见', '节点审批', {
    confirmButtonText: reject ? '确认驳回' : '确认批准',
    cancelButtonText: '取消',
    inputPlaceholder: reject ? '驳回原因（必填）' : '审批意见（可选）',
    inputValidator: (val) => reject ? !!val : true,
    inputErrorMessage: '驳回必须填写原因',
  }).then(({ value }) => {
    handleApprove(row, nodeId, reject ? 'rejected' : 'approved', { ...nodeData, comment: value })
  }).catch(() => {})
}

async function handleApprove(row: TaskHistoryItem, nodeId: string, status: 'approved' | 'rejected', nodeData: any) {
  const key = row.TaskCode + '|' + nodeId
  const state = approveState[key] || { status: '', comment: '', confidence: 0.8 }
  if (!state.comment && status === 'approved') state.comment = '认可'
  try {
    await approveNode({
      TaskCode: row.TaskCode,
      NodeId: nodeId,
      ApprovalStatus: status,
      Comment: state.comment,
      Confidence: state.confidence,
      ManualResult: nodeData?.ApprovedOutput || undefined,
    })
    ElMessage.success(status === 'approved' ? '已批准' : '已驳回')
    // 刷新详情
    if (details[row.TaskCode]) {
      const detail = await getTaskDetail(row.TaskCode)
      details[row.TaskCode] = detail
    }
  } catch (e: any) {
    ElMessage.error('审批失败: ' + (e?.message || e))
  }
}

// 以下函数仅通过 Vue template 引用，vue-tsc 的 noUnusedLocals 无法识别模板引用
// @vue/compiler-sfc 会在编译时正确处理
function formatJson(obj: any) {
  if (obj == null) return ''
  try {
    return typeof obj === 'string' ? JSON.stringify(JSON.parse(obj), null, 2) : JSON.stringify(obj, null, 2)
  } catch {
    return String(obj)
  }
}
</script>

<style scoped lang="less">
.history-drawer { display: flex; flex-direction: column; gap: 10px; height: 100%; }
.filter-bar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding-bottom: 8px; border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5); }
.history-table { font-size: 12px; flex: 1; min-height: 0; }
.node-cell { display: flex; flex-direction: column; line-height: 1.3; .node-title { font-family: monospace; } .node-desc { color: var(--yzh-color-text-tertiary, #909399); font-size: 11px; } }
.muted { color: var(--yzh-color-text-disabled, #c0c4cc); }
.error-text { color: var(--yzh-color-danger, #F56C6C); }
.detail-wrapper { padding: 8px 12px; background: var(--yzh-color-bg-subtle, #fafbfc); }
.detail-loading { color: var(--yzh-color-text-tertiary, #909399); font-size: 12px; padding: 8px 0; }
.detail-summary { display: flex; gap: 16px; flex-wrap: wrap; font-size: 12px; color: var(--yzh-color-text-regular, #606266); margin-bottom: 8px; code { font-family: monospace; } }
.detail-section { margin-bottom: 12px; }
.detail-section-title { font-size: 12px; font-weight: 600; color: var(--yzh-color-text-regular, #606266); margin-bottom: 4px; }

.node-approval-bar { display: flex; align-items: center; gap: 8px; padding: 6px 0; font-size: 12px; color: var(--yzh-color-text-regular, #606266); flex-wrap: wrap; }
.approval-label { color: var(--yzh-color-text-tertiary, #909399); font-weight: 600; }
.approval-node { display: inline-flex; align-items: center; gap: 4px; margin-right: 8px; }
.source-badge { font-size: 11px; color: var(--yzh-color-success, #67c23a); }
.ai-badge { font-size: 11px; color: #9c27b0; }
.json-block { background: var(--yzh-color-bg-page, #f5f7fa); padding: 8px; border-radius: 4px; font-size: 11px; font-family: monospace; max-height: 340px; overflow: auto; margin: 0; white-space: pre-wrap; word-break: break-all; }
</style>
