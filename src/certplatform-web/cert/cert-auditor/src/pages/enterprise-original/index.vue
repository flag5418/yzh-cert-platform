<script setup lang="ts">
/**
 * 企业原始资料管理（专家端）
 *
 * 菜单 `MENU_AUD_11`｜路由 `/enterprise-original`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★★ 2026-10-07 四轮：**定位纠偏**（用户逐字反馈）
 *
 * 用户原话：「其实针对审核员，是<b>不需要他去确认任何信息</b>的，他甚至可以不关心我们提取出来的
 * 详细内容、分组及文档作用，他需要的只是通过这个进行<b>管理</b>：可以预览，可以针对单个文件进行
 * 替换，可以删除，可以删除整个文件夹，或针对整个文件夹进行更新等操作……针对我们提取的 markdown、
 * 标签、文档作用<b>不用做强制的审批</b>，也<b>不要将审核员最核心的文件管理作用给弱化了</b>……
 * 我们设计系统，不但要考虑程序的逻辑底层，更要考虑<b>审核员他们的操作意愿</b>。」
 *
 * ⇒ **页面主角是「文件管理」，不是「AI 结果审批台」**：
 *   ① 主区 ⛔ 不再按标准分 tab（用户裁决去掉）；标准切换搬进「提取信息」抽屉。
 *   ② 顶部一条「!」说明（`el-alert` 的 `show-icon` 就是那个感叹号）：这页干什么、
 *      改这些有什么用 —— 让审核员自己判断，⛔ 不替他决定。
 *   ③ 行操作 = 提取信息 / 预览 / 下载原件 / 历史版本 / 替换 / 排除提取 / 删除。
 *   ④ **文件夹是一等公民**：更新本文件夹内容 / 不参与提取 / 恢复参与 / 删除整个文件夹。
 *   ⑤ 「提取信息」= 一个**可选**入口（分组与文档作用可改 + 提取内容只读），
 *      ⛔ 不弹「待确认」、⛔ 不阻断流程 —— 不看也能一路走下去。
 *
 * ⚠️ 本轮**不动**的既有约束：
 *   · 状态口径唯一来源 = `logic.statusOf`（根目录表与文件夹树共用一份）
 *   · 页面外壳必须自带滚动容器（见文件末尾 `<style>` 顶部注释）
 *   · ⛔ 不出现 Markdown / 转换链 / 队列 / Sha256 等技术词
 * ════════════════════════════════════════════════════════════════════════
 */
import { onBeforeUnmount, onMounted } from 'vue'
import { YzhTreeTableLayout, YzhTable, YzhEmptyState, YzhDrawer, type YzhAction } from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { YzhFolderUpload } from '@share/components'
import { buildAcceptAttribute, describeAllowed } from '@share/constants/upload-file-policy'

/** ★ accept 由共享契约生成（⛔ 不再本地硬编码后缀串） */
const ACCEPT = buildAcceptAttribute()
import type { AnalyzePolicyKey, PolicyReasonKey } from '@share/api/ent/enterprise-original'
import {
  loadTree, treeNodes, treeHint, scopeLabel, scopeLoading,
  stageCode, files, statusBar, selected, filterTags, onlyUsable, tagOptions,
  filterActive, filterSummary, scopeTotal, filteredCount,
  isBusy, queueProgress, queueLabel, describeQueueType, startPolling, stopPolling,
  onNodeClick, loadTags, clearFilters, refresh, dataRevision,
  standards, activeStandard, activeStandardName,
  previewVisible, previewRow, previewUrl, previewKind, previewText, previewLoading, openPreview, closePreview,
  contentText, contentMessage, contentLoading,
  regenerating, onRegenerate,
  tagDraft, purposeSaving, statusOf, statusTip,
  uploadVisible, uploadStage, planRows, planSummary, uploadProgress,
  uploadTitle, uploadTargetLabel, uploadTargetAlertType,
  openUpload, openFolderUpload, openReplace, onFilesPicked, startUpload, closeUpload, removePlanRow,
  formatSize,
  rootFiles, folderNodes,
  onDownload, onVersions, versionVisible, versions, currentVersion, onRestore,
  detailVisible, detailRow, detailLoading, detailPurposeText, infoTab,
  openDetail, closeDetail, saveDetail, onDetailStandardChange,
  excludeExtract, allowExtract,
  excludeFolder, allowFolder, deleteFolder,
  policyVisible, policyValue, policyReason, openPolicy, submitPolicy,
  onDelete,
  queueVisible, queueDetail, queueLoading, openQueueDetail,
} from './logic'
import { FolderOpened, Document, Loading } from '@element-plus/icons-vue'
import OriginalFolderTree from './components/OriginalFolderTree.vue'
import type { OriginalFolderNode } from './components/OriginalFolderTree.vue'
import type { OriginalFile } from '@share/api/ent/enterprise-original'

/** ★ 专家语言：不是「是否参与识别」而是「这份文件要不要参与自动提取」 */
const POLICY_OPTIONS: Array<{ value: AnalyzePolicyKey; label: string }> = [
  { value: 'analyze', label: '允许提取（默认）' },
  { value: 'skip', label: '只留存，不提取' },
  { value: 'ignore', label: '排除提取（仅作证据）' },
]

/**
 * 列表列（用户 2026-10-07 裁决）：**文件名（含大小）· 提取状态 · 版本 · 操作**。
 *
 * ⛔ 刻意删掉「标签」「作用」两列：它们**按标准各有一份**
 * （一个文件在 N 个标准下有 N 行画像），摊在列表里既占宽、又必须在行上写清「这是哪个标准的」；
 * 专家日常只做「预览 / 更新文件」，标签与作用改到行操作「详情」里按当前标准编辑。
 */
const rootColumns: YzhTableColumn<any>[] = [
  { prop: 'FileName', label: '文件名', minWidth: 260, slot: true },
  { prop: 'ExtractState', label: '提取状态', width: 110, slot: true },
  { prop: 'VersionNumber', label: '版本', width: 60, align: 'center', slot: true }
  // ⛔ 操作列不手写（2026-10-07）：YzhTable 内置行动作列 + action-dropdown-only
  //    列宽按 actionColWidth 自适应；action-fixed=false 保持 36 号 §8.1a 的裁决
  //    —— 本项目 el-table 的 fixed 层里按钮宽度算成 0（点不动）
]

/**
 * 根目录表行操作（纯下拉：一个「操作 ▾」装下全部按钮）。
 *
 * ★ 2026-10-07 四轮：按「文件管理为主角」重排 ——
 *   ① 「详情」+「提取内容」两条合并成一条「**提取信息**」：用户要的是「**一个**提取信息的展示」，
 *      ⛔ 不是页面上一堆并列入口（合并后抽屉内是两个 Tab）。
 *   ② 新增「**替换**」：单个文件换一份新的（走 `upload/*`，旧版本自动保留）。
 *   ③ 「忽略 / 恢复提取」→「**排除提取 / 允许提取**」（用户裁决：「忽略」让人猜不到会发生什么）。
 *   ④ 破坏性动作排最后，且「排除提取」也标 danger —— 它会让这份文件不再产出内容，不是无副作用操作。
 */
function rootRowActions(row: OriginalFile): YzhAction[] {
  return [
    { key: 'info', text: '提取信息' },
    { key: 'preview', text: '预览' },
    { key: 'download', text: '下载原件' },
    { key: 'versions', text: '历史版本' },
    { key: 'replace', text: '替换' },
    row.AnalyzePolicy === 'analyze'
      ? { key: 'exclude', text: '排除提取', type: 'danger' }
      : { key: 'allow', text: '允许提取' },
    { key: 'delete', text: '删除', type: 'danger' }
  ]
}

/** YzhTable @row-action(key,row) → 逻辑层函数 */
function onRootRowAction(key: string, row: OriginalFile) {
  switch (key) {
    case 'info':
      return openDetail(row)
    case 'preview':
      return openPreview(row)
    case 'download':
      return onDownload(row)
    case 'versions':
      return onVersions(row)
    case 'replace':
      return openReplace(row)
    case 'exclude':
      return excludeExtract(row)
    case 'allow':
      return allowExtract(row)
    case 'delete':
      return onDelete(row)
  }
}

/** 「不建议提取」的文件在详情抽屉里一键恢复提取（并关抽屉：表单要按新状态重新加载） */
async function allowFromDetail(): Promise<void> {
  const row = detailRow.value
  if (!row) return
  await allowExtract(row)
  closeDetail()
}

/**
 * ⛔ 原 `extractTip` 已删除（2026-10-07 四轮）。
 *
 * <para>它原来会在状态 tooltip 后追加「系统建议这份文件不参与提取，<b>尚未生效</b>，
 * 可在行操作「详情」里<b>确认</b>」—— 这是一句<b>要审核员去办事</b>的话。
 * 用户逐字：「审核员不需要确认任何信息……不用做强制的审批」⇒ 状态 tooltip 统一回
 * {@link statusTip}（只陈述事实），AI 建议改到「提取信息」抽屉里以中性一行展示。</para>
 */

const REASON_OPTIONS: Array<{ value: PolicyReasonKey; label: string }> = [
  { value: 'covered_by_params', label: '内容已在全局参数里定义过' },
  { value: 'irrelevant', label: '与企业体系无关' },
  { value: 'duplicate', label: '与其他文件重复' },
  { value: 'manual', label: '人工判断' },
]

/** ★ 文件类型中文名（专家不需要看扩展名；⛔ 不用 emoji 图标，用 Element Plus 的 <el-icon>） */
function fileKindText(fileType: string): string {
  const t = (fileType || '').toLowerCase()
  const map: Record<string, string> = {
    '.pdf': 'PDF', '.doc': 'Word', '.docx': 'Word', '.xls': 'Excel', '.xlsx': 'Excel',
    '.ppt': 'PPT', '.pptx': 'PPT', '.txt': '文本', '.md': '文本', '.csv': '表格',
    '.jpg': '图片', '.jpeg': '图片', '.png': '图片', '.gif': '图片', '.bmp': '图片', '.webp': '图片',
  }
  return map[t] || '文件'
}

function selectAll(checked: boolean): void {
  selected.value = checked ? [...files.value] : []
}

onMounted(() => { loadTree(); loadTags(); startPolling() })
onBeforeUnmount(() => { stopPolling() })
</script>

<template>
  <div class="eo-page">
    <YzhTreeTableLayout
      :tree-data="treeNodes"
      :tree-width="280"
      label-field="Label"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      @tree-node-click="onNodeClick"
    >
      <template #treeFooter>
        <div v-if="treeHint" class="eo-hint">{{ treeHint }}</div>
      </template>

      <template #default>
        <div v-loading="scopeLoading" class="eo-main">
          <!-- ══════════ 页头：标题 + 上传 ══════════ -->
          <div class="eo-header">
            <div class="eo-header__left">
              <div class="eo-header__title">{{ scopeLabel || '请先在左侧选择「企业 › 阶段」' }}</div>
              <div v-if="statusBar && stageCode" class="eo-header__sub">
                共 {{ scopeTotal }} 个文件
                <!-- ★ 筛选态必须显式写出「命中几份」：接口的 Total 是过滤前口径，
                     只写「共 N 个文件」会在筛选 0 命中时与空列表自相矛盾（2026-10-06 实测投诉） -->
                <template v-if="filterActive">，其中符合当前筛选的 {{ filteredCount }} 个</template>
                <template v-if="statusBar.UsableCount">，{{ statusBar.UsableCount }} 个已就绪</template>
                <template v-if="statusBar.ConvertingCount || statusBar.AnalyzingCount">，正在处理 {{ statusBar.ConvertingCount + statusBar.AnalyzingCount }} 个</template>
                <template v-if="statusBar.FailedCount">，{{ statusBar.FailedCount }} 个需处理</template>
              </div>
            </div>
            <div class="eo-header__right">
              <el-button size="small" text type="primary" @click="openQueueDetail">处理进度</el-button>
              <el-button v-if="stageCode" size="small" text type="primary" @click="refresh">刷新</el-button>
              <!-- ★ 2026-10-06 文件级队列：⛔ 不再按「阶段忙」禁用上传 —— 同阶段其他文件在跑
                   与本批次无关；同文件重传由后端「取消旧队列重建」兜底（旧阶段级拒绝实测误伤） -->
              <el-button v-if="stageCode" type="primary" @click="openUpload">上传文件</el-button>
            </div>
          </div>

          <!-- ══════════ ★ 处理进度横幅（照抄标准文档管理的 queue-banner）══════════
               有队列在跑时：显示进度条 + 详情 + 自动轮询（2026-10-03 用户要求「显示进度的
               详情，否则会造成误判」——进度详情保留；「卡住该阶段不允许继续上传」随
               2026-10-06 文件级队列重构废除，上传按钮不再禁用） -->
          <div v-if="isBusy && statusBar" class="eo-queue-banner">
            <el-icon class="is-spinning"><Loading /></el-icon>
            <span class="eo-queue-banner__label">{{ queueLabel }}</span>
            <el-progress
              :percentage="queueProgress"
              :stroke-width="6"
              class="eo-queue-banner__progress"
            />
            <span class="eo-queue-banner__count">
              {{ statusBar.QueueCompleted ?? 0 }} / {{ statusBar.QueueTotal ?? 0 }} 个文件
            </span>
            <el-button link type="primary" size="small" @click="openQueueDetail">查看详情</el-button>
          </div>

          <!-- ══════════ 筛选栏（专家语言）══════════ -->
          <div v-if="stageCode" class="eo-filterbar">
            <el-select
              v-model="filterTags" multiple collapse-tags collapse-tags-tooltip clearable
              placeholder="按标签筛选（当前标准）" style="width: 260px"
            >
              <el-option v-for="t in tagOptions" :key="t.TagCode" :label="t.TagName" :value="t.TagCode">
                <span>{{ t.TagName }}</span>
                <span class="eo-dim" style="float: right; font-size: 11px">{{ t.TagGroup }}</span>
              </el-option>
            </el-select>
            <el-checkbox v-model="onlyUsable">只看已就绪的</el-checkbox>
            <el-button v-if="filterTags.length || onlyUsable" size="small" text type="primary" @click="clearFilters">清空筛选</el-button>

            <span class="eo-filterbar__spacer" />

            <el-checkbox
              :model-value="selected.length > 0 && selected.length === files.length"
              :indeterminate="selected.length > 0 && selected.length < files.length"
              @change="selectAll(!!$event)"
            >
              全选
            </el-checkbox>
            <el-button v-if="selected.length > 1" size="small" type="primary" @click="openPolicy('batch')">
              批量设置（已选 {{ selected.length }}）
            </el-button>
          </div>

          <!-- ══════════ 文件夹树（照抄资料库布局）══════════
               每个文件夹 = 一张紧凑文件表；企业文件多，摊平/卡片都看不清归属。 -->
          <div v-if="!stageCode" class="eo-placeholder">
            <p>请在左侧选择「企业 › 阶段」，然后上传企业交来的原始资料。</p>
          </div>

          <YzhEmptyState
            v-else-if="folderNodes.length === 0 && rootFiles.length === 0"
            :title="filterActive ? '没有符合筛选条件的文件' : '这里还没有文件'"
            :description="filterActive
              ? '本阶段共 ' + scopeTotal + ' 个文件，当前筛选（' + filterSummary + '）没有匹配到任何一份。'
              : ''"
          >
            <template #action>
              <!-- ★ 筛选未命中的出路是「清空筛选」，⛔ 不是「上传文件」——
                   给错动作会让用户以为文件丢了，重新上传一遍（2026-10-06 实测投诉） -->
              <el-button v-if="filterActive" type="primary" @click="clearFilters">清空筛选</el-button>
              <el-button v-else type="primary" @click="openUpload">上传文件</el-button>
            </template>
          </YzhEmptyState>

          <div v-else class="eo-tree-wrap">
            <!-- 根目录 -->
            <div v-if="rootFiles.length" class="eo-root">
              <div class="eo-root__title">
                <el-icon><FolderOpened /></el-icon>
                <span>根目录</span>
                <el-tag size="small" type="info">{{ rootFiles.length }} 个文件</el-tag>
              </div>
              <YzhTable
                :key="'root/' + dataRevision"
                :columns="rootColumns"
                :data-loader="async () => ({ rows: rootFiles, total: rootFiles.length })"
                :toolbar="false"
                :show-pagination="false"
                :row-action-buttons="rootRowActions"
                :action-dropdown-only="true"
                :action-fixed="false"
                select-mode="multiple"
                row-key="Code"
                empty-text="暂无文件"
                @row-action="onRootRowAction"
              >
                <template #column-FileName="{ row }">
                  <div class="f-name">
                    <el-icon class="f-name__icon"><Document /></el-icon>
                    <span class="f-name__text" :title="row.FileName">{{ row.FileName }}</span>
                  </div>
                  <!-- ★ 大小放文件名副行（用户裁决：文件名 + 大小合成一列，不必单开一列） -->
                  <div class="f-sub">{{ formatSize(row.FileSize) }}</div>
                </template>
                <!-- ★ 提取状态与文件夹树**共用同一份口径**（logic.statusOf）——
                     ⛔ 不再各判一套：原来上表只判两态、文件夹树判七态，
                     同一份文件在页面上下两处显示不同状态 = 用户报的「显示的信息不正确」之一 -->
                <template #column-ExtractState="{ row }">
                  <el-tooltip v-if="statusTip(row)" :content="statusTip(row)" placement="top">
                    <span>
                      <el-tag size="small" :type="statusOf(row).type">{{ statusOf(row).text }}</el-tag>
                    </span>
                  </el-tooltip>
                  <el-tag v-else size="small" :type="statusOf(row).type">{{ statusOf(row).text }}</el-tag>
                </template>
                <template #column-VersionNumber="{ row }">
                  <span v-if="row.VersionNumber > 1">v{{ row.VersionNumber }}</span>
                  <span v-else class="f-dim">—</span>
                </template>
                <!-- 操作列改由 YzhTable 内置列渲染（rootRowActions + action-dropdown-only），
                     原 #column-Actions 手写插槽已于 2026-10-07 删除 -->
              </YzhTable>
            </div>

            <!-- 文件夹树 -->
            <OriginalFolderTree
              v-if="folderNodes.length"
              :nodes="folderNodes as OriginalFolderNode[]"
              :data-revision="dataRevision"
              :busy="isBusy"
              @detail="openDetail"
              @regenerate="onRegenerate"
              @preview="openPreview"
              @replace="openReplace"
              @exclude="excludeExtract"
              @allow="allowExtract"
              @versions="onVersions"
              @download="onDownload"
              @delete="onDelete"
              @folder-update="openFolderUpload"
              @folder-exclude="excludeFolder"
              @folder-allow="allowFolder"
              @folder-delete="deleteFolder"
            />
          </div>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- ═══════════ 预览抽屉：专家直接看文件内容 ═══════════ -->
    <el-drawer v-model="previewVisible" :title="previewRow?.FileName || '预览'" size="900px" @close="closePreview">
      <div v-loading="previewLoading" class="eo-preview">
        <template v-if="previewKind === 'image'">
          <img :src="previewUrl" class="eo-preview__img" alt="预览" />
        </template>
        <template v-else-if="previewKind === 'pdf'">
          <iframe :src="previewUrl" class="eo-preview__pdf" title="文件预览" />
        </template>
        <template v-else-if="previewKind === 'text'">
          <pre class="eo-preview__text">{{ previewText || '（文件内容为空）' }}</pre>
        </template>
        <YzhEmptyState v-else-if="!previewLoading" title="这个格式暂时无法预览，请下载原件查看" />
      </div>
    </el-drawer>

    <!-- ═══════════ ★ 「提取信息」抽屉（2026-10-07 四轮：详情 + 提取内容 合并）═══════════
         用户逐字：「可以增加一个**提取信息的展示**，审核员**可以关心，可以点击查看**，
         或去更改按标准显示的分组、文档作用、提取信息这些，**但不是强求**」。
         ⇒ 它不再是「请审核员确认 AI 结果」的审批台，而是一个**可选**入口：
           Tab1「分组与文档作用」可改；Tab2「提取内容」只读。
           ⛔ 不弹「待确认」、⛔ 不阻断流程 —— 不看也能一路走下去。
         ★ **标准切换器搬进了这里**：主区已按用户裁决去掉标准 tab，但
           「一个文件 × 一个标准 = 一行画像」这个事实没变 ⇒ 阶段绑了多个标准时必须能选
           「按哪个标准看」，否则后端只能取「某一行」，标签会串到另一个标准。
         ★ 用 `YzhDrawer`（法条 S07）—— 原来是「裸 el-drawer + 另一个 YzhDrawer」，
           合并后只剩一个，裸抽屉数量是净减少。 -->
    <YzhDrawer
      :model-value="detailVisible" size="720px"
      :title="detailRow ? ('提取信息 · ' + detailRow.FileName) : '提取信息'"
      @update:model-value="(v: boolean) => { if (!v) closeDetail() }"
    >
      <div v-loading="detailLoading" class="eo-detail">
        <template v-if="detailRow">
          <!-- 一行元信息：提取状态 + 大小 + 类型 +（有才显示）系统建议 -->
          <div class="eo-detail__meta">
            <el-tag size="small" :type="statusOf(detailRow).type">{{ statusOf(detailRow).text }}</el-tag>
            <span class="eo-dim">{{ formatSize(detailRow.FileSize) }}</span>
            <span class="eo-dim">{{ fileKindText(detailRow.FileType) }}</span>
            <!-- ★ AI 建议改**中性陈述**：⛔ 不再写「尚未生效，请人工确认」
                 （用户裁决：不做强制的审批 —— 系统给个参考，采不采由人自己判断） -->
            <span v-if="detailRow.PolicySource === 'ai' && detailRow.PolicyReason" class="eo-dim">
              系统建议：不参与提取（仅供参考）
            </span>
          </div>

          <!-- 预览 / 下载 / 历史版本 / 替换：一行文字按钮（就近入口，⛔ 不重复做区块） -->
          <div class="eo-detail__quick">
            <el-button link type="primary" size="small" @click="openPreview(detailRow)">预览</el-button>
            <el-divider direction="vertical" />
            <el-button link type="primary" size="small" @click="onDownload(detailRow)">下载原件</el-button>
            <el-divider direction="vertical" />
            <el-button link type="primary" size="small" @click="onVersions(detailRow)">历史版本</el-button>
            <el-divider direction="vertical" />
            <el-button link type="primary" size="small" @click="openReplace(detailRow)">替换</el-button>
          </div>

          <!-- ★ 标准选择器（该阶段绑了多个标准才出）—— 见抽屉头部注释 -->
          <div v-if="standards.length > 1" class="eo-detail__block">
            <div class="eo-detail__label">按哪个标准查看</div>
            <el-radio-group
              :model-value="activeStandard"
              @change="(v: any) => onDetailStandardChange(String(v))"
            >
              <el-radio-button v-for="s in standards" :key="s.Code" :value="s.Code">
                {{ s.StandardName || s.StandardCode }}
              </el-radio-button>
            </el-radio-group>
          </div>

          <el-tabs v-model="infoTab" class="eo-detail__tabs">
            <!-- ── Tab 1：分组与文档作用（可改，但不是强求） ── -->
            <el-tab-pane label="分组与文档作用" name="group">
              <!-- 该阶段没关联标准 ⇒ 标签/作用无处可存，说清楚，⛔ 不给一个存不进去的空表单 -->
              <el-alert v-if="!activeStandard" type="warning" :closable="false" show-icon class="eo-alert">
                该认证阶段还没有关联标准，标签与文档作用无法按标准保存。
                请先到「认证阶段」里为该阶段关联标准。
              </el-alert>

              <!-- ⛔ 不参与提取的文件不提供标签/作用编辑（营业执照、身份证等特定证件
                   没有「体系文件作用」语义，硬打标签会污染召回词表） -->
              <el-alert v-else-if="detailRow.IsNotSuggested" type="info" :closable="false" show-icon class="eo-alert">
                这份文件当前<b>不参与提取</b>，因此没有标签和文档作用。若判断有误，点下方「允许提取」。
              </el-alert>

              <template v-else>
                <div class="eo-detail__block">
                  <div class="eo-detail__label">
                    标签
                    <span class="eo-dim">（决定这份资料在「{{ activeStandardName }}」下归到哪几类，只能从清单里选）</span>
                  </div>
                  <el-select
                    v-model="tagDraft" multiple filterable clearable size="large" style="width: 100%"
                    placeholder="选择这份资料属于哪几类"
                  >
                    <el-option v-for="t in tagOptions" :key="t.TagCode" :label="t.TagName" :value="t.TagCode">
                      <div class="eo-opt"><span>{{ t.TagName }}</span><span class="eo-dim">{{ t.TagGroup }}</span></div>
                    </el-option>
                  </el-select>
                </div>

                <!-- 文档作用：**一个文本框**。
                     ⛔ 不再拆四段 —— 「审核关注点 / 审核内容」系统根本不使用、审核员也不需要填（用户裁决）。
                     ⚠️ 后端 DocPurpose 只作自由文本存储（无代码解析【】分段；结构化要素另存 InfoItemsJson）
                        ⇒ 整段编辑不会破坏任何下游。 -->
                <div class="eo-detail__block">
                  <div class="eo-detail__label">
                    文档作用
                    <span class="eo-dim">（系统自动生成，可直接改；只影响「{{ activeStandardName }}」这一份）</span>
                  </div>
                  <el-input
                    v-model="detailPurposeText" type="textarea" :rows="10"
                    placeholder="尚未生成，可手工填写这份资料能证明什么"
                  />
                </div>
              </template>
            </el-tab-pane>

            <!-- ── Tab 2：提取内容（只读，出问题时查证用，⛔ 不是给人逐字核对） ── -->
            <el-tab-pane label="提取内容" name="content">
              <div v-loading="contentLoading" class="eo-content">
                <pre v-if="contentText" class="eo-content__md">{{ contentText }}</pre>
                <YzhEmptyState
                  v-else-if="!contentLoading"
                  title="这份文件还没有提取内容"
                  :description="contentMessage"
                >
                  <template #action>
                    <el-button
                      v-if="detailRow" type="primary"
                      :loading="regenerating === detailRow.Code"
                      @click="onRegenerate(detailRow)"
                    >重新生成</el-button>
                  </template>
                </YzhEmptyState>
              </div>
            </el-tab-pane>
          </el-tabs>
        </template>
      </div>

      <template #footer>
        <el-button type="default" @click="closeDetail">关闭</el-button>
        <el-button
          v-if="detailRow && !detailRow.IsNotSuggested"
          type="default"
          :loading="regenerating === detailRow.Code"
          @click="onRegenerate(detailRow)"
        >重新生成内容</el-button>
        <!-- 不参与提取的文件：给出一条恢复的出路（原来只能去行操作下拉里找） -->
        <el-button v-if="detailRow?.IsNotSuggested" type="primary" @click="allowFromDetail">允许提取</el-button>
        <el-button
          v-if="detailRow && !detailRow.IsNotSuggested && activeStandard"
          type="primary" :loading="purposeSaving" @click="saveDetail"
        >保存</el-button>
      </template>
    </YzhDrawer>

    <!-- ═══════════ 上传抽屉 ═══════════
         ★ 2026-10-07 四轮：同一个抽屉现在有三个入口（上传文件 / 更新文件夹内容 / 替换单个文件），
           标题与目标提示条都跟着「本次目标」走 —— 否则从「替换」点进来的人看到的仍是
           「上传文件」，只能靠猜「我这次传的东西会落到哪、会不会改名」。 -->
    <el-drawer v-model="uploadVisible" :title="uploadTitle" size="620px" @close="closeUpload">
      <!-- ★ 目标提示条（常规上传时不出现）—— 说明「这次会落到哪里」 -->
      <el-alert
        v-if="uploadTargetLabel"
        :type="uploadTargetAlertType"
        :closable="false" show-icon class="eo-alert"
      >
        {{ uploadTargetLabel }}
      </el-alert>
      <el-alert type="info" :closable="false" show-icon class="eo-alert">
        会保留文件夹结构。已存在且内容相同的文件会自动跳过；内容不同的会作为新版本保留旧版。
      </el-alert>
      <el-alert type="warning" :closable="false" show-icon class="eo-alert">
        支持：{{ describeAllowed() }}。压缩包和可执行文件不支持。
      </el-alert>

      <div class="eo-upload">
        <YzhFolderUpload :multiple="true" :accept="ACCEPT" @change="onFilesPicked" />
      </div>

      <div v-if="planRows.length > 0 || planSummary.FilteredCount > 0" class="eo-plan">
        <!-- ★ 被静默剔除的系统文件：明确告诉用户「已自动忽略」，而不是让它们变成一堆
             莫名其妙的不合规项（2026-10-03 用户报障：.DS_Store 没被过滤、且删不掉） -->
        <el-alert
          v-if="planSummary.FilteredCount > 0"
          type="info" :closable="false" show-icon class="eo-alert"
        >
          已自动忽略 <b>{{ planSummary.FilteredCount }}</b> 个系统文件<template v-if="planSummary.FilteredNames?.length">（{{ planSummary.FilteredNames.join('、') }}{{ planSummary.FilteredCount > planSummary.FilteredNames.length ? ' 等' : '' }}）</template>，这些不是企业资料，无需处理。
        </el-alert>

        <div class="eo-plan__summary">
          共 {{ planRows.length }} 个文件
          <template v-if="planSummary.CreateCount">，新建 {{ planSummary.CreateCount }}</template>
          <template v-if="planSummary.ReplaceCount">，{{ planSummary.ReplaceCount }} 个是更新旧文件</template>
          <template v-if="planSummary.SkipCount">，{{ planSummary.SkipCount }} 个内容相同会跳过</template>
          <template v-if="planSummary.BlockedCount">，<b class="eo-plan__bad">{{ planSummary.BlockedCount }} 个不支持</b>（上传时自动跳过）</template>
        </div>
        <ul class="eo-plan__list">
          <li v-for="(r, i) in planRows" :key="r.FileName + '_' + i">
            <span class="eo-plan__name">{{ r.FileName }}</span>
            <span v-if="r.Blocked" class="eo-plan__why">{{ r.BlockReason }}</span>
            <el-tag v-if="r.Blocked" size="small" type="danger">不支持</el-tag>
            <el-tag v-else-if="r.Action === 'skip'" size="small" type="info">内容相同，跳过</el-tag>
            <el-tag v-else-if="r.Action === 'replace'" size="small" type="warning">更新为新版本</el-tag>
            <el-tag v-else size="small" type="success">新增</el-tag>
            <!-- ★ 可移除：解决「不支持的文件在弹窗里删不掉」
                 ⚠️ type 用 danger（移除动作）⛔ 不用 info/success/warning —— 守卫 S03a：
                    状态色（info/success/warning）只该给 el-tag，按钮语义色是 primary/danger -->
            <el-button v-if="r.Blocked" link type="danger" size="small" class="eo-plan__del" @click="removePlanRow(i)">
              移除
            </el-button>
          </li>
        </ul>
      </div>

      <div v-if="uploadStage === 'uploading'" class="eo-progress">{{ uploadProgress }}</div>
      <div v-else-if="uploadStage === 'done'" class="eo-progress eo-progress--done">{{ uploadProgress }}</div>

      <template #footer>
        <el-button type="primary" @click="closeUpload">{{ uploadStage === 'done' ? '关闭' : '取消' }}</el-button>
        <el-button
          v-if="uploadStage !== 'done'" type="primary"
          :loading="uploadStage === 'uploading'" :disabled="planRows.length === 0"
          @click="startUpload"
        >开始上传</el-button>
      </template>
    </el-drawer>

    <!-- ═══════════ 「不参与识别」抽屉（专家语言）══════════ -->
    <el-drawer v-model="policyVisible" title="设置这份文件是否参与识别" size="440px">
      <el-form label-width="90px">
        <el-form-item label="参与识别">
          <el-radio-group v-model="policyValue">
            <el-radio v-for="o in POLICY_OPTIONS" :key="o.value" :value="o.value">{{ o.label }}</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="原因">
          <el-select v-model="policyReason" clearable placeholder="可不填" style="width: 100%">
            <el-option v-for="o in REASON_OPTIONS" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
        </el-form-item>
      </el-form>
      <el-alert type="info" :closable="false" show-icon>
        「只留存，不参与识别」的文件仍会作为证据保留，只是不再自动提取内容。
      </el-alert>
      <template #footer>
        <el-button type="primary" @click="policyVisible = false">取消</el-button>
        <el-button type="primary" @click="submitPolicy">保存</el-button>
      </template>
    </el-drawer>

    <!-- ═══════════ 处理进度抽屉 ═══════════ -->
    <el-drawer v-model="queueVisible" title="处理进度" size="720px">
      <div v-loading="queueLoading">
        <YzhEmptyState v-if="!queueLoading && (queueDetail?.Rows ?? []).length === 0" title="当前没有进行中的处理" />
        <div v-for="q in queueDetail?.Rows ?? []" :key="q.Code" class="eo-q">
          <div class="eo-q__head">
            <span>{{ describeQueueType(q.QueueType) }}</span>
            <el-tag size="small" :type="q.Status === 'completed' ? 'success' : q.Status === 'failed' ? 'danger' : 'info'">
              {{ { pending: '排队中', running: '处理中', processing: '处理中', completed: '完成', failed: '失败', cancelled: '已取消' }[q.Status as string] || q.Status }}
            </el-tag>
            <span class="eo-dim">进度 {{ q.CompletedCount }}/{{ q.TotalCount }}</span>
          </div>
        </div>
      </div>
    </el-drawer>

    <!-- ═══════════ 历史版本抽屉 ═══════════ -->
    <el-drawer v-model="versionVisible" title="历史版本" size="560px">
      <el-alert type="info" :closable="false" show-icon class="eo-alert">
        当前是第 {{ currentVersion }} 版。恢复旧版本后，当前版本会作为历史保留。
      </el-alert>
      <YzhTable
        :columns="[
          { prop: 'VersionNumber', label: '版本', width: 80 },
          { prop: 'FileName', label: '文件名', minWidth: 180 },
          { prop: 'CreateTime', label: '归档时间', width: 170 },
          { prop: '_op', label: '', width: 90 },
        ]"
        :data-loader="async () => ({ rows: versions, total: versions.length })"
        :show-pagination="false" :toolbar="false" row-key="Code"
      >
        <template #column-_op="{ row }">
          <el-button link type="primary" size="small" @click="onRestore(row)">恢复</el-button>
        </template>
      </YzhTable>
    </el-drawer>
  </div>
</template>

<style scoped>
/* ══════════ ★ 页面外壳：本页必须自己提供滚动容器（2026-10-07 修复）══════════
   病因：`YzhTreeTableLayout` 的右面板是 `overflow: hidden`（布局壳的既定约束，
        多页共用、⛔ 不在这里改），所以**页面必须自己给出滚动容器**。
        原来 `.eo-main` 只有 `min-height: 0`、没有 `flex: 1` / `overflow: auto`
        ⇒ 它按内容高度撑开、被父面板裁掉，**既看不到下方内容、也滚不动**。
   症状（用户报障「为什么技术类的不能展开了」）：
        文件夹树默认展开前两层 ⇒ 「技术类」**本来就是展开的**（箭头朝下），
        但它的表格落在视口之外且**永远不可达** ⇒ 用户点一下其实是「收起」，
        可视区毫无变化 ⇒ 看起来像「展不开」。
   修法：与同仓 `enterprise-normalize` 的 `.en-page/.en-main` 逐字同款。 */
.eo-page { height: 100%; display: flex; flex-direction: column; overflow: hidden; }
.eo-main {
  flex: 1; min-height: 0; overflow: auto;
  padding: 14px; display: flex; flex-direction: column; gap: 10px;
  /* ★ 2026-10-07：显式给白底。原来不设背景 ⇒ `.eo-main` 是透明，露出外层
     `el-main`（`.auditor-layout__main`）的 `--el-fill-color-light` 灰底（实测 rgb(248,250,252)），
     看起来像「主区没铺满/发灰」。⛔ 不要指望父级变白 —— 那个灰底是全站布局的一部分。 */
  background: var(--el-bg-color);
}

/* ══════════ ★★ 子项一律不收缩（2026-10-07 修复「告警只显示一行」）══════════
   病因：`.eo-main` 是**列方向 flex 容器**且高度由外部给定（`flex:1` + `overflow:auto`）。
        flex 子项默认 `flex-shrink:1`，容器内容超高时会把它们**压缩**；
        正常情况下子项的 `min-height:auto` 会兜住（= 不许压到内容高度以下），
        **但 `min-height:auto` 只在 `overflow:visible` 时生效** ——
        而 Element Plus 的 `.el-alert` 自带 `overflow:hidden` ⇒ 它的自动最小尺寸直接塌成 `0`
        ⇒ 被压成一条线、文字被自己的 `overflow:hidden` 裁掉。
   实测（真实数据 101 份文件，主区可视高 354px）：
        `.el-alert` clientHeight **16** / scrollHeight **290**（被压掉 274px）；
        同层兄弟 `.eo-header` 261/261、`.eo-filterbar` 132/132、`.eo-tree-wrap` 7991/7992 —— 全部完好，
        因为它们的 `overflow` 是 `visible`。⇒ **症状只落在「自带 overflow:hidden 的子项」上**，
        这也是它看起来像「某个组件坏了」而不是「布局问题」的原因。
   修法：`flex: none`（= 0 0 auto）让高度回归内容，超高交给 `.eo-main` 的 `overflow:auto` 滚动。
        ⛔ 不用逐个给 `.el-alert` 打补丁 —— 任何将来加进来的 `overflow:hidden` 子项都会踩同一个坑。 */
.eo-main > * { flex: none; }

.eo-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
.eo-header__title { font-size: 15px; font-weight: 600; }
.eo-header__sub { font-size: 12px; color: var(--el-text-color-secondary); margin-top: 2px; }
.eo-header__right { display: flex; gap: 6px; flex: none; }

.eo-hint { color: var(--el-text-color-secondary); font-size: 12px; }
.eo-dim { color: var(--el-text-color-placeholder); }

.eo-queue-banner {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 12px; border-radius: 4px;
  background: var(--el-color-primary-lighter);
}
.eo-queue-banner__label { font-size: 13px; font-weight: 500; color: var(--el-color-primary); flex: none; }
.eo-queue-banner__progress { flex: 1; min-width: 120px; }
.eo-queue-banner__count { font-size: 12px; color: var(--el-text-color-regular); flex: none; }

.eo-filterbar {
  display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
  padding: 8px 10px; background: var(--el-fill-color-lighter); border-radius: 4px;
}
.eo-filterbar__spacer { flex: 1; }

/* ⛔ `.eo-stdtabs*` 已于 2026-10-07 四轮删除 —— 主区不再按标准分 tab
   （用户裁决去掉），标准切换搬进「提取信息」抽屉，这几条选择器已无任何元素命中。 */

.eo-placeholder { padding: 60px 0; text-align: center; color: var(--el-text-color-secondary); }

/* ══════════ 文件夹树 ══════════ */
.eo-tree-wrap { min-width: 0; }
.eo-root { margin-bottom: 10px; }
.eo-root__title { display: flex; align-items: center; gap: 6px; font-weight: 500; padding: 4px 0 6px; }
.f-name { display: flex; align-items: center; gap: 5px; }
.f-name__icon { color: var(--el-text-color-secondary); flex: none; }
.f-name__text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.f-sub { font-size: 11px; color: var(--el-text-color-placeholder); margin-top: 1px; }
.f-dim { color: var(--el-text-color-placeholder); }
/* ⛔ `.f-tags` / `.f-tags__item` / `.f-purpose` 已于 2026-10-07 删除 ——
   标签与作用不再上表（用户裁决），它们只活在详情抽屉里 */

/* ══════════ 详情抽屉 ══════════ */
.eo-detail { min-height: 200px; }
.eo-content { min-height: 200px; }
.eo-content__md {
  max-height: 60vh; overflow: auto; margin: 0;
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-subtle, var(--el-fill-color-light));
  border-radius: var(--yzh-radius-sm, 4px);
  font-family: SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.7);
  white-space: pre-wrap; word-break: break-word;
  color: var(--yzh-color-text-primary);
}
.eo-detail__meta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; font-size: 13px; }
/* 详情抽屉里的「预览 / 下载原件 / 提取内容 / 历史版本」一行文字入口 */
.eo-detail__quick { display: flex; align-items: center; gap: var(--yzh-space-1, 4px); margin-bottom: var(--yzh-space-3, 12px); }
.eo-detail__block { margin-bottom: 16px; }
.eo-detail__label { font-size: 13px; font-weight: 600; margin-bottom: 6px; }
.eo-detail__preview { display: flex; gap: 6px; }
.eo-opt { display: flex; justify-content: space-between; gap: 10px; }

/* ══════════ 旧卡片样式（保留兼容，实际不再使用）══════════ */
.eo-cards { display: flex; flex-direction: column; gap: 10px; overflow-y: auto; padding-right: 4px; }
.eo-card {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px; padding: 12px 14px;
  background: var(--el-bg-color);
  transition: border-color .15s, box-shadow .15s;
}
.eo-card:hover { border-color: var(--el-color-primary-light-5); }
.eo-card--picked { border-color: var(--el-color-primary); background: var(--el-color-primary-lighter); }

.eo-card__head { display: flex; align-items: center; gap: 10px; }
.eo-card__icon { font-size: 20px; line-height: 1; }
.eo-card__title { flex: 1; min-width: 0; }
.eo-card__name { font-size: 14px; font-weight: 600; display: flex; align-items: center; gap: 6px; }
.eo-card__meta { font-size: 12px; color: var(--el-text-color-secondary); margin-top: 1px; }

.eo-card__line { display: flex; align-items: flex-start; gap: 10px; margin-top: 9px; }
.eo-card__label { width: 34px; flex: none; color: var(--el-text-color-secondary); font-size: 13px; padding-top: 3px; }
.eo-card__tags { flex: 1; display: flex; gap: 5px; flex-wrap: wrap; }
.eo-card__tag { cursor: pointer; }
.eo-card__purpose { flex: 1; font-size: 13px; color: var(--el-text-color-regular); padding-top: 3px;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.eo-card__purpose-text { cursor: pointer; }
.eo-card__empty { cursor: pointer; color: var(--el-text-color-placeholder); font-size: 13px; }
.eo-card__alert { margin-top: 9px; }
.eo-card__foot { display: flex; align-items: center; gap: 4px; margin-top: 10px; }
.eo-card__spacer { flex: 1; }

/* ══════════ 抽屉 ══════════ */
.eo-alert { margin-bottom: 10px; }
.eo-preview { min-height: 300px; }
.eo-preview__img { max-width: 100%; display: block; margin: 0 auto; }
.eo-preview__pdf { width: 100%; height: 70vh; border: 1px solid var(--el-border-color-lighter); border-radius: 4px; }
.eo-preview__text {
  max-height: 70vh; overflow: auto; padding: 14px; line-height: 1.8;
  background: var(--el-fill-color-light); border-radius: 4px;
  white-space: pre-wrap; overflow-wrap: anywhere; font-size: 13px;
}
.eo-orig { margin-top: 14px; font-size: 12px; color: var(--el-text-color-secondary);
  display: flex; gap: 5px; flex-wrap: wrap; align-items: center; }
.eo-orig__label { margin-right: 4px; }

.eo-upload { margin: 12px 0; }
.eo-plan__summary { font-size: 13px; margin-bottom: 6px; }
.eo-plan__list { list-style: none; margin: 0; padding: 0; max-height: 260px; overflow: auto; }
.eo-plan__list li { display: flex; align-items: center; gap: 8px; padding: 4px 0; font-size: 13px; }
.eo-plan__why { flex: 1; min-width: 0; color: var(--el-text-color-secondary);
  font-size: var(--yzh-font-size-xs, 12px);
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.eo-plan__bad { color: var(--el-color-danger); }
.eo-plan__del { flex: none; }
.eo-plan__name { flex: none; max-width: 42%; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.eo-progress { margin-top: 10px; font-size: 13px; }
.eo-progress--done { color: var(--el-color-success); }

.eo-q { padding: 8px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.eo-q__head { display: flex; align-items: center; gap: 10px; font-size: 13px; }
</style>
