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
 *   - 底部纯队列状态
 */
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import {
  ElButton, ElTable, ElTableColumn, ElTag, ElDrawer, ElInput,
  ElDescriptions, ElDescriptionsItem, ElEmpty, ElMessageBox, ElMessage,
  ElPopover, ElForm, ElFormItem, ElSelect, ElOption, ElDialog,
} from 'element-plus'
import { yzhApi } from '@yzh-core/api/client'
import { getPlanChecklist, getPlanCheckItemDetail, exportPlanReport } from '@share/api/auditor/cert-check-task'
import type { CertPlanCheckItemRow, CertPlanChecklistResponse, CertPlanCheckItemDetail, CertEvidenceRow, ContactPerson } from '@share/api/auditor/cert-check-task'

const props = defineProps<{
  planCode: string
  planName: string
}>()

// ──── 数据状态 ────
const loading = ref(false)
const checklist = ref<CertPlanChecklistResponse | null>(null)
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
const supplementList = ref<Array<{ text: string }>>([])
const newSupplement = ref('')

// ──── 队列状态（模拟） ────
const queueStatus = reactive({
  running: false,
  total: 0,
  done: 0,
  queued: 0,
  duration: '',
  lastTaskKind: '',
})

// 计算属性
const allContacts = computed((): ContactPerson[] => {
  const all: ContactPerson[] = []
  rows.value.forEach(r => {
    if (r.ContactsJson) {
      try { all.push(...(JSON.parse(r.ContactsJson) as ContactPerson[]) } catch { /* skip */ }
    }
  })
  return all
})

const deptContactsDisplay = computed(() => {
  if (!detail.value?.DeptContactsJson) return []
  try { return JSON.parse(detail.value.DeptContactsJson) } catch { return [] }
})

const doubtsDisplay = computed(() => {
  if (!detail.value?.DoubtsJson) return []
  try { return JSON.parse(detail.value.DoubtsJson) } catch { return []
})

// ──── 加载数据 ────
async function loadChecklist() {
  loading.value = true
  try {
    const res = await getPlanChecklist(props.planCode)
    if (res?.success && res.data) {
      checklist.value = res.data
      rows.value = res.data.Rows || []
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
  try {
    const res = await getPlanCheckItemDetail(row.DetailCode)
    if (res?.success && res.data) {
      detail.value = res.data
      editForm.conclusion = res.data.Conclusion || ''
      editForm.reason = res.data.Reason || ''
      // 从检查证据中获取补充材料
      supplementList.value = (res.data.CheckEvidences || [])
        .filter(e => e.EvidenceKind === 'manual' && e.CapturedIn === 'manual')
        .map(e => ({ text: e.Snippet || e.LlmSummary || '' }))
        .filter(s => s.text)
    }
  } catch (e: any) {
    ElMessage.error('加载详情失败：' + (e?.message || ''))
  } finally {
    detailLoading.value = false
  }
}

// ──── 全局操作 ────
async function handleAutoAnalyze() {
  // 调起任务创建 + 启动队列流程
  await ElMessageBox.confirm(
    '将创建新的认证准备任务并启动队列分析，是否继续？',
    '启动自动分析',
    { confirmButtonText: '确定', cancelButtonText: '取消', type: 'info' }
  ).catch(() => {})
}

async function handleSaveReport() {
  // TODO: 保存报告草稿（后端待实现报告草稿接口）
  ElMessage.info('开发中：报告保存功能')
}

async function handleExport() {
  const res = await exportPlanReport(props.planCode)
  if (res?.success && res.data) {
    ElMessage.success(res.data.Message || '导出已排队')
  } else {
    ElMessage.error(res?.message || '导出失败')
  }
}

// ──── 侧边栏保存 ────
function saveItemDetail() {
  // TODO: 提交结论 + 补充材料（后端待实现）
  ElMessage.success('已保存')
  editingConclusion.value = false
}

function addSupplement() {
  const text = newSupplement.value.trim()
  if (!text) return
  supplementList.value.push({ text })
  newSupplement.value = ''
}

function removeSupplement(idx: number) {
  supplementList.value.splice(idx, 1)
}

// ──── 原始证据预览（新窗口） ────
function openEvidencePreview(evidenceCode: string, anchor?: string | null) {
  const url = `/audit-task/doc-preview?evidenceCode=${encodeURIComponent(evidenceCode)}${anchor ? '&anchor=' + encodeURIComponent(anchor) : ''}`
  window.open(url, '_blank')
}

// ──── 队列 ────
function startQueueSimulation() {
  queueStatus.running = true
  queueStatus.total = rows.value.filter(r => !r.Conclusion || r.Conclusion === 'na').length
  queueStatus.done = rows.value.filter(r => r.Conclusion && r.Conclusion !== 'na').length
  queueStatus.queued = queueStatus.total - queueStatus.done
  queueStatus.lastTaskKind = 'prep'
  queueStatus.duration = '00:00'
}

onMounted(() => {
  loadChecklist()
})

onBeforeUnmount(() => {
  // cleanup timers
})
</script>

<template>
  <div class="wb-container">
    <!-- 顶部工具栏 -->
    <div class="wb-toolbar">
      <div class="wb-toolbar-left">
        <span class="wb-plan-name">{{ planName }}</span>
        <span class="wb-meta" v-if="checklist">共 {{ rows.length }} 项</span>
      </div>
      <div class="wb-toolbar-right">
        <el-button type="primary" @click="handleAutoAnalyze" :disabled="queueStatus.running">自动分析</el-button>
        <el-button @click="handleSaveReport">保存报告</el-button>
        <el-button @click="handleExport">导出报告</el-button>
      </div>
    </div>

    <!-- 主表 -->
    <div class="wb-table-wrap">
      <el-table
        :data="rows"
        border
        stripe
        size="small"
        v-loading="loading"
        height="100%"
        empty-text="暂无检查项"
        row-key="DetailCode"
        highlight-current-row
        @row-click="openDetail"
      >
        <el-table-column label="检查项" prop="ClauseNumbers" min-width="180" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="wb-clause">{{ row.ClauseNumbers || '-' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="部门" prop="DeptName" width="110" show-overflow-tooltip>
          <template #default="{ row }">
            {{ row.DeptName || '-' }}
          </template>
        </el-table-column>
        <el-table-column label="部门联系人" width="160">
          <template #default="{ row }">
            <el-popover
              v-if="row.ContactsJson"
              placement="right"
              :width="220"
              trigger="hover"
              :content="JSON.parse(row.ContactsJson || '[]').map((c: ContactPerson) => `${c.name} / ${c.position} / ${c.phone}`).join('\n')"
            >
              <template #reference>
                <el-tag size="small" type="info">联系人({{ JSON.parse(row.ContactsJson || '[]').length }})</el-tag>
              </template>
            </el-popover>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.Conclusion" size="small" type="success" effect="plain">已分析</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">待分析</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="PrepState / 结论" width="120" align="center">
          <template #default="{ row }">
            <span v-if="row.PrepState" class="wb-prep-state">{{ row.PrepState }}</span>
            <span v-if="row.Conclusion" class="wb-conclusion">{{ row.Conclusion }}</span>
            <span v-if="!row.PrepState && !row.Conclusion" class="wb-muted">-</span>
          </template>
        </el-table-column>
        <el-table-column label="NC" width="60" align="center">
          <template #default="{ row }">
            <el-tag v-if="row.NcSuggested" size="small" type="danger">NC</el-tag>
            <span v-else class="wb-muted">-</span>
          </template>
        </el-table-column>
        <el-table-column label="原始证据" width="80" align="center">
          <template #default="{ row }">
            {{ row.RawEvidenceCount || 0 }}
          </template>
        </el-table-column>
        <el-table-column label="检查证据" width="80" align="center">
          <template #default="{ row }">
            {{ row.CheckEvidenceCount || 0 }}
          </template>
        </el-table-column>
        <el-table-column label="疑点" width="60" align="center">
          <template #default="{ row }">
            <span v-if="row.DoubtCount" class="wb-doubt">{{ row.DoubtCount }}</span>
            <span v-else class="wb-muted">0</span>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <!-- 底部队列进度栏 -->
    <div class="wb-queue-bar" v-if="queueStatus.running">
      <span class="wb-queue-label">队列进度</span>
      <span class="wb-queue-info">已完成 {{ queueStatus.done }} / 共 {{ queueStatus.total }}</span>
      <span class="wb-queue-info">排队中 {{ queueStatus.queued }}</span>
      <span class="wb-queue-info">任务类型：{{ queueStatus.lastTaskKind === 'prep' ? '认证准备' : '认证检查' }}</span>
      <span class="wb-queue-info">已耗时 {{ queueStatus.duration }}</span>
      <el-button size="small" type="warning" text>暂停队列</el-button>
    </div>
    <div class="wb-queue-bar idle" v-else>
      <span class="wb-queue-label">空闲</span>
      <span class="wb-queue-info">无执行中的队列任务</span>
    </div>

    <!-- 右侧侧边栏 Drawer -->
    <el-drawer
      v-model="drawerVisible"
      direction="rtl"
      size="420px"
      :title="detail?.ClauseNumbers || '检查项详情'"
      :close-on-click-modal="true"
    >
      <div v-loading="detailLoading" class="wb-drawer">
        <template v-if="detail">
          <!-- 摘要条 -->
          <div class="wb-summary">
            <div class="wb-summary-row">
              <span class="wb-summary-label">结论：</span>
              <el-tag v-if="detail.Conclusion" size="small" type="primary">{{ detail.Conclusion }}</el-tag>
              <span v-else class="wb-muted">待分析</span>
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
              <el-tag size="small" type="warning">已锁定 ({{ detail.LockedBy }})</el-tag>
            </div>
          </div>

          <!-- 部门 & 联系人 -->
          <div class="wb-zone">
            <div class="wb-zone-title">部门 & 联系人</div>
            <div v-if="deptContactsDisplay.length" class="wb-dept-list">
              <div v-for="dc in deptContactsDisplay" :key="dc.deptCode" class="wb-dept">
                <div class="wb-dept-name">{{ dc.deptName }}</div>
                <div v-if="dc.leaderName" class="wb-dept-leader">{{ dc.leaderName }} / {{ dc.leaderPhone }}</div>
                <div v-for="c in dc.contacts" :key="c.name" class="wb-dept-contact">
                  {{ c.name }} / {{ c.position || c.roleType }} / {{ c.phone }}
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
              <div v-for="(s, i) in supplementList" :key="i" class="wb-sup-item">
                <span>{{ s.text }}</span>
                <el-button link type="danger" size="small" @click="removeSupplement(i)">删除</el-button>
              </div>
            </div>
            <div class="wb-supplement-add">
              <el-input
                v-model="newSupplement"
                type="textarea"
                :rows="2"
                placeholder="输入补充材料..."
                resize="none"
              />
              <el-button type="primary" size="small" @click="addSupplement">添加</el-button>
            </div>
          </div>

          <!-- 结论编辑 -->
          <div class="wb-zone" v-if="editingConclusion">
            <div class="wb-zone-title">编辑结论</div>
            <el-form label-position="top" size="small">
              <el-form-item label="结论">
                <el-select v-model="editForm.conclusion" style="width: 100%;">
                  <el-option label="pass" value="pass" />
                  <el-option label="ofi" value="ofi" />
                  <el-option label="nc" value="nc" />
                  <el-option label="na" value="na" />
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
          <el-button @click="saveItemDetail" type="primary" :disabled="detail?.IsLocked">保存</el-button>
          <el-button
            :type="detail?.IsLocked ? 'warning' : 'danger'"
            @click="detail && (detail.IsLocked = !detail.IsLocked)"
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
  gap: 12px;
}
.wb-plan-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.wb-meta {
  font-size: 12px;
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
