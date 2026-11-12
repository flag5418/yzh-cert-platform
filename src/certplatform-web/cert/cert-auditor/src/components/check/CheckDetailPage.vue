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
import { useRoute, useRouter } from 'vue-router'
import {
  ElAlert, ElButton, ElForm, ElFormItem, ElInput, ElMessage, ElMessageBox, ElOption, ElPopover,
  ElProgress, ElSelect, ElTable, ElTableColumn,
} from 'element-plus'
import {
  YzhDialog, YzhDrawer, YzhStatusBadge, YzhTable, confirmOrFalse,
} from '@yzh-core'
import { useTabs } from '@yzh-core/composables/useTabs'
import type { YzhAction, YzhTableColumn } from '@yzh-core/components/table/types'
import {
  addEvidence, analyzeCheckItem, createCertTask, deleteCheckContent, deleteEvidence,
  exportPlanReport, generateCheckContents, getClauseContent, getMemberOptions, getPlanCheckItemDetail,
  getPlanChecklist, getWorklist, issueNc, listCheckContents, lockConclusion, saveCheckContent,
  saveCheckResult, setCheckItemOwner, unlockConclusion, updateEvidence,
  type CertCheckContentRow, type CertClauseContentRow, type CertEvidenceRow, type CertMemberOption,
  type CertPlanCheckItemDetail, type CertPlanChecklistResponse, type CertWorklistRow,
  type CertWorklistSummary,
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
const route = useRoute()
/** 多标签页：详情路由是动态段（/audit-onsite/:code），菜单匹配不到标题，需页面自己回填 */
const { tabs: openTabs, open: openTab, retitle } = useTabs()
const meta = computed(() => STAGE_META[props.stageKind])
const isPrep = computed(() => props.stageKind === 'prep')

/**
 * 判定依据渲染：把 LLM 输出的 Markdown-lite 文本转成可读 HTML。
 * 支持段落 / 有序·无序列表 / **加粗** / 单换行 —— 认证报告是给人读的，
 * 一整段挤在一起无法审阅。渲染前先转义 HTML，避免证据内容注入标签。
 */
function mdToHtml(src?: string | null): string {
  if (!src) return ''
  const esc = (s: string) => s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
  const inline = (s: string) => esc(s)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
  const blocks = src.replace(/\r\n/g, '\n').split(/\n{2,}/)

  return blocks.map((block) => {
    const lines = block.split('\n').filter(l => l.trim() !== '')
    if (lines.length === 0) return ''
    if (lines.every(l => /^\s*[-*]\s+/.test(l))) {
      return '<ul>' + lines
        .map(l => `<li>${inline(l.replace(/^\s*[-*]\s+/, ''))}</li>`)
        .join('') + '</ul>'
    }
    if (lines.every(l => /^\s*\d+[.、)]\s+/.test(l))) {
      return '<ol>' + lines
        .map(l => `<li>${inline(l.replace(/^\s*\d+[.、)]\s+/, ''))}</li>`)
        .join('') + '</ol>'
    }
    return '<p>' + lines.map(inline).join('<br>') + '</p>'
  }).join('')
}

// ── 状态 ──
const loading = ref(false)
const rows = ref<CertWorklistRow[]>([])
const summary = ref<CertWorklistSummary>({
  ItemTotal: 0, Waiting: 0, Analyzed: 0, Locked: 0,
  NcCount: 0, MissingCount: 0, DoubtCount: 0, RunningPlans: 0,
})
const planInfo = ref<CertPlanChecklistResponse | null>(null)
const query = reactive({ keyword: '', status: '' })

/** 表格勾选（自动分析作用对象） */
const selectedRows = ref<CertWorklistRow[]>([])
function onSelectionChange(list: CertWorklistRow[]) {
  selectedRows.value = list
}

/** 自动分析进度（顺序执行，避免并发打满 LLM 配额） */
const autoRunning = ref(false)
const autoDone = ref(0)
const autoTotal = ref(0)

/** 页面锁：队列在跑 ⇒ 整页只读（唯一真相来自后端） */
const pageLocked = computed(() => planInfo.value?.TaskRunning === true)
/** 计划已关闭：对应的认证准备 / 认证检查已作废，整页只读（只能查看） */
const planClosed = computed(() => planInfo.value?.PlanClosed === true)
/** 编辑总开关：队列运行中 或 计划已关闭 */
const editLocked = computed(() => pageLocked.value || planClosed.value)

const planTitle = computed(() => {
  const ent = rows.value[0]?.EnterpriseName
  const name = planInfo.value?.PlanName ?? rows.value[0]?.PlanName
  if (!name) return ent || '检查项清单'
  // ★ 计划名本身已按「企业·阶段·认证计划」拼装过，再前置企业名会重复
  if (ent && name.startsWith(ent)) return name
  return [ent, name].filter(Boolean).join(' · ')
})

/** 把标签页标题回填成「认证检查 · 企业·阶段」而不是原始路由 path */
function syncTabTitle() {
  const path = route.path
  const title = `${meta.value.title} · ${planTitle.value}`
  // 标签通常已由布局在路由变化时打开；只有在缺失时才补开（避免异步回填把 activePath 抢回来）
  if (!openTabs.value.some(t => t.path === path)) openTab(path, title)
  retitle(path, title)
}

// ── 抽屉 / 弹窗 ──
const drawer = ref(false)
const detail = ref<CertPlanCheckItemDetail | null>(null)
const detailLoading = ref(false)
const currentRow = ref<CertWorklistRow | null>(null)

const ncDialog = ref(false)
const ncForm = reactive({ FactDesc: '', RequirementRef: '', Severity: 'major' })
const ncTarget = ref<CertWorklistRow | null>(null)

/**
 * 审核员判定（结论 + 不符合程度 + 判定依据）。
 * ★ 2026-11-03 用户口径：系统只给「建议」，最终是否符合 / 不符合到什么程度
 *   必须由审核员在这里判定；未判定时列表与详情都显示「待判定」。
 */
const editDialog = ref(false)
const editLoading = ref(false)
const editForm = reactive({ Conclusion: '', Severity: '', Reason: '' })
/** 结论下拉选项：落库四态，与后端 CertVerdictMapper 同一值域 */
const CONCLUSION_OPTIONS = [
  { value: 'pass', label: '符合' },
  { value: 'ofi', label: '观察项' },
  { value: 'nc', label: '不符合' },
  { value: 'na', label: '不适用' },
]
/** 不符合程度（仅 Conclusion = nc 时必选） */
const SEVERITY_OPTIONS = [
  { value: 'minor', label: '一般不符合' },
  { value: 'major', label: '严重不符合' },
]

const ownerDialog = ref(false)
const ownerForm = reactive({ code: '', name: '' })
const ownerTarget = ref<CertWorklistRow | null>(null)
/** 专家组成员池（审核员只能从这里选，不允许手打姓名） */
const memberOptions = ref<CertMemberOption[]>([])

/** 现场补证 / 审核员补充证据（新增与编辑共用同一个弹窗） */
const evDialog = ref(false)
const evLoading = ref(false)
/** 空 = 新增；非空 = 正在编辑的证据 Code */
const editingEvidenceCode = ref('')
const evForm = reactive({
  EvidenceKind: 'interview',
  SourceName: '',
  OccurredAt: '',
  Place: '',
  Interviewee: '',
  Content: '',
})
/** 证据类型 —— 取值必须落在后端 cert_evidence.EvidenceKind 值域内 */
const EVIDENCE_KIND_OPTIONS = [
  { value: 'interview', label: '访谈记录' },
  { value: 'onsite_media', label: '现场照片 / 音视频' },
  { value: 'sample', label: '抽样记录' },
  { value: 'manual', label: '文字说明' },
]
const EVIDENCE_KIND_LABELS: Record<string, string> = {
  doc_slice: '文档切片',
  pq_result: '填报参数',
  lead: '线索',
  onsite_media: '现场媒体',
  interview: '访谈记录',
  sample: '抽样记录',
  manual: '文字说明',
  prep: '准备期证据',
}
const memberLoading = ref(false)

/**
 * 认证准备「检查内容」（用户 2026-11-03）：
 * 指定该检查项应该检查什么、用什么方式检查，可多条；可由系统自动生成。
 * ★ 数据表 cert_onsite_plan_item，认证检查阶段据此生成入场计划（二期）。
 */
const checkContents = ref<CertCheckContentRow[]>([])
const checkContentLoading = ref(false)
const generatingContents = ref(false)
const checkContentDialog = ref(false)
const checkContentSaving = ref(false)
/** null = 新增；非空 = 正在编辑的行 */
const checkContentTarget = ref<CertCheckContentRow | null>(null)
const checkContentForm = reactive({
  CheckWhat: '',
  CheckMethod: '',
  TargetDept: '',
  SampleRequirement: '',
  ExpectedFinding: '',
})
/** 检查方式选项（与后端约定值域保持一致，可多选组合由系统生成时给出） */
const CHECK_METHOD_OPTIONS = [
  { value: '文件查阅', label: '文件查阅' },
  { value: '现场观察', label: '现场观察' },
  { value: '人员访谈', label: '人员访谈' },
  { value: '抽样验证', label: '抽样验证' },
  { value: '记录核对', label: '记录核对' },
]

// ══════════════════════════════════════════════════════════════════
// 列定义
// ══════════════════════════════════════════════════════════════════

/**
 * ★ 插槽列必须同时声明 `slotKey` + `slot: true`（YzhTable 的渲染开关是
 *   `v-if="col.slot"`，只给 slotKey 会静默渲染成空白列）。
 */
const columns = computed<YzhTableColumn<CertWorklistRow>[]>(() => [
  // ★ 列宽合计 + 操作列(224) 必须 ≤ 表格可视宽，否则最右的「锁定」列会被固定列压住半截。
  //   当前合计 708 + 224 = 932（1080 宽窗口下可视 944）。
  { slotKey: 'Clause', slot: true, label: '条款', minWidth: 160 },
  // ★ 部门为空是真实数据缺失（计划明细未指定部门），给占位文字而不是留白，
  //   否则看起来像渲染缺陷。
  { prop: 'DeptNames', label: '部门', width: 76, formatter: (v: any) => v || '未指定' },
  { slotKey: 'Owner', slot: true, label: '审核员', width: 92 },
  { slotKey: 'Verdict', slot: true, label: meta.value.stateLabel, width: 104, align: 'center' },
  { slotKey: 'Doubt', slot: true, label: '疑点', width: 48, align: 'center' },
  { slotKey: 'Evidence', slot: true, label: '证据', width: 64, align: 'center' },
  // ★ 2026-11-03：显式「自动分析」状态列 —— 未分析 / 已完成 / 失败 / 建议重跑。
  //   审核员进入页面即可知道系统是否已经分析过，而不是一进来就面对空白表单。
  { slotKey: 'Analysis', slot: true, label: '自动分析', width: 88, align: 'center' },
  { slotKey: 'Lock', slot: true, label: '锁定', width: 76, align: 'center' },
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
    syncTabTitle()
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

/**
 * 顶部「自动分析」——勾选检查项即可做初步分析。
 *
 * ★ 刚建计划、未补任何证据时该分支还没有任务实例，行上 TaskItemCode 为空；
 *   此时先按计划建一次任务（只生成任务项，不启动队列），拿到分析入口后逐项分析。
 * ★ 顺序执行：LLM 分析是重调用，并发会把配额与队列打满。
 */
async function doAutoAnalyze() {
  if (!assertEditable()) return
  if (autoRunning.value) return

  let picked = selectedRows.value.filter(r => !r.IsLocked)
  if (!picked.length) {
    if (selectedRows.value.length) {
      ElMessage.warning('勾选的检查项均已锁定，锁定项不会被重跑覆盖')
      return
    }
    // ★ 2026-11-03：未勾选时默认分析全部未锁定检查项。
    //   认证准备刚进入时还没有任何证据，审核员常直接点「自动分析」；
    //   这里不能拦成「请先勾选」，否则会被误认为「没有资料就不能分析」。
    const candidates = rows.value.filter(r => !r.IsLocked)
    if (!candidates.length) {
      ElMessage.warning('当前没有可分析的检查项')
      return
    }
    const ok = await confirmOrFalse(
      `未勾选检查项，将依次分析全部 ${candidates.length} 项（含尚未上传资料的检查项）。是否继续？`,
      '自动分析全部',
    )
    if (!ok) return
    picked = candidates
  }

  autoRunning.value = true
  autoDone.value = 0
  autoTotal.value = picked.length
  try {
    if (picked.some(r => !r.TaskItemCode)) {
      const created = await createCertTask({
        PlanCode: props.planCode,
        TaskKind: props.stageKind,
        RunMode: 'incremental',
      })
      if (!created.success) {
        ElMessage.error(created.err || created.message || '创建分析任务失败')
        return
      }
      await reloadOnly()
      const fresh = new Map(rows.value.map(r => [r.DetailCode, r]))
      picked.forEach(r => {
        const hit = fresh.get(r.DetailCode)
        if (hit) r.TaskItemCode = hit.TaskItemCode
      })
    }

    let ok = 0
    const failures: Array<{ label: string; reason: string }> = []
    for (const r of picked) {
      const label = r.ClauseTitles || r.ClauseNumbers || r.DetailCode
      if (!r.TaskItemCode) {
        // ★ 不再吞掉原因：没有任务项 Code 时，把「任务创建/刷新后仍拿不到」写清楚
        failures.push({ label, reason: '未取得任务项 Code（任务创建后刷新列表仍未返回，请重试或检查后端日志）' })
        autoDone.value++
        continue
      }
      try {
        const res = await analyzeCheckItem(r.TaskItemCode)
        if (res.success) {
          ok++
        } else {
          failures.push({ label, reason: res.err || res.message || '接口返回失败但未给出原因' })
        }
      } catch (e) {
        // 网络/异常也按项记录，避免整页只看到「失败 N 项」
        failures.push({ label, reason: e instanceof Error ? e.message : String(e) })
      }
      autoDone.value++
    }

    await reloadOnly()
    if (drawer.value && currentRow.value) await openDetail(currentRow.value)

    if (failures.length === 0) {
      ElMessage.success(`自动分析完成：成功 ${ok} 项`)
    } else {
      const esc = (s: string) => s
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      const list = failures
        .map((f, i) => `<li style="margin-bottom:6px"><b>${i + 1}. ${esc(f.label)}</b><br/>${esc(f.reason)}</li>`)
        .join('')
      ElMessageBox.alert(
        `<div style="max-height:320px;overflow:auto;line-height:1.6">`
          + `<p>成功 ${ok} 项，失败 ${failures.length} 项。逐项失败原因：</p>`
          + `<ol style="padding-left:18px;margin:0">${list}</ol></div>`,
        '自动分析结果',
        {
          dangerouslyUseHTMLString: true,
          type: ok > 0 ? 'warning' : 'error',
          confirmButtonText: '知道了',
        },
      )
    }
  } finally {
    autoRunning.value = false
  }
}

// ══════════════════════════════════════════════════════════════════
// 操作
// ══════════════════════════════════════════════════════════════════

function assertEditable(): boolean {
  if (planClosed.value) {
    ElMessage.warning('认证计划已关闭：对应的认证准备 / 认证检查已作废，只能查看')
    return false
  }
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
  if (row.IsLocked) {
    ElMessage.warning('该检查项已锁定，锁定项不会被重跑覆盖；如需重跑请先解锁')
    return
  }

  const firstRun = !row.TaskItemCode
  const ok = await confirmOrFalse(
    firstRun
      ? `将分析条款 ${row.ClauseNumbers ?? ''}（即使该检查项尚未上传任何证据也可分析）。`
      : `将重新分析条款 ${row.ClauseNumbers ?? ''} 并覆盖当前未锁定的结论。`,
    firstRun ? '开始分析' : '重新分析',
  )
  if (!ok) return

  // ★ 2026-11-03：没有任务实例时先建任务（只生成任务项，不启动队列），
  //   否则「认证准备刚进入、未上传资料」的检查项永远无法分析。
  if (firstRun) {
    const created = await createCertTask({
      PlanCode: props.planCode,
      TaskKind: props.stageKind,
      RunMode: 'incremental',
    })
    if (!created.success) {
      ElMessage.error(created.err || created.message || '创建分析任务失败')
      return
    }
    await reloadOnly()
    const fresh = rows.value.find(r => r.DetailCode === row.DetailCode)
    if (!fresh?.TaskItemCode) {
      ElMessage.error('创建任务后仍未找到该检查项，请稍后重试')
      return
    }
    row.TaskItemCode = fresh.TaskItemCode
  }

  const res = await analyzeCheckItem(row.TaskItemCode!)
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

/** 打开「现场补证 / 审核员补充证据」（新增） */
function openEvidence() {
  const row = currentRow.value
  if (!row) return
  if (!assertEditable()) return
  editingEvidenceCode.value = ''
  evForm.EvidenceKind = 'interview'
  evForm.SourceName = ''
  evForm.OccurredAt = ''
  evForm.Place = row.DeptNames || ''
  evForm.Interviewee = ''
  evForm.Content = ''
  evDialog.value = true
}

/** 打开证据编辑（复用同一个弹窗） */
function openEvidenceEdit(e: CertEvidenceRow) {
  if (!assertEditable()) return
  if (detail.value?.IsLocked) {
    ElMessage.warning('该检查项已锁定，请先解锁再修改证据')
    return
  }
  editingEvidenceCode.value = e.Code
  evForm.EvidenceKind = e.EvidenceKind || 'manual'
  evForm.SourceName = e.SourceName || ''
  evForm.Content = e.Snippet || e.LlmSummary || ''
  // Anchor 由「发生时间 · 地点 · 受访人」拼成，按分隔符还原来回填
  const parts = (e.Anchor || '').split(' · ')
  evForm.OccurredAt = parts[0] || ''
  evForm.Place = parts[1] || ''
  evForm.Interviewee = parts[2] || ''
  evDialog.value = true
}

/**
 * 保存证据（新增 / 编辑共用）。
 * ★ 审核员只录客观事实（看到 / 听到 / 查到什么），不做「能证明什么」的判断 ——
 *   证明作用由系统在分析时结合条款、企业资料与证据内容自行推论，否则等于让审核员
 *   替 LLM 把结论写好，智能分析就失去意义。
 * ★ CapturedIn：准备期补充证据记 manual（归入检查证据），检查期记 onsite（现场证据，分析主依据）。
 */
async function submitEvidence() {
  const row = currentRow.value
  if (!row) return
  if (!evForm.SourceName.trim()) {
    ElMessage.warning('请填写来源 / 标题')
    return
  }
  if (!evForm.Content.trim()) {
    ElMessage.warning('请填写证据内容 —— 客观记录看到 / 听到 / 查到的内容')
    return
  }
  const anchorParts = [evForm.OccurredAt.trim(), evForm.Place.trim(), evForm.Interviewee.trim()].filter(Boolean)

  evLoading.value = true
  try {
    if (editingEvidenceCode.value) {
      // 编辑：只更新内容字段，不改归属检查项 / 采集方向
      const res = await updateEvidence({
        Code: editingEvidenceCode.value,
        SourceName: evForm.SourceName.trim(),
        EvidenceKind: evForm.EvidenceKind,
        Snippet: evForm.Content.trim(),
        Anchor: anchorParts.join(' · '),
      })
      flash(res, '证据已更新', '保存失败')
      if (res.success) {
        evDialog.value = false
        await openDetail(row)
      }
    } else {
      const res = await addEvidence({
        SourceName: evForm.SourceName.trim(),
        CheckItemCode: row.DetailCode,
        EvidenceKind: evForm.EvidenceKind,
        CapturedIn: isPrep.value ? 'manual' : 'onsite',
        Snippet: evForm.Content.trim(),
        Anchor: anchorParts.join(' · '),
      })
      flash(res, isPrep.value ? '已补充证据' : '已添加现场证据', '保存失败')
      if (res.success) {
        evDialog.value = false
        await reloadOnly()
        await openDetail(row)
      }
    }
  } finally {
    evLoading.value = false
  }
}

/** 删除证据（软删，不再参与分析） */
async function removeEvidence(e: CertEvidenceRow) {
  if (!assertEditable()) return
  if (detail.value?.IsLocked) {
    ElMessage.warning('该检查项已锁定，请先解锁再删除证据')
    return
  }
  const ok = await confirmOrFalse(
    `删除证据「${e.SourceName || '未命名来源'}」？删除后该证据不再参与分析。`,
    '删除证据',
  )
  if (!ok) return

  const res = await deleteEvidence(e.Code)
  flash(res, '证据已删除', '删除失败')
  if (res.success) {
    await reloadOnly()
    const row = currentRow.value
    if (row) await openDetail(row)
  }
}

/** 抽屉内的证据表：原始证据（来自企业资料）+ 检查证据（审核员补录） */
const allEvidences = computed<Array<CertEvidenceRow & { __raw: boolean }>>(() => {
  const d = detail.value
  if (!d) return []
  return [
    ...(d.RawEvidences ?? []).map(e => ({ ...e, __raw: true })),
    ...(d.CheckEvidences ?? []).map(e => ({ ...e, __raw: false })),
  ]
})

// ─── 条款详情弹窗（点条款编号） ───
const clausePopup = reactive<{ loading: boolean; items: CertClauseContentRow[]; error: string }>({
  loading: false,
  items: [],
  error: '',
})
/** 同一检查项只取一次条款内容（后端按明细的 Clause Code 精确取，避免跨标准同号歧义） */
const clauseCache = new Map<string, CertClauseContentRow[]>()

/** 条款属性编码 → 中文标签（与后端 CertCheckContextBuilder.AttrOrder 一致） */
const CLAUSE_ATTR_LABELS: Record<string, string> = {
  ClauseIntent: '管什么',
  Obligation: '约束强度',
  Applicability: '适用性',
  ApplicabilityCondition: '适用条件',
  Severity: '严重度',
  DocCheckMode: '资料核对方式',
  Process: '涉及过程',
  Artifacts: '企业应提供的资料',
  EvidenceChain: '取证要求',
}

function clauseAttrLabel(code: string): string {
  return CLAUSE_ATTR_LABELS[code] || code
}

async function showClause(detailCode: string, clauseNumber: string) {
  clausePopup.items = []
  clausePopup.error = ''
  if (!detailCode) {
    clausePopup.error = '缺少检查项标识'
    return
  }
  clausePopup.loading = true
  try {
    let all = clauseCache.get(detailCode)
    if (!all) {
      const res = await getClauseContent(detailCode)
      if (!res.success) {
        clausePopup.error = res.err || res.message || '查询条款失败'
        return
      }
      all = res.data || []
      clauseCache.set(detailCode, all)
    }
    // 同号可能来自多个标准（9001/14001 都有 4.2），命中则全部展示
    const hit = all.filter(c => (c.ClauseNumber || '') === clauseNumber)
    clausePopup.items = hit.length ? hit : all
    if (!clausePopup.items.length) clausePopup.error = '该检查项未关联条款原文'
  } finally {
    clausePopup.loading = false
  }
}

/** 打开「审核员判定」（结论 + 不符合程度 + 判定依据） */
function openEdit() {
  const d = detail.value
  if (!d) return
  if (!assertEditable()) return
  if (d.IsLocked) {
    ElMessage.warning('该检查项已锁定，请先解锁再修改')
    return
  }
  if (!d.ResultCode) {
    ElMessage.warning('该检查项尚未分析，暂无可判定的检查证据')
    return
  }
  // 已判定 ⇒ 回填审核员的最终值；未判定 ⇒ 回填系统建议（仅作起点，仍需审核员确认）
  editForm.Conclusion = (d.ConclusionConfirmed ? d.Conclusion : d.AiConclusion) ?? ''
  editForm.Severity = (d.ConclusionConfirmed ? d.Severity : d.AiSeverity) === 'minor' ? 'minor' : 'major'
  editForm.Reason = d.Reason ?? ''
  editDialog.value = true
}

/** 保存审核员判定 */
async function submitEdit() {
  const d = detail.value
  if (!d?.ResultCode) return
  if (!isPrep.value && !editForm.Conclusion) {
    ElMessage.warning('请选择认证结论（是否符合）')
    return
  }
  if (!isPrep.value && editForm.Conclusion === 'nc' && !editForm.Severity) {
    ElMessage.warning('不符合必须选择程度（一般 / 严重）')
    return
  }
  if (!editForm.Reason.trim()) {
    ElMessage.warning('判定依据不能为空')
    return
  }
  editLoading.value = true
  try {
    const res = await saveCheckResult(
      d.ResultCode,
      isPrep.value ? null : editForm.Conclusion,
      isPrep.value || editForm.Conclusion !== 'nc' ? null : editForm.Severity,
      editForm.Reason.trim(),
    )
    flash(res, '审核员判定已保存', '保存失败')
    if (res.success) {
      editDialog.value = false
      await reloadOnly()
      const row = currentRow.value
      if (row) await openDetail(row)
    }
  } finally {
    editLoading.value = false
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
    // ★ 2026-11-03：不再要求 TaskItemCode 已存在 —— 没有任务项时 doAnalyze 会先建任务，
    //   否则「认证准备刚进入、未上传资料」的检查项会被永久禁用。
    disabled: row.IsLocked,
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
  checkContents.value = []
  try {
    const [res] = await Promise.all([
      getPlanCheckItemDetail(row.DetailCode),
      // ★ 准备期可编辑、检查期只读 —— 两边都要加载，认证检查也需看到「该查什么」
      loadCheckContents(row.DetailCode),
    ])
    if (res.success && res.data) detail.value = res.data
    else ElMessage.error(res.err || res.message || '加载详情失败')
  } finally {
    detailLoading.value = false
  }
}

// ── 检查内容（认证准备） ──

async function loadCheckContents(checkItemCode: string) {
  checkContentLoading.value = true
  try {
    const res = await listCheckContents(checkItemCode)
    if (res.success) checkContents.value = res.data ?? []
    else ElMessage.error(res.err || res.message || '加载检查内容失败')
  } finally {
    checkContentLoading.value = false
  }
}

function canEditCheckContent(): boolean {
  if (!assertEditable()) return false
  if (detail.value?.IsLocked) {
    ElMessage.warning('该检查项已锁定，请先解锁再修改检查内容')
    return false
  }
  return true
}

function openCheckContentCreate() {
  if (!canEditCheckContent()) return
  checkContentTarget.value = null
  checkContentForm.CheckWhat = ''
  checkContentForm.CheckMethod = '文件查阅'
  checkContentForm.TargetDept = currentRow.value?.DeptNames ?? ''
  checkContentForm.SampleRequirement = ''
  checkContentForm.ExpectedFinding = ''
  checkContentDialog.value = true
}

function openCheckContentEdit(row: CertCheckContentRow) {
  if (!canEditCheckContent()) return
  checkContentTarget.value = row
  checkContentForm.CheckWhat = row.CheckWhat
  checkContentForm.CheckMethod = row.CheckMethod ?? ''
  checkContentForm.TargetDept = row.TargetDept ?? ''
  checkContentForm.SampleRequirement = row.SampleRequirement ?? ''
  checkContentForm.ExpectedFinding = row.ExpectedFinding ?? ''
  checkContentDialog.value = true
}

async function submitCheckContent() {
  const checkItemCode = currentRow.value?.DetailCode
  if (!checkItemCode) return
  if (!checkContentForm.CheckWhat.trim()) {
    ElMessage.warning('请填写检查内容')
    return
  }
  checkContentSaving.value = true
  try {
    const res = await saveCheckContent({
      Code: checkContentTarget.value?.Code,
      CheckItemCode: checkItemCode,
      CheckWhat: checkContentForm.CheckWhat.trim(),
      CheckMethod: checkContentForm.CheckMethod || undefined,
      TargetDept: checkContentForm.TargetDept || undefined,
      SampleRequirement: checkContentForm.SampleRequirement || undefined,
      ExpectedFinding: checkContentForm.ExpectedFinding || undefined,
    })
    flash(res, '已保存', '保存失败')
    if (res.success) {
      checkContentDialog.value = false
      await loadCheckContents(checkItemCode)
    }
  } finally {
    checkContentSaving.value = false
  }
}

async function removeCheckContent(row: CertCheckContentRow) {
  if (!canEditCheckContent()) return
  const ok = await confirmOrFalse(`确认删除检查内容「${row.CheckWhat}」？`, '删除')
  if (!ok) return
  const res = await deleteCheckContent(row.Code)
  flash(res, '已删除', '删除失败')
  if (res.success) {
    const checkItemCode = currentRow.value?.DetailCode
    if (checkItemCode) await loadCheckContents(checkItemCode)
  }
}

async function doGenerateCheckContents() {
  if (!canEditCheckContent()) return
  const checkItemCode = currentRow.value?.DetailCode
  if (!checkItemCode) return
  const ok = await confirmOrFalse(
    '系统将根据条款信息自动生成「查什么 / 怎么查」清单，并替换上一次系统生成的内容（人工维护的内容保留）。是否继续？',
    '系统生成',
  )
  if (!ok) return
  generatingContents.value = true
  try {
    const res = await generateCheckContents(checkItemCode)
    flash(res, '已生成', '生成失败')
    if (res.success) checkContents.value = res.data ?? []
  } finally {
    generatingContents.value = false
  }
}

/** 导出认证报告（二期：当前只返回导出排队元数据） */
const exportingReport = ref(false)
async function doExportReport() {
  if (exportingReport.value) return
  exportingReport.value = true
  try {
    const res = await exportPlanReport(props.planCode)
    flash(res, '导出任务已创建', '导出失败')
  } finally {
    exportingReport.value = false
  }
}

/** 生成入场计划（二期占位） */
function doGenerateEntryPlan() {
  ElMessage.info(
    '入场计划生成将在二期上线：届时会把认证准备维护的「检查内容」按审核员归并为入场检查清单。当前可先在认证准备页维护检查内容。',
  )
}

/** 自动分析状态文案（列表 + 详情共用） */
function analysisStateOf(row: { AnalysisState?: string; RerunState?: string; AnalyzeCount?: number; State?: string }) {
  const state = row.AnalysisState || (row.AnalyzeCount ? 'done' : 'none')
  if (state === 'failed' || row.State === 'failed') return { text: '分析失败', type: 'danger' as const }
  if (row.RerunState === 'suggested') return { text: '建议重跑', type: 'warning' as const }
  if (state === 'done' || row.State === 'analyzed') return { text: '已完成', type: 'success' as const }
  return { text: '未分析', type: 'info' as const }
}

/** 抽屉里的疑点解析 */
const doubts = computed<Array<{ kind: string; text: string; point?: string; detail?: string }>>(() => {
  const raw = detail.value?.DoubtsJson
  if (!raw) return []
  try {
    const arr = JSON.parse(raw)
    if (!Array.isArray(arr)) return []
    // 后端给的是 {kind, point, detail, text}；旧数据可能只有 kind+text
    return arr.filter((d: unknown) => d && typeof d === 'object') as Array<{
      kind: string; text: string; point?: string; detail?: string
    }>
  } catch {
    return []
  }
})

const DOUBT_KIND_LABELS: Record<string, string> = {
  prep_nc_hint: '准备期疑点提示',
  conflict: '资料冲突',
  onsite_doubt: '系统疑点',
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
        <!-- ★ 自动分析：勾选检查项即可做初步分析（刚建计划、未补证据也能跑条款约定 / NC 疑点） -->
        <el-button
          type="primary"
          :loading="autoRunning"
          :disabled="editLocked"
          @click="doAutoAnalyze"
        >{{ autoRunning ? `分析中 ${autoDone}/${autoTotal}` : `自动分析（已选 ${selectedRows.length}）` }}</el-button>
        <!-- ★ 2026-11-03：认证检查产出物，统一放在「自动分析」之后（二期完善内容，先给入口） -->
        <el-button
          v-if="!isPrep"
          type="default"
          :disabled="editLocked"
          @click="doGenerateEntryPlan"
        >生成入场计划</el-button>
        <el-button
          v-if="!isPrep"
          type="default"
          :loading="exportingReport"
          :disabled="planClosed"
          @click="doExportReport"
        >导出认证报告</el-button>
        <el-button type="default" @click="loadAll">刷新</el-button>
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

    <!-- 计划已关闭提示 -->
    <el-alert
      v-if="planClosed"
      type="info"
      :closable="false"
      show-icon
      class="cd-alert"
      title="认证计划已关闭"
      description="该计划对应的认证准备与认证检查已作废，本页只能查看，所有编辑入口已停用。"
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
        <el-option label="分析失败" value="failed" />
        <el-option label="建议重跑" value="rerun" />
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
        selectable
        :row-action-buttons="rowActions"
        :action-max-inline="4"
        empty-text="该计划下还没有检查项"
        @row-action="onRowAction"
        @selection-change="onSelectionChange"
      >
        <!-- 条款：编号 + 权威标题；点编号弹出条款详情 -->
        <template #column-Clause="{ row }">
          <div class="cd-clause">
            <el-popover
              v-for="(n, i) in String(row.ClauseNumbers || '').split(',').filter(Boolean)"
              :key="i"
              trigger="click"
              placement="bottom-start"
              :width="420"
              :teleported="true"
              @show="showClause(row.DetailCode, n)"
            >
              <template #reference>
                <span class="cd-clause__no">{{ n }}</span>
              </template>
              <div class="cd-clause-pop">
                <div v-if="clausePopup.loading" class="cd-muted">加载中…</div>
                <div v-else-if="clausePopup.error" class="cd-muted">{{ clausePopup.error }}</div>
                <template v-else>
                  <div v-for="c in clausePopup.items" :key="c.Code" class="cd-clause-pop__item">
                    <div class="cd-clause-pop__head">
                      <span class="cd-clause-pop__no">{{ c.ClauseNumber }}</span>
                      <span class="cd-clause-pop__title">{{ c.Title }}</span>
                      <span v-if="c.Abbrev" class="cd-clause-pop__std">{{ c.Abbrev }}</span>
                    </div>
                    <div class="cd-clause-pop__body">{{ c.Description || '该条款未维护原文 / 摘要。' }}</div>
                    <div v-if="c.Attributes?.length" class="cd-clause-pop__attrs">
                      <div v-for="a in c.Attributes" :key="a.AttrCode" class="cd-clause-pop__attr">
                        <span class="cd-clause-pop__attr-label">{{ clauseAttrLabel(a.AttrCode) }}</span>
                        <span class="cd-clause-pop__attr-value">{{ a.AttrValue || '—' }}</span>
                      </div>
                    </div>
                  </div>
                </template>
              </div>
            </el-popover>
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

        <!-- 状态：准备看资料，检查看结论（★ 审核员判定 vs 系统建议分开显示） -->
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
            <!-- 已由审核员判定：显示最终结论（这才算「认证结论」） -->
            <template v-if="row.ConclusionConfirmed && row.Conclusion">
              <YzhStatusBadge
                :type="CONCLUSION_TAGS[row.Conclusion] || 'info'"
                :text="labelOf(CONCLUSION_LABELS, row.Conclusion)"
              />
              <YzhStatusBadge
                v-if="row.Severity && row.Conclusion === 'nc'"
                :type="SEVERITY_TAGS[row.Severity] || 'info'"
                :text="labelOf(SEVERITY_LABELS, row.Severity)"
                class="cd-tag"
              />
            </template>
            <!-- 未判定：系统建议只作参考，必须标出「待判定」 -->
            <template v-else-if="row.AiConclusion">
              <YzhStatusBadge
                :type="CONCLUSION_TAGS[row.AiConclusion] || 'info'"
                :text="`建议 · ${labelOf(CONCLUSION_LABELS, row.AiConclusion)}`"
              />
              <YzhStatusBadge
                v-if="row.AiSeverity && row.AiConclusion === 'nc'"
                :type="SEVERITY_TAGS[row.AiSeverity] || 'info'"
                :text="`建议 · ${labelOf(SEVERITY_LABELS, row.AiSeverity)}`"
                class="cd-tag"
              />
              <YzhStatusBadge type="info" text="待判定" class="cd-tag" />
            </template>
            <span v-else class="cd-muted">{{ row.AnalyzeCount > 0 ? '待判定' : '未分析' }}</span>
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

        <!-- ★ 2026-11-03：自动分析状态 —— 让审核员一眼看到系统是否已分析过（未分析 / 已完成 / 失败 / 建议重跑） -->
        <template #column-Analysis="{ row }">
          <YzhStatusBadge
            :type="analysisStateOf(row).type"
            :text="analysisStateOf(row).text"
          />
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
            <!-- ★ 认证检查必须带出审核人信息（谁负责、谁判定） -->
            <div class="cd-dl__row">
              <span class="cd-dl__k">负责审核员</span>
              <span class="cd-dl__v">
                <span v-if="detail.OwnerName">{{ detail.OwnerName }}</span>
                <span v-else class="cd-muted">未分配</span>
              </span>
            </div>
            <div class="cd-dl__row">
              <span class="cd-dl__k">{{ isPrep ? '资料状态' : '认证结论' }}</span>
              <span class="cd-dl__v">
                <template v-if="isPrep">
                  <YzhStatusBadge
                    v-if="detail.PrepState"
                    :type="PREP_STATE_TAGS[detail.PrepState] || 'info'"
                    :text="labelOf(PREP_STATE_LABELS, detail.PrepState)"
                  />
                  <span v-else class="cd-muted">未分析</span>
                </template>
                <template v-else-if="detail.ConclusionConfirmed && detail.Conclusion">
                  <YzhStatusBadge
                    :type="CONCLUSION_TAGS[detail.Conclusion] || 'info'"
                    :text="labelOf(CONCLUSION_LABELS, detail.Conclusion)"
                  />
                  <YzhStatusBadge
                    v-if="detail.Conclusion === 'nc' && detail.Severity"
                    :type="SEVERITY_TAGS[detail.Severity] || 'info'"
                    :text="labelOf(SEVERITY_LABELS, detail.Severity)"
                    class="cd-tag"
                  />
                </template>
                <template v-else>
                  <YzhStatusBadge type="info" text="待判定" />
                  <span class="cd-muted">（需审核员判定）</span>
                </template>
              </span>
            </div>
            <!-- 系统建议：只给参考，⛔ 不是最终结论 -->
            <div v-if="!isPrep" class="cd-dl__row">
              <span class="cd-dl__k">系统建议</span>
              <span class="cd-dl__v">
                <template v-if="detail.AiConclusion">
                  <YzhStatusBadge
                    :type="CONCLUSION_TAGS[detail.AiConclusion] || 'info'"
                    :text="`建议 · ${labelOf(CONCLUSION_LABELS, detail.AiConclusion)}`"
                  />
                  <YzhStatusBadge
                    v-if="detail.AiConclusion === 'nc' && detail.AiSeverity"
                    :type="SEVERITY_TAGS[detail.AiSeverity] || 'info'"
                    :text="`建议 · ${labelOf(SEVERITY_LABELS, detail.AiSeverity)}`"
                    class="cd-tag"
                  />
                </template>
                <span v-else class="cd-muted">暂无系统建议</span>
                <span class="cd-muted">（仅供参考）</span>
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
            <div v-if="!isPrep" class="cd-dl__row">
              <span class="cd-dl__k">判定人</span>
              <span class="cd-dl__v">
                <span v-if="detail.ConclusionByName">{{ detail.ConclusionByName }}</span>
                <span v-else class="cd-muted">—</span>
                <span v-if="detail.ConclusionAt" class="cd-muted"> · {{ detail.ConclusionAt }}</span>
              </span>
            </div>
            <!-- ★ 2026-11-03：自动分析状态（未分析 / 已完成 / 分析失败 / 建议重跑） -->
            <div class="cd-dl__row">
              <span class="cd-dl__k">自动分析</span>
              <span class="cd-dl__v">
                <YzhStatusBadge
                  :type="analysisStateOf(detail).type"
                  :text="analysisStateOf(detail).text"
                />
                <span v-if="detail.AnalyzeCount" class="cd-muted"> · 已分析 {{ detail.AnalyzeCount }} 次</span>
                <span v-if="detail.LastAnalyzedAt" class="cd-muted"> · {{ detail.LastAnalyzedAt }}</span>
                <span v-if="detail.RerunState === 'suggested'" class="cd-muted">（证据已变化，建议重新分析）</span>
              </span>
            </div>
          </div>

          <!-- 判定依据 -->
          <div class="cd-block">
            <div class="cd-block__title">
              {{ isPrep ? '准备期判定依据 / 疑点提示' : '判定依据（检查证据）' }}
              <el-button
                v-if="detail.ResultCode && !detail.IsLocked"
                type="primary"
                link
                size="small"
                @click="openEdit"
              >{{ isPrep ? '编辑' : '判定 / 修改判定' }}</el-button>
            </div>
            <div v-if="detail.Reason" class="cd-reason" v-html="mdToHtml(detail.Reason)"></div>
            <div v-else class="cd-muted">暂无判定依据</div>
          </div>

          <!-- 疑点分析（可多条；无疑点 ⇒ 系统建议该检查项合格） -->
          <div v-if="!isPrep" class="cd-block">
            <div class="cd-block__title">疑点分析（{{ doubts.length }} 条）</div>
            <div v-if="doubts.length" v-for="(d, i) in doubts" :key="i" class="cd-doubt">
              <YzhStatusBadge
                type="danger"
                :text="DOUBT_KIND_LABELS[d.kind] ?? d.kind"
              />
              <span class="cd-doubt__text">{{ d.text }}</span>
            </div>
            <div v-else class="cd-muted">
              <template v-if="!detail.AiConclusion">尚未分析，暂无疑点。</template>
              <template v-else-if="detail.AiConclusion === 'pass'">
                系统未发现疑点，建议该检查项合格；是否合格仍由审核员判定。
              </template>
              <template v-else>
                系统未输出结构化疑点（旧分析数据），请参考上方判定依据；结论仍由审核员判定。
              </template>
            </div>
          </div>
          <!-- 准备期疑点提示 -->
          <div v-else-if="doubts.length" class="cd-block">
            <div class="cd-block__title">疑点提示（{{ doubts.length }}）</div>
            <div v-for="(d, i) in doubts" :key="i" class="cd-doubt">
              <YzhStatusBadge
                type="danger"
                :text="DOUBT_KIND_LABELS[d.kind] ?? d.kind"
              />
              <span class="cd-doubt__text">{{ d.text }}</span>
            </div>
          </div>

          <!-- ★ 2026-11-03：检查内容 —— 指定该检查项「查什么 / 怎么查」，可多条，系统可自动生成。
               认证准备可增删改；认证检查只读（入场计划据此编排）。 -->
          <div class="cd-evi-panel">
            <div class="cd-evi-panel__head">
              <div class="cd-block__title">检查内容（{{ checkContents.length }}）</div>
              <div v-if="isPrep" class="cd-content-acts">
                <el-button
                  type="primary"
                  plain
                  size="small"
                  :disabled="editLocked || detail.IsLocked"
                  @click="openCheckContentCreate"
                >+ 新增</el-button>
                <el-button
                  type="default"
                  plain
                  size="small"
                  :loading="generatingContents"
                  :disabled="editLocked || detail.IsLocked"
                  @click="doGenerateCheckContents"
                >系统生成</el-button>
              </div>
            </div>
            <div class="cd-evi-panel__tip">
              {{ isPrep
                ? '检查内容说明该检查项要查什么、用什么方式查，可多条维护；系统可按条款自动生成，生成只替换上一次系统生成的内容。'
                : '检查内容来自认证准备阶段的维护结果，认证检查阶段只读。' }}
            </div>
            <el-table
              v-loading="checkContentLoading"
              :data="checkContents"
              border
              size="small"
              empty-text="暂无检查内容"
            >
              <el-table-column label="序号" width="56" align="center">
                <template #default="{ row }">{{ row.SeqNo }}</template>
              </el-table-column>
              <el-table-column label="检查内容" min-width="160">
                <template #default="{ row }">
                  <span class="cd-evi-body">{{ row.CheckWhat }}</span>
                </template>
              </el-table-column>
              <el-table-column label="检查方式" width="96">
                <template #default="{ row }">{{ row.CheckMethod || '—' }}</template>
              </el-table-column>
              <el-table-column label="部门" width="86" show-overflow-tooltip>
                <template #default="{ row }">{{ row.TargetDept || '—' }}</template>
              </el-table-column>
              <el-table-column label="抽样要求 / 期望发现" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">
                  <div v-if="row.SampleRequirement" class="cd-evi-anchor">抽样：{{ row.SampleRequirement }}</div>
                  <div v-if="row.ExpectedFinding" class="cd-evi-anchor">期望：{{ row.ExpectedFinding }}</div>
                  <span v-if="!row.SampleRequirement && !row.ExpectedFinding" class="cd-muted">—</span>
                </template>
              </el-table-column>
              <el-table-column label="来源" width="76" align="center">
                <template #default="{ row }">
                  <YzhStatusBadge
                    :type="row.Source === 'ai' ? 'warning' : 'info'"
                    :text="row.Source === 'ai' ? '系统生成' : '人工'"
                  />
                </template>
              </el-table-column>
              <el-table-column v-if="isPrep" label="操作" width="96" align="center">
                <template #default="{ row }">
                  <el-button
                    type="primary"
                    link
                    size="small"
                    :disabled="editLocked || detail.IsLocked"
                    @click="openCheckContentEdit(row as CertCheckContentRow)"
                  >编辑</el-button>
                  <el-button
                    type="danger"
                    link
                    size="small"
                    :disabled="editLocked || detail.IsLocked"
                    @click="removeCheckContent(row as CertCheckContentRow)"
                  >删除</el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>

          <!-- 证据区：新增按钮与证据表格同属一个区域；新增 / 编辑走弹窗，行内可改可删 -->
          <div class="cd-evi-panel">
            <div class="cd-evi-panel__head">
              <div class="cd-block__title">证据（{{ allEvidences.length }}）</div>
              <el-button
                type="primary"
                plain
                size="small"
                :disabled="editLocked"
                @click="openEvidence"
              >+ 添加{{ isPrep ? '补充' : '现场' }}证据</el-button>
            </div>
            <div class="cd-evi-panel__tip">
              {{ isPrep
                ? '原始证据来自企业资料；审核员补充的检查证据同样参与系统分析，已补证据可修改、可删除。'
                : '原始证据来自企业资料与入场计划；现场补录的证据是分析主依据，可修改、可删除。' }}
            </div>
            <el-table :data="allEvidences" border size="small" empty-text="暂无证据">
              <el-table-column label="分类" width="76" align="center">
                <template #default="{ row }">
                  <YzhStatusBadge :type="row.__raw ? 'info' : 'success'" :text="row.__raw ? '原始' : '检查'" />
                </template>
              </el-table-column>
              <el-table-column label="类型" width="110">
                <template #default="{ row }">
                  {{ EVIDENCE_KIND_LABELS[row.EvidenceKind] || row.EvidenceKind || '—' }}
                </template>
              </el-table-column>
              <el-table-column label="来源 / 标题" min-width="150" show-overflow-tooltip>
                <template #default="{ row }">
                  <div class="cd-evi-name">{{ row.SourceName || '未命名来源' }}</div>
                  <div v-if="row.Anchor" class="cd-evi-anchor">{{ row.Anchor }}</div>
                </template>
              </el-table-column>
              <el-table-column label="内容" min-width="200" show-overflow-tooltip>
                <template #default="{ row }">
                  <span class="cd-evi-body">{{ row.LlmSummary || row.Snippet || '（无内容）' }}</span>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="110" align="center">
                <template #default="{ row }">
                  <!-- 原始证据来自企业资料 / 切片，只读；审核员补录的检查证据可改可删 -->
                  <template v-if="!row.__raw">
                    <el-button
                      type="primary"
                      link
                      size="small"
                      :disabled="editLocked || detail?.IsLocked"
                      @click="openEvidenceEdit(row as CertEvidenceRow)"
                    >编辑</el-button>
                    <el-button
                      type="danger"
                      link
                      size="small"
                      :disabled="editLocked || detail?.IsLocked"
                      @click="removeEvidence(row as CertEvidenceRow)"
                    >删除</el-button>
                  </template>
                  <span v-else class="cd-muted">只读</span>
                </template>
              </el-table-column>
            </el-table>
          </div>

          <!-- 抽屉操作 -->
          <div class="cd-drawer__acts">
            <el-button
              type="default"
              :disabled="currentRow?.IsLocked"
              @click="currentRow && doAnalyze(currentRow)"
            >{{ currentRow?.AnalyzeCount ? '重新分析' : '自动分析' }}</el-button>
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

    <!-- 现场补证 / 审核员补充证据弹窗（新增与编辑共用） -->
    <YzhDialog
      v-model="evDialog"
      :title="editingEvidenceCode ? '编辑证据' : (isPrep ? '补充证据' : '现场补证')"
      width="600px"
      :confirm-text="editingEvidenceCode ? '保存修改' : '保存并纳入分析'"
      :confirm-loading="evLoading"
      @confirm="submitEvidence"
    >
      <el-alert
        type="info"
        :closable="false"
        show-icon
        class="cd-nc-alert"
        title="只录客观事实"
        description="审核员负责记录看到 / 听到 / 查到的内容；这些证据能证明什么，由系统结合条款与资料分析判断。"
      />
      <el-form :model="evForm" label-width="96px" size="small">
        <el-form-item label="证据类型">
          <el-select v-model="evForm.EvidenceKind" class="cd-evi-form__sel">
            <el-option
              v-for="opt in EVIDENCE_KIND_OPTIONS"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="来源 / 标题" required>
          <el-input
            v-model="evForm.SourceName"
            placeholder="如：与生产部王主管的访谈记录 / 现场照片-仓库标识"
          />
        </el-form-item>
        <el-form-item label="证据内容" required>
          <el-input
            v-model="evForm.Content"
            type="textarea"
            :rows="6"
            placeholder="客观描述看到 / 听到 / 查到的内容，尽量带人名、时间、数量，例如：仓库货架上物料标识与台账一致，抽查 3 个批次均能追溯到入库单"
          />
        </el-form-item>
        <el-form-item label="发生时间">
          <el-input v-model="evForm.OccurredAt" placeholder="如：2026-11-03 09:30" />
        </el-form-item>
        <el-form-item label="地点 / 部门">
          <el-input v-model="evForm.Place" placeholder="如：一号车间 · 生产部" />
        </el-form-item>
        <el-form-item label="受访人">
          <el-input v-model="evForm.Interviewee" placeholder="如：王主管（生产部）" />
        </el-form-item>
      </el-form>
    </YzhDialog>

    <!-- 审核员判定（认证结论 + 不符合程度 + 判定依据） -->
    <YzhDialog
      v-model="editDialog"
      :title="isPrep ? '编辑准备期判定依据' : '审核员判定（认证结论）'"
      width="720px"
      confirm-text="保存"
      :confirm-loading="editLoading"
      @confirm="submitEdit"
    >
      <el-alert
        type="info"
        :closable="false"
        show-icon
        class="cd-nc-alert"
        title="系统只给建议，最终结论由审核员判定"
        description="系统分析出的建议结论、疑点与判定依据仅作参考，可以直接改写。是否符合、不符合到什么程度（一般 / 严重），必须由审核员在这里确认。"
      />
      <el-form :model="editForm" label-width="88px" size="small">
        <el-form-item v-if="!isPrep" label="认证结论" required>
          <el-select v-model="editForm.Conclusion" class="cd-evi-form__sel" placeholder="请选择结论">
            <el-option
              v-for="opt in CONCLUSION_OPTIONS"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="!isPrep && editForm.Conclusion === 'nc'" label="不符合程度" required>
          <el-select v-model="editForm.Severity" class="cd-evi-form__sel" placeholder="请选择程度">
            <el-option
              v-for="opt in SEVERITY_OPTIONS"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="判定依据" required>
          <el-input
            v-model="editForm.Reason"
            type="textarea"
            :rows="14"
            placeholder="按真实认证报告的写法组织：先结论，再证据支持，再证据缺口 / 风险，分段书写"
          />
        </el-form-item>
      </el-form>
    </YzhDialog>

    <!-- 检查内容新增 / 编辑（认证准备） -->
    <YzhDialog
      v-model="checkContentDialog"
      :title="checkContentTarget ? '编辑检查内容' : '新增检查内容'"
      width="560px"
      confirm-text="保存"
      :confirm-loading="checkContentSaving"
      @confirm="submitCheckContent"
    >
      <el-alert
        type="info"
        :closable="false"
        show-icon
        class="cd-nc-alert"
        title="检查内容 = 查什么 + 怎么查"
        description="一条检查内容描述一个可执行的检查动作；同一检查项可维护多条。系统生成的条目会标记来源，人工条目不会被覆盖。"
      />
      <el-form :model="checkContentForm" label-width="96px" size="small">
        <el-form-item label="检查内容" required>
          <el-input
            v-model="checkContentForm.CheckWhat"
            type="textarea"
            :rows="3"
            placeholder="如：查阅近 3 个月的管理评审记录，确认输入完整、输出有决议与跟踪"
          />
        </el-form-item>
        <el-form-item label="检查方式">
          <el-select v-model="checkContentForm.CheckMethod" class="cd-evi-form__sel" clearable>
            <el-option
              v-for="opt in CHECK_METHOD_OPTIONS"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="责任部门">
          <el-input v-model="checkContentForm.TargetDept" placeholder="如：管理层 / 生产部" />
        </el-form-item>
        <el-form-item label="抽样要求">
          <el-input v-model="checkContentForm.SampleRequirement" placeholder="如：抽查 3 个批次 / 抽 2 份记录" />
        </el-form-item>
        <el-form-item label="期望发现">
          <el-input v-model="checkContentForm.ExpectedFinding" placeholder="如：记录齐全、可追溯，无缺失项" />
        </el-form-item>
      </el-form>
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
  cursor: pointer;
}
.cd-clause__no:hover { background: var(--el-color-primary-light-9); }
.cd-clause-pop__item + .cd-clause-pop__item {
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px solid var(--el-border-color-lighter);
}
.cd-clause-pop__head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
.cd-clause-pop__no {
  padding: 0 var(--yzh-space-1, 4px);
  border: 1px solid var(--el-color-primary-light-5);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-color-primary);
}
.cd-clause-pop__title {
  flex: 1;
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-clause-pop__std {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-clause-pop__body {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.7);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
  white-space: pre-wrap;
  word-break: break-word;
  max-height: var(--yzh-height-pop, 260px);
  overflow-y: auto;
}
.cd-clause-pop__attrs {
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border-light, var(--el-border-color-lighter));
}
.cd-clause-pop__attr {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-normal, 1.5);
}
.cd-clause-pop__attr-label {
  flex: 0 0 auto;
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-clause-pop__attr-value {
  flex: 1;
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
  white-space: pre-wrap;
  word-break: break-word;
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
}
.cd-reason :deep(p) { margin: 0 0 var(--yzh-space-2, 8px); }
.cd-reason :deep(p:last-child) { margin-bottom: 0; }
.cd-reason :deep(ul),
.cd-reason :deep(ol) {
  margin: var(--yzh-space-1, 4px) 0;
  padding-left: var(--yzh-space-5, 20px);
}
.cd-reason :deep(li) { margin-bottom: var(--yzh-space-1, 4px); }
.cd-reason :deep(strong) { color: var(--yzh-color-text-primary, var(--el-text-color-primary)); }
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
.cd-evi-panel { margin-top: var(--yzh-space-3, 12px); }
.cd-evi-panel__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-2, 8px);
}
.cd-evi-panel__tip {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.6);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-content-acts { display: flex; gap: var(--yzh-space-2, 8px); }
.cd-evi-name {
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
}
.cd-evi-anchor {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, var(--el-text-color-secondary));
}
.cd-evi-body {
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.7);
  color: var(--yzh-color-text-secondary, var(--el-text-color-regular));
  white-space: pre-wrap;
  word-break: break-word;
}
.cd-evi-form__sel { width: var(--yzh-width-sel, 180px); }
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
