<script setup lang="ts">
/**
 * 企业文档管理（专家端 `/resources`，菜单 `MENU_AUD_04`「认证资料管理」）
 *
 * 依据：`docs/70-V2设计/36-企业资料上传重写-V1.md`（§6.3 落地实现）
 * 实施：`docs/70-V2设计/实施-TODO-上传重写-V1.md`（P3）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★★ 页面主角是「文件管理」，不是「AI 结果审批台」（2026-10-07 用户裁决，沿用）
 *
 * 用户原话：「针对审核员，是**不需要他去确认任何信息**的，他甚至可以不关心我们提取出来的
 * 详细内容……他需要的只是通过这个进行**管理**：可以预览，可以针对单个文件进行**替换**，
 * 可以删除，可以删除整个文件夹……」
 *
 * ⇒ ① 主区是**文件表**；提取结果是**可选抽屉**（不看也能一路走下去，⛔ 不弹「待确认」）
 *   ② 顶部一条说明（`el-alert` 的 `!` 图标）交代这页干什么，⛔ 不替用户决定
 *   ③ 状态只用业务语言（已转换 / 已提取 / 待提取），⛔ 不出现 Markdown / 队列 / Sha256
 * ════════════════════════════════════════════════════════════════════════
 *
 * ★ 与老 `/resources` 的结构差别（36 号 D-1）：左树**只有企业一层**（无阶段），
 *   「按标准查看」是给列表传 `StandardCode` 的**筛子**，⛔ 不是存储层。
 *
 * ⚠️ 状态一律解构出来：解构后每个名字是顶层 ref ⇒ 模板自动解包。
 *   ⛔ 不能写 `logic.rows`：模板对非 ref 绑定不会解包内部 ref，会渲染成 `[object Object]`。
 *   ⛔ 方法不能解构（丢 `this`）⇒ 模板里一律 `logic.xxx()`。
 */
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import {
  YzhDialog,
  YzhDrawer,
  YzhEmptyState,
  YzhStatusBadge,
  YzhTable,
  YzhTreeTableLayout,
} from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { Document, FolderOpened, Loading, Refresh, Upload } from '@element-plus/icons-vue'
import {
  buildAcceptAttribute,
  describeAllowed,
  partitionUploadable,
} from '@share/constants/upload-file-policy'
import type {
  EntDocClauseHit,
  EntDocQueueRow,
  EntDocQueueTask,
  EntDocRow,
  EntDocVersionRow,
} from '@share/api/ent/enterprise-doc'
import { EnterpriseDocLogic } from './logic'
import {
  confidenceBadge,
  convertBadge,
  extractBadge,
  fileKindText,
  folderLabel,
  formatDuration,
  formatSize,
  planActionBadge,
  queueStatusBadge,
  standardExtractBadge,
  taskTypeText,
} from './pure'
import type { UploadPlanItem } from './useUpload'

const logic = new EnterpriseDocLogic()

// ── 左树 / 列表 / 筛选 ──────────────────────────────────────────
const {
  treeNodes, treeLoading, treeConfigured, treeMessage, selectedNode, selectedCode, selectedName,
  planStandards, hasPlan, planReason, totalAll, statusBar, activeStandardCode, keyword, onlyFailed,
  summary, filterActive, filterSummary, isBusy, queueProgress, queueLabel, standardOptions, rows,
  selectedRows,
} = logic

/** 表格引用（`logic.refresh()` 靠它触发 dataLoader 重载） */
const tableRef = ref<unknown>(null)

// ── 提取结果抽屉 ────────────────────────────────────────────────
const {
  extractionVisible, extractionLoading, extractionFile, extractionTabs, extractionActive,
} = logic

// ── 原文核对抽屉 ────────────────────────────────────────────────
const {
  markdownVisible, markdownLoading, markdownFile, markdownText, markdownMessage, markdownParts,
} = logic

// ── PDF 预览 ────────────────────────────────────────────────────
const { previewVisible, previewLoading, previewUrl, previewName } = logic

// ── 版本时间线 ──────────────────────────────────────────────────
const { versionVisible, versionLoading, versionFile, versionRows, versionCurrent, versionMessage } = logic

// ── 队列详情 ────────────────────────────────────────────────────
const { queueVisible, queueLoading, queueDetail } = logic

// ── 上传弹窗（`upload` 是纯对象，其内部 ref 不解包 ⇒ 逐个解构出来）──
const {
  visible: uploadVisible, stage: uploadStage, picked: uploadPicked, planRows: uploadPlanRows,
  planSummary: uploadPlanSummary, doneCount: uploadDone, totalCount: uploadTotal,
  progressText: uploadProgressText, running: uploadRunning, canStart: uploadCanStart,
  checkedCount: uploadCheckedCount, progressPercent: uploadPercent,
} = logic.upload

// ═══════════════════════ 列定义 ═══════════════════════

/** 文件表（守卫 R6：页面禁内联 `<el-table>`） */
const columns = logic.fileColumns

/** 上传预检表 */
const planColumns: YzhTableColumn<UploadPlanItem>[] = [
  { prop: 'Checked', label: '', width: 48, slot: true },
  { prop: 'FileName', label: '文件名', minWidth: 220 },
  { prop: 'RelFolderPath', label: '文件夹', minWidth: 150, slot: true },
  { prop: 'FileSize', label: '大小', width: 90, align: 'right', slot: true },
  { prop: 'Action', label: '处理', width: 110, slot: true },
  { prop: 'BlockReason', label: '说明', minWidth: 180 },
]

/**
 * 提取结果里的条款表。
 *
 * ⚠️ 列宽合计（含「定位原文」操作列 ~88）必须 ≤ 抽屉可用宽度（980 − 内边距 ≈ 930），
 *   否则操作列被推到横向溢出区 ⇒ 用户点不到（真机实测踩到过，与文件表同一个坑）。
 */
const clauseColumns: YzhTableColumn<EntDocClauseHit>[] = [
  { prop: 'ClauseNumber', label: '条款号', width: 80 },
  { prop: 'Title', label: '条款标题', width: 120, showOverflowTooltip: true },
  { prop: 'Anchor', label: '锚点（原文片段）', minWidth: 260, slot: true },
  { prop: 'Excerpts', label: '摘录', minWidth: 220, slot: true },
  { prop: 'Confidence', label: '置信度', width: 72, align: 'center', slot: true },
]

const clauseRowActions = [{ key: 'locate', text: '定位原文' }]

/** 版本时间线（列宽合计同样受抽屉宽度约束，见 `clauseColumns` 注释） */
const versionColumns: YzhTableColumn<EntDocVersionRow>[] = [
  { prop: 'VersionNumber', label: '版本', width: 70, slot: true },
  { prop: 'FileName', label: '文件名', minWidth: 150, showOverflowTooltip: true },
  { prop: 'FileSize', label: '大小', width: 80, align: 'right', slot: true },
  { prop: 'CreateTime', label: '时间', width: 150 },
  { prop: 'Reason', label: '说明', minWidth: 110, showOverflowTooltip: true },
]

const versionRowActions = (row: EntDocVersionRow) =>
  row.IsCurrent ? [] : [{ key: 'restore', text: '回滚到此版本', type: 'danger' as const }]

/** 队列任务 */
const taskColumns: YzhTableColumn<EntDocQueueTask>[] = [
  { prop: 'FileName', label: '文件', minWidth: 190 },
  { prop: 'TaskType', label: '类型', width: 170, slot: true },
  { prop: 'Status', label: '状态', width: 96, slot: true },
  { prop: 'RetryCount', label: '重试', width: 64, align: 'center' },
  { prop: 'Message', label: '失败原因', minWidth: 240, showOverflowTooltip: true },
]

// ═══════════════════════ 选择文件 ═══════════════════════

/**
 * ★ `accept` 由**唯一事实源** `upload-file-policy.json` 生成（守卫 R21③：⛔ 不手写后缀串）。
 * 运行时仍由 {@link ingest} 的 `partitionUploadable` 兜底 —— `accept` 只是选择器的提示，
 * 用户可以拖拽 / 手输文件名绕过它。
 */
const UPLOAD_ACCEPT = buildAcceptAttribute()

/** 支持哪些格式（页面提示文案，避免各处漂移） */
const UPLOAD_ALLOWED_TEXT = describeAllowed()

const fileInput = ref<HTMLInputElement | null>(null)
const folderInput = ref<HTMLInputElement | null>(null)

function pickFiles(): void {
  fileInput.value?.click()
}

function pickFolder(): void {
  folderInput.value?.click()
}

/**
 * 统一入口：先按上传契约分区，再交给状态机。
 *
 * ★ 为什么前端也要筛一遍：后端 `PlanUploadAsync` 会再筛（那是权威），
 *   但让用户先看到「这 3 个文件不支持」比等预检结果更省一轮往返；
 *   系统噪声（`.DS_Store`）则**静默剔除**（用户根本没打算传，报错反而困惑）。
 */
function ingest(files: File[]): void {
  if (files.length === 0) return
  const { accepted, noise, rejected } = partitionUploadable(files)
  if (noise.length > 0) ElMessage.info(`已自动忽略 ${noise.length} 个系统文件`)
  if (rejected.length > 0) {
    ElMessage.warning(rejected[0].message || `${rejected.length} 个文件不支持上传`)
  }
  if (accepted.length > 0) logic.upload.addFiles(accepted as File[])
}

function onFilesChosen(e: Event): void {
  const el = e.target as HTMLInputElement
  ingest(Array.from(el.files ?? []))
  el.value = ''
}

/** ⚠️ 文件夹选择必须保留 `webkitRelativePath`，否则「文件夹」列全是根目录 */
function onFolderChosen(e: Event): void {
  const el = e.target as HTMLInputElement
  ingest(Array.from(el.files ?? []))
  el.value = ''
}

// ═══════════════════════ 弹窗 / 抽屉开关（关时要收尾） ═══════════════════════

/** 上传弹窗：未确认就关 ⇒ 自动回滚批次（⛔ 不留半截脏数据） */
function onUploadVisibleChange(v: boolean): void {
  if (v) {
    uploadVisible.value = true
    return
  }
  void logic.upload.close()
}

function onExtractionVisibleChange(v: boolean): void {
  if (v) extractionVisible.value = true
  else logic.closeExtractions()
}

function onMarkdownVisibleChange(v: boolean): void {
  if (v) markdownVisible.value = true
  else logic.closeMarkdown()
}

function onVersionVisibleChange(v: boolean): void {
  if (v) versionVisible.value = true
  else logic.closeVersions()
}

function onQueueVisibleChange(v: boolean): void {
  if (v) queueVisible.value = true
  else logic.closeQueueDetail()
}

function onPreviewVisibleChange(v: boolean): void {
  if (v) previewVisible.value = true
  else logic.closePreview()
}

// ═══════════════════════ 表格事件 ═══════════════════════

function onRowAction(key: string, row: EntDocRow): void {
  void logic.handleRowAction(key, row)
}

function onClauseRowAction(key: string, row: EntDocClauseHit): void {
  if (key === 'locate') void logic.locateInMarkdown(row)
}

function onVersionRowAction(key: string, row: EntDocVersionRow): void {
  if (key === 'restore') void logic.restoreVersion(row)
}

/** 队列明细里的任务表（v-for 内层） */
function queueTasks(q: EntDocQueueRow): EntDocQueueTask[] {
  return q.Tasks ?? []
}

// ═══════════════════════ 生命周期 ═══════════════════════

onMounted(async () => {
  // 先注入表格引用再 init：`dataLoader` 模式下 `logic.refresh()` 靠它触发重载
  await nextTick()
  logic.setTableRef(tableRef.value)
  await logic.init()
  logic.startPolling()
})

onBeforeUnmount(() => {
  logic.stopPolling()
  logic.closePreview()
})
</script>

<template>
  <div class="ed-page">
    <YzhTreeTableLayout
      :tree-data="treeNodes"
      :tree-width="264"
      label-field="Label"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      status-field=""
      @tree-node-click="logic.onNodeClick($event)"
    >
      <template #treeFooter>
        <div v-if="!treeConfigured" class="ed-hint">{{ treeMessage || '当前工作区还没有配置企业' }}</div>
        <div v-else-if="treeLoading" class="ed-hint">正在加载企业…</div>
        <div v-else-if="selectedNode && !hasPlan" class="ed-hint ed-hint--warn">
          该企业还没有有效认证计划，提取功能不可用。
        </div>
        <div v-else-if="selectedNode" class="ed-hint">
          计划标准：{{ planStandards.length }} 个
        </div>
      </template>

      <template #default>
        <div class="ed-main">
          <!-- ══════════ 页头 ══════════ -->
          <div class="ed-header">
            <div class="ed-header__left">
              <div class="ed-header__title">
                {{ selectedName || '请先在左侧选择企业' }}
              </div>
              <div v-if="selectedCode" class="ed-header__sub">
                共 {{ totalAll }} 份文件
                <template v-if="filterActive">，其中符合筛选的 {{ summary.total }} 份</template>
                <template v-if="summary.ready">，{{ summary.ready }} 份可提取</template>
                <template v-if="summary.extracted">，{{ summary.extracted }} 份已提取</template>
                <template v-if="summary.converting">，{{ summary.converting }} 份处理中</template>
                <template v-if="summary.failed">，{{ summary.failed }} 份需处理</template>
              </div>
            </div>
            <div class="ed-header__right">
              <el-select
                v-if="selectedCode && standardOptions.length"
                v-model="activeStandardCode"
                placeholder="按标准查看"
                clearable
                size="small"
                class="ed-header__select"
                @change="logic.onFilterChange()"
              >
                <el-option v-for="o in standardOptions" :key="o.value" :label="o.label" :value="o.value" />
              </el-select>
              <el-input
                v-if="selectedCode"
                v-model="keyword"
                placeholder="文件名 / 文件夹"
                clearable
                size="small"
                class="ed-header__search"
                @change="logic.onFilterChange()"
              />
              <el-checkbox v-if="selectedCode" v-model="onlyFailed" @change="logic.onFilterChange()">
                只看需处理
              </el-checkbox>
              <el-button v-if="selectedCode" type="primary" size="small" text @click="logic.openQueueDetail()">
                处理进度
              </el-button>
              <el-button v-if="selectedCode" type="primary" size="small" text :icon="Refresh" @click="logic.refreshAll()">
                刷新
              </el-button>
              <el-button
                v-if="selectedCode"
                type="primary"
                size="small"
                :disabled="!hasPlan"
                @click="logic.startExtract()"
              >
                开始提取
              </el-button>
              <el-button v-if="selectedCode" type="primary" size="small" :icon="Upload" @click="logic.openUpload()">
                上传文件
              </el-button>
            </div>
          </div>

          <!-- ══════════ 这页干什么（让审核员自己判断，⛔ 不替他决定） ══════════ -->
          <el-alert
            v-if="selectedCode"
            type="info"
            :closable="false"
            show-icon
            class="ed-tip"
            title="企业交来的资料按企业集中存放，不分阶段。文件会自动转换成 PDF（供预览）和原文（供提取）；「提取」是让系统按认证计划里的标准，把每个条款对应的原文句子找出来。"
          />

          <!-- ══════════ 无计划提示（提取不可用的唯一原因） ══════════ -->
          <el-alert
            v-if="selectedCode && !hasPlan"
            type="warning"
            :closable="false"
            show-icon
            class="ed-tip"
            :title="planReason || '该企业还没有有效认证计划：请先在「认证计划」里建一个计划，系统才知道要按哪些标准提取。'"
          />

          <!-- ══════════ 处理进度横幅 ══════════ -->
          <div v-if="isBusy && statusBar" class="ed-banner">
            <el-icon class="ed-banner__spin"><Loading /></el-icon>
            <span class="ed-banner__label">{{ queueLabel }}</span>
            <el-progress :percentage="queueProgress" :stroke-width="6" class="ed-banner__progress" />
            <span class="ed-banner__count">
              {{ statusBar.QueueCompleted ?? 0 }} / {{ statusBar.QueueTotal ?? 0 }}
            </span>
            <el-button type="primary" size="small" text @click="logic.openQueueDetail()">查看详情</el-button>
          </div>

          <!-- ══════════ 空态 / 表格 ══════════ -->
          <div v-if="!selectedCode" class="ed-placeholder">
            <p>请在左侧选择企业，然后上传企业交来的资料。</p>
          </div>

          <YzhEmptyState
            v-else-if="rows.length === 0"
            :title="filterActive ? '没有符合筛选条件的文件' : '这里还没有文件'"
            :description="filterActive
              ? '当前筛选（' + filterSummary + '）没有匹配到任何一份。'
              : '点右上角「上传文件」把企业交来的资料放进来。'"
          >
            <template #action>
              <!-- ★ 筛选未命中的出路是「清空筛选」，⛔ 不是「上传文件」
                   —— 给错动作会让用户以为文件丢了，重新上传一遍 -->
              <el-button v-if="filterActive" type="primary" @click="logic.clearFilters()">清空筛选</el-button>
              <el-button v-else type="primary" @click="logic.openUpload()">上传文件</el-button>
            </template>
          </YzhEmptyState>

          <div v-else class="ed-table-wrap">
            <div v-if="selectedRows.length > 0" class="ed-bulkbar">
              <span class="ed-bulkbar__text">已选 {{ selectedRows.length }} 个文件</span>
              <el-button type="danger" size="small" @click="logic.batchDelete()">删除选中</el-button>
            </div>
            <YzhTable
              ref="tableRef"
              :columns="columns"
              :data-loader="logic.dataLoader.bind(logic)"
              :toolbar="false"
              :show-pagination="false"
              :row-action-buttons="logic.docRowActions"
              :action-dropdown-only="true"
              :action-fixed="false"
              select-mode="multiple"
              row-key="Code"
              empty-text="暂无文件"
              @row-action="onRowAction"
              @selection-change="logic.onRowsSelected($event)"
            >
              <template #column-FileName="{ row }">
                <div class="ed-file">
                  <el-icon class="ed-file__icon"><Document /></el-icon>
                  <span class="ed-file__name" :title="row.FileName">{{ row.FileName }}</span>
                  <span class="ed-file__kind">{{ fileKindText(row.FileType) }}</span>
                </div>
              </template>

              <template #column-RelFolderPath="{ row }">
                <div class="ed-folder">
                  <el-icon class="ed-folder__icon"><FolderOpened /></el-icon>
                  <span :title="folderLabel(row.RelFolderPath)">{{ folderLabel(row.RelFolderPath) }}</span>
                </div>
              </template>

              <template #column-FileSize="{ row }">
                <span>{{ formatSize(row.FileSize) }}</span>
              </template>

              <template #column-ConvertStatus="{ row }">
                <el-tooltip
                  v-if="row.ConvertMessage || row.MarkdownMessage"
                  :content="row.MarkdownMessage || row.ConvertMessage"
                  placement="top"
                  :show-after="600"
                >
                  <span><YzhStatusBadge :type="convertBadge(row.MarkdownStatus || row.ConvertStatus).type" :text="convertBadge(row.MarkdownStatus || row.ConvertStatus).text" /></span>
                </el-tooltip>
                <YzhStatusBadge
                  v-else
                  :type="convertBadge(row.MarkdownStatus || row.ConvertStatus).type"
                  :text="convertBadge(row.MarkdownStatus || row.ConvertStatus).text"
                />
              </template>

              <template #column-ExtractStatus="{ row }">
                <el-tooltip v-if="extractBadge(row).tip" :content="extractBadge(row).tip" placement="top" :show-after="600">
                  <span><YzhStatusBadge :type="extractBadge(row).type" :text="extractBadge(row).text" /></span>
                </el-tooltip>
                <YzhStatusBadge v-else :type="extractBadge(row).type" :text="extractBadge(row).text" />
              </template>

              <template #column-VersionNumber="{ row }">
                <span class="ed-dim">v{{ row.VersionNumber }}</span>
              </template>
            </YzhTable>
          </div>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- ══════════════════ 上传弹窗 ══════════════════ -->
    <YzhDialog
      :model-value="uploadVisible"
      title="上传企业资料"
      width="820px"
      :show-footer="false"
      @update:model-value="onUploadVisibleChange"
    >
      <div class="ed-upload">
        <!-- 阶段 1：选文件 -->
        <template v-if="uploadStage === 'pick'">
          <el-alert
            type="info"
            :closable="false"
            show-icon
            :title="'一次可以选择整个文件夹（保留子文件夹结构）。支持：' + UPLOAD_ALLOWED_TEXT + '。同名同路径的文件再传一次会变成新版本，旧版本自动归档保留。'"
          />
          <div class="ed-upload__pick">
            <el-button type="primary" :icon="Document" @click="pickFiles()">选择文件</el-button>
            <el-button type="primary" :icon="FolderOpened" @click="pickFolder()">选择文件夹</el-button>
            <el-button v-if="uploadPicked.length" type="danger" text @click="logic.upload.clearPicked()">
              清空
            </el-button>
          </div>
          <div v-if="uploadPicked.length" class="ed-upload__list">
            <div v-for="(p, i) in uploadPicked" :key="p.key" class="ed-upload__item">
              <span class="ed-upload__item-name" :title="p.key">{{ p.file.name }}</span>
              <span class="ed-dim">{{ folderLabel(p.relFolderPath) }}</span>
              <span class="ed-dim">{{ formatSize(p.file.size) }}</span>
              <el-button type="danger" text size="small" @click="logic.upload.removeAt(i)">移除</el-button>
            </div>
          </div>
          <YzhEmptyState v-else title="还没有选择文件" description="点上面的按钮选择文件或整个文件夹。" />
        </template>

        <!-- 阶段 2：预检确认 -->
        <template v-else-if="uploadStage === 'plan'">
          <div v-if="uploadPlanSummary" class="ed-upload__summary">
            共 {{ uploadPlanSummary.Total }} 个：新增 {{ uploadPlanSummary.CreateCount }}、
            替换 {{ uploadPlanSummary.ReplaceCount }}、跳过 {{ uploadPlanSummary.SkipCount }}
            <template v-if="uploadPlanSummary.BlockedCount">、不可传 {{ uploadPlanSummary.BlockedCount }}</template>
            <template v-if="uploadPlanSummary.FilteredCount">
              ；已自动忽略 {{ uploadPlanSummary.FilteredCount }} 个系统文件（如 .DS_Store）
            </template>
          </div>
          <YzhTable
            :columns="planColumns"
            :data="uploadPlanRows"
            :toolbar="false"
            :show-pagination="false"
            row-key="Key"
            size="small"
            empty-text="没有可上传的文件"
          >
            <template #column-Checked="{ row }">
              <el-checkbox
                :model-value="row.Checked"
                :disabled="row.Blocked || !row.File"
                @change="logic.upload.toggleChecked(row, !!$event)"
              />
            </template>
            <template #column-RelFolderPath="{ row }">
              <span>{{ folderLabel(row.RelFolderPath) }}</span>
            </template>
            <template #column-FileSize="{ row }">
              <span>{{ formatSize(row.FileSize) }}</span>
            </template>
            <template #column-Action="{ row }">
              <YzhStatusBadge :type="planActionBadge(row.Action).type" :text="planActionBadge(row.Action).text" />
            </template>
          </YzhTable>
        </template>

        <!-- 阶段 3：传输中 -->
        <template v-else-if="uploadStage === 'uploading'">
          <div class="ed-upload__progress">
            <el-progress :percentage="uploadPercent" :stroke-width="10" />
            <div class="ed-dim">{{ uploadDone }} / {{ uploadTotal }} — {{ uploadProgressText }}</div>
          </div>
        </template>

        <!-- 阶段 4：完成 -->
        <template v-else>
          <el-alert
            type="success"
            :closable="false"
            show-icon
            title="上传完成，系统正在转换。转换完成后会自动按计划里的标准提取条款，进度见「处理进度」。"
          />
        </template>
      </div>

      <template #footer>
        <div class="ed-upload__footer">
          <template v-if="uploadStage === 'pick'">
            <el-button type="primary" :disabled="uploadPicked.length === 0 || uploadRunning" @click="logic.upload.runPlan()">
              下一步（检查 {{ uploadPicked.length }} 个文件）
            </el-button>
          </template>
          <template v-else-if="uploadStage === 'plan'">
            <el-button type="primary" text @click="logic.upload.reset()">重新选择</el-button>
            <el-button type="primary" :disabled="!uploadCanStart" @click="logic.upload.start()">
              上传 {{ uploadCheckedCount }} 个文件
            </el-button>
          </template>
          <template v-else-if="uploadStage === 'uploading'">
            <el-button type="primary" :disabled="true">上传中…</el-button>
          </template>
          <template v-else>
            <el-button type="primary" @click="logic.upload.close()">关闭</el-button>
          </template>
        </div>
      </template>
    </YzhDialog>

    <!-- ══════════════════ 提取结果抽屉（按标准 Tab） ══════════════════ -->
    <YzhDrawer
      :model-value="extractionVisible"
      :title="'提取结果 — ' + (extractionFile?.FileName || '')"
      size="980px"
      :show-footer="false"
      @update:model-value="onExtractionVisibleChange"
    >
      <div v-loading="extractionLoading" class="ed-drawer">
        <YzhEmptyState
          v-if="!extractionLoading && extractionTabs.length === 0"
          title="还没有提取结果"
          description="这份文件还没有按任何标准提取过。点右上角「开始提取」或行操作「重新提取」。"
        />

        <el-tabs v-else v-model="extractionActive" class="ed-drawer__tabs">
          <el-tab-pane
            v-for="tab in extractionTabs"
            :key="tab.standardCode"
            :name="tab.standardCode"
            :label="tab.standardName + '（' + tab.clauses.length + ' 条）'"
          >
            <div class="ed-drawer__head">
              <YzhStatusBadge
                :type="standardExtractBadge(tab.row).type"
                :text="standardExtractBadge(tab.row).text"
              />
              <span class="ed-dim">标准编号：{{ tab.shortCode }}</span>
              <span v-if="tab.row?.ExtractedAt" class="ed-dim">提取时间：{{ tab.row.ExtractedAt }}</span>
              <span v-if="tab.row?.PromptVersion" class="ed-dim">提示词：{{ tab.row.PromptCode }} / {{ tab.row.PromptVersion }}</span>
            </div>

            <el-alert
              v-if="tab.row?.IsStale"
              type="warning"
              :closable="false"
              show-icon
              title="源文件已变更（替换或回滚），这份提取结果不可信，请重新提取。"
            />
            <el-alert
              v-if="tab.row?.ExtractMessage"
              type="info"
              :closable="false"
              show-icon
              :title="tab.row.ExtractMessage"
            />
            <el-alert
              v-if="tab.broken"
              type="warning"
              :closable="false"
              show-icon
              title="这份提取结果的数据格式有问题（无法解析），请重新提取。"
            />

            <YzhEmptyState
              v-if="tab.clauses.length === 0"
              title="这个标准没有提取到条款"
              :description="tab.row ? '模型没有在这份文件里找到可引用的原文，可看上面的提示了解原因。' : '计划里有这个标准，但还没有跑过提取。'"
            />

            <YzhTable
              v-else
              :columns="clauseColumns"
              :data="tab.clauses"
              :toolbar="false"
              :show-pagination="false"
              :row-action-buttons="clauseRowActions"
              :action-fixed="false"
              row-key="ClauseNumber"
              size="small"
              @row-action="onClauseRowAction"
            >
              <template #column-Anchor="{ row }">
                <span class="ed-anchor" :title="row.Anchor">{{ row.Anchor || '（无锚点）' }}</span>
              </template>
              <template #column-Excerpts="{ row }">
                <span class="ed-excerpt">{{ (row.Excerpts || []).join(' / ') || '（无摘录）' }}</span>
              </template>
              <template #column-Confidence="{ row }">
                <YzhStatusBadge :type="confidenceBadge(row.Confidence).type" :text="confidenceBadge(row.Confidence).text" />
              </template>
            </YzhTable>
          </el-tab-pane>
        </el-tabs>
      </div>
    </YzhDrawer>

    <!-- ══════════════════ 原文核对抽屉（锚点高亮） ══════════════════ -->
    <YzhDrawer
      :model-value="markdownVisible"
      :title="'原文 — ' + (markdownFile?.FileName || '')"
      size="760px"
      :show-footer="false"
      @update:model-value="onMarkdownVisibleChange"
    >
      <div v-loading="markdownLoading" class="ed-drawer">
        <div v-if="markdownParts" class="ed-drawer__head">
          <YzhStatusBadge
            type="info"
            :text="logic.locateModeText(markdownParts.mode)"
          />
          <span v-if="markdownParts.clauseNumber" class="ed-dim">条款 {{ markdownParts.clauseNumber }}</span>
        </div>

        <el-alert
          v-if="markdownMessage"
          type="warning"
          :closable="false"
          show-icon
          :title="markdownMessage"
        />

        <pre v-if="!markdownParts" class="ed-md">{{ markdownText || '（暂无原文）' }}</pre>
        <pre v-else class="ed-md"><span>{{ markdownParts.before }}</span><mark
          v-if="markdownParts.hit"
          class="ed-md__hit"
        >{{ markdownParts.hit }}</mark><span>{{ markdownParts.after }}</span></pre>
      </div>
    </YzhDrawer>

    <!-- ══════════════════ PDF 预览 ══════════════════ -->
    <YzhDialog
      :model-value="previewVisible"
      :title="'预览 — ' + previewName"
      width="82%"
      :show-footer="false"
      @update:model-value="onPreviewVisibleChange"
    >
      <div v-loading="previewLoading" class="ed-preview">
        <iframe v-if="previewUrl" :src="previewUrl" class="ed-preview__frame" title="PDF 预览" />
        <YzhEmptyState v-else title="预览还在生成" description="请等待转换完成，或点行操作「重新生成」。" />
      </div>
    </YzhDialog>

    <!-- ══════════════════ 版本时间线 ══════════════════ -->
    <YzhDrawer
      :model-value="versionVisible"
      :title="'历史版本 — ' + (versionFile?.FileName || '') + (versionCurrent ? '（当前 v' + versionCurrent + '）' : '')"
      size="760px"
      :show-footer="false"
      @update:model-value="onVersionVisibleChange"
    >
      <div v-loading="versionLoading" class="ed-drawer">
        <el-alert
          v-if="versionMessage"
          type="warning"
          :closable="false"
          show-icon
          :title="versionMessage"
        />
        <el-alert
          type="info"
          :closable="false"
          show-icon
          title="回滚会把历史版本的内容写回当前文件并重新转换、重新提取；版本号继续 +1（历史版本都保留，不会被覆盖）。"
        />
        <YzhTable
          :columns="versionColumns"
          :data="versionRows"
          :toolbar="false"
          :show-pagination="false"
          :row-action-buttons="versionRowActions"
          :action-fixed="false"
          row-key="Code"
          size="small"
          empty-text="还没有历史版本"
          @row-action="onVersionRowAction"
        >
          <template #column-VersionNumber="{ row }">
            <span>v{{ row.VersionNumber }}</span>
            <YzhStatusBadge v-if="row.IsCurrent" type="success" text="当前" class="ed-version__cur" />
          </template>
          <template #column-FileSize="{ row }">
            <span>{{ formatSize(row.FileSize) }}</span>
          </template>
        </YzhTable>
      </div>
    </YzhDrawer>

    <!-- ══════════════════ 处理进度（队列明细） ══════════════════ -->
    <YzhDrawer
      :model-value="queueVisible"
      title="处理进度"
      size="880px"
      :show-footer="false"
      @update:model-value="onQueueVisibleChange"
    >
      <div v-loading="queueLoading" class="ed-drawer">
        <div v-if="queueDetail" class="ed-drawer__head">
          <YzhStatusBadge type="warning" :text="'处理中 ' + queueDetail.RunningCount" />
          <YzhStatusBadge type="info" :text="'排队 ' + queueDetail.PendingCount" />
          <YzhStatusBadge type="danger" :text="'失败 ' + queueDetail.FailedCount" />
        </div>

        <YzhEmptyState
          v-if="!queueLoading && (queueDetail?.Rows.length ?? 0) === 0"
          title="最近没有处理任务"
          description="上传文件后，转换与提取的进度会出现在这里。"
        />

        <el-collapse v-else class="ed-queue">
          <el-collapse-item
            v-for="q in queueDetail?.Rows ?? []"
            :key="q.Code"
            :name="q.Code"
          >
            <template #title>
              <div class="ed-queue__title">
                <YzhStatusBadge :type="queueStatusBadge(q.Status).type" :text="queueStatusBadge(q.Status).text" />
                <span class="ed-queue__name">{{ q.QueueName || q.QueueType }}</span>
                <span class="ed-dim">{{ q.Code }}</span>
                <span class="ed-dim">{{ q.CompletedCount }} / {{ q.TotalCount }}</span>
              </div>
            </template>
            <el-progress :percentage="q.Progress ?? 0" :stroke-width="6" class="ed-queue__progress" />
            <YzhTable
              :columns="taskColumns"
              :data="queueTasks(q)"
              :toolbar="false"
              :show-pagination="false"
              row-key="Code"
              size="small"
            >
              <template #column-TaskType="{ row }">
                <span class="ed-dim">{{ taskTypeText(row.TaskType) }}</span>
              </template>
              <template #column-Status="{ row }">
                <YzhStatusBadge :type="queueStatusBadge(row.Status).type" :text="queueStatusBadge(row.Status).text" />
              </template>
            </YzhTable>
            <div v-if="q.EndTime && q.StartTime" class="ed-dim">
              耗时：{{ formatDuration(new Date(q.EndTime).getTime() - new Date(q.StartTime).getTime()) }}
            </div>
          </el-collapse-item>
        </el-collapse>
      </div>
    </YzhDrawer>

    <!-- 隐藏的文件选择器（⛔ 不用 el-upload：它拿不到 webkitRelativePath） -->
    <input ref="fileInput" type="file" multiple :accept="UPLOAD_ACCEPT" class="ed-hidden" @change="onFilesChosen" />
    <input
      ref="folderInput"
      type="file"
      multiple
      webkitdirectory
      :accept="UPLOAD_ACCEPT"
      class="ed-hidden"
      @change="onFolderChosen"
    />
  </div>
</template>

<style scoped>
/*
 * ⚠️ 页面外壳必须自带滚动容器：
 *   `YzhTreeTableLayout` 右面板是 `overflow: hidden`，放入页若不自给滚动容器，
 *   内容会被裁掉且整页滚不动（法条 S10 的配套约束）。
 */
.ed-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.ed-main {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
  padding: var(--yzh-space-3, 12px);
  gap: var(--yzh-space-2, 8px);
  box-sizing: border-box;
}

/* 子项不许被压缩（否则表格被挤成 0 高） */
.ed-main > * {
  flex: none;
}

.ed-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
}

.ed-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  flex-wrap: wrap;
}

.ed-header__left {
  min-width: 220px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.ed-header__title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.ed-header__sub {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-secondary);
}

.ed-header__right {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.ed-header__select {
  width: 190px;
}

.ed-header__search {
  width: 170px;
}

.ed-tip {
  flex: none;
}

.ed-banner {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-primary-light-9, #ecf5ff);
}

.ed-banner__spin {
  animation: ed-spin 1.2s linear infinite;
  color: var(--yzh-color-primary, #409eff);
}

@keyframes ed-spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.ed-banner__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-primary);
  white-space: nowrap;
}

.ed-banner__progress {
  flex: 1;
  min-width: 120px;
}

.ed-banner__count {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}

.ed-bulkbar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) 0;
}

.ed-bulkbar__text {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.ed-placeholder {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--el-text-color-secondary);
  font-size: var(--yzh-font-size-sm, 13px);
}

.ed-hint {
  padding: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}

.ed-hint--warn {
  color: var(--yzh-color-warning, #e6a23c);
}

.ed-file {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.ed-file__icon {
  color: var(--yzh-color-primary, #409eff);
  flex: none;
}

.ed-file__name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ed-file__kind {
  flex: none;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-placeholder);
}

.ed-folder {
  display: flex;
  align-items: center;
  gap: 4px;
  min-width: 0;
  color: var(--el-text-color-regular);
}

.ed-folder__icon {
  flex: none;
  color: var(--yzh-color-warning, #e6a23c);
}

.ed-dim {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

/* ── 抽屉通用 ── */
.ed-drawer {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  height: 100%;
  min-height: 0;
  overflow: auto;
}

.ed-drawer__head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.ed-drawer__tabs {
  flex: 1;
  min-height: 0;
}

.ed-anchor,
.ed-excerpt {
  display: block;
  max-height: 60px;
  overflow: auto;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-all;
}

.ed-anchor {
  color: var(--el-text-color-primary);
}

.ed-excerpt {
  color: var(--el-text-color-regular);
}

.ed-version__cur {
  margin-left: var(--yzh-space-1, 4px);
}

/* ── 原文 ── */
.ed-md {
  margin: 0;
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-fill-light, #fafafa);
  border-radius: var(--yzh-radius-sm, 4px);
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  white-space: pre-wrap;
  word-break: break-word;
  color: var(--el-text-color-primary);
}

.ed-md__hit {
  background: var(--yzh-color-warning-light-5, #fdf6ec);
  color: var(--yzh-color-warning, #e6a23c);
  font-weight: 600;
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: 2px;
}

/* ── 预览 ── */
.ed-preview {
  height: 70vh;
  display: flex;
  flex-direction: column;
}

.ed-preview__frame {
  flex: 1;
  width: 100%;
  border: 0;
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-fill-light, #fafafa);
}

/* ── 上传 ── */
.ed-upload {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  max-height: 60vh;
  overflow: auto;
}

.ed-upload__pick {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.ed-upload__list {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--yzh-radius-sm, 4px);
}

.ed-upload__item {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.ed-upload__item:last-child {
  border-bottom: 0;
}

.ed-upload__item-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ed-upload__summary {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-regular);
}

.ed-upload__progress {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-4, 16px) 0;
}

.ed-upload__footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--yzh-space-2, 8px);
}

/* ── 队列 ── */
.ed-queue {
  border-top: 0;
}

.ed-queue__title {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.ed-queue__name {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ed-queue__progress {
  margin-bottom: var(--yzh-space-2, 8px);
}

.ed-hidden {
  display: none;
}
</style>
