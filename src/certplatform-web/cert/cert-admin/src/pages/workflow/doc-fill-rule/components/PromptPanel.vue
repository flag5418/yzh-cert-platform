<script setup lang="ts">
/**
 * 全文填写规则 —— 模板上挂的「全文填写提示词」版本列表 + 挂接/新建入口
 *
 * 【与「锚点规则」的关系】
 *   两条通路**并列、不互斥**：
 *   - 锚点规则（`cert_doc_template_anchor`）：确定性落笔，逐处 `{AnchorCode: value}`
 *   - 全文规则（本页）：把整份文档 + 结构化企业信息交给模型，让它一次性产出「值」
 *   引擎先跑锚点（便宜、可重放），未覆盖的部分才可能走全文。
 *
 * 【为什么必须显示「当前生效版本」】
 *   同一 `PromptCode` 下可以有多个版本（v1 draft / v2 active / v3 …）。
 *   实施人员看到 3 行却不知道运行期会用哪一个，就会「改了没生效」。
 *   顶部横幅直接给出后端 `resolve` 的**唯一选取口径**结果：
 *   ① 机构更具体优先 → ② `IsDefault` 优先 → ③ `Version` 新优先。
 *
 * 【★ 2026-10-04 修「先有鸡还是先有蛋」】
 *   旧版只在 `promptCode` 非空时才有内容，而**新模板的 `FillPromptCode` 必然是空的**
 *   ⇒ ① 父页把页签写成 `:disabled="!promptCode"` ⇒ 页签点不开；
 *     ② 即便点开，面板里只有「版本表 + 设为默认」，**没有任何新建/挂接入口**。
 *   现在补上两条破环路径：
 *   - **挂接已有**：从 `DocFillPrompt/codes` 拉现有 `PromptCode` 选一个
 *   - **新建**：直接建 v1（`Version=0` 由后端自增），建成后自动挂到本模板
 *   两者最终都调 `DocTemplate/set-prompt` —— 它**只改 `FillPromptCode` 一列**，
 *   ⛔ 不走通用 `update`（那会把全部 `BcFlag` 列提交一遍，漏传的业务键被清空）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { RefreshRight, Link, Plus } from '@element-plus/icons-vue'
import {
  confirmOrFalse,
  YzhDialog,
  YzhTable,
  unwrapOk,
  type PageParams,
  type YzhAction,
  type YzhTableColumn,
} from '@yzh-core'
import {
  getDocFillPromptVersions,
  getDocFillPromptCodes,
  addDocFillPrompt,
  setDocTemplatePrompt,
  resolveDocFillPrompt,
  setDocFillPromptDefault,
  type DocFillPromptVersion,
} from '@share/api/workflow/doc-fill-rule'

const props = defineProps<{
  /** 当前模板 Code（挂接/解绑的对象） */
  templateCode: string
  /** 已挂接的提示词编码；空串 = 未挂接 */
  promptCode: string
  /** 模板所属机构（提示词「机构更具体优先」的选取依据） */
  orgCode: string
  /** 模板名（对话框标题用） */
  templateName: string
}>()

/** 挂接/解绑成功 ⇒ 通知父页刷新左树徽标 */
const emit = defineEmits<{ bound: [promptCode: string] }>()

const tableRef = ref<any>(null)
const loading = ref(false)
const resolved = ref<any>(null)
const total = ref(0)

const hasPrompt = computed(() => !!props.promptCode)

const columns: YzhTableColumn<DocFillPromptVersion>[] = [
  { prop: 'Version', label: '版本', width: 80, slot: true },
  { prop: 'PromptName', label: '名称', minWidth: 200 },
  { prop: 'Status', label: '状态', width: 100, slot: true },
  { prop: 'IsDefault', label: '默认', width: 90, slot: true },
  { prop: 'Temperature', label: '温度', width: 80 },
  { prop: 'MaxTokens', label: '最大 tokens', width: 110 },
  { prop: 'UpdateTime', label: '更新时间', width: 180 },
]

/**
 * 客户端分页。
 *
 * ⚠️ `versions` 是**一次性全量**（含已软删）—— 同一 `PromptCode` 的版本数在个位数，
 * 不值得为它加服务端分页。分页只在本组件内做。
 */
async function dataLoader(params: PageParams) {
  if (!hasPrompt.value) return { rows: [], total: 0 }
  const data = unwrapOk(
    await getDocFillPromptVersions(props.promptCode, props.orgCode),
    '加载提示词版本失败',
  )
  const all = data?.Items ?? []
  total.value = all.length
  const page = params.page || 1
  const rows = params.rows || 20
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

/** 行按钮：已是默认 → 禁用；已软删 → 只读 */
function rowActions(row: DocFillPromptVersion): YzhAction[] {
  if (row.IsDeleted) return [{ key: 'deleted', text: '已删除', disabled: true }]
  if (row.IsDefault) return [{ key: 'default', text: '当前默认', disabled: true }]
  return [{ key: 'setDefault', text: '设为默认', type: 'primary' }]
}

async function handleRowAction(key: string, row: DocFillPromptVersion) {
  if (key !== 'setDefault') return
  const ok = await confirmOrFalse(
    `把「${row.PromptName}」（v${row.Version}）设为默认？\n\n` +
      '同一 PromptCode 下默认位是排他的 —— 原默认版本会被自动取消。',
    '设为默认',
    { type: 'warning', confirmButtonText: '设为默认', cancelButtonText: '取消' },
  )
  if (!ok) return
  unwrapOk(await setDocFillPromptDefault(row), '设置失败')
  ElMessage.success('已设为默认')
  await refresh()
}

/** 生效版本横幅：与版本表一起刷新，避免「横幅说 v2、表里 v2 已不是默认」 */
async function loadResolved() {
  if (!hasPrompt.value) {
    resolved.value = null
    return
  }
  try {
    resolved.value = unwrapOk(await resolveDocFillPrompt(props.promptCode, props.orgCode), '解析生效版本失败')
  } catch {
    resolved.value = null
  }
}

async function refresh() {
  loading.value = true
  try {
    await Promise.all([loadResolved(), tableRef.value?.refresh()])
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadResolved()
})

// 切文件/切模板 ⇒ 重新解析生效版本（版本表由 YzhTable 的 dataLoader 自己重载）
watch(
  () => props.promptCode + '|' + props.orgCode,
  () => {
    loadResolved()
  },
)

/* ========================================================
   挂接 / 新建 对话框
   ======================================================== */

type BindMode = 'pick' | 'create'

const bindVisible = ref(false)
const bindMode = ref<BindMode>('pick')
const saving = ref(false)

/** 可选提示词（`DocFillPrompt/codes` 聚合结果） */
interface PromptCodeOption {
  PromptCode: string
  PromptName?: string
  VersionCount: number
  MaxVersion: number
  Orgs: string[]
  ActiveVersion: number | null
}
const codeOptions = ref<PromptCodeOption[]>([])
const codesLoading = ref(false)
const pickedCode = ref('')

/** 新建表单 */
const newCode = ref('')
const newName = ref('')
const newSystemPrompt = ref('')
const newUserTemplate = ref('')

const CODE_PATTERN = /^[a-z][a-z0-9_]{1,49}$/

async function loadCodeOptions() {
  codesLoading.value = true
  try {
    const data = unwrapOk(await getDocFillPromptCodes(), '加载提示词清单失败')
    codeOptions.value = data?.Items ?? []
  } catch (e: any) {
    codeOptions.value = []
    ElMessage.error(e?.message || '加载提示词清单失败')
  } finally {
    codesLoading.value = false
  }
}

function openBind(mode: BindMode) {
  if (!props.templateCode) {
    ElMessage.warning('请先在左侧选择一个已上传空白模板的文件')
    return
  }
  bindMode.value = mode
  pickedCode.value = ''
  newCode.value = ''
  newName.value = props.templateName ? `${props.templateName} 全文填写` : ''
  newSystemPrompt.value = ''
  newUserTemplate.value = ''
  bindVisible.value = true
  if (mode === 'pick') loadCodeOptions()
}

/** 挂接已有：选中的 code 直接写回模板 */
async function confirmPick() {
  if (!pickedCode.value) {
    ElMessage.warning('请选择一个提示词')
    return
  }
  saving.value = true
  try {
    unwrapOk(await setDocTemplatePrompt(props.templateCode, pickedCode.value), '挂接失败')
    ElMessage.success(`已挂接「${pickedCode.value}」`)
    bindVisible.value = false
    emit('bound', pickedCode.value)
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '挂接失败')
  } finally {
    saving.value = false
  }
}

/**
 * 新建：先建 v1，再挂到本模板。
 *
 * ⚠️ 两步**不是事务**（两个不同实体，跨控制器）。若第二步失败，
 * 提示词已存在但没挂上 —— 这时提示用户「提示词已建好，可在『挂接已有』里选它」，
 * 而不是假装整体成功。
 */
async function confirmCreate() {
  const code = newCode.value.trim()
  const name = newName.value.trim()
  if (!CODE_PATTERN.test(code)) {
    ElMessage.warning('编码需以小写字母开头，只含小写字母 / 数字 / 下划线，长度 2~50')
    return
  }
  if (!name) {
    ElMessage.warning('请填写名称')
    return
  }
  saving.value = true
  try {
    unwrapOk(
      await addDocFillPrompt({
        PromptCode: code,
        PromptName: name,
        SystemPrompt: newSystemPrompt.value.trim() || undefined,
        UserTemplate: newUserTemplate.value.trim() || undefined,
      }),
      '新建提示词失败',
    )

    try {
      unwrapOk(await setDocTemplatePrompt(props.templateCode, code), '挂接失败')
    } catch (e: any) {
      ElMessage.warning(`提示词「${code}」已建好，但挂接失败：${e?.message || ''}。可在「挂接已有」里选它`)
      bindVisible.value = false
      await loadCodeOptions()
      return
    }

    ElMessage.success(`已新建并挂接「${code}」v1（草稿）`)
    bindVisible.value = false
    emit('bound', code)
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '新建提示词失败')
  } finally {
    saving.value = false
  }
}

/** 解绑：空串 = 该模板不走全文规则 */
async function onUnbind() {
  const ok = await confirmOrFalse(
    `解绑后本模板将只走「锚点规则」填充，不再使用全文提示词。\n\n提示词本身不会被删除，可随时再挂接。`,
    '解绑提示词',
    { type: 'warning', confirmButtonText: '解绑', cancelButtonText: '取消' },
  )
  if (!ok) return
  try {
    unwrapOk(await setDocTemplatePrompt(props.templateCode, ''), '解绑失败')
    ElMessage.success('已解绑')
    emit('bound', '')
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '解绑失败')
  }
}

defineExpose({ refresh })
</script>

<template>
  <div class="prompt-panel">
    <!--
      未挂提示词：**不是错误** —— 锚点填充依然可用。
      但必须给出「怎么挂上」的入口，否则就是个死胡同（旧版就是死胡同）。
    -->
    <div v-if="!hasPrompt" class="prompt-empty">
      <p class="prompt-empty__title">该模板未挂「全文填写提示词」</p>
      <p class="prompt-empty__desc">
        不挂也能用 —— 只走<strong>锚点规则</strong>逐处填充。
        需要让模型按上下文通篇组织内容时，再挂一个提示词。
      </p>
      <div class="prompt-empty__actions">
        <el-button type="primary" :icon="Link" @click="openBind('pick')">挂接已有提示词</el-button>
        <el-button type="default" :icon="Plus" @click="openBind('create')">新建提示词</el-button>
      </div>
    </div>

    <template v-else>
      <el-alert type="success" :closable="false" show-icon class="prompt-panel__banner">
        <template #title>
          <template v-if="resolved?.Found">
            当前生效：<strong>{{ resolved.Picked.PromptName }}</strong>
            （v{{ resolved.Picked.Version }} · {{ resolved.Picked.Status }}）
            <span class="prompt-panel__banner-sub">
              · 同 PromptCode 下共 {{ resolved.CandidateCount }} 个候选版本
            </span>
          </template>
          <template v-else>
            未找到可用版本（<code>{{ promptCode }}</code>）—— 运行期将<strong>不</strong>走全文规则。
          </template>
        </template>
      </el-alert>

      <YzhTable
        ref="tableRef"
        :columns="columns"
        :data-loader="dataLoader"
        :row-action-buttons="rowActions"
        row-key="Code"
        :page-size="10"
        empty-text="该提示词还没有任何版本"
        @row-action="handleRowAction"
      >
        <template #column-Version="{ row }">
          <el-tag size="small" :type="row.IsDefault ? 'success' : 'info'">v{{ row.Version }}</el-tag>
        </template>

        <template #column-Status="{ row }">
          <el-tag
            size="small"
            :type="row.Status === 'active' ? 'success' : row.Status === 'draft' ? 'warning' : 'info'"
          >
            {{ row.Status }}
          </el-tag>
        </template>

        <template #column-IsDefault="{ row }">
          <el-tag v-if="row.IsDefault" size="small" type="success">默认</el-tag>
          <span v-else class="prompt-panel__muted">—</span>
        </template>

        <template #toolbar-left>
          <el-button type="default" :icon="RefreshRight" :loading="loading" @click="refresh">刷新</el-button>
          <el-button type="default" :icon="Link" @click="openBind('pick')">换绑</el-button>
          <el-button type="danger" plain @click="onUnbind">解绑</el-button>
          <span class="prompt-panel__meta">
            已挂接 <code>{{ promptCode }}</code> · 共 {{ total }} 个版本（含已删除）
          </span>
        </template>
      </YzhTable>
    </template>

    <!-- ── 挂接 / 新建 对话框（★ 用 YzhDialog，法条 S07 禁裸 el-dialog）── -->
    <YzhDialog
      v-model="bindVisible"
      :title="bindMode === 'pick' ? '挂接已有提示词' : '新建提示词'"
      width="640px"
      :confirm-text="bindMode === 'pick' ? '挂接' : '新建并挂接'"
      :confirm-loading="saving"
      @confirm="bindMode === 'pick' ? confirmPick() : confirmCreate()"
    >
      <template v-if="bindMode === 'pick'">
        <p class="bind-hint">
          选择后，本模板的 <code>FillPromptCode</code> 会被指向该提示词（只改这一列）。
        </p>
        <el-select
          v-model="pickedCode"
          filterable
          clearable
          class="bind-select"
          :loading="codesLoading"
          placeholder="搜索并选择提示词"
        >
          <el-option
            v-for="o in codeOptions"
            :key="o.PromptCode"
            :label="`${o.PromptCode} · ${o.PromptName || '(未命名)'} · ${o.VersionCount} 版`"
            :value="o.PromptCode"
          >
            <span class="bind-option">
              <code>{{ o.PromptCode }}</code>
              <span class="bind-option__name">{{ o.PromptName || '(未命名)' }}</span>
              <span class="bind-option__meta">
                {{ o.VersionCount }} 版<template v-if="o.ActiveVersion">
                  · 生效 v{{ o.ActiveVersion }}</template>
                <template v-else>· 无 active 默认版</template>
              </span>
            </span>
          </el-option>
        </el-select>
        <p v-if="!codesLoading && codeOptions.length === 0" class="bind-hint bind-hint--warn">
          系统里还没有任何提示词 —— 请改用「新建提示词」。
        </p>
      </template>

      <template v-else>
        <p class="bind-hint">
          新建的是<strong>第 1 版（草稿）</strong>；版本号由后端自增。
          提示词落<strong>全局作用域</strong>（机构级差异化请在「提示词工作台」另建）。
        </p>
        <el-form label-width="96px" label-position="right">
          <el-form-item label="编码" required>
            <el-input v-model="newCode" placeholder="如 doc_fill_quality_manual" />
            <div class="bind-hint">小写字母开头，只含小写字母 / 数字 / 下划线，长度 2~50</div>
          </el-form-item>
          <el-form-item label="名称" required>
            <el-input v-model="newName" placeholder="如 质量手册全文填写" />
          </el-form-item>
          <el-form-item label="系统提示词">
            <el-input
              v-model="newSystemPrompt"
              type="textarea"
              :rows="3"
              placeholder="角色设定，可留空后在工作台补写"
            />
          </el-form-item>
          <el-form-item label="用户模板">
            <el-input
              v-model="newUserTemplate"
              type="textarea"
              :rows="4"
              placeholder="含 {{__FILL__.xxx}} 占位符的用户模板，可留空后在工作台补写"
            />
          </el-form-item>
        </el-form>
      </template>
    </YzhDialog>
  </div>
</template>

<style scoped>
.prompt-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.prompt-panel__banner {
  margin-bottom: var(--yzh-space-2, 8px);
  flex-shrink: 0;
}

.prompt-panel__banner-sub {
  font-weight: var(--yzh-font-weight-normal, 400);
  opacity: 0.75;
}

.prompt-panel__muted {
  color: var(--yzh-color-text-placeholder, #c0c4cc);
}

.prompt-panel__meta {
  margin-left: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  line-height: var(--yzh-space-8, 32px);
}

/* ── 未挂提示词的空状态（带两个破环入口）── */
.prompt-empty {
  margin: auto;
  max-width: 460px;
  padding: var(--yzh-space-6, 24px);
  text-align: center;
}
.prompt-empty__title {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-text-regular, #606266);
}
.prompt-empty__desc {
  margin: 0 0 var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-tertiary, #909399);
}
.prompt-empty__actions {
  display: flex;
  justify-content: center;
  gap: var(--yzh-space-2, 8px);
}

/* ── 对话框 ── */
.bind-hint {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-tertiary, #909399);
}
.bind-hint--warn {
  color: var(--yzh-color-warning, #e6a23c);
}
.bind-select {
  width: 100%;
}
.bind-option {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
.bind-option__name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}
.bind-option__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}
</style>
