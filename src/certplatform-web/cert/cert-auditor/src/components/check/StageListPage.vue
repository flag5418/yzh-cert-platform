<script setup lang="ts">
/**
 * 检查实例列表页 —— 认证准备 / 认证检查 两个菜单共用
 *
 * ★ 一行 = 一个「计划 × 分支」的执行实例（原型列表页的口径）。
 *   点进去才是检查项清单（CheckDetailPage）。
 *
 * 后端：`POST /api/Auditor/CertTask/stage-list`
 *
 * ★ 用户 2026-11-01 的六条原型反馈，在本页的落点：
 *   ① 计划可调整 → 行内下拉里给「查看认证计划」，计划本身的增删改归「认证计划」页
 *   ② 操作按钮用下拉 → `actionMaxInline: 1`，只把「详情」留在行内，其余全进「更多 ▾」
 *   ③ 状态可点击 → 点状态徽标弹执行明细抽屉（轮次 / 任务号 / 起止时间 / 进度）
 *   ④ 锁定在表格内 → 本页只显示锁定计数，锁定动作在详情页的检查项表内
 *   ⑤ 可反复重新执行 → 「重新执行」不新增行，开新一轮（RunRound+1），已锁定的检查项会跳过
 *
 * ⚠️ 统一组件（法条 R6 / S03–S08）：表格用 YzhTable、状态用 YzhStatusBadge、
 *   抽屉用 YzhDrawer、确认用 confirmOrFalse、空态由 YzhTable 内置 ——
 *   ⛔ 不要退回裸 el-table / el-tag / el-drawer / ElMessageBox.confirm。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElInput, ElOption, ElSelect, ElProgress } from 'element-plus'
import { YzhTable, YzhStatusBadge, YzhDrawer, confirmOrFalse } from '@yzh-core'
import type { YzhAction, YzhTableColumn } from '@yzh-core/components/table/types'
import {
  createCertTask,
  getStageList,
  pauseCertTask,
  startCertTask,
  type CertStageInstanceRow,
  type CertStageListSummary,
} from '@share/api/auditor/cert-check-task'
import {
  STAGE_META, STAGE_STATE_LABELS, STAGE_STATE_TAGS,
  labelOf, percent, progressColor, type StageKind,
} from './verdict'

const props = defineProps<{ stageKind: StageKind }>()

/**
 * 统一提示出口。
 * ★ 22-接口返回规范 §二：失败原因只放 `err`，成功信息放 `message` ——
 *   失败时一律读 `err || message`，只读 message 只会给用户看兜底文案。
 */
function flash(
  res: { success: boolean; message?: string; err?: string },
  okText: string,
  failText: string,
) {
  if (res.success) ElMessage.success(res.message || okText)
  else ElMessage.error(res.err || res.message || failText)
}

const router = useRouter()
const meta = computed(() => STAGE_META[props.stageKind])
const isPrep = computed(() => props.stageKind === 'prep')

const loading = ref(false)
const rows = ref<CertStageInstanceRow[]>([])
const total = ref(0)
const summary = ref<CertStageListSummary>({
  InstanceTotal: 0, Running: 0, Done: 0, Paused: 0,
  ItemTotal: 0, NcTotal: 0, LockedTotal: 0,
})

const query = reactive({ keyword: '', status: '' })

/** 状态抽屉 */
const statusDrawer = ref(false)
const statusRow = ref<CertStageInstanceRow | null>(null)

// ══════════════════════════════════════════════════════════════════
// 列定义
// ══════════════════════════════════════════════════════════════════

/**
 * ★ 插槽列必须同时声明 `slotKey` + `slot: true`（YzhTable 的渲染开关是
 *   `v-if="col.slot"`，只给 slotKey 会静默渲染成空白列 —— 这是本次踩过的坑）。
 *   `slotKey` 决定插槽名 `column-${slotKey}`，`slot` 决定是否走插槽分支。
 */
const columns = computed<YzhTableColumn<CertStageInstanceRow>[]>(() => [
  { prop: 'EnterpriseName', label: '企业', minWidth: 150 },
  { prop: 'PlanName', label: '计划', minWidth: 200, showOverflowTooltip: true },
  { prop: 'StageName', label: '阶段', width: 100 },
  { slotKey: 'Standards', slot: true, label: '认证标准', minWidth: 200 },
  { prop: 'ItemTotal', label: '检查项', width: 80, align: 'center' },
  { slotKey: 'Progress', slot: true, label: '进度', width: 160 },
  {
    slotKey: 'Focus',
    slot: true,
    label: isPrep.value ? '资料缺失' : 'NC',
    width: 90,
    align: 'center',
  },
  { slotKey: 'State', slot: true, label: '状态', width: 130, align: 'center' },
  { slotKey: 'Round', slot: true, label: '轮次', width: 84, align: 'center' },
  { prop: 'StartedAt', label: '开启时间', width: 150 },
])

// ══════════════════════════════════════════════════════════════════
// 加载
// ══════════════════════════════════════════════════════════════════

async function loadList() {
  loading.value = true
  try {
    const res = await getStageList({
      StageKind: props.stageKind,
      Keyword: query.keyword || undefined,
      Status: query.status || undefined,
      PageIndex: 1,
      PageSize: 100,
    })
    if (res.success && res.data) {
      rows.value = res.data.Rows ?? []
      total.value = res.data.Total ?? 0
      summary.value = res.data.Summary ?? summary.value
    } else {
      ElMessage.error(res.err || res.message || '加载失败')
    }
  } finally {
    loading.value = false
  }
}

function resetQuery() {
  query.keyword = ''
  query.status = ''
  void loadList()
}

function goDetail(row: CertStageInstanceRow) {
  router.push(`/audit-${props.stageKind}/${row.PlanCode}`)
}

function openStatus(row: CertStageInstanceRow) {
  statusRow.value = row
  statusDrawer.value = true
}

// ══════════════════════════════════════════════════════════════════
// 执行操作
// ══════════════════════════════════════════════════════════════════

/**
 * 启动 / 继续执行。
 * 没有任务实例（从没跑过）⇒ 先建任务再启动；
 * 已有任务（paused / pending）⇒ 直接启动同一任务，不新建。
 */
async function doStart(row: CertStageInstanceRow) {
  const ok = await confirmOrFalse(
    `确认启动「${row.EnterpriseName ?? ''} · ${row.PlanName ?? ''}」的${meta.value.title}？`,
    '启动执行',
  )
  if (!ok) return

  let taskCode = row.TaskCode
  if (!taskCode) {
    const created = await createCertTask({
      PlanCode: row.PlanCode,
      TaskKind: props.stageKind,
      RunMode: 'incremental',
    })
    if (!created.success || !created.data) {
      ElMessage.error(created.err || created.message || '创建任务失败')
      return
    }
    taskCode = created.data.TaskCode
  }

  const res = await startCertTask(taskCode)
  flash(res, '已启动', '启动失败')
  if (res.success) await loadList()
}

async function doPause(row: CertStageInstanceRow) {
  if (!row.TaskCode) return
  const ok = await confirmOrFalse(
    '确认暂停当前执行队列？暂停后已写入的结果保留。',
    '暂停执行',
  )
  if (!ok) return
  const res = await pauseCertTask(row.TaskCode)
  flash(res, '已暂停', '暂停失败')
  if (res.success) await loadList()
}

/**
 * 重新执行 —— 不新增行，开新一轮。
 * ★ 已锁定的检查项会被后端跳过（锁的作用就是保护审核员已经复核过的结论），
 *   所以这里必须说清楚，否则审核员会以为「重跑把我之前的工作作废了」。
 */
async function doRerun(row: CertStageInstanceRow) {
  const lockedHint = row.ItemLocked > 0
    ? `已锁定的 ${row.ItemLocked} 个检查项会被跳过，不会覆盖审核员已确认的结论。`
    : ''
  const ok = await confirmOrFalse(
    `重新执行会开启第 ${row.RunRound + 1} 轮，覆盖未锁定检查项的分析结果。${lockedHint}`,
    '重新执行',
  )
  if (!ok) return

  const created = await createCertTask({
    PlanCode: row.PlanCode,
    TaskKind: props.stageKind,
    RunMode: 'incremental',
  })
  if (!created.success || !created.data) {
    ElMessage.error(created.err || created.message || '创建任务失败')
    return
  }
  const res = await startCertTask(created.data.TaskCode)
  flash(res, `已开启第 ${row.RunRound + 1} 轮`, '启动失败')
  if (res.success) await loadList()
}

// ══════════════════════════════════════════════════════════════════
// 行动作（按行求值）
// ══════════════════════════════════════════════════════════════════

const canStart = (row: CertStageInstanceRow) =>
  !row.TaskRunning
  && (row.State === 'waiting' || row.State === 'paused' || row.State === 'failed')

const canPause = (row: CertStageInstanceRow) => row.State === 'running'
const canRerun = (row: CertStageInstanceRow) => !row.TaskRunning && row.RunRound > 0

const rowActions = (row: CertStageInstanceRow): YzhAction[] => {
  const acts: YzhAction[] = [{ key: 'detail', text: '详情', type: 'primary' }]

  if (canStart(row)) {
    acts.push({
      key: 'start',
      text: row.RunRound > 0 ? '继续执行' : '启动执行',
      type: 'primary',
    })
  }
  if (canPause(row)) acts.push({ key: 'pause', text: '暂停执行', type: 'danger' })

  acts.push({
    key: 'rerun',
    text: `重新执行（第 ${row.RunRound + 1} 轮）`,
    type: 'default',
    visible: canRerun(row),
    disabled: !canRerun(row),
  })

  acts.push({ key: 'plan', text: '查看认证计划', type: 'default' })
  acts.push(
    isPrep.value
      ? { key: 'export', text: '导出入场计划', type: 'default' }
      : { key: 'export', text: '导出认证报告', type: 'default' },
  )
  if (!isPrep.value) acts.push({ key: 'nc', text: 'NC 清单', type: 'default' })

  return acts
}

function onRowAction(key: string, row: CertStageInstanceRow) {
  switch (key) {
    case 'detail': goDetail(row); break
    case 'start': void doStart(row); break
    case 'pause': void doPause(row); break
    case 'rerun': void doRerun(row); break
    case 'plan': router.push('/audit-plan'); break
    case 'export':
      ElMessage.info(isPrep.value ? '入场计划导出（二期）' : '认证报告导出（二期）')
      break
    case 'nc': router.push('/nc-results'); break
  }
}

onMounted(loadList)
</script>

<template>
  <div class="sl-page">
    <!-- 页头 -->
    <div class="sl-head">
      <div class="sl-head__main">
        <div class="sl-title">
          <span class="sl-bar" :class="isPrep ? 'is-prep' : 'is-onsite'" />
          {{ meta.title }}
        </div>
        <div class="sl-sub">{{ meta.subtitle }}</div>
      </div>
      <el-button type="primary" plain @click="loadList">刷新</el-button>
    </div>

    <!-- 统计 -->
    <div class="sl-stats">
      <div class="sl-stat is-a">
        <div class="sl-stat__n">{{ summary.Running }}</div>
        <div class="sl-stat__l">执行中</div>
      </div>
      <div class="sl-stat is-b">
        <div class="sl-stat__n">{{ summary.Done }}</div>
        <div class="sl-stat__l">已完成</div>
      </div>
      <div class="sl-stat is-c">
        <div class="sl-stat__n">{{ summary.Paused }}</div>
        <div class="sl-stat__l">已暂停</div>
      </div>
      <div class="sl-stat">
        <div class="sl-stat__n">{{ summary.ItemTotal }}</div>
        <div class="sl-stat__l">检查项合计</div>
      </div>
      <div class="sl-stat">
        <div class="sl-stat__n">{{ summary.LockedTotal }}</div>
        <div class="sl-stat__l">已锁定</div>
      </div>
      <div v-if="!isPrep" class="sl-stat is-d">
        <div class="sl-stat__n">{{ summary.NcTotal }}</div>
        <div class="sl-stat__l">NC 疑点</div>
      </div>
    </div>

    <!-- 筛选 -->
    <div class="sl-filters">
      <el-input
        v-model="query.keyword"
        placeholder="企业 / 计划 / 标准 / 阶段"
        clearable
        class="sl-filters__kw"
        @keyup.enter="loadList"
      />
      <el-select v-model="query.status" placeholder="执行状态" clearable class="sl-filters__sel">
        <el-option label="待启动" value="waiting" />
        <el-option label="执行中" value="running" />
        <el-option label="已暂停" value="paused" />
        <el-option label="已完成" value="done" />
        <el-option label="部分完成" value="partial" />
        <el-option label="失败" value="failed" />
      </el-select>
      <el-button type="primary" @click="loadList">检索</el-button>
      <el-button type="default" @click="resetQuery">重置</el-button>
    </div>

    <!-- 列表
         ⛔ 不绑 @row-click：行内已有「状态」徽标、「更多」下拉等控件，整行可点会把
            这些点击一并吃成跳转（实测点「更多」会直接进详情页）。进详情走行内「详情」。 -->
    <div class="sl-card">
      <YzhTable
        :columns="columns"
        :data="rows"
        :loading="loading"
        :show-pagination="false"
        :toolbar="false"
        :no-padding="true"
        row-key="PlanCode"
        size="small"
        :row-action-buttons="rowActions"
        :action-max-inline="1"
        :empty-text="`还没有${meta.title}实例，先在「认证计划」里建计划`"
        @row-action="onRowAction"
      >
        <!-- 认证标准 -->
        <template #column-Standards="{ row }">
          <template v-if="row.Standards">
            <span
              v-for="(s, i) in String(row.Standards).split(',')"
              :key="i"
              class="sl-std"
            >{{ s }}</span>
          </template>
          <span v-else class="sl-muted">—</span>
        </template>

        <!-- 进度 -->
        <template #column-Progress="{ row }">
          <div class="sl-prog">
            <el-progress
              :percentage="percent(row.ItemDone, row.ItemTotal)"
              :stroke-width="8"
              :show-text="false"
              :color="progressColor(percent(row.ItemDone, row.ItemTotal))"
            />
            <span class="sl-prog__num">{{ row.ItemDone }}/{{ row.ItemTotal }}</span>
          </div>
        </template>

        <!-- 准备看资料缺失，检查看 NC 数 -->
        <template #column-Focus="{ row }">
          <YzhStatusBadge
            v-if="(isPrep ? row.ItemMissing : row.ItemNc) > 0"
            :type="isPrep ? 'warning' : 'danger'"
            :text="String(isPrep ? row.ItemMissing : row.ItemNc)"
          />
          <span v-else class="sl-muted">—</span>
        </template>

        <!-- ★ 状态可点击 -->
        <template #column-State="{ row }">
          <span class="sl-state" @click.stop="openStatus(row)">
            <YzhStatusBadge
              :type="STAGE_STATE_TAGS[row.State] || 'info'"
              :text="labelOf(STAGE_STATE_LABELS, row.State)"
            />
            <YzhStatusBadge
              v-if="row.TaskRunning"
              type="info"
              text="队列中"
              class="sl-tag"
            />
          </span>
        </template>

        <template #column-Round="{ row }">
          <span :class="row.RunRound > 0 ? '' : 'sl-muted'">
            {{ row.RunRound > 0 ? `第 ${row.RunRound} 轮` : '—' }}
          </span>
        </template>
      </YzhTable>

      <div class="sl-foot">
        共 {{ total }} 份 · 点状态徽标看执行明细 · 「重新执行」不新增行，开新一轮
      </div>
    </div>

    <!-- 执行明细抽屉 -->
    <YzhDrawer
      v-model="statusDrawer"
      title="执行明细"
      size="460px"
      :show-footer="false"
    >
      <template v-if="statusRow">
        <div class="sl-dl">
          <div class="sl-dl__row">
            <span class="sl-dl__k">企业</span>
            <span class="sl-dl__v">{{ statusRow.EnterpriseName || '—' }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">计划</span>
            <span class="sl-dl__v">{{ statusRow.PlanName || '—' }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">分支</span>
            <span class="sl-dl__v">{{ meta.title }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">状态</span>
            <span class="sl-dl__v">
              <YzhStatusBadge
                :type="STAGE_STATE_TAGS[statusRow.State] || 'info'"
                :text="labelOf(STAGE_STATE_LABELS, statusRow.State)"
              />
            </span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">执行轮次</span>
            <span class="sl-dl__v">
              {{ statusRow.RunRound > 0 ? `第 ${statusRow.RunRound} 轮` : '未执行' }}
            </span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">任务编号</span>
            <span class="sl-dl__v">{{ statusRow.TaskNo || '—' }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">进度</span>
            <span class="sl-dl__v">
              {{ statusRow.ItemDone }}/{{ statusRow.ItemTotal }}
              （{{ percent(statusRow.ItemDone, statusRow.ItemTotal) }}%）
            </span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">已锁定</span>
            <span class="sl-dl__v">{{ statusRow.ItemLocked }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">{{ isPrep ? '资料缺失' : 'NC 疑点' }}</span>
            <span class="sl-dl__v">{{ isPrep ? statusRow.ItemMissing : statusRow.ItemNc }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">负责审核员</span>
            <span class="sl-dl__v">{{ statusRow.OwnerNames || '未分配' }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">开始时间</span>
            <span class="sl-dl__v">{{ statusRow.StartedAt || '—' }}</span>
          </div>
          <div class="sl-dl__row">
            <span class="sl-dl__k">结束时间</span>
            <span class="sl-dl__v">{{ statusRow.FinishedAt || '—' }}</span>
          </div>
        </div>

        <div class="sl-note">
          状态由后端任务表实时返回（不是前端自己记的）。换个人打开、队列在后台跑完，
          刷新即同步。
        </div>

        <div class="sl-drawer-acts">
          <el-button
            v-if="canStart(statusRow)"
            type="primary"
            @click="doStart(statusRow)"
          >{{ statusRow.RunRound > 0 ? '继续执行' : '启动执行' }}</el-button>
          <el-button v-if="canPause(statusRow)" type="danger" @click="doPause(statusRow)">
            暂停执行
          </el-button>
          <el-button v-if="canRerun(statusRow)" type="default" @click="doRerun(statusRow)">
            重新执行
          </el-button>
          <el-button type="primary" plain @click="goDetail(statusRow)">查看检查项</el-button>
        </div>
      </template>
    </YzhDrawer>
  </div>
</template>

<style scoped>
.sl-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
}

/* ── 页头 ── */
.sl-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--yzh-space-4, 16px);
  margin-bottom: var(--yzh-space-4, 16px);
}
.sl-head__main { min-width: 0; }
.sl-title {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, var(--el-text-color-primary));
}
.sl-bar {
  display: inline-block;
  width: var(--yzh-space-1, 4px);
  height: var(--yzh-space-4, 16px);
  border-radius: var(--yzh-radius-sm, 4px);
}
.sl-bar.is-prep { background: var(--el-color-primary); }
.sl-bar.is-onsite { background: var(--el-color-warning); }
.sl-sub {
  margin-top: var(--yzh-space-1, 4px);
  max-width: var(--yzh-content-narrow, 880px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.6);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}

/* ── 统计 ── */
.sl-stats {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-4, 16px);
}
.sl-stat {
  flex: 1 1 var(--yzh-space-30, 120px);
  min-width: var(--yzh-space-28, 110px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  border-radius: var(--yzh-radius-base, 6px);
  background: var(--yzh-color-bg-container, var(--el-bg-color));
}
.sl-stat__n {
  font-size: var(--yzh-font-size-2xl, 22px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  line-height: var(--yzh-line-height-tight, 1.2);
  color: var(--yzh-color-text-primary, var(--el-text-color-primary));
}
.sl-stat__l {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.sl-stat.is-a .sl-stat__n { color: var(--el-color-primary); }
.sl-stat.is-b .sl-stat__n { color: var(--el-color-success); }
.sl-stat.is-c .sl-stat__n { color: var(--el-color-warning); }
.sl-stat.is-d .sl-stat__n { color: var(--el-color-danger); }

/* ── 筛选 ── */
.sl-filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-3, 12px);
}
.sl-filters__kw { width: var(--yzh-width-kw, 240px); }
.sl-filters__sel { width: var(--yzh-width-sel, 150px); }

/* ── 表格 ── */
.sl-card {
  border: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  border-radius: var(--yzh-radius-base, 6px);
  background: var(--yzh-color-bg-container, var(--el-bg-color));
  overflow: hidden;
}
.sl-muted { color: var(--el-text-color-placeholder); }
.sl-tag { margin-left: var(--yzh-space-1, 4px); }
.sl-std {
  display: inline-block;
  margin-right: var(--yzh-space-1, 4px);
  padding: 0 var(--yzh-space-1, 4px);
  border: 1px solid var(--el-border-color);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.5);
  color: var(--el-color-primary);
}
.sl-state { cursor: pointer; }
.sl-prog { display: flex; align-items: center; gap: var(--yzh-space-2, 8px); }
.sl-prog :deep(.el-progress) { flex: 1; }
.sl-prog__num {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}
.sl-foot {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-top: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
  background: var(--el-fill-color-lighter);
}

/* ── 抽屉 ── */
.sl-dl { display: flex; flex-direction: column; gap: var(--yzh-space-2, 8px); }
.sl-dl__row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  padding-bottom: var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  font-size: var(--yzh-font-size-sm, 13px);
}
.sl-dl__k {
  flex: 0 0 var(--yzh-space-24, 96px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.sl-dl__v {
  flex: 1;
  color: var(--yzh-color-text-primary, var(--el-text-color-primary));
}
.sl-note {
  margin: var(--yzh-space-3, 12px) 0;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-left: 3px solid var(--el-color-info-light-5);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.6);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
  background: var(--el-fill-color-lighter);
}
.sl-drawer-acts { display: flex; flex-wrap: wrap; gap: var(--yzh-space-2, 8px); }
</style>
