<script setup lang="ts">
/**
 * 认证计划（专家端 · 路由 /audit-plan · MENU_AUD_17）
 *
 * 按条款为核心的体系认证入场计划。向导式：
 *   ① 基本信息：企业 + 认证标准（可多选，多体系）+ 当前阶段
 *   ② 选择条款与部门：部门树 + 条款树勾选 → 加入已选（可只勾条款 / 多部门+条款组合）→ 已选可删
 *   ③ 确认生成：预览计划主表 + 明细，保存
 *
 * 数据源（全部真实接口）：
 *   企业   POST /api/Auditor/Enterprise/filter
 *   标准   GET  /api/Auditor/CertQuery/standards   （含简称 Abbrev）
 *   条款   GET  /api/Auditor/CertQuery/clauses?standardCode=xx
 *   阶段   GET  /api/Auditor/CertQuery/stages      （下拉只取 Category='audit'）
 *   部门   POST /api/Auditor/EnterpriseDept/tree/children （企业节点 → 部门子树）
 *   保存   POST /api/Auditor/AuditPlan/save
 *   明细   GET  /api/Auditor/AuditPlan/details?planCode=xx
 *
 * ★ 简称只在「条款序号」上作为前缀（如 e 4.3），不替换标准名称。
 * ★ 阶段只列 GB/T 审核类阶段（S1/S2/SV/RC/SA/TT）；申请受理/合同评审/认证决定/证书状态
 *   属于认证流程动作，不构成入场计划要执行的审核阶段。
 * ★ 部门可空：多数条款（4.1/5.1/6.1 等公司级条款）在计划期并不落到具体部门，
 *   报告里也按「受审核部门/场所」分册而非全局必填（见 案例资料/脱敏版）。
 */
import { ref, reactive, computed, onMounted } from 'vue'
import {
  ElButton, ElSelect, ElOption, ElDialog, ElTable, ElTableColumn,
  ElTree, ElTag, ElSteps, ElStep, ElDescriptions, ElDescriptionsItem, ElMessage,
} from 'element-plus'
import { yzhApi } from '@yzh-core/api/client'
import { confirmOrFalse } from '@yzh-core'
import router from '@/router'
import {
  closeAuditPlan, getMemberOptions, type CertMemberOption,
} from '@share/api/auditor/cert-check-task'

interface StandardItem { Code: string; StandardCode: string; StandardName: string; VersionYear: number; Abbrev: string }
interface StageItem { Code: string; StageCode: string; StageName: string; Category: string }
interface EnterpriseItem { Code: string; Name: string }
interface DeptItem { Code: string; DeptName: string; ParentCode: string | null }
interface DeptNode { Code: string; Name: string; ParentCode: string | null; NodeType?: string; IsLeaf?: boolean; Extra?: Record<string, any> }
interface ClauseItem { Code: string; StandardCode: string; ClauseNumber: string; Title: string; ParentCode: string | null }
interface PlanRow { Code: string; Name: string; EnterpriseCode: string; StageCode: string; Standards: string; IsValid: number; Status?: string; CreateTime?: string }
interface Selection {
  deptCodes: string[]
  deptNames: string[]
  clauseCodes: string[]
  clauseNumbers: string[]
  /** 标准短码、版本和条款号共同标识一个条款。 */
  clauseRefs: Array<{ StandardNo: string; VersionYear?: number | null; ClauseNumber: string }>
  ownerCode: string
  ownerName: string
}

const plans = ref<PlanRow[]>([])
const enterprises = ref<EnterpriseItem[]>([])
const standards = ref<StandardItem[]>([])
const stages = ref<StageItem[]>([])
const depts = ref<DeptItem[]>([])
const clausesByStd = ref<Record<string, ClauseItem[]>>({})
const members = ref<CertMemberOption[]>([])

const stdMap = computed(() => { const m: Record<string, StandardItem> = {}; standards.value.forEach(s => { m[s.Code] = s }); return m })
const stageMap = computed(() => { const m: Record<string, StageItem> = {}; stages.value.forEach(s => { m[s.Code] = s }); return m })
const clauseMap = computed(() => {
  const m: Record<string, ClauseItem> = {}
  Object.values(clausesByStd.value).forEach(arr => arr.forEach(c => { m[c.Code] = c }))
  return m
})

const entName = (c: string) => enterprises.value.find(e => e.Code === c)?.Name || c
const stdName = (c: string) => stdMap.value[c]?.StandardName || c
const stdAbbrev = (c: string) => stdMap.value[c]?.Abbrev || ''
const stageName = (c: string) => stageMap.value[c]?.StageName || c
const deptName = (c: string) => depts.value.find(d => d.Code === c)?.DeptName || c
const splitCsv = (s?: string) => (s || '').split(',').filter(Boolean)
/** 后端时间形如 2026-11-03T10:20:30 / 2026-11-03 10:20:30 —— 统一成可读格式 */
const fmtTime = (s?: string) => (s ? s.replace('T', ' ').slice(0, 19) : '—')

// ──── 加载 ────
async function loadPlans() {
  // ★ ShowDisabled=true：计划只增不废，历史计划（旧数据里 IsValid=0）也必须显示，
  //   否则用户会看到「新建一次计划，之前的计划全没了」。
  const res = await yzhApi.post('/api/Auditor/AuditPlan/filter', {
    Page: 1, PageSize: 200, ShowDisabled: true, Filters: [],
  })
  const items: PlanRow[] = res?.data?.Items || []
  // ★ 按建立时间倒序：最新计划在最上面
  plans.value = items.sort((a, b) => (b.CreateTime || '').localeCompare(a.CreateTime || ''))
}
async function loadEnterprises() {
  const res = await yzhApi.post('/api/Auditor/Enterprise/filter', {
    Page: 1, PageSize: 300, Filters: [{ Field: 'IsValid', Operator: 'eq', Value: '1' }],
  })
  enterprises.value = res?.data?.Items || []
}
async function loadStandards() {
  const res = await yzhApi.request('/api/Auditor/CertQuery/standards', { method: 'GET' })
  standards.value = res?.data || []
}
/** 入场计划下拉只允许选择 GB/T 审核类阶段（cert_cert_stage.Category='audit'），并按固定顺序排列 */
const AUDIT_STAGE_ORDER = ['S1', 'S2', 'SV', 'RC', 'SA', 'TT']
async function loadStages() {
  const res = await yzhApi.request('/api/Auditor/CertQuery/stages', { method: 'GET' })
  stages.value = res?.data || []
}
/** 下拉数据源（audit 类阶段）；stages 保留全量用于历史计划 StageCode → 名称映射 */
const auditStages = computed(() =>
  stages.value
    .filter(s => s.Category === 'audit')
    .sort((a, b) => AUDIT_STAGE_ORDER.indexOf(a.StageCode) - AUDIT_STAGE_ORDER.indexOf(b.StageCode)),
)

/**
 * 部门数据源 = 左树接口（企业节点 → 顶层部门 → 子部门逐级展开）。
 * ⛔ 不能用 `/EnterpriseDept/filter`：基类的 filter 查的是右表「部门人员」
 *    （cert_enterprise_dept_member），根本没有 DeptName 列，取回来必然是空树。
 *
 * 部门可选：企业未维护部门时返回空列表，向导仍允许「只勾条款」加入已选。
 */
async function loadDepts(entCode: string) {
  depts.value = []
  if (!entCode) return
  const collected: DeptItem[] = []

  async function walk(parentCode: string, level: number) {
    const res = await yzhApi.post('/api/Auditor/EnterpriseDept/tree/children', {
      ParentCode: parentCode,
      Level: level,
    })
    // 失败要显式报出，否则空树会被误读成「企业本来就没有部门」
    if (res?.success === false) {
      ElMessage.error(res?.err || res?.message || '加载企业部门失败')
      return
    }
    const nodes: DeptNode[] = res?.data || []
    for (const n of nodes) {
      if (n.NodeType !== 'dept') continue
      if (Number(n.Extra?.IsValid ?? 1) === 0) continue
      collected.push({ Code: n.Code, DeptName: n.Name, ParentCode: n.ParentCode ?? null })
      if (!n.IsLeaf) await walk(n.Code, level + 1)
    }
  }

  await walk(entCode, 0)
  depts.value = collected
}
async function loadClausesForStandards(stdCodes: string[]) {
  const map: Record<string, ClauseItem[]> = {}
  for (const code of stdCodes) {
    const res = await yzhApi.request('/api/Auditor/CertQuery/clauses', { method: 'GET', params: { standardCode: code } })
    map[code] = res?.data || []
  }
  clausesByStd.value = map
}

onMounted(async () => {
  await Promise.all([loadPlans(), loadEnterprises(), loadStandards(), loadStages()])
})

// ──── 向导状态 ────
const wizardVisible = ref(false)
const step = ref(1)
const draft = reactive({ enterprise: '', standards: [] as string[], stage: '' })
const selections = ref<Selection[]>([])
const saving = ref(false)
const ownerCode = ref('')
const deptTreeRef = ref<any>(null)
const clauseTreeRef = ref<any>(null)
/** 空 = 新建；非空 = 正在调整的计划 Code */
const editingPlanCode = ref('')
const wizardTitle = computed(() => (editingPlanCode.value ? '调整认证计划' : '新建认证计划'))

const previewMaster = computed(() => ({
  Name: `${entName(draft.enterprise)}·${stageName(draft.stage)}·认证计划`,
}))

// 部门树
const deptTreeData = computed(() => {
  const map: Record<string, any> = {}
  depts.value.forEach(d => { map[d.Code] = { Code: d.Code, label: d.DeptName, children: [] } })
  const roots: any[] = []
  depts.value.forEach(d => {
    const p = d.ParentCode ? map[d.ParentCode] : null
    if (p) p.children.push(map[d.Code]); else roots.push(map[d.Code])
  })
  return roots
})

// 条款树（按所选标准分组，label 带简称前缀）
const clauseTreeData = computed(() => {
  return draft.standards.map(stdCode => {
    const std = stdMap.value[stdCode]
    const list = clausesByStd.value[stdCode] || []
    const map: Record<string, any> = {}
    list.forEach(c => { map[c.Code] = { Code: c.Code, label: `${std?.Abbrev || ''} ${c.ClauseNumber} · ${c.Title}`, children: [] } })
    const roots: any[] = []
    list.forEach(c => {
      const p = c.ParentCode ? map[c.ParentCode] : null
      if (p) p.children.push(map[c.Code]); else roots.push(map[c.Code])
    })
    return { Code: `STD:${stdCode}`, label: std?.StandardName || stdCode, children: roots }
  })
})

async function openWizard() {
  editingPlanCode.value = ''
  step.value = 1
  draft.enterprise = ''
  draft.standards = []
  draft.stage = ''
  selections.value = []
  depts.value = []
  clausesByStd.value = {}
  ownerCode.value = ''
  wizardVisible.value = true
  // 审核员下拉数据源 = 专家组成员池（与检查项「设置负责审核员」同一来源）
  await ensureMembers()
}

async function ensureMembers() {
  if (members.value.length) return
  const res = await getMemberOptions()
  members.value = res?.data || []
}

/**
 * 调整已有计划（2026-11-03 用户纠正：计划可重新调整）。
 * 把已有明细还原成「已选组合」，部门 / 条款范围与负责审核员都可改，保存时带 Code 走更新。
 */
async function openEdit(row: PlanRow) {
  editingPlanCode.value = row.Code
  step.value = 1
  selections.value = []
  depts.value = []
  clausesByStd.value = {}
  draft.enterprise = row.EnterpriseCode
  draft.standards = splitCsv(row.Standards)
  draft.stage = row.StageCode
  ownerCode.value = ''
  wizardVisible.value = true
  await ensureMembers()
  await loadDepts(row.EnterpriseCode)
  await loadClausesForStandards(draft.standards)
  const res = await yzhApi.get('/api/Auditor/AuditPlan/details', { planCode: row.Code })
  const details: any[] = res?.data || []
  for (const d of details) {
    try {
      const raw = d.ClauseRefsJson ? JSON.parse(d.ClauseRefsJson) : []
      if (!Array.isArray(raw) || raw.some((r: any) => !r?.StandardNo || !r?.ClauseNumber))
        throw new Error('invalid clause refs')
    } catch {
      ElMessage.error('计划包含格式错误的条款引用，无法安全编辑，请先修复数据')
      wizardVisible.value = false
      return
    }
  }
  selections.value = details.map(d => {
    let refs: Array<{ StandardNo: string; VersionYear?: number | null; ClauseNumber: string }> = []
    try {
      const raw = d.ClauseRefsJson ? JSON.parse(d.ClauseRefsJson) : []
      if (Array.isArray(raw)) refs = raw.filter((r: any) => r?.StandardNo && r?.ClauseNumber)
    } catch { refs = [] }
    const clauseByRef: Record<string, string[]> = {}
    Object.values(clausesByStd.value).forEach(arr => arr.forEach(c => {
      const std = stdMap.value[c.StandardCode]
      if (!std) return
      const key = `${std.StandardCode}|${std.VersionYear}|${c.ClauseNumber}`
      const legacyKey = `${std.StandardCode}||${c.ClauseNumber}`
      ;(clauseByRef[key] ||= []).push(c.Code)
      ;(clauseByRef[legacyKey] ||= []).push(c.Code)
    }))
    const clauseCodes = refs.length
      ? refs.map(r => {
        const matches = clauseByRef[`${r.StandardNo}|${r.VersionYear ?? ''}|${r.ClauseNumber}`] || []
        return matches.length === 1 ? matches[0] : undefined
      }).filter((code): code is string => !!code)
      : splitCsv(d.Clauses)
    // 旧数据可能只有 ClauseNumbers 没有 Clauses —— 此时按编号标签原样展示，避免组合凭空消失
    const clauseNumbers = clauseCodes.length
      ? clauseCodes.map(c => { const cl = clauseMap.value[c]; return cl ? `${stdAbbrev(cl.StandardCode)} ${cl.ClauseNumber}` : c })
      : splitCsv(d.ClauseNumbers)
    return {
      deptCodes: splitCsv(d.Departments),
      deptNames: splitCsv(d.DeptNames),
      clauseCodes,
      clauseNumbers,
      clauseRefs: refs.length ? refs : clauseCodes.map(c => {
        const cl = clauseMap.value[c]
        const std = cl ? stdMap.value[cl.StandardCode] : undefined
        return { StandardNo: std?.StandardCode || '', VersionYear: std?.VersionYear, ClauseNumber: cl?.ClauseNumber || '' }
      }).filter(r => r.StandardNo && r.ClauseNumber),
      ownerCode: d.OwnerCode || '',
      ownerName: d.OwnerName || '',
    }
  })
}

/** 已选组合里改审核员：姓名以成员池为准（与后端校验同源） */
function onOwnerChange(row: Selection) {
  row.ownerName = members.value.find(m => m.Code === row.ownerCode)?.Name || ''
}

function toggleStd(code: string) {
  const i = draft.standards.indexOf(code)
  if (i >= 0) draft.standards.splice(i, 1); else draft.standards.push(code)
}

async function next() {
  if (step.value === 1) {
    if (!draft.enterprise) { ElMessage.warning('请选择企业'); return }
    if (!draft.standards.length) { ElMessage.warning('请至少选择一个认证标准'); return }
    if (!draft.stage) { ElMessage.warning('请选择阶段'); return }
    await loadDepts(draft.enterprise)
    await loadClausesForStandards(draft.standards)
    step.value = 2
  } else if (step.value === 2) {
    if (!selections.value.length) { ElMessage.warning('请至少添加一个选择组合'); return }
    step.value = 3
  }
}

/**
 * 收集树勾选结果（含半选的父级）。
 *
 * 树已去掉 check-strictly：勾父级 → 自动勾选全部子级；只勾子级时父级为半选，
 * 这里把半选父级一并收进来（用户 2026-11-03 要求「选子节点时父节点也要选中」）。
 * 标准分组伪节点（STD:xxx）永远排除。
 */
function collectTreeKeys(treeRef: any): string[] {
  const checked: string[] = (treeRef?.getCheckedKeys() || []) as string[]
  const half: string[] = (treeRef?.getHalfCheckedKeys?.() || []) as string[]
  return Array.from(new Set([...checked, ...half])).filter(c => !c.startsWith('STD:'))
}

function addSelection() {
  const deptCodes = collectTreeKeys(deptTreeRef.value)
  const clauseCodes = collectTreeKeys(clauseTreeRef.value)
  if (!clauseCodes.length) { ElMessage.warning('请至少勾选一个条款'); return }
  // 每个组合必须落到一个负责审核员（报告里的条款要有审核人）；成员池为空时允许留空，后续在准备阶段补
  if (members.value.length && !ownerCode.value) { ElMessage.warning('请先选择负责审核员'); return }
  const member = members.value.find(m => m.Code === ownerCode.value)
  const deptNames = deptCodes.map(c => deptName(c))
  const clauseNumbers = clauseCodes.map(c => {
    const cl = clauseMap.value[c]
    return cl ? `${stdAbbrev(cl.StandardCode)} ${cl.ClauseNumber}` : c
  })
  const clauseRefs = clauseCodes.map(c => {
    const cl = clauseMap.value[c]
    const std = cl ? stdMap.value[cl.StandardCode] : undefined
    return { StandardNo: std?.StandardCode || '', VersionYear: std?.VersionYear, ClauseNumber: cl?.ClauseNumber || '' }
  }).filter(r => r.StandardNo && r.ClauseNumber)
  selections.value.push({
    deptCodes, deptNames, clauseCodes, clauseNumbers, clauseRefs,
    ownerCode: ownerCode.value,
    ownerName: member?.Name || '',
  })
  deptTreeRef.value?.setCheckedKeys([])
  clauseTreeRef.value?.setCheckedKeys([])
}

async function savePlan() {
  if (!selections.value.length) { ElMessage.warning('请先添加选择组合'); return }
  if (selections.value.some(s => s.clauseRefs.length !== s.clauseCodes.length)) {
    ElMessage.warning('计划中有无法匹配的条款，请删除对应组合并重新选择')
    return
  }
  saving.value = true
  try {
    const payload = {
      Plan: {
        Code: editingPlanCode.value,
        Name: previewMaster.value.Name,
        EnterpriseCode: draft.enterprise,
        StageCode: draft.stage,
        Standards: draft.standards.join(','),
        IsValid: 1,
        Sort: 0,
      },
      Details: selections.value.map(s => ({
        Clauses: s.clauseCodes.join(','),
        Departments: s.deptCodes.join(','),
        DeptNames: s.deptNames.join(','),
        ClauseNumbers: s.clauseNumbers.join(','),
        ClauseRefsJson: JSON.stringify(s.clauseRefs || []),
        OwnerCode: s.ownerCode || null,
      })),
    }
    const res = await yzhApi.post('/api/Auditor/AuditPlan/save', payload)
    if (res?.success) {
      ElMessage.success(editingPlanCode.value ? '计划已调整' : '计划已保存')
      wizardVisible.value = false
      editingPlanCode.value = ''
      await loadPlans()
    } else {
      ElMessage.error(res?.err || res?.message || '保存失败')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// ──── 关闭计划 ────
/**
 * 关闭计划（2026-11-03 用户要求）：关闭后该计划对应的认证准备 / 认证检查一并作废，
 * 只能查看、不能再编辑；计划本身也不再允许调整。
 */
async function closePlan(row: PlanRow) {
  const ok = await confirmOrFalse(
    `关闭计划「${row.Name}」后，其认证准备与认证检查将一并作废，只能查看、不能再编辑。`,
    '关闭计划',
  )
  if (!ok) return
  const res = await closeAuditPlan(row.Code)
  if (res?.success === false) {
    ElMessage.error(res?.err || res?.message || '关闭计划失败')
    return
  }
  ElMessage.success('计划已关闭，对应认证准备与认证检查已作废（只读）')
  await loadPlans()
}

// ──── 进入认证准备 / 认证检查 ────
// ★ 一个计划只对应唯一的准备 / 检查分支：从计划直接进详情，不再绕道任务列表或工作台。
function openPrep(row: any) {
  router.push(`/audit-prep/${row.Code}`)
}
function openOnsite(row: any) {
  router.push(`/audit-onsite/${row.Code}`)
}

// ──── 查看详情（独立） ────
const viewVisible = ref(false)
const viewPlan = ref<PlanRow | null>(null)
const viewDetails = ref<any[]>([])
async function openView(row: any) {
  viewPlan.value = row as PlanRow
  viewDetails.value = []
  viewVisible.value = true
  const res = await yzhApi.request('/api/Auditor/AuditPlan/details', { method: 'GET', params: { planCode: row.Code } })
  viewDetails.value = res?.data || []
}
</script>

<template>
  <div class="audit-plan-page">
    <div class="ap-header">
      <span class="ap-title">认证计划</span>
      <el-button type="primary" @click="openWizard">+ 新建计划</el-button>
    </div>

    <el-table :data="plans" border stripe size="default">
      <el-table-column prop="Name" label="计划名称" min-width="220" show-overflow-tooltip />
      <el-table-column label="企业" min-width="160">
        <template #default="{ row }">{{ entName(row.EnterpriseCode) }}</template>
      </el-table-column>
      <el-table-column label="阶段" width="140">
        <template #default="{ row }">{{ stageName(row.StageCode) }}</template>
      </el-table-column>
      <el-table-column label="认证标准" min-width="200">
        <template #default="{ row }">
          <el-tag v-for="c in splitCsv(row.Standards)" :key="c" size="small" class="ap-tag">{{ stdName(c) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="CreateTime" label="建立时间" width="170" align="center" sortable>
        <template #default="{ row }">{{ fmtTime(row.CreateTime) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag v-if="row.Status === 'closed'" type="info" size="small">已关闭</el-tag>
          <el-tag v-else :type="row.IsValid === 1 ? 'success' : 'info'" size="small">{{ row.IsValid === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="360" align="center">
        <template #default="{ row }">
          <el-button link type="primary" @click="openView(row)">查看</el-button>
          <el-button link type="primary" :disabled="row.Status === 'closed'" @click="openEdit(row as PlanRow)">调整</el-button>
          <el-button link type="warning" @click="openPrep(row)">认证准备</el-button>
          <el-button link type="success" @click="openOnsite(row)">认证检查</el-button>
          <el-button
            v-if="row.Status !== 'closed'"
            link
            type="danger"
            @click="closePlan(row as PlanRow)"
          >关闭</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新建 / 调整向导 -->
    <el-dialog v-model="wizardVisible" :title="wizardTitle" width="1000px" :close-on-click-modal="false" destroy-on-close>
      <el-steps :active="step - 1" simple class="ap-steps">
        <el-step title="基本信息" />
        <el-step title="选择条款与部门" />
        <el-step title="确认生成" />
      </el-steps>

      <!-- ① 基本信息 -->
      <div v-if="step === 1">
        <div class="ap-field">
          <label>企业</label>
          <el-select v-model="draft.enterprise" filterable placeholder="请选择企业" class="ap-full">
            <el-option v-for="e in enterprises" :key="e.Code" :label="e.Name" :value="e.Code" />
          </el-select>
        </div>
        <div class="ap-field">
          <label>认证标准（可多选，多体系）</label>
          <div class="ap-chips">
            <span
              v-for="s in standards"
              :key="s.Code"
              class="ap-chip"
              :class="{ sel: draft.standards.includes(s.Code) }"
              @click="toggleStd(s.Code)"
            >{{ s.StandardName }}</span>
            <span v-if="!standards.length" class="ap-hint">暂无标准数据</span>
          </div>
        </div>
        <div class="ap-field">
          <label>审核阶段（GB/T 审核类型，只列审核类）</label>
          <el-select v-model="draft.stage" placeholder="请选择审核阶段" class="ap-full">
            <el-option v-for="s in auditStages" :key="s.Code" :label="`${s.StageCode} · ${s.StageName}`" :value="s.Code" />
          </el-select>
        </div>
        <div class="ap-hint">
          第一步只定范围：选企业、勾选本次要认证的标准（可多个）、选审核阶段。第二步再勾条款与部门。
          申请受理 / 合同评审 / 认证决定 / 证书状态属于认证流程动作，不在入场计划的可选阶段内。
        </div>
      </div>

      <!-- ② 选择条款与部门 -->
      <div v-else-if="step === 2">
        <div class="ap-two-col">
          <div class="ap-box">
            <div class="ap-box-title">部门（企业部门树，可不选）</div>
            <el-tree
              ref="deptTreeRef"
              :data="deptTreeData"
              show-checkbox
              node-key="Code"
              :props="{ label: 'label', children: 'children' }"
              default-expand-all
            />
            <div v-if="!deptTreeData.length" class="ap-hint">
              该企业未维护部门 —— 部门为可选项，直接勾选条款即可加入。公司级条款（4.1 / 5.1 / 6.1 等）通常不落具体部门。
            </div>
            <div class="ap-hint">勾选父级部门会自动勾选全部下级；只勾下级时父级显示为半选，并会一并纳入本次范围。</div>
          </div>
          <div class="ap-box">
            <div class="ap-box-title">条款（按所选标准过滤，可多选）</div>
            <el-tree
              ref="clauseTreeRef"
              :data="clauseTreeData"
              show-checkbox
              node-key="Code"
              :props="{ label: 'label', children: 'children' }"
              default-expand-all
            />
            <div v-if="!clauseTreeData.length" class="ap-hint">请返回上一步选择标准</div>
            <div v-else class="ap-hint">勾选父级条款会自动勾选全部下级；只勾下级时父级显示为半选，并会一并纳入本次范围。</div>
          </div>
        </div>
        <div class="ap-add-row">
          <el-select v-model="ownerCode" filterable clearable placeholder="负责审核员（必选）" class="ap-owner">
            <el-option
              v-for="m in members"
              :key="m.Code"
              :label="m.Name + (m.Title ? ' · ' + m.Title : '')"
              :value="m.Code"
            />
          </el-select>
          <el-button type="primary" @click="addSelection">+ 加入已选</el-button>
          <span class="ap-hint">可只勾条款（不选部门），或勾多个部门 + 条款一起加入。每次加入生成一条「选择组合」，并落到该审核员名下。</span>
        </div>
        <div v-if="!members.length" class="ap-hint">
          专家组成员池为空：请先在「专家组设置」维护审核员，否则组合只能留空、后续在认证准备阶段再分配。
        </div>
        <div class="ap-selected">
          <div class="ap-box-title">已选页面（可删除）</div>
          <el-table :data="selections" border size="small">
            <el-table-column label="部门" min-width="160">
              <template #default="{ row }">
                <template v-if="row.deptNames.length">
                  <el-tag v-for="(n, i) in row.deptNames" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
                </template>
                <el-tag v-else type="info" size="small">（无部门）</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="条款" min-width="260">
              <template #default="{ row }">
                <el-tag v-for="(n, i) in row.clauseNumbers" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="负责审核员" width="200">
              <template #default="{ row }">
                <el-select
                  v-model="row.ownerCode"
                  size="small"
                  filterable
                  clearable
                  placeholder="未分配"
                  @change="onOwnerChange(row as Selection)"
                >
                  <el-option
                    v-for="m in members"
                    :key="m.Code"
                    :label="m.Name + (m.Title ? ' · ' + m.Title : '')"
                    :value="m.Code"
                  />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="80" align="center">
              <template #default="{ $index }">
                <el-button link type="danger" @click="selections.splice($index, 1)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <div v-if="!selections.length" class="ap-hint">尚未加入任何选择组合</div>
        </div>
      </div>

      <!-- ③ 确认生成 -->
      <div v-else>
        <div class="ap-box-title">计划主表</div>
        <el-descriptions :column="2" border size="small" class="ap-desc">
          <el-descriptions-item label="计划名称">{{ previewMaster.Name }}</el-descriptions-item>
          <el-descriptions-item label="企业">{{ entName(draft.enterprise) }}</el-descriptions-item>
          <el-descriptions-item label="阶段">{{ stageName(draft.stage) }}</el-descriptions-item>
          <el-descriptions-item label="认证标准">{{ draft.standards.map(stdName).join('、') }}</el-descriptions-item>
        </el-descriptions>
        <div class="ap-box-title">计划明细（{{ selections.length }} 项）</div>
        <el-table :data="selections" border size="small">
          <el-table-column label="条款序号（标准简称 + 编号）" min-width="280">
            <template #default="{ row }">
              <el-tag v-for="(n, i) in row.clauseNumbers" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="部门名称" min-width="180">
            <template #default="{ row }">
              <template v-if="row.deptNames.length">
                <el-tag v-for="(n, i) in row.deptNames" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
              </template>
              <el-tag v-else type="info" size="small">（无部门）</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="负责审核员" width="120">
            <template #default="{ row }">
              <el-tag v-if="row.ownerName" size="small" class="ap-tag">{{ row.ownerName }}</el-tag>
              <el-tag v-else type="warning" size="small">未分配</el-tag>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <template #footer>
        <el-button @click="wizardVisible = false">取消</el-button>
        <el-button v-if="step > 1" @click="step--">上一步</el-button>
        <el-button v-if="step < 3" type="primary" @click="next">下一步</el-button>
        <el-button v-if="step === 3" type="primary" :loading="saving" @click="savePlan">保存计划</el-button>
      </template>
    </el-dialog>

    <!-- 查看详情 -->
    <el-dialog v-model="viewVisible" :title="viewPlan?.Name || '计划详情'" width="900px">
      <template v-if="viewPlan">
        <el-descriptions :column="2" border size="small" class="ap-desc">
          <el-descriptions-item label="企业">{{ entName(viewPlan.EnterpriseCode) }}</el-descriptions-item>
          <el-descriptions-item label="阶段">{{ stageName(viewPlan.StageCode) }}</el-descriptions-item>
          <el-descriptions-item label="认证标准" :span="2">{{ splitCsv(viewPlan.Standards).map(stdName).join('、') }}</el-descriptions-item>
        </el-descriptions>
        <div class="ap-box-title">计划明细（{{ viewDetails.length }} 项）</div>
        <el-table :data="viewDetails" border size="small">
          <el-table-column label="条款序号" min-width="280">
            <template #default="{ row }">
              <el-tag v-for="(n, i) in splitCsv(row.ClauseNumbers)" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="部门名称" min-width="180">
            <template #default="{ row }">
              <template v-if="splitCsv(row.DeptNames).length">
                <el-tag v-for="(n, i) in splitCsv(row.DeptNames)" :key="i" size="small" class="ap-tag">{{ n }}</el-tag>
              </template>
              <el-tag v-else type="info" size="small">（无部门）</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="负责审核员" width="120">
            <template #default="{ row }">
              <el-tag v-if="row.OwnerName" size="small" class="ap-tag">{{ row.OwnerName }}</el-tag>
              <el-tag v-else type="warning" size="small">未分配</el-tag>
            </template>
          </el-table-column>
        </el-table>
      </template>
      <template #footer>
        <el-button @click="viewVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.audit-plan-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-3, 12px);
}
.ap-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--yzh-space-3, 12px);
}
.ap-title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.ap-steps {
  margin-bottom: var(--yzh-space-4, 16px);
}
.ap-field {
  margin-bottom: var(--yzh-space-3, 12px);
}
.ap-field > label {
  display: block;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-secondary);
  margin-bottom: var(--yzh-space-1, 4px);
}
.ap-full {
  width: 100%;
}
.ap-chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
}
.ap-chip {
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border: 1px solid var(--el-border-color);
  border-radius: var(--yzh-radius-base, 6px);
  cursor: pointer;
  user-select: none;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}
.ap-chip.sel {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.ap-two-col {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: var(--yzh-space-3, 12px);
}
.ap-box {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--yzh-radius-base, 6px);
  padding: var(--yzh-space-2, 8px);
  max-height: 42vh;
  overflow: auto;
}
.ap-box-title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  color: var(--el-text-color-regular);
  margin-bottom: var(--yzh-space-2, 8px);
}
.ap-add-row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin: var(--yzh-space-3, 12px) 0;
  flex-wrap: wrap;
}
.ap-owner {
  width: 240px;
}
.ap-hint {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-secondary);
}
.ap-selected {
  margin-top: var(--yzh-space-2, 8px);
}
.ap-desc {
  margin-bottom: var(--yzh-space-3, 12px);
}
.ap-tag {
  margin: 0 var(--yzh-space-1, 4px) var(--yzh-space-1, 4px) 0;
}
</style>
