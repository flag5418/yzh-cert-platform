<!--
  ExecutionHistoryDrawer.vue — 工作流测试历史抽屉（共享层，阶段四新增）

  设计约束（项目「工作流规则页的抽象设计」）：本组件必须建在 cert-share 共享层，
  让 NC 规则设置与报告规则设置**自动同时获得**该能力；绝不在两个页面各写一遍。

  数据来源（后端 WorkflowTestController 只读端点）：
    POST /api/Workflow/test/history            列表（四层模型第一层）
    GET  /api/Workflow/test/detail/{taskCode}  展开时按需加载四层聚合详情

  为什么用「展开行懒加载」而不是「点开跳新页」：
  一次测试的四层数据（task/item/path/node）通常几十行，抽屉内直接展开比跳转更省事；
  但列表可能有上百条，所以详情**只在展开时请求**，避免一次性拉全量。

  「节点复用」怎么读：路径表 `ReusedCount` 是 DB 层的唯一证据
  （节点表 wf_node_execution 已按 NodeId 去重，IsReused 恒为 0 属预期行为）。
-->
<template>
  <el-drawer
    :model-value="modelValue"
    :title="drawerTitle"
    size="1080px"
    direction="rtl"
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

      <!-- ===== 列表 ===== -->
      <el-table
        v-loading="loading"
        :data="rows"
        size="small"
        border
        row-key="TaskCode"
        class="history-table"
        @expand-change="onExpandChange"
      >
        <el-table-column type="expand">
          <template #default="{ row }">
            <div class="detail-wrapper">
              <div v-if="detailLoading === row.TaskCode" class="detail-loading">加载中…</div>
              <template v-else-if="details[row.TaskCode]">
                <div class="detail-summary">
                  <span>ItemCode: <code>{{ details[row.TaskCode].Items?.[0]?.ItemCode || '—' }}</code></span>
                  <span>企业: {{ row.EnterpriseCode || '—' }}</span>
                  <span>阶段: {{ row.PhaseCode || '—' }}</span>
                  <span v-if="row.ErrorMessage" class="error-text">错误: {{ row.ErrorMessage }}</span>
                </div>

                <!-- 路径层 -->
                <div class="detail-section">
                  <div class="detail-section-title">路径执行（{{ details[row.TaskCode].Paths?.length || 0 }} 条）</div>
                  <el-table :data="details[row.TaskCode].Paths" size="small" border>
                    <el-table-column label="路径" width="70" align="center">
                      <template #default="{ row: p }">{{ p.PathIndex + 1 }}</template>
                    </el-table-column>
                    <el-table-column label="状态" width="90" align="center">
                      <template #default="{ row: p }">
                        <el-tag :type="p.Status === 'completed' ? 'success' : 'danger'" size="small">{{ p.Status }}</el-tag>
                      </template>
                    </el-table-column>
                    <el-table-column label="耗时" width="80" align="right">
                      <template #default="{ row: p }">{{ p.DurationMs }}ms</template>
                    </el-table-column>
                    <el-table-column label="复用" width="70" align="center">
                      <template #default="{ row: p }">
                        <el-tag v-if="p.ReusedCount > 0" type="info" size="small">{{ p.ReusedCount }}</el-tag>
                        <span v-else class="muted">—</span>
                      </template>
                    </el-table-column>
                    <el-table-column label="节点链" min-width="260">
                      <template #default="{ row: p }"><span class="mono">{{ (p.NodeIds || []).join(' → ') }}</span></template>
                    </el-table-column>
                    <el-table-column label="失败点 / 输出" min-width="200">
                      <template #default="{ row: p }">
                        <span v-if="p.ErrorMessage" class="error-text">{{ p.FailedAtNodeId }}: {{ p.ErrorMessage }}</span>
                        <el-popover v-else-if="p.Output" placement="left" :width="440" trigger="click">
                          <template #reference><el-button link size="small">查看输出</el-button></template>
                          <pre class="json-block">{{ formatJson(p.Output) }}</pre>
                        </el-popover>
                        <span v-else class="muted">—</span>
                      </template>
                    </el-table-column>
                  </el-table>
                </div>

                <!-- 节点层 -->
                <div class="detail-section">
                  <div class="detail-section-title">节点执行（{{ details[row.TaskCode].Nodes?.length || 0 }} 个，已按 NodeId 去重）</div>
                  <el-table :data="details[row.TaskCode].Nodes" size="small" border>
                    <el-table-column label="节点" min-width="140">
                      <template #default="{ row: n }"><span class="mono">{{ n.NodeId }}</span></template>
                    </el-table-column>
                    <el-table-column prop="NodeType" label="类型" width="90" />
                    <el-table-column prop="NodeTitle" label="名称" min-width="120" />
                    <el-table-column label="状态" width="80" align="center">
                      <template #default="{ row: n }">
                        <el-tag :type="n.ExecStatus === 'completed' ? 'success' : 'danger'" size="small">{{ n.ExecStatus }}</el-tag>
                      </template>
                    </el-table-column>
                    <el-table-column label="耗时" width="80" align="right">
                      <template #default="{ row: n }">{{ n.ExecutionTimeMs }}ms</template>
                    </el-table-column>
                    <el-table-column label="时序" width="150">
                      <template #default="{ row: n }">
                        <span class="muted">{{ shortTime(n.StartedAt) }} → {{ shortTime(n.CompletedAt) }}</span>
                      </template>
                    </el-table-column>
                    <el-table-column label="输出 / 错误" min-width="200">
                      <template #default="{ row: n }">
                        <span v-if="n.ErrorMessage" class="error-text">{{ n.ErrorMessage }}</span>
                        <el-popover v-else-if="n.Output" placement="left" :width="440" trigger="click">
                          <template #reference><el-button link size="small">查看输出</el-button></template>
                          <pre class="json-block">{{ formatJson(n.Output) }}</pre>
                        </el-popover>
                        <span v-else class="muted">—</span>
                      </template>
                    </el-table-column>
                  </el-table>
                </div>
              </template>
              <div v-else class="detail-loading">无详情数据</div>
            </div>
          </template>
        </el-table-column>
        <el-table-column prop="CreateTime" label="时间" width="160">
          <template #default="{ row }">{{ formatTime(row.CreateTime) }}</template>
        </el-table-column>
        <el-table-column label="范围" width="90" align="center">
          <template #default="{ row }">
            <el-tag size="small" :type="scopeTagType(row.TestScope)">{{ row.TestScope }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag size="small" :type="row.TaskStatus === 'completed' ? 'success' : 'danger'">{{ row.TaskStatus }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="RuleCode" label="规则编码" min-width="180" show-overflow-tooltip />
        <el-table-column label="耗时" width="80" align="right">
          <template #default="{ row }">{{ row.DurationMs ?? '—' }}ms</template>
        </el-table-column>
        <el-table-column prop="PathCount" label="路径" width="64" align="center" />
        <el-table-column prop="NodeCount" label="节点" width="64" align="center" />
        <el-table-column label="TaskCode" min-width="240">
          <template #default="{ row }"><span class="mono muted">{{ row.TaskCode }}</span></template>
        </el-table-column>
      </el-table>

      <div class="pager">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next"
          background
          small
          @current-change="load"
          @size-change="reload"
        />
      </div>
    </div>
  </el-drawer>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { Search } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import {
  getTaskDetail,
  getTaskHistory,
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

const drawerTitle = computed(() => '测试历史')

const rows = ref<TaskHistoryItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)

const filters = reactive({
  TestScope: props.testScope || '',
  TaskStatus: '',
  RuleCode: props.ruleCode || '',
})

/** TaskCode → 四层详情（展开时懒加载并缓存） */
const details = reactive<Record<string, TaskExecutionDetail>>({})
const detailLoading = ref<string | null>(null)

function onOpen() {
  // 每次打开都同步一次外部传入的筛选，并刷新列表
  filters.RuleCode = props.ruleCode || filters.RuleCode
  if (props.testScope) filters.TestScope = props.testScope
  reload()
}

async function load() {
  loading.value = true
  try {
    const res = await getTaskHistory({
      Page: page.value,
      PageSize: pageSize.value,
      RuleCode: filters.RuleCode || undefined,
      TestScope: filters.TestScope || undefined,
      TaskStatus: filters.TaskStatus || undefined,
      TaskType: 'TEST',
    })
    rows.value = res?.Items || []
    total.value = res?.TotalCount || 0
  } catch (e: any) {
    ElMessage.error('测试历史查询失败: ' + (e?.message || e))
    rows.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function reload() {
  page.value = 1
  load()
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
.filter-bar { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; padding-bottom: 8px; border-bottom: 1px solid #ebeef5; }
.history-table { font-size: 12px; }
.pager { display: flex; justify-content: flex-end; padding-top: 8px; }
.mono { font-family: monospace; }
.muted { color: #c0c4cc; }
.error-text { color: #F56C6C; }
.detail-wrapper { padding: 8px 12px; background: #fafbfc; }
.detail-loading { color: #909399; font-size: 12px; padding: 8px 0; }
.detail-summary { display: flex; gap: 16px; flex-wrap: wrap; font-size: 12px; color: #606266; margin-bottom: 8px; code { font-family: monospace; } }
.detail-section { margin-bottom: 12px; }
.detail-section-title { font-size: 12px; font-weight: 600; color: #606266; margin-bottom: 4px; }
.json-block { background: #f5f7fa; padding: 8px; border-radius: 4px; font-size: 11px; font-family: monospace; max-height: 340px; overflow: auto; margin: 0; white-space: pre-wrap; word-break: break-all; }
</style>
