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
 *   阶段   GET  /api/Auditor/CertQuery/stages
 *   部门   POST /api/Auditor/EnterpriseDept/filter （需先选企业）
 *   保存   POST /api/Auditor/AuditPlan/save
 *   明细   GET  /api/Auditor/AuditPlan/details?planCode=xx
 *
 * ★ 简称只在「条款序号」上作为前缀（如 e 4.3），不替换标准名称。
 */
import { ref, reactive, computed, onMounted } from 'vue'
import {
  ElButton, ElSelect, ElOption, ElDialog, ElTable, ElTableColumn,
  ElTree, ElTag, ElSteps, ElStep, ElDescriptions, ElDescriptionsItem, ElMessage,
} from 'element-plus'
import { yzhApi } from '@yzh-core/api/client'
import router from '@/router'
import { createCertTask } from '@share/api/auditor/cert-check-task'
import AuditPrepWorkbench from './audit-prep-workbench.vue'

interface StandardItem { Code: string; StandardCode: string; StandardName: string; Abbrev: string }
interface StageItem { Code: string; StageCode: string; StageName: string; Category: string }
interface EnterpriseItem { Code: string; Name: string }
interface DeptItem { Code: string; DeptName: string; ParentCode: string | null }
interface ClauseItem { Code: string; StandardCode: string; ClauseNumber: string; Title: string; ParentCode: string | null }
interface PlanRow { Code: string; Name: string; EnterpriseCode: string; StageCode: string; Standards: string; IsValid: number; CreateTime?: string }
interface Selection { deptCodes: string[]; deptNames: string[]; clauseCodes: string[]; clauseNumbers: string[] }

const plans = ref<PlanRow[]>([])
const enterprises = ref<EnterpriseItem[]>([])
const standards = ref<StandardItem[]>([])
const stages = ref<StageItem[]>([])
const depts = ref<DeptItem[]>([])
const clausesByStd = ref<Record<string, ClauseItem[]>>({})

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

// ──── 加载 ────
async function loadPlans() {
  const res = await yzhApi.post('/api/Auditor/AuditPlan/filter', { Page: 1, PageSize: 100, Filters: [] })
  plans.value = res?.data?.Items || []
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
async function loadStages() {
  const res = await yzhApi.request('/api/Auditor/CertQuery/stages', { method: 'GET' })
  stages.value = res?.data || []
}
async function loadDepts(entCode: string) {
  const res = await yzhApi.post('/api/Auditor/EnterpriseDept/filter', {
    Page: 1, PageSize: 500,
    Filters: [
      { Field: 'EnterpriseCode', Operator: 'eq', Value: entCode },
      { Field: 'IsValid', Operator: 'eq', Value: '1' },
    ],
  })
  depts.value = res?.data?.Items || []
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
const deptTreeRef = ref<any>(null)
const clauseTreeRef = ref<any>(null)

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

function openWizard() {
  step.value = 1
  draft.enterprise = ''
  draft.standards = []
  draft.stage = ''
  selections.value = []
  depts.value = []
  clausesByStd.value = {}
  wizardVisible.value = true
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

function addSelection() {
  const deptCodes: string[] = (deptTreeRef.value?.getCheckedKeys() || []).filter((c: string) => !c.startsWith('STD:'))
  const clauseCodes: string[] = (clauseTreeRef.value?.getCheckedKeys() || []).filter((c: string) => !c.startsWith('STD:'))
  if (!clauseCodes.length) { ElMessage.warning('请至少勾选一个条款'); return }
  const deptNames = deptCodes.map(c => deptName(c))
  const clauseNumbers = clauseCodes.map(c => {
    const cl = clauseMap.value[c]
    return cl ? `${stdAbbrev(cl.StandardCode)} ${cl.ClauseNumber}` : c
  })
  selections.value.push({ deptCodes, deptNames, clauseCodes, clauseNumbers })
  deptTreeRef.value?.setCheckedKeys([])
  clauseTreeRef.value?.setCheckedKeys([])
}

async function savePlan() {
  if (!selections.value.length) { ElMessage.warning('请先添加选择组合'); return }
  saving.value = true
  try {
    const payload = {
      Plan: {
        Code: '',
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
      })),
    }
    const res = await yzhApi.post('/api/Auditor/AuditPlan/save', payload)
    if (res?.success) {
      ElMessage.success('计划已保存')
      wizardVisible.value = false
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

// ──── 启动任务 ────
const creatingTask = ref('')

async function startTask(row: any, taskKind: string) {
  creatingTask.value = taskKind
  try {
    const res = await createCertTask({ PlanCode: row.Code, TaskKind: taskKind, RunMode: 'full' })
    const taskInfo = (res as any)?.data
    ElMessage.success({ message: `已创建${taskKind === 'prep' ? '准备' : '检查'}任务：${taskInfo?.Name || taskInfo?.Code}`, duration: 3000 })
    await router.push({ path: '/audit-task', query: { taskCode: taskInfo?.Code } })
  } catch (e: any) {
    ElMessage.error({ message: '创建任务失败：' + (e?.message || ''), duration: 5000 })
  } finally {
    creatingTask.value = ''
  }
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

// ──── 认证工作台（统一视图）────
const workbenchVisible = ref(false)
const workbenchPlanCode = ref('')
const workbenchPlanName = ref('')
function openWorkbench(row: PlanRow) {
  workbenchPlanCode.value = row.Code
  workbenchPlanName.value = row.Name || ''
  workbenchVisible.value = true
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
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">{{ row.IsValid === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="300" align="center">
        <template #default="{ row }">
          <el-button link type="primary" @click="openView(row)">查看</el-button>
          <el-button link type="primary" @click="openWorkbench(row)">进入工作台</el-button>
          <el-button link type="warning" :loading="creatingTask === 'prep'" :disabled="!!creatingTask" @click="startTask(row, 'prep')">开启准备</el-button>
          <el-button link type="success" :loading="creatingTask === 'onsite'" :disabled="!!creatingTask" @click="startTask(row, 'onsite')">开启检查</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新建向导 -->
    <el-dialog v-model="wizardVisible" title="新建认证计划" width="1000px" :close-on-click-modal="false" destroy-on-close>
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
          <label>当前阶段</label>
          <el-select v-model="draft.stage" placeholder="请选择阶段" class="ap-full">
            <el-option v-for="s in stages" :key="s.Code" :label="s.StageName" :value="s.Code" />
          </el-select>
        </div>
        <div class="ap-hint">第一步只定范围：选企业、勾选本次要认证的标准（可多个）、选阶段。第二步再勾条款与部门。</div>
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
              check-strictly
              node-key="Code"
              :props="{ label: 'label', children: 'children' }"
              default-expand-all
            />
            <div v-if="!deptTreeData.length" class="ap-hint">该企业暂无部门数据</div>
          </div>
          <div class="ap-box">
            <div class="ap-box-title">条款（按所选标准过滤，可多选）</div>
            <el-tree
              ref="clauseTreeRef"
              :data="clauseTreeData"
              show-checkbox
              check-strictly
              node-key="Code"
              :props="{ label: 'label', children: 'children' }"
              default-expand-all
            />
            <div v-if="!clauseTreeData.length" class="ap-hint">请返回上一步选择标准</div>
          </div>
        </div>
        <div class="ap-add-row">
          <el-button type="primary" @click="addSelection">+ 加入已选</el-button>
          <span class="ap-hint">可只勾条款（不选部门），或勾多个部门 + 条款一起加入。每次加入生成一条「选择组合」。</span>
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
        </el-table>
      </template>
      <template #footer>
        <el-button @click="viewVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 认证工作台（V2 准备+检查一体化） -->
    <el-dialog
      v-model="workbenchVisible"
      :title="workbenchPlanName ? `认证工作台 - ${workbenchPlanName}` : '认证工作台'"
      width="96%"
      top="2vh"
      :close-on-click-modal="false"
      destroy-on-close
      class="workbench-dialog"
    >
      <AuditPrepWorkbench
        v-if="workbenchVisible"
        :plan-code="workbenchPlanCode"
        :plan-name="workbenchPlanName"
      />
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
.workbench-dialog :deep(.el-dialog__body) {
  padding: 0;
  height: 88vh;
  overflow: hidden;
}
</style>
