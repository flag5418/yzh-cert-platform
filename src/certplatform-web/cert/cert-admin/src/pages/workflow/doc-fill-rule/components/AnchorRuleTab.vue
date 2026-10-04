<script setup lang="ts">
/**
 * Tab1 · 锚点与字段规则
 *
 * 【设计决策：锚点清单与字段规则合并成一张表】（`37` 号 §3.4 Tab1）
 *   锚点与规则是 1:1 的；拆两张表 ⇒ 用户要来回切，且看不出「哪个锚点还没配」。
 *
 * 【⛔ 这里没有「新建锚点」按钮】
 *   锚点是**扫描出来的**（用户 2026-10-03 裁定）：模板自己声明了要填什么，
 *   扫描是「读声明」不是「猜意图」。手工新建锚点 = 造一个运行期永远匹配不到的行。
 *   ⇒ 新增锚点的唯一路径是「在 Word/Excel 里加 `{{标签}}` → 重新上传 → 重新扫描」。
 *
 * 【★ 交互：点一行 → 右侧侧边栏配属性】
 *   不用弹窗：锚点配置是「边看文档边配」的动作，弹窗会盖住中栏预览。
 *
 * 【统计口径】见 `logic.loadAnchors()` 的注释 —— 统计与快速筛选必须作用在**全量**上。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { RefreshRight, InfoFilled } from '@element-plus/icons-vue'
import { YzhTable, YzhEmptyState, YzhStatusBadge, type PageParams } from '@yzh-core'
import { parseSourceSpec, summarizeSourceSpec } from './sourceSpec'
import AnchorSidePanel from './AnchorSidePanel.vue'

const props = defineProps<{
  /** `DocFillRuleLogic` 实例（列/筛选/加载都由它提供，⛔ 不在本组件重写一套） */
  logic: any
  templateCode: string
  /** 是否已选中「模板」叶子 */
  hasTemplate: boolean
  /** `cert_doc_template.ScanStatus` —— 未扫描过时给引导 */
  scanStatus?: string
}>()

const emit = defineEmits<{ (e: 'saved'): void }>()

const tableRef = ref<any>(null)
const rows = ref<any[]>([])
const loading = ref(false)

// ★ 锚点声明样例：必须在脚本里写成常量。
//   若直接写进模板 `{{ '{{标签}}' }}`，Vue 的插值 tokenizer 会在第一个 `}}` 处截断，
//   导致 vite 构建报「Unterminated string constant」（vue-tsc 却不报，容易漏过）。
const tokenSample = '{{标签}}'
/** 快速筛选（客户端，作用在全量上） */
const quickFilter = ref<'all' | 'unconfigured' | 'required' | 'orphan'>('all')

const drawerVisible = ref(false)
const currentAnchor = ref<any>(null)

/* ============ 统计 ============ */
const stats = computed(() => {
  const all = rows.value
  let auto = 0
  let manual = 0
  let compute = 0
  let unconfigured = 0
  let orphan = 0
  for (const r of all) {
    if (r.IsOrphan) orphan++
    const { model } = parseSourceSpec(r.SourceSpec)
    if (!model.sources.length) {
      // 域自动值本来就不需要来源，不计入「未配」
      const isAuto = String(r.AnchorType) === 'domain' && String(r.DomainKind || '') === 'auto'
      if (!isAuto) unconfigured++
      continue
    }
    for (const s of model.sources) {
      if (s.kind === 'manual') manual++
      else if (s.kind === 'compute') compute++
      else auto++
    }
  }
  return { total: all.length, auto, manual, compute, unconfigured, orphan }
})

/* ============ 筛选 ============ */
const filtered = computed(() => {
  const all = rows.value
  switch (quickFilter.value) {
    case 'unconfigured':
      return all.filter((r) => {
        const { model } = parseSourceSpec(r.SourceSpec)
        const isAuto = String(r.AnchorType) === 'domain' && String(r.DomainKind || '') === 'auto'
        return model.sources.length === 0 && !isAuto
      })
    case 'required':
      return all.filter((r) => !!r.Required)
    case 'orphan':
      return all.filter((r) => !!r.IsOrphan)
    default:
      return all
  }
})

/* ============ 加载 ============ */
async function loadAll() {
  if (!props.templateCode) {
    rows.value = []
    return
  }
  loading.value = true
  try {
    rows.value = await props.logic.loadAnchors()
  } catch (e: any) {
    ElMessage.error(e?.message || '加载锚点清单失败')
    rows.value = []
  } finally {
    loading.value = false
  }
}

/** 展示层分页：数据已全量在手，分页只影响渲染（锚点规模在几十量级） */
async function dataLoader(params: PageParams) {
  const page = params.page || 1
  const size = params.rows || 20
  const all = filtered.value
  return { rows: all.slice((page - 1) * size, page * size), total: all.length }
}

async function refresh() {
  await loadAll()
  await tableRef.value?.refresh()
}

defineExpose({ refresh })

/**
 * 首次挂载就取一次全量。
 *
 * ⚠️ 不能只依赖 `YzhTable` 的 `dataLoader`：它只负责**展示层分页切片**，
 * 而统计与快速筛选依赖 `rows`（全量）。两者不取同一份数据时，
 * 会出现「表格有 20 行、统计说 0 个锚点」这种自相矛盾的界面。
 */
onMounted(() => {
  if (props.templateCode) loadAll()
})

/** 切模板时清空旧行，避免短暂显示上一个模板的锚点 */
watch(
  () => props.templateCode,
  () => {
    rows.value = []
    quickFilter.value = 'all'
    loadAll()
  },
)

/* ============ 交互 ============ */
function onRowClick(row: any) {
  currentAnchor.value = row
  drawerVisible.value = true
}

function onSaved() {
  emit('saved')
  refresh()
}

/** 来源摘要（前端现算：`SourceSummary` 是后端从不写入的视图列） */
function sourceSummary(row: any) {
  const { model } = parseSourceSpec(row.SourceSpec)
  return summarizeSourceSpec(model)
}

/** 未配来源且不是域自动值 ⇒ 运行期会留空，必须显眼 */
function isUnconfigured(row: any) {
  const isAuto = String(row.AnchorType) === 'domain' && String(row.DomainKind || '') === 'auto'
  if (isAuto) return false
  return parseSourceSpec(row.SourceSpec).model.sources.length === 0
}

const FILTERS = [
  { value: 'all', label: '全部' },
  { value: 'unconfigured', label: '未配来源' },
  { value: 'required', label: '必填' },
  { value: 'orphan', label: '孤儿锚点' },
] as const
</script>

<template>
  <div class="anchor-tab">
    <!-- 未选中模板 -->
    <YzhEmptyState v-if="!hasTemplate" :icon="InfoFilled" title="请在左侧选择一个模板" />

    <!-- H-2：没有空白模板就不能配规则 -->
    <YzhEmptyState
      v-else-if="scanStatus !== 'completed'"
      :icon="InfoFilled"
      title="该模板还没有扫描出锚点"
    >
      <template #description>
        <p>锚点来自<strong>空白模板本身</strong>的声明（<code>{{ tokenSample }}</code> / 书签）。</p>
        <p>请先用上方操作条完成：<strong>下载可编辑版 → 本地加工 → 上传空白模板 → 重新扫描</strong>。</p>
      </template>
    </YzhEmptyState>

    <template v-else>
      <!-- 统计（37 号 §3.4 Tab1 要求） -->
      <div class="stats">
        <span class="stats__total">本模板 {{ stats.total }} 个锚点</span>
        <YzhStatusBadge type="success" :text="`${stats.auto} 自动`" />
        <YzhStatusBadge type="warning" :text="`${stats.manual} 人工`" />
        <YzhStatusBadge type="info" :text="`${stats.compute} 计算`" />
        <YzhStatusBadge
          v-if="stats.unconfigured"
          type="danger"
          :text="`${stats.unconfigured} 未配来源`"
        />
        <YzhStatusBadge v-if="stats.orphan" type="warning" :text="`${stats.orphan} 孤儿`" />
      </div>

      <YzhTable
        ref="tableRef"
        :columns="logic.columns"
        :data-loader="dataLoader"
        row-key="Code"
        :show-pagination="true"
        :page-size="20"
        empty-text="该模板还没有锚点，请点「重新扫描」"
        @row-click="onRowClick"
      >
        <template #column-AnchorRef="{ row }">
          <div class="anchor-ref">
            <code class="mono">{{ row.AnchorRef }}</code>
            <YzhStatusBadge v-if="row.IsOrphan" type="warning" text="孤儿" />
            <YzhStatusBadge v-else-if="isUnconfigured(row)" type="danger" text="未配来源" />
          </div>
        </template>

        <template #column-AnchorType="{ row }">
          <YzhStatusBadge :type="row.AnchorType === 'domain' ? 'info' : 'success'" :text="row.AnchorType" />
          <YzhStatusBadge v-if="row.DomainKind" type="warning" :text="row.DomainKind" class="ml4" />
        </template>

        <template #column-SourceSpec="{ row }">
          <span v-if="sourceSummary(row)" class="src-summary">{{ sourceSummary(row) }}</span>
          <span v-else class="src-empty">— 未配置 —</span>
        </template>

        <template #column-Required="{ row }">
          <YzhStatusBadge v-if="row.Required" type="danger" text="必填" />
          <span v-else class="src-empty">—</span>
        </template>

        <template #toolbar-left>
          <el-radio-group v-model="quickFilter" size="small">
            <el-radio-button v-for="f in FILTERS" :key="f.value" :value="f.value">
              {{ f.label }}
            </el-radio-button>
          </el-radio-group>
          <el-button
            type="default"
            :icon="RefreshRight"
            size="small"
            :loading="loading"
            class="ml12"
            @click="refresh"
          >
            刷新
          </el-button>
          <span class="hint">点任意一行 → 右侧打开属性侧边栏</span>
        </template>
      </YzhTable>
    </template>

    <!-- ★ 侧边栏：点锚点后配置属性 -->
    <AnchorSidePanel
      v-model:visible="drawerVisible"
      :anchor="currentAnchor"
      :template-code="templateCode"
      @saved="onSaved"
    />
  </div>
</template>

<style scoped>
.anchor-tab {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  gap: var(--yzh-space-2, 8px);
}

.stats {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) 0;
  flex-wrap: wrap;
}
.stats__total {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.anchor-ref {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}
.mono {
  font-family: var(--yzh-font-family-mono, 'Courier New', monospace);
  font-size: var(--yzh-font-size-xs, 12px);
}
.ml4 {
  margin-left: var(--yzh-space-1, 4px);
}
.ml12 {
  margin-left: var(--yzh-space-3, 12px);
}

.src-summary {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}
.src-empty {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.hint {
  margin-left: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  line-height: var(--yzh-space-6, 24px);
}
</style>
