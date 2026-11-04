<script setup lang="ts">
/**
 * 认证准备/检查统一工作台（V2 一体化）
 *
 * 布局：顶部工具栏 + 主表 + 右侧 Drawer + 底部队列进度条
 *
 * 特点：
 *   - 不区分准备/检查 Tab，单一大表展示全部检查项
 *   - 关键信息外露：部门 + 部门联系人
 *   - 原始证据与检查证据分离（原始可跳新页面预览）
 *   - 仅「自动分析 / 保存报告 / 导出报告」三个全局按钮
 *   - 侧边栏「保存」「锁定」行级操作
 *   - 底部队列状态由轮询真实驱动
 *
 * 所有写操作均走真实接口：
 *   保存结论   POST /api/Auditor/CertTask/save-result
 *   锁定/解锁  POST /api/Auditor/CertTask/lock-result | unlock-result
 *   补充材料   POST /api/Auditor/CertEvidence/add | update
 *   自动分析   POST /api/Auditor/CertTask/create + /start
 */
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import {
  ElButton, ElTable, ElTableColumn, ElTag, ElDrawer, ElInput,
  ElEmpty, ElMessageBox, ElMessage,
  ElPopover, ElForm, ElFormItem, ElSelect, ElOption,
} from 'element-plus'
import {
  getPlanChecklist, getPlanCheckItemDetail, exportPlanReport,
  lockConclusion, unlockConclusion, saveCheckResult, addEvidence, updateEvidence,
  createCertTask, startCertTask,
} from '@share/api/auditor/cert-check-task'
import type {
  CertPlanCheckItemRow, CertPlanCheckItemDetail, ContactPerson,
} from '@share/api/auditor/cert-check-task'
import { CONCLUSION_LABELS, labelOf } from '@/components/check/verdict'

const props = defineProps<{
  planCode: string
  planName: string
}>()

// ──── 数据状态 ────
const loading = ref(false)
const rows = ref<CertPlanCheckItemRow[]>([])

// ──── 侧边栏 ────
const drawerVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<CertPlanCheckItemDetail | null>(null)
const selectedDetailCode = ref('')
const editingConclusion = ref(false)
const editForm = reactive({
  conclusion: '',
  reason: '',
})

// ──── 补充材料 ────
const supplementList = ref<Array<{ code: string; text: string }>>([])
const newSupplement = ref('')

// ──── 操作进行中标记 ────
const busy = reactive({
  analyze: false,
  save: false,
  lock: false,
  supplement: false,
})

// ──── 队列状态（轮询真实驱动） ────
const queueStatus = reactive({
  running: false,
  taskCode: '',
  taskNo: '',
  total: 0,
  done: 0,
  elapsed: 0,
})
let pollTimer: number | null = null
const POLL_INTERVAL_MS = 5000

/**
 * 页面锁（F12）：队列在跑 ⇒ 整页只读。
 *
 * <p>⛔ 不要把它做成「我点了开始所以只读」的本地开关 —— 那样刷新一下就失效了。
 * 唯一真相是后端每轮返回的 <c>TaskRunning</c>（来源 <c>cert_task.Status='running'</c>），
 * 前端只做展示层拦截，后端写接口还会各自 409 兜底（两道防线，缺一不可）。</p>
 */
const pageLocked = computed(() => queueStatus.running)
const lockTip = computed(() =>
  pageLocked.value
    ? `队列执行中${queueStatus.taskNo ? '（' + queueStatus.taskNo + '）' : ''}，完成后才能编辑`
    : '',
)

// ════════════════════════════════════════════════════════════
// 解析辅助（统一兜底，避免脏 JSON 导致整表渲染中断）
// ════════════════════════════════════════════════════════════

function parseContacts(json: string | null | undefined): ContactPerson[] {
  if (!json) return []
  try {
    const arr = JSON.parse(json)
    return Array.isArray(arr) ? (arr as ContactPerson[]) : []
  } catch {
    return []
  }
}

/** 表格行视图：预解析联系人，避免模板内重复 JSON.parse */
interface RowView extends CertPlanCheckItemRow {
  contacts: ContactPerson[]
}
const rowsView = computed<RowView[]>(() =>
  rows.value.map(r => ({ ...r, contacts: parseContacts(r.ContactsJson) })),
)

interface DeptContactView {
  deptCode: string
  deptName: string
  leaderName: string
  leaderPhone: string
  contacts: Array<{ name: string; position: string; phone: string }>
}

/** 部门联系人：后端外层为 camelCase、内层为 PascalCase，此处统一归一 */
const deptContactsDisplay = computed<DeptContactView[]>(() => {
  const raw = detail.value?.DeptContactsJson
  if (!raw) return []
  try {
    const arr = JSON.parse(raw)
    if (!Array.isArray(arr)) return []
    return arr.map((d: any) => ({
      deptCode: d.deptCode ?? d.DeptCode ?? '',
      deptName: d.deptName ?? d.DeptName ?? '',
      leaderName: d.leaderName ?? d.LeaderName ?? '',
      leaderPhone: d.leaderPhone ?? d.LeaderPhone ?? '',
      contacts: (Array.isArray(d.contacts) ? d.contacts : []).map((c: any) => ({
        name: c.name ?? c.Name ?? '',
        position: c.position ?? c.Position ?? c.roleType ?? c.RoleType ?? '',
        phone: c.phone ?? c.Phone ?? '',
      })),
    }))
  } catch {
    return []
  }
})

const doubtsDisplay = computed<Array<{ kind: string; text: string }>>(() => {
  const raw = detail.value?.DoubtsJson
  if (!raw) return []
  try {
    const arr = JSON.parse(raw)
    if (!Array.isArray(arr)) return []
    return arr.map((d: any) => ({
      kind: d.kind ?? d.Kind ?? '',
      text: d.text ?? d.Text ?? '',
    }))
  } catch {
    return []
  }
})

/** 工具栏统计（仅统计可由数据真实推导的项） */
const stats = computed(() => {
  const total = rows.value.length
  const done = rows.value.filter(r => !!r.Conclusion).length
  const locked = rows.value.filter(r => r.IsLocked).length
  return { total, done, locked, pending: total - done }
})

// ════════════════════════════════════════════════════════════
// 加载
// ════════════════════════════════════════════════════════════

async function loadChecklist() {
  loading.value = true
  try {
    const res = await getPlanChecklist(props.planCode)
    if (res?.success && res.data) {
      rows.value = res.data.Rows || []
      // ★ 页面锁（F12）：running 与否以后端为准 ——
      //   刷新页面 / 换个人打开 / 队列在后台跑完，都能立刻跟上（本地状态做不到）。
      queueStatus.running = !!res.data.TaskRunning
      queueStatus.taskCode = res.data.RunningTaskCode || ''
      queueStatus.taskNo = res.data.RunningTaskNo || ''
      queueStatus.total = res.data.RunningItemTotal || 0
      queueStatus.done = res.data.RunningItemDone || 0
      if (!queueStatus.running) {
        queueStatus.elapsed = 0
        stopPolling()
      }
    } else {
      ElMessage.error(res?.message || '加载失败')
    }
  } catch (e: any) {
    ElMessage.error('加载失败：' + (e?.message || ''))
  } finally {
    loading.value = false
  }
}

async function openDetail(row: CertPlanCheckItemRow) {
  selectedDetailCode.value = row.DetailCode
  detailLoading.value = true
  drawerVisible.value = true
  detail.value = null
  editingConclusion.value = false
  supplementList.value = []
  newSupplement.value = ''
  try {
    const res = await getPlanCheckItemDetail(row.DetailCode)
    if (res?.success && res.data) {
      detail.value = res.data
      editForm.conclusion = res.data.Conclusion || ''
      editForm.reason = res.data.Reason || ''
      // 补充材料 = 人工录入类检查证据
      supplementList.value = (res.data.CheckEvidences || [])
        .filter(e => e.EvidenceKind === 'manual' && e.CapturedIn === 'manual')
        .map(e => ({ code: e.Code, text: e.Snippet || e.LlmSummary || '' }))
        .filter(s => s.text)
    } else {
      ElMessage.error(res?.message || '加载详情失败')
    }
  } catch (e: any) {
    ElMessage.error('加载详情失败：' + (e?.message || ''))
  } finally {
    detailLoading.value = false
  }
}

function onRowClick(row: any) {
  openDetail(row as CertPlanCheckItemRow)
}

function rowClassName({ row }: { row: any }) {
  const cls: string[] = []
  if (row.IsLocked) cls.push('wb-row-locked')
  if (row.DetailCode && row.DetailCode === selectedDetailCode.value) cls.push('wb-row-active')
  return cls.join(' ')
}

// ════════════════════════════════════════════════════════════
// 全局操作
// ════════════════════════════════════════════════════════════

async function handleAutoAnalyze() {
  if (busy.analyze) return
  try {
    await ElMessageBox.confirm(
      '将创建新的认证准备任务并启动队列分析，是否继续？',
      '启动自动分析',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'info' },
    )
  } catch {
    return // 用户取消
  }

  busy.analyze = true
  try {
    const created = await createCertTask({
      PlanCode: props.planCode,
      TaskKind: 'prep',
      RunMode: 'full',
    })
    if (!created?.success || !created.data) {
      ElMessage.error(created?.message || '创建任务失败')
      return
    }

    const taskCode = created.data.TaskCode
    const started = await startCertTask(taskCode)
    if (!started?.success) {
      ElMessage.error(started?.message || '启动任务失败')
      return
    }

    ElMessage.success(`已启动分析：${created.data.TaskNo || taskCode}`)
    queueStatus.taskCode = taskCode
    queueStatus.taskNo = created.data.TaskNo || ''
    queueStatus.total = created.data.ItemTotal || rows.value.length
    queueStatus.done = rows.value.filter(r => !!r.Conclusion).length
    queueStatus.elapsed = 0
    queueStatus.running = true
    startPolling()
  } catch (e: any) {
    ElMessage.error('启动分析失败：' + (e?.message || ''))
  } finally {
    busy.analyze = false
  }
}

async function handleSaveReport() {
  ElMessage.info('报告草稿保存尚未开放，请使用「导出报告」生成正式报告')
}

async function handleExport() {
  try {
    const res = await exportPlanReport(props.planCode)
    if (res?.success && res.data) {
      ElMessage.success(res.data.Message || '导出已排队')
    } else {
      ElMessage.error(res?.message || '导出失败')
    }
  } catch (e: any) {
    ElMessage.error('导出失败：' + (e?.message || ''))
  }
}

// ════════════════════════════════════════════════════════════
// 侧边栏：保存结论 / 锁定
// ════════════════════════════════════════════════════════════

async function saveItemDetail() {
  const d = detail.value
  if (!d) return
  if (!d.ResultCode) {
    ElMessage.warning('该项尚无结论记录，请先执行「自动分析」')
    return
  }
  if (d.IsLocked) {
    ElMessage.warning('结论已锁定，请先解锁再修改')
    return
  }
  busy.save = true
  try {
    const res = await saveCheckResult(d.ResultCode, editForm.conclusion, null, editForm.reason)
    if (res?.success) {
      d.Conclusion = editForm.conclusion || d.Conclusion
      d.Reason = editForm.reason
      editingConclusion.value = false
      ElMessage.success('已保存')
      await loadChecklist()
    } else {
      ElMessage.error(res?.message || '保存失败')
    }
  } catch (e: any) {
    ElMessage.error('保存失败：' + (e?.message || ''))
  } finally {
    busy.save = false
  }
}

async function toggleLock() {
  const d = detail.value
  if (!d) return
  if (!d.ResultCode) {
    ElMessage.warning('该项尚无结论记录，无法锁定')
    return
  }
  busy.lock = true
  try {
    const res = d.IsLocked
      ? await unlockConclusion(d.ResultCode)
      : await lockConclusion(d.ResultCode, 'manual_confirm')
    if (res?.success) {
      d.IsLocked = !d.IsLocked
      ElMessage.success(d.IsLocked ? '已锁定该项' : '已解锁该项')
      await loadChecklist()
    } else {
      ElMessage.error(res?.message || '操作失败')
    }
  } catch (e: any) {
    ElMessage.error('操作失败：' + (e?.message || ''))
  } finally {
    busy.lock = false
  }
}

// ════════════════════════════════════════════════════════════
// 补充材料（真实落库：cert_evidence）
// ════════════════════════════════════════════════════════════

async function addSupplement() {
  const text = newSupplement.value.trim()
  const d = detail.value
  if (!text || !d) return
  busy.supplement = true
  try {
    const res = await addEvidence({
      SourceName: '审核员补充材料',
      CheckItemCode: d.DetailCode,
      EvidenceKind: 'manual',
      CapturedIn: 'manual',
      Snippet: text,
    })
    if (res?.success && res.data) {
      supplementList.value.push({ code: res.data.EvidenceCode, text })
      newSupplement.value = ''
      ElMessage.success('已添加补充材料')
      await loadChecklist()
    } else {
      ElMessage.error(res?.message || '添加失败')
    }
  } catch (e: any) {
    ElMessage.error('添加失败：' + (e?.message || ''))
  } finally {
    busy.supplement = false
  }
}

async function removeSupplement(idx: number) {
  const item = supplementList.value[idx]
  if (!item) return
  if (!item.code) {
    supplementList.value.splice(idx, 1)
    return
  }
  busy.supplement = true
  try {
    // 软删除：置为 dropped，保留审计痕迹；详情接口已过滤 dropped
    const res = await updateEvidence({
      Code: item.code,
      Status: 'dropped',
      DroppedReason: '审核员删除补充材料',
    })
    if (res?.success) {
      supplementList.value.splice(idx, 1)
      ElMessage.success('已删除')
      await loadChecklist()
    } else {
      ElMessage.error(res?.message || '删除失败')
    }
  } catch (e: any) {
    ElMessage.error('删除失败：' + (e?.message || ''))
  } finally {
    busy.supplement = false
  }
}

// ════════════════════════════════════════════════════════════
// 原始证据预览（新窗口）
// ════════════════════════════════════════════════════════════

function openEvidencePreview(evidenceCode: string, anchor?: string | null) {
  const url = `/audit-task/doc-preview?evidenceCode=${encodeURIComponent(evidenceCode)}${anchor ? '&anchor=' + encodeURIComponent(anchor) : ''}`
  window.open(url, '_blank')
}

// ════════════════════════════════════════════════════════════
// 队列轮询
// ════════════════════════════════════════════════════════════

function startPolling() {
  stopPolling()
  pollTimer = window.setInterval(async () => {
    queueStatus.elapsed += Math.round(POLL_INTERVAL_MS / 1000)
    // loadChecklist 内部会刷新 queueStatus.running（以后端 TaskRunning 为准），
    // 并在「不再 running」时自己停轮询 —— 这里⛔ 不要再按「有结论的项数 == 总数」判停：
    // 准备分支的结论是 PrepState（Conclusion 恒为空），那个条件永远不成立，会一直转到刷新页面为止。
    await loadChecklist()
  }, POLL_INTERVAL_MS)
}

function stopPolling() {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer)
    pollTimer = null
  }
}

function formatDuration(sec: number) {
  const m = String(Math.floor(sec / 60)).padStart(2, '0')
  const s = String(sec % 60).padStart(2, '0')
  return `${m}:${s}`
}

onMounted(() => {
  loadChecklist()
})

onBeforeUnmount(() => {
  stopPolling()
})
</script>

<template>
  <div class="wb-container">
    <!-- 顶部工具栏 -->
    <div class="wb-toolbar">
      <div class="wb-toolbar-left">
        <span class="wb-plan-name">{{ planName }}</span>
        <span class="wb-meta">共 {{ stats.total }} 项</span>
        <span class="wb-badge done">已完成 {{ stats.done }}</span>
        <span class="wb-badge lock">已锁定 {{ stats.locked }}</span>
        <span class="wb-badge idle">待分析 {{ stats.pending }}</span>
      </div>
      <div class="wb-toolbar-right">
        <el-button type="primary" :loading="busy.analyze" :disabled="pageLocked" @click="handleAutoAnalyze">自动分析</el-button>
        <el-button @click="handleSaveReport">保存报告</el-button>
        <el-button @click="handleExport">导出报告</el-button>
      </div>
    </div>

    <!-- 页面锁提示（F12）：队列在跑时整页只读，必须明确告诉用户「为什么点不动」 -->
    <div class="wb-lock-banner" v-if="pageLocked">
      <span class="wb-lock-icon">🔒</span>
      <span class="wb-lock-text">{{ lockTip }}</span>
    </div>

    <!-- 主表 -->
    <div class="wb-table-wrap">
      <el-table
        :data="rowsView"
        border
        stripe
        size="small"
        v-loading="loading"
        height="100%"
        empty-text="暂无检查项"
        row-key="DetailCode"
        highlight-current-row
        :row-class-name="rowClassName"
        @row-click="onRowClick"
      >
        <el-table-column label="#" width="48" align="center">
          <template #default="{ $index }">{{ $index + 1 }}</template>
        </el-table-column>
        <el-table-column label="检查项" prop="ClauseNumbers" min-width="200" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="wb-clause">{{ row.ClauseNumbers || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="部门" prop="DeptName" width="120" show-overflow-tooltip>
          <template #default="{ row }">
            <span v-if="row.DeptName">{{ row.DeptName }}</span>
            <span v-else class="wb-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="部门联系人" width="170">
          <template #default="{ row }">
            <div v-if="row.contacts.length" class="wb-contacts">
              <span
                v-for="c in row.contacts.slice(0, 2)"
                :key="(c.name || '') + (c.phone || '')"
                class="wb-contact-chip"
                :title="[c.name, c.position, c.phone].filter(Boolean).join(' · ')"
              >
                <span class="wb-avatar">{{ (c.name || '?').charAt(0) }}</span>
                <span class="wb-contact-name">{{ c.name || '未命名' }}</span>
              </span>
              <el-popover
                v-if="row.contacts.length > 2"
                placement="right"
                :width="240"
                trigger="hover"
              >
                <template #reference>
                  <span class="wb-contact-more">+{{ row.contacts.length - 2 }}</span>
                </template>
                <div v-for="c in row.contacts" :key="(c.name || '') + (c.phone || '')" class="wb-contact-line">
                  {{ c.name || '未命名' }}<span v-if="c.position"> · {{ c.position }}</span><span v-if="c.phone"> · {{ c.phone }}</span>
                </div>
              </el-popover>
            </div>
            <span v-else class="wb-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="92" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.IsLocked" size="small" type="warning" effect="plain">🔒 已锁定</el-tag>
            <el-tag v-else-if="row.Conclusion" size="small" type="success" effect="plain">已分析</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">待分析</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="PrepState / 结论" width="140" align="center">
          <template #default="{ row }">
            <span v-if="row.PrepState" class="wb-prep-state">{{ row.PrepState }}</span>
            <span v-if="row.Conclusion" class="wb-conclusion">
              {{ labelOf(CONCLUSION_LABELS, row.Conclusion) }}
            </span>
            <span v-if="!row.PrepState && !row.Conclusion" class="wb-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="严重度" width="90" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.Severity" size="small" :type="row.Severity === 'major' ? 'danger' : 'warning'" effect="plain">
              {{ row.Severity }}
            </el-tag>
            <span v-else class="wb-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="建议NC" width="80" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.NcSuggested" size="small" type="danger">NC</el-tag>
            <span v-else class="wb-muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="原始证据" width="88" align="center">
          <template #default="{ row }">{{ row.RawEvidenceCount || 0 }}</template>
        </el-table-column>
        <el-table-column label="检查证据" width="88" align="center">
          <template #default="{ row }">{{ row.CheckEvidenceCount || 0 }}</template>
        </el-table-column>
        <el-table-column label="疑点" width="70" align="center">
          <template #default="{ row }">
            <span v-if="row.DoubtCount" class="wb-doubt">{{ row.DoubtCount }}</span>
            <span v-else class="wb-muted">0</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="80" align="center" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click.stop="onRowClick(row)">详情</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 底部队列进度栏 -->
    <div class="wb-queue-bar" v-if="queueStatus.running">
      <span class="wb-queue-label">队列</span>
      <span class="wb-queue-info">已完成 {{ queueStatus.done }} / 共 {{ queueStatus.total }}</span>
      <span class="wb-queue-info">排队 {{ Math.max(queueStatus.total - queueStatus.done, 0) }}</span>
      <span class="wb-queue-info">已用 {{ formatDuration(queueStatus.elapsed) }}</span>
      <span class="wb-queue-info" v-if="queueStatus.taskNo">任务：{{ queueStatus.taskNo }}</span>
    </div>
    <div class="wb-queue-bar idle" v-else>
      <span class="wb-queue-label">空闲</span>
      <span class="wb-queue-info">无执行中的队列任务</span>
      <span class="wb-queue-info" v-if="stats.pending > 0">尚有 {{ stats.pending }} 项待分析</span>
    </div>

    <!-- 右侧侧边栏 Drawer -->
    <el-drawer
      v-model="drawerVisible"
      direction="rtl"
      size="480px"
      :title="detail?.ClauseNumbers || '检查项详情'"
      :close-on-click-modal="true"
    >
      <div v-loading="detailLoading" class="wb-drawer">
        <template v-if="detail">
          <!-- 摘要条 -->
          <div class="wb-summary">
            <div class="wb-summary-row">
              <span class="wb-summary-label">结论：</span>
              <el-tag v-if="detail.Conclusion" size="small" type="primary">
                {{ labelOf(CONCLUSION_LABELS, detail.Conclusion) }}
              </el-tag>
              <span v-else class="wb-muted">待分析</span>
              <el-button
                link
                type="primary"
                size="small"
                :disabled="detail.IsLocked || pageLocked"
                @click="editingConclusion = !editingConclusion"
              >{{ editingConclusion ? '收起' : '编辑' }}</el-button>
            </div>
            <div class="wb-summary-row">
              <span class="wb-summary-label">严重程度：</span>
              <el-tag v-if="detail.Severity" size="small" :type="detail.Severity === 'major' ? 'danger' : 'warning'">{{ detail.Severity }}</el-tag>
              <span v-else class="wb-muted">-</span>
            </div>
            <div class="wb-summary-row">
              <span class="wb-summary-label">NC建议：</span>
              <el-tag v-if="detail.NcSuggested" size="small" type="danger">是</el-tag>
              <span v-else class="wb-muted">否</span>
            </div>
            <div class="wb-summary-row" v-if="detail.IsLocked">
              <el-tag size="small" type="warning">已锁定 ({{ detail.LockedBy || '—' }})</el-tag>
            </div>
          </div>

          <!-- 部门 & 联系人 -->
          <div class="wb-zone">
            <div class="wb-zone-title">部门 & 联系人</div>
            <div v-if="deptContactsDisplay.length" class="wb-dept-list">
              <div v-for="dc in deptContactsDisplay" :key="dc.deptCode" class="wb-dept">
                <div class="wb-dept-name">{{ dc.deptName }}</div>
                <div v-if="dc.leaderName" class="wb-dept-leader">{{ dc.leaderName }} / {{ dc.leaderPhone }}</div>
                <div v-for="c in dc.contacts" :key="c.name + c.phone" class="wb-dept-contact">
                  {{ c.name }} / {{ c.position || '—' }} / {{ c.phone || '—' }}
                </div>
              </div>
            </div>
            <el-empty v-else description="暂无部门" :image-size="40" />
          </div>

          <!-- 检查证据 -->
          <div class="wb-zone">
            <div class="wb-zone-title">检查证据</div>
            <div v-if="detail.CheckEvidences.length" class="wb-evidence-list">
              <div v-for="e in detail.CheckEvidences" :key="e.Code" class="wb-evidence">
                <div class="wb-ev-head">
                  <span class="wb-ev-kind">{{ e.EvidenceKind }}</span>
                  <span class="wb-ev-source">{{ e.SourceName || '-' }}</span>
                </div>
                <div class="wb-ev-snippet">{{ e.Snippet || e.LlmSummary || '(空)' }}</div>
              </div>
            </div>
            <el-empty v-else description="暂无检查证据" :image-size="40" />
          </div>

          <!-- 原始证据（可跳转预览） -->
          <div class="wb-zone">
            <div class="wb-zone-title">原始证据（{{ detail.RawEvidences.length }}）</div>
            <div v-if="detail.RawEvidences.length" class="wb-evidence-list">
              <div v-for="e in detail.RawEvidences" :key="e.Code" class="wb-evidence raw">
                <div class="wb-ev-head">
                  <span class="wb-ev-kind">{{ e.EvidenceKind }}</span>
                  <a
                    v-if="e.Code"
                    class="wb-ev-link"
                    href="#"
                    @click.prevent="openEvidencePreview(e.Code, e.Anchor)"
                  >{{ e.SourceName || '查看原文' }}</a>
                  <span v-else class="wb-ev-source">{{ e.SourceName || '-' }}</span>
                </div>
                <div class="wb-ev-snippet">{{ e.Snippet ? e.Snippet.slice(0, 100) + '...' : e.LlmSummary?.slice(0, 100) || '' }}</div>
              </div>
            </div>
            <el-empty v-else description="暂无原始证据" :image-size="40" />
          </div>

          <!-- 疑点 -->
          <div class="wb-zone">
            <div class="wb-zone-title">疑点</div>
            <div v-if="doubtsDisplay.length" class="wb-doubt-list">
              <div v-for="(d, i) in doubtsDisplay" :key="i" class="wb-doubt-item">{{ d.text }}</div>
            </div>
            <div v-else class="wb-zone-empty text-muted">无</div>
          </div>

          <!-- 补充材料 -->
          <div class="wb-zone">
            <div class="wb-zone-title">补充材料</div>
            <div class="wb-supplement-list">
              <div v-for="(s, i) in supplementList" :key="s.code || i" class="wb-sup-item">
                <span>{{ s.text }}</span>
                <el-button link type="danger" size="small" :disabled="busy.supplement || pageLocked" @click="removeSupplement(i)">删除</el-button>
              </div>
              <div v-if="!supplementList.length" class="wb-zone-empty text-muted">暂无</div>
            </div>
            <div class="wb-supplement-add">
              <el-input
                v-model="newSupplement"
                type="textarea"
                :rows="2"
                placeholder="输入补充材料..."
                resize="none"
              />
              <el-button type="primary" size="small" :loading="busy.supplement" :disabled="pageLocked" @click="addSupplement">添加</el-button>
            </div>
          </div>

          <!-- 结论编辑 -->
          <div class="wb-zone" v-if="editingConclusion">
            <div class="wb-zone-title">编辑结论</div>
            <el-form label-position="top" size="small">
              <el-form-item label="结论">
                <el-select v-model="editForm.conclusion" placeholder="请选择结论" style="width: 100%;">
                  <el-option label="符合（pass）" value="pass" />
                  <el-option label="观察项（ofi）" value="ofi" />
                  <el-option label="不符合（nc）" value="nc" />
                  <el-option label="不适用（na）" value="na" />
                </el-select>
              </el-form-item>
              <el-form-item label="判定依据">
                <el-input v-model="editForm.reason" type="textarea" :rows="3" placeholder="请输入判定依据" />
              </el-form-item>
            </el-form>
          </div>
        </template>
        <el-empty v-else-if="!detailLoading" description="请选择一项查看详情" />
      </div>

      <!-- 抽屉底部 -->
      <template #footer>
        <div class="wb-drawer-footer">
          <el-button
            @click="saveItemDetail"
            type="primary"
            :loading="busy.save"
            :disabled="!detail?.ResultCode || detail?.IsLocked || pageLocked"
          >保存</el-button>
          <el-button
            :type="detail?.IsLocked ? 'warning' : 'danger'"
            :loading="busy.lock"
            :disabled="!detail?.ResultCode || pageLocked"
            @click="toggleLock"
          >
            {{ detail?.IsLocked ? '解锁此项' : '锁定此项' }}
          </el-button>
          <el-button @click="drawerVisible = false">关闭</el-button>
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<style scoped>
.wb-container {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);
  border-radius: 4px;
  overflow: hidden;
}
.wb-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color-page);
  flex-shrink: 0;
}
.wb-toolbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.wb-plan-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
/* 页面锁横幅（F12）—— 用 warning 色，和「已锁定」徽标同一套语义 */
.wb-lock-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  background: var(--el-color-warning-light-9);
  border-bottom: 1px solid var(--el-color-warning-light-7);
  color: var(--el-color-warning-dark-2);
  font-size: 12px;
  flex-shrink: 0;
}
.wb-lock-icon {
  font-size: 13px;
  line-height: 1;
}
.wb-lock-text {
  font-weight: 500;
}
.wb-meta {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.wb-badge {
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 4px;
  white-space: nowrap;
}
.wb-badge.done {
  background: var(--el-color-success-light-9);
  color: var(--el-color-success);
}
.wb-badge.lock {
  background: var(--el-color-warning-light-9);
  color: var(--el-color-warning);
}
.wb-badge.idle {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}
.wb-toolbar-right {
  display: flex;
  gap: 8px;
}
.wb-table-wrap {
  flex: 1;
  overflow: hidden;
  padding: 0;
}
.wb-clause {
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.wb-prep-state {
  font-size: 12px;
  color: var(--el-color-success);
  margin-right: 4px;
}
.wb-conclusion {
  font-size: 12px;
  color: var(--el-color-primary);
  font-weight: 600;
}
.wb-muted {
  color: var(--el-text-color-placeholder);
}
.wb-doubt {
  color: var(--el-color-danger);
  font-weight: 600;
}
/* 联系人 */
.wb-contacts {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-wrap: wrap;
}
.wb-contact-chip {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  max-width: 68px;
}
.wb-avatar {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  border: 1px solid var(--el-color-primary-light-7);
  font-size: 10px;
  font-weight: 700;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.wb-contact-name {
  font-size: 12px;
  color: var(--el-text-color-regular);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.wb-contact-more {
  font-size: 11px;
  color: var(--el-color-primary);
  cursor: pointer;
}
.wb-contact-line {
  font-size: 12px;
  line-height: 1.7;
  color: var(--el-text-color-regular);
}
/* 行状态 */
:deep(.wb-row-locked) > td {
  background: var(--el-color-warning-light-9) !important;
}
:deep(.wb-row-active) > td {
  background: var(--el-color-primary-light-9) !important;
}
/* 队列栏 */
.wb-queue-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 6px 16px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-color-warning-light-9);
  font-size: 12px;
  flex-shrink: 0;
}
.wb-queue-bar.idle {
  background: var(--el-bg-color-page);
  color: var(--el-text-color-placeholder);
}
.wb-queue-label {
  font-weight: 600;
  color: var(--el-color-warning);
}
.wb-queue-bar.idle .wb-queue-label {
  color: var(--el-text-color-secondary);
}
.wb-queue-info {
  color: var(--el-text-color-secondary);
}
/* Drawer */
.wb-drawer {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-height: 100%;
  overflow-y: auto;
}
.wb-summary {
  background: var(--el-color-primary-light-9);
  border-radius: 6px;
  padding: 10px 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px 16px;
}
.wb-summary-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}
.wb-summary-label {
  color: var(--el-text-color-secondary);
}
.wb-zone {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 10px 12px;
}
.wb-zone-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 8px;
  padding-bottom: 6px;
  border-bottom: 1px dashed var(--el-border-color-lighter);
}
.wb-zone-empty {
  font-size: 12px;
  color: var(--el-text-color-placeholder);
  padding: 4px 0;
}
.text-muted {
  color: var(--el-text-color-placeholder);
}
/* Department */
.wb-dept-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.wb-dept {
  font-size: 12px;
  line-height: 1.6;
}
.wb-dept-name {
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.wb-dept-leader {
  color: var(--el-color-primary);
}
.wb-dept-contact {
  color: var(--el-text-color-regular);
  padding-left: 8px;
}
/* Evidence */
.wb-evidence-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.wb-evidence {
  padding: 6px 8px;
  border-radius: 4px;
  background: var(--el-bg-color-page);
  border-left: 2px solid var(--el-color-primary);
}
.wb-evidence.raw {
  border-left-color: var(--el-color-info);
}
.wb-ev-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 4px;
  font-size: 11px;
}
.wb-ev-kind {
  color: var(--el-color-info);
  font-weight: 500;
}
.wb-ev-source {
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 60%;
}
.wb-ev-link {
  color: var(--el-color-primary);
  text-decoration: none;
  cursor: pointer;
}
.wb-ev-link:hover {
  text-decoration: underline;
}
.wb-ev-snippet {
  font-size: 11px;
  color: var(--el-text-color-regular);
  line-height: 1.5;
}
/* Doubts */
.wb-doubt-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.wb-doubt-item {
  font-size: 12px;
  color: var(--el-color-danger);
  background: var(--el-color-danger-light-9);
  padding: 4px 8px;
  border-radius: 4px;
  line-height: 1.5;
}
/* Supplement */
.wb-supplement-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 8px;
}
.wb-sup-item {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  font-size: 12px;
  padding: 4px 8px;
  background: var(--el-bg-color-page);
  border-radius: 4px;
}
.wb-supplement-add {
  display: flex;
  flex-direction: column;
  gap: 6px;
  align-items: flex-end;
}
.wb-supplement-add .el-input {
  width: 100%;
}
/* Drawer footer */
.wb-drawer-footer {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}
</style>
