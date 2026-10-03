<script setup lang="ts">
/**
 * 全文规则 —— 模板上挂的「全文填写提示词」版本列表
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
 */
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { RefreshRight } from '@element-plus/icons-vue'
import { YzhTable, unwrapOk, type PageParams, type YzhAction, type YzhTableColumn } from '@yzh-core'
import {
  getDocFillPromptVersions,
  resolveDocFillPrompt,
  setDocFillPromptDefault,
  type DocFillPromptVersion,
} from '@share/api/workflow/doc-fill-rule'

const props = defineProps<{
  promptCode: string
  orgCode: string
  templateName: string
}>()

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
  try {
    await ElMessageBox.confirm(
      `把「${row.PromptName}」（v${row.Version}）设为默认？\n\n` +
        '同一 PromptCode 下默认位是排他的 —— 原默认版本会被自动取消。',
      '设为默认',
      { type: 'warning', confirmButtonText: '设为默认', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
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
</script>

<template>
  <div class="prompt-panel">
    <!-- 未挂提示词：不是错误，锚点填充依然可用 -->
    <el-empty v-if="!hasPrompt">
      <template #description>
        <p>该模板未挂「全文填写提示词」—— 只走<strong>锚点规则</strong>填充。</p>
        <p class="prompt-panel__hint">
          需要时可在模板行上填写 <code>FillPromptCode</code>，再在「提示词工作台」维护对应版本。
        </p>
      </template>
    </el-empty>

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
          <el-button :icon="RefreshRight" :loading="loading" @click="refresh">刷新</el-button>
          <span class="prompt-panel__meta">共 {{ total }} 个版本（含已删除）</span>
        </template>
      </YzhTable>
    </template>
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
  margin-bottom: 8px;
  flex-shrink: 0;
}

.prompt-panel__banner-sub {
  font-weight: 400;
  opacity: 0.75;
}

.prompt-panel__hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.prompt-panel__muted {
  color: var(--el-text-color-placeholder);
}

.prompt-panel__meta {
  margin-left: 12px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 32px;
}
</style>
