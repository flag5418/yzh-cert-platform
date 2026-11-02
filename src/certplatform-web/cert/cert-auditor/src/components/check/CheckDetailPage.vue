<script setup lang="ts">
/**
 * 检查项详情页 —— 认证准备 / 认证检查 两个菜单共用
 *
 * ★ 两个分支的差异（用户 2026-11-01 口径）：
 *   ┌──────────┬────────────────────────────┬────────────────────────────┐
 *   │          │ 认证准备 prep              │ 认证检查 onsite            │
 *   ├──────────┼────────────────────────────┼────────────────────────────┤
 *   │ 证据顺序 │ 系统先按企业资料分析，      │ 审核员先挂接证据，          │
 *   │          │ 审核员再补证据             │ 再交给系统分析             │
 *   │ 状态列   │ PrepState 资料齐不齐       │ Conclusion 符合/观察/不符合│
 *   │ 签发 NC  │ ✗ 不签发（只有提示）       │ ✓ 逐条签发                 │
 *   │ 审核员   │ ✓ 可定义（入场计划核心）   │ ✗ 只读（准备期已定）       │
 *   │ 产出     │ 入场计划                   │ 认证报告                   │
 *   └──────────┴────────────────────────────┴────────────────────────────┘
 *
 * 后端：
 *   列表   POST /api/Auditor/CertTask/worklist
 *   页面锁 POST /api/Auditor/CertTask/plan-checklist
 *   详情   POST /api/Auditor/CertTask/plan-checkitem-detail
 *   分析   POST /api/Auditor/CertTask/analyze-item
 *   锁定   POST /api/Auditor/CertTask/lock-result / unlock-result
 *   审核员 POST /api/Auditor/CertTask/set-owner
 *   签发NC POST /api/Auditor/CertNc/issue
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  ElAlert, ElButton, ElForm, ElFormItem, ElInput, ElMessage, ElOption, ElProgress,
  ElSelect, ElTabPane, ElTabs,
} from 'element-plus'
import {
  YzhDialog, YzhDrawer, YzhStatusBadge, YzhTable, confirmOrFalse,
} from '@yzh-core'
import type { YzhAction, YzhTableColumn } from '@yzh-core/components/table/types'
import {
  analyzeCheckItem, getMemberOptions, getPlanCheckItemDetail, getPlanChecklist,
  getWorklist, issueNc, lockConclusion, setCheckItemOwner, unlockConclusion,
  type CertMemberOption, type CertPlanCheckItemDetail, type CertPlanChecklistResponse,
  type CertWorklistRow, type CertWorklistSummary,
} from '@share/api/auditor/cert-check-task'
import {
  CONCLUSION_LABELS, CONCLUSION_TAGS, ITEM_STATE_LABELS, ITEM_STATE_TAGS,
  PREP_STATE_LABELS, PREP_STATE_TAGS, SEVERITY_LABELS, SEVERITY_TAGS, STAGE_META,
  labelOf, percent, progressColor, type StageKind,
} from './verdict'

const props = defineProps<{ stageKind: StageKind; planCode: string }>()

/**
 * 统一提示出口。
 * ★ 22-接口返回规范 §二：失败原因只放 `err`，成功信息放 `message` ——
 *   失败时一律读 `err || message`，只读 message 会让用户看到兜底文案而看不到真实原因
 *   （例如「该检查项已进入认证检查阶段，审核员不可再变更」）。
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

// ── 状态 ──
const loading = ref(false)
const rows = ref<CertWorklistRow[]>([])
const summary = ref<CertWorklistSummary>({
  ItemTotal: 0, Waiting: 0, Analyzed: 0, Locked: 0,
  NcCount: 0, MissingCount: 0, DoubtCount: 0, RunningPlans: 0,
})
const planInfo = ref<CertPlanChecklistResponse | null>(null)
const query = reactive({ keyword: '', status: '' })

/** 页面锁：队列在跑 ⇒ 整页只读（唯一真相来自后端） */
const pageLocked = computed(() => planInfo.value?.TaskRunning === true)

const planTitle = computed(() => {
  const ent = rows.value[0]?.EnterpriseName
  const name = planInfo.value?.PlanName ?? rows.value[0]?.PlanName
  if (!name) return ent || '检查项清单'
  // ★ 计划名本身已按「企业·阶段·认证计划」拼装过，再前置企业名会重复
  if (ent && name.startsWith(ent)) return name
  return [ent, name].filter(Boolean).join(' · ')
})

// ── 抽屉 / 弹窗 ──
const drawer = ref(false)
const detail = ref<CertPlanCheckItemDetail | null>(null)
const detailLoading = ref(false)
const currentRow = ref<CertWorklistRow | null>(null)

const ncDialog = ref(false)
const ncForm = reactive({ FactDesc: '', RequirementRef: '', Severity: 'major' })
const ncTarget = ref<CertWorklistRow | null>(null)

const ownerDialog = ref(false)
const ownerForm = reactive({ code: '', name: '' })
const ownerTarget = ref<CertWorklistRow | null>(null)
/** 专家组成员池（审核员只能从这里选，不允许手打姓名） */
const memberOptions = ref<CertMemberOption[]>([])
const memberLoading = ref(false)

// ══════════════════════════════════════════════════════════════════
// 列定义
// ══════════════════════════════════════════════════════════════════

/**
 * ★ 插槽列必须同时声明 `slotKey` + `slot: true`（YzhTable 的渲染开关是
 *   `v-if="col.slot"`，只给 slotKey 会静默渲染成空白列）。
 */
const columns = computed<YzhTableColumn<CertWorklistRow>[]>(() => [
  // ★ 列宽合计 + 操作列(224) 必须 ≤ 表格可视宽，否则最右的「锁定」列会被固定列压住半截。
  //   当前合计 716 + 224 = 940（1080 宽窗口下可视 944，正好放下）。
  { slotKey: 'Clause', slot: true, label: '条款', minWidth: 180 },
  // ★ 部门为空是真实数据缺失（计划明细未指定部门），给占位文字而不是留白，
  //   否则看起来像渲染缺陷。
  { prop: 'DeptNames', label: '部门', width: 92, formatter: (v: any) => v || '未指定' },
  { slotKey: 'Owner', slot: true, label: '审核员', width: 104 },
  { slotKey: 'Verdict', slot: true, label: meta.value.stateLabel, width: 112, align: 'center' },
  { slotKey: 'Doubt', slot: true, label: '疑点', width: 56, align: 'center' },
  { slotKey: 'Evidence', slot: true, label: '证据', width: 80, align: 'center' },
  // ★ 不再单列「分析次数」：行内动作按钮已按 AnalyzeCount 显示「分析 / 重跑」，
  //   单列会挤掉「锁定」列（表格宽度不够），信息在详情抽屉里仍可见。
  { slotKey: 'Lock', slot: true, label: '锁定', width: 92, align: 'center' },
])

// ══════════════════════════════════════════════════════════════════
// 加载
// ══════════════════════════════════════════════════════════════════

async function loadAll() {
  loading.value = true
  try {
    const [wl, pl] = await Promise.all([
      getWorklist({
        StageKind: props.stageKind,
        PlanCode: props.planCode,
        Keyword: query.keyword || undefined,
        Status: query.status || undefined,
        PageIndex: 1,
        PageSize: 200,
      }),
      getPlanChecklist(props.planCode),
    ])

    if (wl.success && wl.data) {
      rows.value = wl.data.Rows ?? []
      summary.value = wl.data.Summary ?? summary.value
    } else {
      ElMessage.error(wl.err || wl.message || '加载检查项失败')
    }

    if (pl.success && pl.data) planInfo.value = pl.data
  } finally {
    loading.value = false
  }
}

async function reloadOnly() {
  const wl = await getWorklist({
    StageKind: props.stageKind,
    PlanCode: props.planCode,
    Keyword: query.keyword || undefined,
    Status: query.status || undefined,
    PageIndex: 1,
    PageSize: 200,
  })
  if (wl.success && wl.data) {
    rows.value = wl.data.Rows ?? []
    summary.value = wl.data.Summary ?? summary.value
  }
}

// ══════════════════════════════════════════════════════════════════
// 操作
// ══════════════════════════════════════════════════════════════════

function assertEditable(): boolean {
  if (pageLocked.value) {
    ElMessage.warning(
      `执行队列正在运行（${planInfo.value?.RunningTaskNo ?? ''}），为避免与队列写入互相覆盖，本页暂不可操作`,
    )
    return false
  }
  return true
}

/** 逐项分析（重跑） */
async function doAnalyze(row: CertWorklistRow) {
  if (!assertEditable()) return
  if (!row.TaskItemCode) {
    ElMessage.warning('该检查项还没有任务实例，请先在列表页启动一次执行')
    return
  }
  if (row.IsLocked) {
    ElMessage.warning('该检查项已锁定，锁定项不会被重跑覆盖；如需重跑请先解锁')
    return
  }

  const ok = await confirmOrFalse(
    `将重新分析条款 ${row.ClauseNumbers ?? ''} 并覆盖当前未锁定的结论。`,
    '重新分析',
  )
  if (!ok) return

  const res = await analyzeCheckItem(row.TaskItemCode)
  flash(res, '分析完成', '分析失败')
  if (res.success) {
    await reloadOnly()
    if (drawer.value && currentRow.value?.DetailCode === row.DetailCode) await openDetail(row)
  }
}

/** 锁定 —— 保护审核员已复核的结论，重跑会跳过 */
async function doLock(row: CertWorklistRow) {
  if (!assertEditable()) return
  if (!row.ResultCode) {
    ElMessage.warning('该检查项还没有分析结论，无法锁定')
    return
  }
  const ok = await confirmOrFalse(
    `锁定后该检查项不会被重新执行覆盖，是保护审核员复核成果的关键操作。确认锁定条款 ${row.ClauseNumbers ?? ''}？`,
    '锁定结论',
  )
  if (!ok) return

  const res = await lockConclusion(row.ResultCode, 'manual_confirm')
  flash(res, '已锁定', '锁定失败')
  if (res.success) {
    await reloadOnly()
    if (drawer.value && currentRow.value?.DetailCode === row.DetailCode) await openDetail(row)
  }
}

async function doUnlock(row: CertWorklistRow) {
  if (!assertEditable()) return
  if (!row.ResultCode) return
  const ok = await confirmOrFalse('解锁后该检查项可被重新执行覆盖。', '解锁')
  if (!ok) return

  const res = await unlockConclusion(row.ResultCode)
  flash(res, '已解锁', '解锁失败')
  if (res.success) {
    await reloadOnly()
    if (drawer.value && currentRow.value?.DetailCode === row.DetailCode) await openDetail(row)
  }
}

/** 准备阶段：设置检查项审核员（入场计划的核心信息） */
async function openOwner(row: CertWorklistRow) {
  if (!assertEditable()) return
  if (!isPrep.value) {
    ElMessage.warning('认证检查阶段不能变更审核员，审核员分工在准备期已确定')
    return
  }
  ownerTarget.value = row
  ownerForm.code = row.OwnerCode ?? ''
  ownerForm.name = row.OwnerName ?? ''
  ownerDialog.value = true

  // ★ 审核员只能从专家组成员池里挑（用户 2026-11-01）：手打的姓名在成员池里查不到人，
  //   入场计划按审核员派工时就派不出去。
  if (memberOptions.value.length === 0) {
    memberLoading.value = true
    try {
      const res = await getMemberOptions()
      if (res.success && res.data) {
        memberOptions.value = res.data
      } else {
        ElMessage.error(res.err || res.message || '专家组成员加载失败')
      }
    } finally {
      memberLoading.value = false
    }
  }
}

async function submitOwner() {
  const row = ownerTarget.value
  if (!row) return
  const code = ownerForm.code || null
  const name = memberOptions.value.find(m => m.Code === code)?.Name ?? null
  const res = await setCheckItemOwner(row.DetailCode, code, name)
  flash(res, '已设置', '设置失败')
  if (res.success) {
    ownerDialog.value = false
    await reloadOnly()
  }
}

/** 签发 NC（仅认证检查） */
function openNc(row: CertWorklistRow) {
  if (!assertEditable()) return
  if (isPrep.value) {
    ElMessage.warning('认证准备不签发 NC，只有认证检查才能签发')
    return
  }
  ncTarget.value = row
  ncForm.FactDesc = row.ReasonBrief ?? ''
  ncForm.RequirementRef = row.ClauseTitles ?? ''
  ncForm.Severity = row.Severity ?? 'major'
  ncDialog.value = true
}

async function submitNc() {
  const row = ncTarget.value
  if (!row) return
  if (!ncForm.FactDesc.trim()) {
    ElMessage.warning('请填写不符合事实描述')
    return
  }
  const res = await issueNc({
    CheckItemCode: row.DetailCode,
    FactDesc: ncForm.FactDesc.trim(),
    RequirementRef: ncForm.RequirementRef || undefined,
    Severity: ncForm.Severity,
    TaskCode: planInfo.value?.RunningTaskCode ?? undefined,
  })
  flash(res, 'NC 已签发', '签发失败')
  if (res.success) {
    ncDialog.value = false
    await reloadOnly()
  }
}

// ══════════════════════════════════════════════════════════════════
// 行动作
// ══════════════════════════════════════════════════════════════════

const rowActions = (row: CertWorklistRow): YzhAction[] => {
  const acts: YzhAction[] = [{ key: 'detail', text: '详情', type: 'primary' }]

  acts.push({
    key: 'analyze',
    text: row.AnalyzeCount > 0 ? '重跑' : '分析',
    type: 'default',
    disabled: row.IsLocked || !row.TaskItemCode,
  })

  acts.push(
    row.IsLocked
      ? { key: 'unlock', text: '解锁', type: 'danger' }
      : { key: 'lock', text: '锁定', type: 'primary', disabled: !row.ResultCode },
  )

  if (!isPrep.value && row.Conclusion === 'nc') {
    acts.push({ key: 'nc', text: '签发 NC', type: 'danger' })
  }

  return acts
}

function onRowAction(key: string, row: CertWorklistRow) {
  switch (key) {
    case 'detail': void openDetail(row); break
    case 'analyze': void doAnalyze(row); break
    case 'lock': void doLock(row); break
    case 'unlock': void doUnlock(row); break
    case 'nc': openNc(row); break
  }
}

// ══════════════════════════════════════════════════════════════════
// 抽屉：单检查项详情
// ══════════════════════════════════════════════════════════════════

async function openDetail(row: CertWorklistRow) {
  currentRow.value = row
  drawer.value = true
  detailLoading.value = true
  detail.value = null
  try {
    const res = await getPlanCheckItemDetail(row.DetailCode)
    if (res.success && res.data) detail.value = res.data
    else ElMessage.error(res.err || res.message || '加载详情失败')
  } finally {
    detailLoading.value = false
  }
}

/** 抽屉里的疑点解析 */
const doubts = computed<Array<{ kind: string; text: string }>>(() => {
  const raw = detail.value?.DoubtsJson
  if (!raw) return []
  try {
    const arr = JSON.parse(raw)
    return Array.isArray(arr) ? arr : []
  } catch {
    return []
  }
})

const DOUBT_KIND_LABELS: Record<string, string> = {
  prep_nc_hint: '准备期疑点提示',
  conflict: '资料冲突',
  onsite_nc_suggested: '检查期 NC 建议',
}

function backToList() {
  router.push(`/audit-${props.stageKind}`)
}

function resetQuery() {
  query.keyword = ''
  query.status = ''
  void loadAll()
}

onMounted(loadAll)
</script>

<template>
  <div class="cd-page">
    <!-- 面包屑 -->
    <div class="cd-crumb">
      <a @click="backToList">{{ meta.title }}</a>
      <span class="cd-crumb__sep">/</span>
      <span class="cd-crumb__cur">{{ planTitle }}</span>
    </div>

    <!-- 页头 -->
    <div class="cd-head">
      <div class="cd-head__main">
        <div class="cd-title">
          {{ planTitle }}
          <YzhStatusBadge
            :type="isPrep ? 'info' : 'warning'"
            :text="meta.title"
            size="default"
          />
        </div>
        <div class="cd-meta">
          <span><b>阶段</b> {{ rows[0]?.StageName || '—' }}</span>
          <span><b>检查项</b> {{ summary.ItemTotal }}</span>
          <span><b>已锁定</b> {{ summary.Locked }}</span>
          <span v-if="isPrep"><b>资料缺失</b> {{ summary.MissingCount }}</span>
          <span v-else><b>NC 疑点</b> {{ summary.NcCount }}</span>
        </div>
      </div>
      <div class="cd-head__acts">
        <el-button type="primary" plain @click="loadAll">刷新</el-button>
        <el-button type="default" @click="backToList">返回列表</el-button>
      </div>
    </div>

    <!-- 页面锁提示 -->
    <el-alert
      v-if="pageLocked"
      type="warning"
      :closable="false"
      show-icon
      class="cd-alert"
      :title="`执行队列正在运行（${planInfo?.RunningTaskNo ?? ''}，${planInfo?.RunningItemDone ?? 0}/${planInfo?.RunningItemTotal ?? 0}）`"
      description="队列正在写库，此时人工操作会与队列互相覆盖。本页已临时只读，队列跑完后自动恢复。"
    />

    <!-- 步骤条 -->
    <div class="cd-steps">
      <template v-for="(s, i) in meta.steps" :key="i">
        <span class="cd-step">
          <span class="cd-step__n">{{ i + 1 }}</span>
          <span class="cd-step__t">{{ s }}</span>
        </span>
        <span v-if="i < meta.steps.length - 1" class="cd-step__line" />
      </template>
    </div>

    <!-- 进度 -->
    <div class="cd-prog">
      <el-progress
        :percentage="percent(summary.Analyzed, summary.ItemTotal)"
        :stroke-width="10"
        :color="progressColor(percent(summary.Analyzed, summary.ItemTotal))"
        class="cd-prog__bar"
      />
      <span class="cd-prog__num">已分析 {{ summary.Analyzed }}/{{ summary.ItemTotal }}</span>
    </div>

    <!-- 筛选 -->
    <div class="cd-filters">
      <el-input
        v-model="query.keyword"
        placeholder="条款号 / 部门 / 审核员"
        clearable
        class="cd-filters__kw"
        @keyup.enter="loadAll"
      />
      <el-select v-model="query.status" placeholder="状态" clearable class="cd-filters__sel">
        <el-option label="未分析" value="waiting" />
        <el-option label="已分析" value="analyzed" />
        <el-option label="已锁定" value="locked" />
        <el-option label="有疑点" value="doubt" />
        <el-option v-if="!isPrep" label="NC" value="nc" />
      </el-select>
      <el-button type="primary" @click="loadAll">检索</el-button>
      <el-button type="default" @click="resetQuery">重置</el-button>
    </div>

    <!-- 检查项表 -->
    <div class="cd-card">
      <YzhTable
        :columns="columns"
        :data="rows"
        :loading="loading"
        :show-pagination="false"
        :toolbar="false"
        :no-padding="true"
        row-key="DetailCode"
        size="small"
        :row-action-buttons="rowActions"
        :action-max-inline="4"
        empty-text="该计划下还没有检查项"
        @row-action="onRowAction"
      >
        <!-- 条款：编号 + 权威标题 -->
        <template #column-Clause="{ row }">
          <div class="cd-clause">
            <span
              v-for="(n, i) in String(row.ClauseNumbers || '').split(',').filter(Boolean)"
              :key="i"
              class="cd-clause__no"
            >{{ n }}</span>
          </div>
          <div class="cd-clause__title">{{ row.ClauseTitles || '—' }}</div>
        </template>

        <!-- 审核员：准备可改，检查只读 -->
        <template #column-Owner="{ row }">
          <span v-if="row.OwnerName">{{ row.OwnerName }}</span>
          <span v-else class="cd-muted">未分配</span>
          <!-- ★ 已进场的检查项不再给「设置」：分工在准备期定死，点了也只会被后端拒绝 -->
          <el-button
            v-if="isPrep && !row.OnsiteStarted"
            type="primary"
            link
            size="small"
            @click.stop="openOwner(row)"
          >设置</el-button>
          <span
            v-else-if="isPrep && row.OnsiteStarted"
            class="cd-muted cd-owner-locked"
            title="该检查项已进入认证检查阶段，审核员分工不可再变更"
          >已进场</span>
        </template>

        <!-- 状态：准备看资料，检查看结论 -->
        <template #column-Verdict="{ row }">
          <template v-if="isPrep">
            <YzhStatusBadge
              v-if="row.PrepState"
              :type="PREP_STATE_TAGS[row.PrepState] || 'info'"
              :text="labelOf(PREP_STATE_LABELS, row.PrepState)"
            />
            <span v-else class="cd-muted">未分析</span>
          </template>
          <template v-else>
            <YzhStatusBadge
              v-if="row.Conclusion"
              :type="CONCLUSION_TAGS[row.Conclusion] || 'info'"
              :text="labelOf(CONCLUSION_LABELS, row.Conclusion)"
            />
            <span v-else class="cd-muted">未分析</span>
            <YzhStatusBadge
              v-if="row.Severity && row.Conclusion === 'nc'"
              :type="SEVERITY_TAGS[row.Severity] || 'info'"
              :text="labelOf(SEVERITY_LABELS, row.Severity)"
              class="cd-tag"
            />
          </template>
        </template>

        <template #column-Doubt="{ row }">
          <YzhStatusBadge
            v-if="row.DoubtCount > 0"
            type="danger"
            :text="String(row.DoubtCount)"
          />
          <span v-else class="cd-muted">—</span>
        </template>

        <template #column-Evidence="{ row }">
          <span class="cd-evi">{{ row.RawEvidenceCount }}</span>
          <span class="cd-muted"> / </span>
          <span class="cd-evi">{{ row.CheckEvidenceCount }}</span>
        </template>

        <!-- ★ 锁在表格内（用户反馈第 3 条） -->
        <template #column-Lock="{ row }">
          <YzhStatusBadge v-if="row.IsLocked" type="success" text="已锁定" />
          <YzhStatusBadge
            v-else
            :type="ITEM_STATE_TAGS[row.State] || 'info'"
            :text="labelOf(ITEM_STATE_LABELS, row.State)"
          />
        </template>
      </YzhTable>

      <div class="cd-foot">
        共 {{ summary.ItemTotal }} 项 · 已锁定 {{ summary.Locked }} 项 ·
        锁定后重跑会跳过，保护已复核的结论
      </div>
    </div>

    <!-- 检查项详情抽屉 -->
    <YzhDrawer
      v-model="drawer"
      :title="`检查项详情 · 条款 ${currentRow?.ClauseNumbers ?? ''}`"
      size="640px"
      :show-footer="false"
    >
      <div v-loading="detailLoading" class="cd-drawer">
        <template v-if="detail">
          <!-- 结论面板 -->
          <div class="cd-dl">
            <div class="cd-dl__row">
              <span class="cd-dl__k">部门</span>
              <span class="cd-dl__v">{{ detail.DeptNames || '未指定' }}</span>
            </div>
            <div class="cd-dl__row">
              <span class="cd-dl__k">{{ isPrep ? '资料状态' : '判定结论' }}</span>
              <span class="cd-dl__v">
                <template v-if="isPrep">
                  <YzhStatusBadge
                    v-if="detail.PrepState"
                    :type="PREP_STATE_TAGS[detail.PrepState] || 'info'"
                    :text="labelOf(PREP_STATE_LABELS, detail.PrepState)"
                  />
                  <span v-else class="cd-muted">未分析</span>
                </template>
                <template v-else>
                  <YzhStatusBadge
                    v-if="detail.Conclusion"
                    :type="CONCLUSION_TAGS[detail.Conclusion] || 'info'"
                    :text="labelOf(CONCLUSION_LABELS, detail.Conclusion)"
                  />
                  <span v-else class="cd-muted">未分析</span>
                </template>
              </span>
            </div>
            <div v-if="!isPrep" class="cd-dl__row">
              <span class="cd-dl__k">严重程度</span>
              <span class="cd-dl__v">
                <YzhStatusBadge
                  v-if="detail.Severity"
                  :type="SEVERITY_TAGS[detail.Severity] || 'info'"
                  :text="labelOf(SEVERITY_LABELS, detail.Severity)"
                />
                <span v-else class="cd-muted">—</span>
              </span>
            </div>
            <div class="cd-dl__row">
              <span class="cd-dl__k">锁定状态</span>
              <span class="cd-dl__v">
                <YzhStatusBadge v-if="detail.IsLocked" type="success" text="已锁定" />
                <span v-else class="cd-muted">未锁定</span>
              </span>
            </div>
            <div class="cd-dl__row">
              <span class="cd-dl__k">锁定时间</span>
              <span class="cd-dl__v">{{ detail.LockedAt || '—' }}</span>
            </div>
          </div>

          <!-- 判定依据 -->
          <div class="cd-block">
            <div class="cd-block__title">
              {{ isPrep ? '准备期判定依据 / 疑点提示' : '判定依据' }}
            </div>
            <div v-if="detail.Reason" class="cd-reason">{{ detail.Reason }}</div>
            <div v-else class="cd-muted">暂无判定依据</div>
          </div>

          <!-- 疑点 -->
          <div v-if="doubts.length" class="cd-block">
            <div class="cd-block__title">疑点（{{ doubts.length }}）</div>
            <div v-for="(d, i) in doubts" :key="i" class="cd-doubt">
              <YzhStatusBadge
                type="danger"
                :text="DOUBT_KIND_LABELS[d.kind] ?? d.kind"
              />
              <span class="cd-doubt__text">{{ d.text }}</span>
            </div>
          </div>

          <!-- 证据 -->
          <el-tabs model-value="raw" class="cd-tabs">
            <el-tab-pane :label="`原始证据（${detail.RawEvidences.length}）`" name="raw">
              <div v-if="detail.RawEvidences.length === 0" class="cd-muted">
                暂无原始证据。原始证据来自企业文档按条款切片（doc_slice）与企业填报参数（pq_result）。
              </div>
              <div v-for="e in detail.RawEvidences" :key="e.Code" class="cd-evi-card">
                <div class="cd-evi-card__head">
                  <YzhStatusBadge type="info" :text="e.EvidenceKind" />
                  <span class="cd-evi-card__name">{{ e.SourceName || '未命名来源' }}</span>
                  <span v-if="e.Anchor" class="cd-muted">{{ e.Anchor }}</span>
                </div>
                <div class="cd-evi-card__body">{{ e.LlmSummary || e.Snippet || '（无内容）' }}</div>
              </div>
            </el-tab-pane>
            <el-tab-pane :label="`检查证据（${detail.CheckEvidences.length}）`" name="check">
              <div v-if="detail.CheckEvidences.length === 0" class="cd-muted">
                暂无检查证据。{{ isPrep ? '准备阶段的检查证据由审核员补充。' : '现场检查证据由审核员挂接。' }}
              </div>
              <div v-for="e in detail.CheckEvidences" :key="e.Code" class="cd-evi-card">
                <div class="cd-evi-card__head">
                  <YzhStatusBadge type="info" :text="e.EvidenceKind" />
                  <span class="cd-evi-card__name">{{ e.SourceName || '未命名来源' }}</span>
                </div>
                <div class="cd-evi-card__body">{{ e.LlmSummary || e.Snippet || '（无内容）' }}</div>
              </div>
            </el-tab-pane>
          </el-tabs>

          <!-- 抽屉操作 -->
          <div class="cd-drawer__acts">
            <el-button
              type="default"
              :disabled="currentRow?.IsLocked || !currentRow?.TaskItemCode"
              @click="currentRow && doAnalyze(currentRow)"
            >重新分析</el-button>
            <el-button
              v-if="currentRow && !currentRow.IsLocked"
              type="primary"
              :disabled="!detail.ResultCode"
              @click="currentRow && doLock(currentRow)"
            >锁定结论</el-button>
            <el-button
              v-else-if="currentRow"
              type="danger"
              @click="doUnlock(currentRow)"
            >解锁</el-button>
            <el-button
              v-if="!isPrep && currentRow?.Conclusion === 'nc'"
              type="danger"
              @click="currentRow && openNc(currentRow)"
            >签发 NC</el-button>
          </div>
        </template>
      </div>
    </YzhDrawer>

    <!-- 签发 NC 弹窗（仅认证检查） -->
    <YzhDialog
      v-model="ncDialog"
      title="签发不符合项（NC）"
      width="560px"
      confirm-text="签发"
      confirm-type="danger"
      @confirm="submitNc"
    >
      <el-alert
        type="info"
        :closable="false"
        show-icon
        class="cd-nc-alert"
        title="NC 逐条签发"
        description="一条 NC 对应一个检查项。系统只给建议，是否签发由审核员判断 —— 审核员确认后才算正式签发。"
      />
      <el-form :model="ncForm" label-width="100px" size="small">
        <el-form-item label="条款">
          <span>{{ ncTarget?.ClauseNumbers }}</span>
        </el-form-item>
        <el-form-item label="严重程度">
          <el-select v-model="ncForm.Severity" class="cd-nc-form__sel">
            <el-option label="严重不符合" value="critical" />
            <el-option label="重大不符合" value="major" />
            <el-option label="一般不符合" value="minor" />
          </el-select>
        </el-form-item>
        <el-form-item label="不符合事实" required>
          <el-input
            v-model="ncForm.FactDesc"
            type="textarea"
            :rows="5"
            placeholder="客观描述观察到的事实、时间、地点、涉及人员"
          />
        </el-form-item>
        <el-form-item label="要求引用">
          <el-input v-model="ncForm.RequirementRef" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
    </YzhDialog>

    <!-- 设置审核员弹窗（仅认证准备） -->
    <YzhDialog
      v-model="ownerDialog"
      title="设置负责审核员"
      width="460px"
      confirm-text="保存"
      @confirm="submitOwner"
    >
      <div class="cd-owner-tip">
        审核员分工是入场计划的核心信息：一个企业审核通常 2–3 人，
        不同人负责不同部门。审核员从<b>专家组成员池</b>中挑选，清空表示取消分配。
      </div>
      <el-select
        v-model="ownerForm.code"
        class="cd-owner-select"
        filterable
        clearable
        :loading="memberLoading"
        placeholder="从专家组成员中选择"
        no-data-text="专家组成员池为空，请先到「专家组设置」维护"
      >
        <el-option
          v-for="m in memberOptions"
          :key="m.Code"
          :label="m.Title ? `${m.Name}（${m.Title}）` : m.Name"
          :value="m.Code"
        >
          <span>{{ m.Name }}</span>
          <span class="cd-owner-opt-meta">{{ [m.Title, m.Profession].filter(Boolean).join(' · ') }}</span>
        </el-option>
      </el-select>
    </YzhDialog>
  </div>
</template>

<style scoped>
.cd-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
}

/* ── 面包屑 / 页头 ── */
.cd-crumb {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-crumb a { color: var(--el-color-primary); cursor: pointer; }
.cd-crumb__sep { color: var(--el-text-color-placeholder); }
.cd-crumb__cur { color: var(--yzh-color-text-secondary, var(--el-text-color-regular)); }

.cd-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--yzh-space-4, 16px);
  margin-bottom: var(--yzh-space-3, 12px);
}
.cd-head__main { min-width: 0; }
.cd-head__acts { display: flex; gap: var(--yzh-space-2, 8px); }
.cd-title {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, var(--el-text-color-primary));
}
.cd-meta {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-4, 16px);
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-meta b {
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-alert { margin-bottom: var(--yzh-space-3, 12px); }

/* ── 步骤条 ── */
.cd-steps {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  border-radius: var(--yzh-radius-base, 6px);
  background: var(--el-fill-color-lighter);
}
.cd-step {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-step__n {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--yzh-space-4, 16px);
  height: var(--yzh-space-4, 16px);
  border-radius: 50%;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-color-white);
  background: var(--el-color-primary);
}
.cd-step__line {
  display: inline-block;
  width: var(--yzh-space-6, 24px);
  height: 1px;
  margin: 0 var(--yzh-space-1, 4px);
  background: var(--el-border-color);
}

/* ── 进度 / 筛选 ── */
.cd-prog {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-3, 12px);
}
.cd-prog__bar { flex: 1; }
.cd-prog__num {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
  white-space: nowrap;
}
.cd-filters {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-3, 12px);
}
.cd-filters__kw { width: var(--yzh-width-kw, 240px); }
.cd-filters__sel { width: var(--yzh-width-sel, 150px); }

/* ── 表格 ── */
.cd-card {
  border: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  border-radius: var(--yzh-radius-base, 6px);
  background: var(--yzh-color-bg-container, var(--el-bg-color));
  overflow: hidden;
}
.cd-clause { display: flex; flex-wrap: wrap; gap: var(--yzh-space-1, 4px); }
.cd-clause__no {
  padding: 0 var(--yzh-space-1, 4px);
  border: 1px solid var(--el-border-color);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.5);
  color: var(--el-color-primary);
  font-variant-numeric: tabular-nums;
}
.cd-clause__title {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.5);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-tag { margin-left: var(--yzh-space-1, 4px); }
.cd-muted {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-placeholder);
}
.cd-evi { font-variant-numeric: tabular-nums; }
.cd-foot {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-top: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
  background: var(--el-fill-color-lighter);
}

/* ── 抽屉 ── */
.cd-drawer { min-height: var(--yzh-space-48, 200px); }
.cd-dl { display: flex; flex-direction: column; gap: var(--yzh-space-2, 8px); }
.cd-dl__row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  padding-bottom: var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  font-size: var(--yzh-font-size-sm, 13px);
}
.cd-dl__k {
  flex: 0 0 var(--yzh-space-24, 96px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-dl__v {
  flex: 1;
  color: var(--yzh-color-text-primary, var(--el-text-color-primary));
}
.cd-block { margin: var(--yzh-space-3, 12px) 0; }
.cd-block__title {
  margin-bottom: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-reason {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-left: 3px solid var(--el-color-primary-light-5);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.7);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
  background: var(--el-fill-color-lighter);
  white-space: pre-wrap;
}
.cd-doubt {
  display: flex;
  align-items: flex-start;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) 0;
  font-size: var(--yzh-font-size-sm, 13px);
}
.cd-doubt__text {
  flex: 1;
  line-height: var(--yzh-line-height-relaxed, 1.6);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-tabs { margin-top: var(--yzh-space-2, 8px); }
.cd-evi-card {
  margin-bottom: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
  border-radius: var(--yzh-radius-base, 6px);
}
.cd-evi-card__head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
}
.cd-evi-card__name {
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-evi-card__body {
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.7);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
  white-space: pre-wrap;
  word-break: break-word;
}
.cd-drawer__acts {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-3, 12px);
  padding-top: var(--yzh-space-3, 12px);
  border-top: 1px solid var(--yzh-color-border-light, var(--el-border-color-lighter));
}
.cd-nc-alert { margin-bottom: var(--yzh-space-3, 12px); }
.cd-nc-form__sel { width: var(--yzh-width-sel, 180px); }
.cd-owner-tip {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.6);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-owner-select { width: 100%; }
.cd-owner-locked { margin-left: var(--yzh-space-1, 4px); }
.cd-owner-opt-meta {
  float: right;
  margin-left: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}
</style>
