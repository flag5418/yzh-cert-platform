<script setup lang="ts">
/**
 * 任务建立向导（专家端 · 任务系统第 2 页 · 路由 `/tasks/create`）
 *
 * 四步：① 基本信息（企业 + 阶段 + 分类 + 业务锁实时判定）
 *       ② 任务来源（NEW / REDO / PATCH）
 *       ③ 执行范围（FULL / PARTIAL + 候选检查项勾选）
 *       ④ 确认并创建
 *
 * ★ 本页是**任务系统最核心的入口**，也是最容易做错的地方，三条铁律：
 *
 *  ① **业务锁（D36）必须在第 1 步就给出结论**，而不是等到点「创建」才报错。
 *     判定在后端 `POST /lock-check`（应用层先查，给「是哪条任务挡着」的友好提示），
 *     DB 生成列 + 唯一索引兜底。⛔ 前端不得自己实现一套锁判定。
 *
 *  ② **候选检查项必须来自后端**（`POST /candidates`）——
 *     它已经把「阶段口径（`PhaseCode` = `cert_cert_stage.Code`）」「标准过滤」
 *     「上次检查时间（D30）」「是否已配工作流」都算好了。前端只负责勾选。
 *
 *  ③ **标准必须选「该企业在该阶段下已关联的」** —— 后端 `CreateTaskAsync` 会校验，
 *     未关联的标准会被拒。本页从 `EnterpriseStage/checkTree` 取已关联项，
 *     ⛔ 不要用「全部标准」下拉（会让用户选到建不出来的组合）。
 *
 * ★ 本页的候选表**不是配置驱动**（走 `getCandidates` 而不是 EntityConfig），
 *   所以列在这里内联声明 —— 这是向导的固有形态，不违反「配置驱动」原则。
 */
import { computed, nextTick, onActivated, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { Page, PageParams, YzhTableColumn } from '@yzh-core'
import { YzhTable } from '@yzh-core'
import {
  createTask,
  getCandidates,
  getTaskDetail,
  lockCheck,
  type LockCheckResult,
  type TaskCandidate,
  type TaskCreateResult,
} from '@share/api/auditor/expert-task'
import {
  getEnterpriseTreeRoot,
  getStageStandardTree,
} from '@share/api/cert/enterprise-stage'
import {
  JUDGE_MODE_MAP,
  SCOPE_TYPE_OPTIONS,
  TASK_SOURCE_OPTIONS,
  TASK_TYPE_OPTIONS,
} from '@share/constants/expert-task'
import { formatDateTime } from '@share/utils/format'

const route = useRoute()
const router = useRouter()

// ══════════════════════════════════════════════════════════════════════
// 一、表单状态
// ══════════════════════════════════════════════════════════════════════

const step = ref(0)

const form = reactive({
  TaskName: '',
  TaskType: 'NC_CHECK',
  EnterpriseCode: '',
  StageCode: '',
  TaskSource: 'NEW',
  ScopeType: 'FULL',
  Remark: '',
})

// ── 下拉数据 ──

interface Option {
  Code: string
  Name: string
  /** 标准人读编号（如 iso9001-2015） */
  No?: string
}

/** 带所属阶段的「阶段 × 标准」叶子（同一标准可挂在多个阶段下） */
interface StdLeaf extends Option {
  /** 所属阶段 = `cert_cert_stage.Code`（与 `form.StageCode` 同口径） */
  StageCode: string
}

const enterprises = ref<Option[]>([])
const stages = ref<Option[]>([])

/**
 * ★ 该企业**全部**已关联的「阶段 × 标准」叶子（原始数据，**允许同一标准重复**）。
 *
 * ⛔ 不要直接把 `standards` 绑到这个 ref —— `checkTree` 返回的是「每个阶段各一份
 *   自己的标准子节点」，同一标准挂在 N 个阶段下就会出现 N 条（实测 G4测试企业甲
 *   挂 2 阶段 × 2 标准 = 4 条），直接渲染 = 用户看到重复勾选框。
 */
const linkedStdLeaves = ref<StdLeaf[]>([])

/**
 * 当前阶段下已关联的标准（按 `Code` 去重）—— 页面真正渲染的列表。
 *
 * 口径：`form.StageCode`（`cert_cert_stage.Code`）过滤 + `Code` 去重。
 * 阶段未选 ⇒ 空列表（标准只能在选定阶段下谈）。
 */
const standards = computed<Option[]>(() => {
  const seen = new Set<string>()
  const out: Option[] = []
  for (const leaf of linkedStdLeaves.value) {
    if (!leaf.Code || leaf.StageCode !== form.StageCode || seen.has(leaf.Code)) continue
    seen.add(leaf.Code)
    out.push({ Code: leaf.Code, Name: leaf.Name, No: leaf.No })
  }
  return out
})

const selectedStandards = ref<string[]>([])

const enterprisesLoading = ref(false)
const treeLoading = ref(false)

// ── 业务锁 ──

const lock = ref<LockCheckResult | null>(null)
const lockLoading = ref(false)

// ── 候选检查项 ──

const candidates = ref<TaskCandidate[]>([])
const candidateLoading = ref(false)
const pickedItemCodes = ref<Set<string>>(new Set())

// ── 创建结果 ──

const creating = ref(false)
const created = ref<TaskCreateResult | null>(null)

// ══════════════════════════════════════════════════════════════════════
// 二、派生
// ══════════════════════════════════════════════════════════════════════

const taskTypeLabel = computed(
  () => TASK_TYPE_OPTIONS.find((o) => o.value === form.TaskType)?.label ?? form.TaskType,
)

const enterpriseName = computed(
  () => enterprises.value.find((o) => o.Code === form.EnterpriseCode)?.Name ?? '',
)

const stageName = computed(() => stages.value.find((o) => o.Code === form.StageCode)?.Name ?? '')

const taskSourceLabel = computed(
  () => TASK_SOURCE_OPTIONS.find((o) => o.value === form.TaskSource)?.label ?? form.TaskSource,
)

const scopeLabel = computed(
  () => SCOPE_TYPE_OPTIONS.find((o) => o.value === form.ScopeType)?.label ?? form.ScopeType,
)

/** 第 1 步是否可继续 */
const step0Ok = computed(
  () =>
    form.TaskName.trim().length > 0 &&
    form.EnterpriseCode.length > 0 &&
    form.StageCode.length > 0 &&
    lock.value?.CanCreate === true,
)

/** 第 3 步是否可继续 */
const step2Ok = computed(() => {
  if (selectedStandards.value.length === 0) return false
  if (form.ScopeType === 'PARTIAL') return pickedItemCodes.value.size > 0
  return candidates.value.length > 0
})

const pickedCount = computed(() => pickedItemCodes.value.size)

/** 未配工作流的候选数（执行时必跳过 —— 必须预警） */
const noWorkflowCount = computed(() => candidates.value.filter((c) => !c.HasWorkflow).length)

// ══════════════════════════════════════════════════════════════════════
// 三、加载：企业 / 阶段 / 标准
// ══════════════════════════════════════════════════════════════════════

async function loadEnterprises() {
  enterprisesLoading.value = true
  try {
    const nodes = await getEnterpriseTreeRoot()
    enterprises.value = nodes.map((n) => ({ Code: n.Code, Name: n.Name }))
  } finally {
    enterprisesLoading.value = false
  }
}

/**
 * 拉取该企业的「阶段 → 标准」关联态。
 *
 * ★ 只有 `CheckFlag = true` 的标准才是「已关联」的 ——
 * 任务只能建在已关联的组合上（后端会校验，前端先挡）。
 *
 * ★ `checkTree` 返回的是**扁平列表**，每个阶段下挂自己的一份标准子节点
 * （`ParentCode` = 阶段节点 Code，`Extra.StageCode` = `cert_cert_stage.Code`）。
 * 所以这里只落「原始叶子」，按阶段过滤 + 去重交给 `standards` 计算属性。
 */
async function loadStagesAndStandards() {
  stages.value = []
  linkedStdLeaves.value = []
  selectedStandards.value = []
  if (!form.EnterpriseCode) return

  treeLoading.value = true
  try {
    const nodes = await getStageStandardTree(form.EnterpriseCode)
    const stageRows = nodes.filter((n) => n.NodeType === 'stage')
    const stdRows = nodes.filter((n) => n.NodeType === 'standard' && n.CheckFlag === true)

    // 只保留「至少关联了 1 个标准」的阶段 —— 空阶段建不出任务
    const linkedStageCodes = new Set(stdRows.map((n) => String(n.Extra?.StageCode ?? '')))
    stages.value = stageRows
      .filter((n) => linkedStageCodes.has(String(n.Extra?.StageCode ?? '')))
      .map((n) => ({ Code: String(n.Extra?.StageCode ?? ''), Name: n.Name }))

    linkedStdLeaves.value = stdRows.map((n) => ({
      Code: String(n.Extra?.StandardCode ?? ''),
      Name: String(n.Extra?.StandardName ?? n.Name),
      No: String(n.Extra?.StandardNo ?? ''),
      StageCode: String(n.Extra?.StageCode ?? ''),
    }))
  } finally {
    treeLoading.value = false
  }
}

/** 选中阶段后 → 该阶段下的已关联标准（已去重），默认全选 */
function syncStandardsByStage() {
  selectedStandards.value = standards.value.map((s) => s.Code)
}

// ══════════════════════════════════════════════════════════════════════
// 四、业务锁实时判定
// ══════════════════════════════════════════════════════════════════════

async function runLockCheck() {
  lock.value = null
  if (!form.EnterpriseCode || !form.StageCode) return

  lockLoading.value = true
  try {
    lock.value = await lockCheck({
      EnterpriseCode: form.EnterpriseCode,
      StageCode: form.StageCode,
      TaskType: form.TaskType,
    })
  } catch (e) {
    lock.value = {
      CanCreate: false,
      Message: (e as Error)?.message || '业务锁判定失败，请稍后重试',
      ExistingTaskCount: 0,
      CandidateCount: 0,
      AvgRoundCount: 0,
    }
  } finally {
    lockLoading.value = false
  }
}

// ══════════════════════════════════════════════════════════════════════
// 五、候选检查项
// ══════════════════════════════════════════════════════════════════════

/** 候选表格实例（★ 数据异步填入 ⇒ 必须显式 refresh） */
const candidateTableRef = ref<any>(null)

async function loadCandidates() {
  candidates.value = []
  pickedItemCodes.value = new Set()
  if (!form.EnterpriseCode || !form.StageCode || selectedStandards.value.length === 0) return

  candidateLoading.value = true
  try {
    candidates.value = await getCandidates({
      TaskName: form.TaskName,
      TaskType: form.TaskType,
      EnterpriseCode: form.EnterpriseCode,
      StageCode: form.StageCode,
      ScopeType: form.ScopeType,
      TaskSource: form.TaskSource,
      StandardCodes: selectedStandards.value,
    })
    // ★ 默认勾选「系统建议」（从未检查过 / 上次不符）—— 专家可再改
    pickedItemCodes.value = new Set(
      candidates.value.filter((c) => c.Suggested).map((c) => c.ItemCode),
    )
    // ★ 候选是异步填的，YzhTable 挂载时读到的是空数组 ⇒ 必须显式 refresh
    await nextTick()
    candidateTableRef.value?.refresh?.()
  } finally {
    candidateLoading.value = false
  }
}

function isPicked(itemCode: string): boolean {
  return pickedItemCodes.value.has(itemCode)
}

function togglePick(itemCode: string) {
  const next = new Set(pickedItemCodes.value)
  if (next.has(itemCode)) next.delete(itemCode)
  else next.add(itemCode)
  pickedItemCodes.value = next
}

function pickAll(suggestedOnly: boolean) {
  pickedItemCodes.value = new Set(
    candidates.value.filter((c) => !suggestedOnly || c.Suggested).map((c) => c.ItemCode),
  )
}

function clearPick() {
  pickedItemCodes.value = new Set()
}

// ══════════════════════════════════════════════════════════════════════
// 六、候选表（内联列声明 + 本地分页）
// ══════════════════════════════════════════════════════════════════════

const candidateColumns: YzhTableColumn<TaskCandidate>[] = [
  { prop: 'Picked', label: '选择', width: 64, align: 'center', slot: true },
  { prop: 'StandardName', label: '标准', width: 160 },
  { prop: 'ItemNumber', label: '编号', width: 150, showOverflowTooltip: true },
  { prop: 'ItemName', label: '检查项 / 章节', minWidth: 240, showOverflowTooltip: true },
  { prop: 'ClauseNumber', label: '条款', width: 100 },
  {
    prop: 'JudgeMode',
    label: '判定方式',
    width: 110,
    formatter: (v: unknown) => (v ? JUDGE_MODE_MAP[String(v)] ?? String(v) : '—'),
  },
  {
    prop: 'LastAuditedTime',
    label: '上次检查',
    width: 170,
    formatter: (v: unknown) => (v ? formatDateTime(String(v)) : '从未检查'),
  },
  {
    prop: 'HasWorkflow',
    label: '工作流',
    width: 100,
    formatter: (v: unknown) => (v === true ? '已配置' : '未配置'),
  },
]

/** 候选表数据源 = 本地数组分页（不请求后端：候选已一次性取回） */
async function candidateLoader(params: PageParams): Promise<Page<TaskCandidate>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  return {
    rows: candidates.value.slice((page - 1) * rows, page * rows),
    total: candidates.value.length,
  }
}

// ══════════════════════════════════════════════════════════════════════
// 七、步骤控制
// ══════════════════════════════════════════════════════════════════════

async function nextStep() {
  if (step.value === 0) {
    if (!step0Ok.value) {
      ElMessage.warning(
        lock.value && !lock.value.CanCreate
          ? lock.value.Message
          : '请填写任务名称，并选择企业与阶段',
      )
      return
    }
    step.value = 1
    return
  }
  if (step.value === 1) {
    step.value = 2
    // 进入第 3 步才拉候选（此时企业/阶段/标准/类型都已确定）
    // ⚠️ 这里必须 await：否则「下一步」时候选还没回来，用户会看到空表并以为没数据
    syncStandardsByStage()
    await loadCandidates()
    return
  }
  if (step.value === 2) {
    if (!step2Ok.value) {
      ElMessage.warning(
        selectedStandards.value.length === 0
          ? '请至少勾选一个标准'
          : '「只跑勾选项」模式下，请至少勾选一个检查项',
      )
      return
    }
    step.value = 3
  }
}

function prevStep() {
  if (step.value > 0) step.value -= 1
}

async function onEnterpriseChange() {
  form.StageCode = ''
  await loadStagesAndStandards()
  await runLockCheck()
}

async function onStageChange() {
  syncStandardsByStage()
  await runLockCheck()
}

async function onTaskTypeChange() {
  await runLockCheck()
}

/** 标准勾选变化 → 候选必须重拉（后端按 StandardCodes 过滤） */
async function onStandardsChange() {
  await loadCandidates()
}

// ══════════════════════════════════════════════════════════════════════
// 八、创建
// ══════════════════════════════════════════════════════════════════════

async function doCreate() {
  creating.value = true
  try {
    const res = await createTask({
      TaskName: form.TaskName.trim(),
      TaskType: form.TaskType,
      EnterpriseCode: form.EnterpriseCode,
      StageCode: form.StageCode,
      ScopeType: form.ScopeType,
      TaskSource: form.TaskSource,
      StandardCodes: selectedStandards.value,
      ItemCodes: form.ScopeType === 'PARTIAL' ? [...pickedItemCodes.value] : null,
      Remark: form.Remark || null,
    })
    created.value = res
    ElMessage.success('任务创建成功')
  } catch (e) {
    // F-3：谁 catch 谁弹（BizError 已带后端 err 文案）
    ElMessage.error((e as Error)?.message || '任务创建失败')
  } finally {
    creating.value = false
  }
}

function goDetail() {
  if (!created.value) return
  void router.push({ name: 'TaskDetail', params: { code: created.value.TaskCode } })
}

function goList() {
  void router.push({ name: 'Tasks' })
}

// ══════════════════════════════════════════════════════════════════════
// 九、初始化（支持「从已有任务出发」预填）
// ══════════════════════════════════════════════════════════════════════

/** 从已有任务预填（D25 的 REDO / PATCH 入口） */
async function prefillFromTask(code: string) {
  const detail = await getTaskDetail(code)
  const t = detail.Task
  form.TaskName = `${t.TaskName}（重执行）`
  form.TaskType = t.TaskType
  form.EnterpriseCode = t.EnterpriseCode
  form.StageCode = t.StageCode
  form.TaskSource = 'REDO'
  form.Remark = `基于任务 ${t.TaskNumber} 发起`

  await loadStagesAndStandards()
  syncStandardsByStage()
  await runLockCheck()
}

onMounted(async () => {
  await loadEnterprises()
  const from = route.query.from
  if (typeof from === 'string' && from.length > 0) {
    try {
      await prefillFromTask(from)
      ElMessage.info('已按所选任务预填，请确认后继续')
    } catch (e) {
      ElMessage.warning((e as Error)?.message || '预填失败，请手动选择企业与阶段')
    }
  }
})

// ══════════════════════════════════════════════════════════════════════
// 十、缓存复用时的复位（★ 2026-10-07）
// ══════════════════════════════════════════════════════════════════════

/**
 * 回到本页时把向导复位。
 *
 * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件 ⇒ 第二次进入
 *    `/tasks/create` 时 `onMounted` ⛔ 不再触发，页面上会**残留上次填的内容**
 *    和「创建成功」面板（用户报的「操作反直觉」之一）。
 *
 * ⚠️ 第一次 `onActivated` 与 `onMounted` 同时发生（首载已经初始化过），
 *    用标记跳过 —— 否则会把 `onMounted` 里的「从已有任务出发」预填冲掉。
 */
function resetWizard() {
  step.value = 0
  form.TaskName = ''
  form.TaskType = 'NC_CHECK'
  form.EnterpriseCode = ''
  form.StageCode = ''
  form.TaskSource = 'NEW'
  form.ScopeType = 'FULL'
  form.Remark = ''

  stages.value = []
  linkedStdLeaves.value = []
  selectedStandards.value = []
  lock.value = null
  candidates.value = []
  pickedItemCodes.value = new Set()
  created.value = null
}

let activatedOnce = false
onActivated(() => {
  if (!activatedOnce) {
    activatedOnce = true
    return
  }
  resetWizard()
})
</script>

<template>
  <div class="wiz">
    <!-- ════ 顶部：步骤条 ════ -->
    <div class="wiz__head">
      <div class="wiz__title">创建任务</div>
      <el-steps :active="step" align-center finish-status="success" class="wiz__steps">
        <el-step title="基本信息" description="企业 · 阶段 · 分类" />
        <el-step title="任务来源" description="全新 / 重执行 / 局部" />
        <el-step title="执行范围" description="检查项勾选" />
        <el-step title="确认创建" description="核对并提交" />
      </el-steps>
    </div>

    <!-- ════ 主体 ════ -->
    <div class="wiz__body">
      <!-- ────────── 步骤 1：基本信息 ────────── -->
      <div v-if="step === 0" class="wiz__pane">
        <el-form label-width="110px" class="wiz__form">
          <el-form-item label="任务名称" required>
            <el-input
              v-model="form.TaskName"
              maxlength="200"
              show-word-limit
              placeholder="例如：2026 年度监督审核 · NC 检查"
            />
          </el-form-item>

          <el-form-item label="任务分类" required>
            <el-radio-group v-model="form.TaskType" @change="onTaskTypeChange">
              <el-radio-button v-for="o in TASK_TYPE_OPTIONS" :key="o.value" :value="o.value">
                {{ o.label }}
              </el-radio-button>
            </el-radio-group>
            <div class="wiz__hint">
              {{ TASK_TYPE_OPTIONS.find((o) => o.value === form.TaskType)?.desc }}
            </div>
          </el-form-item>

          <el-form-item label="企业" required>
            <el-select
              v-model="form.EnterpriseCode"
              :loading="enterprisesLoading"
              filterable
              placeholder="请选择企业（本工作区）"
              style="width: 420px"
              @change="onEnterpriseChange"
            >
              <el-option v-for="e in enterprises" :key="e.Code" :label="e.Name" :value="e.Code" />
            </el-select>
            <div v-if="enterprises.length === 0 && !enterprisesLoading" class="wiz__hint wiz__hint--warn">
              本工作区还没有企业 —— 请先到「企业管理」建档。
            </div>
          </el-form-item>

          <el-form-item label="认证阶段" required>
            <el-select
              v-model="form.StageCode"
              :loading="treeLoading"
              :disabled="!form.EnterpriseCode"
              placeholder="请先选择企业"
              style="width: 420px"
              @change="onStageChange"
            >
              <el-option v-for="s in stages" :key="s.Code" :label="s.Name" :value="s.Code" />
            </el-select>
            <div
              v-if="form.EnterpriseCode && !treeLoading && stages.length === 0"
              class="wiz__hint wiz__hint--warn"
            >
              该企业还没有关联任何「阶段 × 标准」—— 请先到「阶段标准关联」建立关联。
            </div>
          </el-form-item>

          <el-form-item label="备注">
            <el-input v-model="form.Remark" maxlength="500" placeholder="选填" />
          </el-form-item>
        </el-form>

        <!-- ── 业务锁实时判定（D36）── -->
        <div v-if="form.EnterpriseCode && form.StageCode" class="wiz__lock">
          <el-alert
            v-if="lockLoading"
            type="info"
            :closable="false"
            title="正在判定业务锁…"
            show-icon
          />
          <el-alert
            v-else-if="lock && lock.CanCreate"
            type="success"
            :closable="false"
            show-icon
          >
            <template #title>可以创建任务</template>
            <div class="wiz__lock-body">
              <div>{{ lock.Message }}</div>
              <div class="wiz__lock-meta">
                生效检查项 <b>{{ lock.CandidateCount }}</b> 项 ·
                同企业同阶段已有 <b>{{ lock.ExistingTaskCount }}</b> 个{{ taskTypeLabel }}任务 ·
                历史平均轮次 <b>{{ lock.AvgRoundCount }}</b>
              </div>
            </div>
          </el-alert>
          <el-alert v-else-if="lock" type="error" :closable="false" show-icon>
            <template #title>该组合已被占用，暂时不能新建任务</template>
            <div class="wiz__lock-body">
              <div>{{ lock.Message }}</div>
              <div v-if="lock.BlockingTaskCode" class="wiz__lock-meta">
                挡着的任务：<b>{{ lock.BlockingTaskNumber }}</b> {{ lock.BlockingTaskName }}
                <el-tag size="small" type="warning" effect="plain" class="wiz__lock-tag">
                  {{ lock.BlockingExecStatus }}
                </el-tag>
                <el-button
                  link
                  type="primary"
                  @click="router.push({ name: 'TaskDetail', params: { code: lock.BlockingTaskCode } })"
                >
                  去看这条任务
                </el-button>
              </div>
              <div class="wiz__lock-meta">
                规则：同一「企业 + 阶段 + 分类」同时只允许一个未结束的任务；
                该任务跑完（执行状态 = 已完成）后自动解锁。
              </div>
            </div>
          </el-alert>
        </div>
      </div>

      <!-- ────────── 步骤 2：任务来源 ────────── -->
      <div v-else-if="step === 1" class="wiz__pane">
        <div class="wiz__section-title">这次执行属于哪种情况？</div>
        <el-radio-group v-model="form.TaskSource" class="wiz__cards">
          <el-radio v-for="o in TASK_SOURCE_OPTIONS" :key="o.value" :value="o.value" border>
            <div class="wiz__card-title">{{ o.label }}</div>
            <div class="wiz__card-desc">{{ o.desc }}</div>
          </el-radio>
        </el-radio-group>
        <div class="wiz__hint">
          任务来源只影响语义标注与历史追溯，不改变执行逻辑；真正的范围由下一步决定。
        </div>
      </div>

      <!-- ────────── 步骤 3：执行范围 ────────── -->
      <div v-else-if="step === 2" class="wiz__pane wiz__pane--wide">
        <div class="wiz__section-title">① 涉及标准</div>
        <div class="wiz__hint">
          只列出「该企业在该阶段下已关联」的标准 —— 未关联的组合后端会拒绝。
        </div>
        <el-checkbox-group
          v-model="selectedStandards"
          class="wiz__std-group"
          @change="onStandardsChange"
        >
          <el-checkbox v-for="s in standards" :key="s.Code" :value="s.Code" border>
            {{ s.Name }}
            <span class="wiz__std-no">{{ s.No }}</span>
          </el-checkbox>
        </el-checkbox-group>

        <div class="wiz__section-title">② 执行范围</div>
        <el-radio-group v-model="form.ScopeType">
          <el-radio v-for="o in SCOPE_TYPE_OPTIONS" :key="o.value" :value="o.value">
            {{ o.label }}
          </el-radio>
        </el-radio-group>

        <div class="wiz__section-title">
          ③ 候选检查项
          <span class="wiz__section-count">
            共 {{ candidates.length }} 项 · 已勾选 {{ pickedCount }} 项
            <template v-if="noWorkflowCount > 0">
              · <span class="wiz__warn">{{ noWorkflowCount }} 项未配工作流（执行时会跳过）</span>
            </template>
          </span>
        </div>

        <div v-loading="candidateLoading" class="wiz__cand">
          <YzhTable
            ref="candidateTableRef"
            :columns="candidateColumns"
            :data-loader="candidateLoader"
            :page-size="20"
            toolbar
            empty-text="该企业在本阶段下没有可执行的检查项（请先在后台配置规则或报告章节）"
          >
            <template #column-Picked="{ row }">
              <el-checkbox
                :model-value="isPicked(row.ItemCode)"
                @change="togglePick(row.ItemCode)"
              />
            </template>

            <template #column-ItemName="{ row }">
              <span>{{ row.ItemName }}</span>
              <el-tag v-if="row.Suggested" size="small" type="primary" effect="plain" class="wiz__sug">
                建议
              </el-tag>
            </template>

            <template #column-HasWorkflow="{ row }">
              <el-tag v-if="row.HasWorkflow" size="small" type="success" effect="plain">已配置</el-tag>
              <el-tag v-else size="small" type="warning" effect="plain">未配置</el-tag>
            </template>

            <template #toolbar-left>
              <el-button size="small" @click="pickAll(false)">全选</el-button>
              <el-button size="small" type="primary" plain @click="pickAll(true)">只选建议项</el-button>
              <el-button size="small" @click="clearPick()">全不选</el-button>
            </template>
          </YzhTable>
        </div>

        <div v-if="form.ScopeType === 'FULL'" class="wiz__hint">
          当前为「全部检查项」：上面勾选仅作参考，实际会执行该阶段下所有生效项。
        </div>
      </div>

      <!-- ────────── 步骤 4：确认 ────────── -->
      <div v-else class="wiz__pane">
        <template v-if="!created">
          <div class="wiz__section-title">请核对以下信息</div>
          <el-descriptions :column="2" border class="wiz__desc">
            <el-descriptions-item label="任务名称">{{ form.TaskName }}</el-descriptions-item>
            <el-descriptions-item label="任务分类">{{ taskTypeLabel }}</el-descriptions-item>
            <el-descriptions-item label="企业">{{ enterpriseName }}</el-descriptions-item>
            <el-descriptions-item label="认证阶段">{{ stageName }}</el-descriptions-item>
            <el-descriptions-item label="任务来源">{{ taskSourceLabel }}</el-descriptions-item>
            <el-descriptions-item label="执行范围">{{ scopeLabel }}</el-descriptions-item>
            <el-descriptions-item label="涉及标准">
              {{ selectedStandards.length }} 个
            </el-descriptions-item>
            <el-descriptions-item label="执行检查项">
              {{
                form.ScopeType === 'PARTIAL'
                  ? `${pickedCount} 项（勾选）`
                  : `${candidates.length} 项（全部）`
              }}
            </el-descriptions-item>
            <el-descriptions-item label="备注" :span="2">
              {{ form.Remark || '—' }}
            </el-descriptions-item>
          </el-descriptions>

          <el-alert type="info" :closable="false" show-icon class="wiz__tip">
            <template #title>创建后回列表点「启动任务」即可开始</template>
            <div>
              创建只落任务与检查项（不跑队列）。回到任务列表点「启动任务」，
              系统会先核对关键资料是否齐全：缺什么会当场弹窗让你补，补齐后自动开始执行；
              也可以选「运行跳过」直接跑 —— 未执行的规则 / 条款会在任务详情
              「未执行清单」里逐条列明原因。
            </div>
          </el-alert>
        </template>

        <template v-else>
          <el-result icon="success" title="任务创建成功">
            <template #sub-title>
              <div class="wiz__ok">
                <div><b>{{ created.TaskNumber }}</b> · {{ created.TaskName }}</div>
                <div>{{ created.Summary }}</div>
              </div>
            </template>
            <template #extra>
              <el-button type="primary" @click="goList">去任务列表启动</el-button>
              <el-button type="default" @click="goDetail">查看详情</el-button>
            </template>
          </el-result>
        </template>
      </div>
    </div>

    <!-- ════ 底部：导航 ════ -->
    <div v-if="!created" class="wiz__foot">
      <el-button @click="goList">取消</el-button>
      <div class="wiz__foot-right">
        <el-button v-if="step > 0" @click="prevStep">上一步</el-button>
        <el-button v-if="step < 3" type="primary" @click="nextStep">下一步</el-button>
        <el-button v-else type="primary" :loading="creating" @click="doCreate">确认创建</el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.wiz {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-container, #fff);
  border-radius: 4px;
  overflow: hidden;
}

.wiz__head {
  flex-shrink: 0;
  padding: 16px 24px 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.wiz__title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 12px;
}

.wiz__steps {
  padding-bottom: 12px;
}

.wiz__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 20px 24px;
}

.wiz__pane {
  max-width: 900px;
}

.wiz__pane--wide {
  max-width: none;
}

.wiz__form {
  max-width: 720px;
}

.wiz__hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.7;
  margin-top: 4px;
}

.wiz__hint--warn {
  color: var(--el-color-warning);
}

.wiz__warn {
  color: var(--el-color-warning);
}

.wiz__lock {
  margin-top: 8px;
  max-width: 900px;
}

.wiz__lock-body {
  font-size: 13px;
  line-height: 1.8;
}

.wiz__lock-meta {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.wiz__lock-tag {
  margin: 0 6px;
}

.wiz__section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 18px 0 8px;
}

.wiz__section-title:first-child {
  margin-top: 0;
}

.wiz__section-count {
  font-weight: 400;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-left: 8px;
}

.wiz__cards {
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: stretch;
}

.wiz__cards :deep(.el-radio) {
  height: auto;
  padding: 12px 16px;
  margin-right: 0;
}

.wiz__cards :deep(.el-radio__label) {
  white-space: normal;
}

.wiz__card-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.wiz__card-desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
  margin-top: 2px;
}

.wiz__std-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.wiz__std-no {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  margin-left: 6px;
}

.wiz__cand {
  height: 420px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  overflow: hidden;
}

.wiz__sug {
  margin-left: 6px;
}

.wiz__desc {
  margin-bottom: 16px;
}

.wiz__tip {
  margin-top: 8px;
}

.wiz__ok {
  line-height: 1.9;
  color: var(--el-text-color-regular);
}

.wiz__foot {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 24px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--yzh-color-bg-container, #fff);
}

.wiz__foot-right {
  display: flex;
  gap: 8px;
}
</style>
