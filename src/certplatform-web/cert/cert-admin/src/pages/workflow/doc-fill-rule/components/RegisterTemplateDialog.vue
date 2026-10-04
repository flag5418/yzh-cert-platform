<script setup lang="ts">
/**
 * 登记空白模板 —— 从标准目录里挑一份已归一的空白文档，登记成「可填写模板」
 *
 * 【为什么要有这一步】
 *   `cert_doc_template` 是**登记表不是生成表**：填写引擎的输入必须是
 *   「已经存在、且格式已被归一为 `.docx`/`.xlsx`」的空白模板。
 *   所以本弹窗的数据源是标准目录的模板行（`EnterpriseCode='YZH-STD-ENT'`），
 *   ⛔ 不是「上传一个新文件」。
 *
 * 【★ 实测得出的关键事实（真实数据推翻理想设计）】
 *   168 个模板行里 **143 个 `.doc` + 11 个 `.xls`**，而填写引擎只认 `.docx`/`.xlsx`。
 *   ⇒ 登记的是**归一产物**（`EffectiveStoragePath`），`RegisterKind` 就是后端据此推导的。
 *   列表里把 `FileType`（原始）与 `RegisterKind`（登记后）**并排显示**，
 *   让实施人员一眼看出「这份文件到底能不能填」。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { RefreshRight } from '@element-plus/icons-vue'
import { confirmOrFalse, YzhDialog, YzhTable, unwrapOk, type PageParams, type SearchField, type YzhAction, type YzhTableColumn } from '@yzh-core'
import {
  getDocTemplateCandidates,
  registerDocTemplate,
  type DocTemplateCandidate,
} from '@share/api/workflow/doc-fill-rule'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  /** 登记成功（父页面据此刷新左树） */
  (e: 'registered'): void
}>()

const tableRef = ref<any>(null)
const submitting = ref(false)
const stats = ref({ all: 0, ready: 0, registered: 0 })

const columns: YzhTableColumn<DocTemplateCandidate>[] = [
  { prop: 'FileName', label: '文件名', minWidth: 240 },
  { prop: 'FileType', label: '原始类型', width: 100 },
  { prop: 'RegisterKind', label: '登记类型', width: 100, slot: true },
  { prop: 'EditableStatus', label: '归一状态', width: 110 },
  { prop: 'Ready', label: '可登记', width: 90, slot: true },
  { prop: 'Registered', label: '已登记', width: 90, slot: true },
]

const searchFields: SearchField[] = [
  { prop: 'keyword', label: '文件名', type: 'text', placeholder: '按文件名模糊筛选' },
]

/**
 * 客户端分页 + 关键字过滤。
 *
 * ⚠️ 后端 `candidates` 是**一次性全量**返回（168 行），没有分页参数 ——
 * 这是有意的：候选集 = 一个标准的全部模板文档，量级在百位，不值得为它加服务端分页。
 * 分页只在本弹窗内做，避免把「假分页」混进 `YzhTable` 的语义里。
 */
async function dataLoader(params: PageParams) {
  const data = unwrapOk(
    await getDocTemplateCandidates(params.keyword as string | undefined),
    '加载候选模板失败',
  )
  const all = data?.Items ?? []
  stats.value = {
    all: data?.AllCount ?? all.length,
    ready: data?.ReadyCount ?? 0,
    registered: data?.RegisteredCount ?? 0,
  }
  const page = params.page || 1
  const rows = params.rows || 20
  const slice = all.slice((page - 1) * rows, page * rows)
  return { rows: slice, total: all.length }
}

/** 行按钮：不可登记 / 已登记 一律禁用（后端也会拒，这里只是别让用户白点） */
function rowActions(row: DocTemplateCandidate): YzhAction[] {
  if (row.Registered) return [{ key: 'registered', text: '已登记', disabled: true }]
  if (!row.Ready) return [{ key: 'notready', text: '不可登记', disabled: true }]
  return [{ key: 'register', text: '登记', type: 'primary' }]
}

async function handleRowAction(key: string, row: DocTemplateCandidate) {
  if (key !== 'register') return
  const ok = await confirmOrFalse(
    `将把「${row.FileName}」登记为标准文档模板。\n\n` +
      `登记类型：${row.RegisterKind}（归一产物）\n` +
      '登记后即可为它配置锚点规则与全文填写规则。',
    '确认登记',
    { type: 'info', confirmButtonText: '登记', cancelButtonText: '取消' },
  )
  if (!ok) return
  submitting.value = true
  try {
    unwrapOk(
      await registerDocTemplate({ StandardFileCode: row.Code, FileName: row.FileName }),
      '登记失败',
    )
    ElMessage.success('登记成功')
    emit('registered')
    await tableRef.value?.refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '登记失败')
  } finally {
    submitting.value = false
  }
}

/** 每次打开都重新拉一次：别的实施人员可能刚登记过，缓存会骗人 */
watch(
  () => props.visible,
  (v) => {
    if (v) tableRef.value?.refresh()
  },
)

const readyHint = computed(() => {
  const { all, ready, registered } = stats.value
  if (!all) return ''
  return `共 ${all} 份模板文档，其中 ${ready} 份已归一转成可填写格式；已登记 ${registered} 份`
})
</script>

<template>
  <YzhDialog
    :model-value="visible"
    title="登记空白模板"
    width="1000px"
    :show-footer="false"
    destroy-on-close
    @update:model-value="emit('update:visible', $event)"
  >
    <div class="register-template-dialog">
      <el-alert type="info" :closable="false" show-icon class="register-template-dialog__hint">
        <template #title>
          登记的是<strong>归一产物</strong>（.docx / .xlsx）。原始 .doc / .xls 无法被填写引擎打开，
          需先在「标准文件管理」里完成归一。
        </template>
      </el-alert>

      <p v-if="readyHint" class="register-template-dialog__stats">{{ readyHint }}</p>

      <YzhTable
        ref="tableRef"
        :columns="columns"
        :data-loader="dataLoader"
        :search-fields="searchFields"
        :row-action-buttons="rowActions"
        row-key="Code"
        :page-size="10"
        empty-text="没有匹配的模板文档"
        @row-action="handleRowAction"
      >
        <template #column-RegisterKind="{ row }">
          <el-tag v-if="row.RegisterKind" size="small" type="success">{{ row.RegisterKind }}</el-tag>
          <el-tag v-else size="small" type="info">不可用</el-tag>
        </template>

        <template #column-Ready="{ row }">
          <el-tag size="small" :type="row.Ready ? 'success' : 'danger'">
            {{ row.Ready ? '是' : '否' }}
          </el-tag>
        </template>

        <template #column-Registered="{ row }">
          <el-tag size="small" :type="row.Registered ? 'warning' : 'info'">
            {{ row.Registered ? '已登记' : '未登记' }}
          </el-tag>
        </template>

        <template #toolbar-left>
          <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
        </template>
      </YzhTable>
    </div>
  </YzhDialog>
</template>

<style scoped>
.register-template-dialog {
  display: flex;
  flex-direction: column;
  height: 62vh;
  min-height: 0;
}

.register-template-dialog__hint {
  margin-bottom: 8px;
  flex-shrink: 0;
}

.register-template-dialog__stats {
  margin: 0 0 8px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}
</style>
