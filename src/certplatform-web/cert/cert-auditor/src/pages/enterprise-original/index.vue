<script setup lang="ts">
/**
 * 企业原始资料管理（专家端）
 *
 * 菜单 `MENU_AUD_11`｜路由 `/enterprise-original`
 *
 * ★★ 面向专家设计（2026-10-03 重做）。专家只关心三件事：
 *   ① 文件处理好了吗  ② 内容对不对（预览）  ③ 系统认出的标签/作用准不准（可改）
 *
 * ⇒ 主体是**卡片列表**而非数据表格；三个动作（预览 / 改标签 / 改作用）摊在卡面上。
 * ⇒ ⛔ 不出现 Markdown / 转换链 / 队列 / Sha256 等技术词。
 *    三态（转换/Markdown/分析）在 UI 上压成**一个**「文件状态」。
 */
import { onBeforeUnmount, onMounted } from 'vue'
import { YzhTreeTableLayout, YzhTable } from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { YzhFolderUpload } from '@share/components'
import type { AnalyzePolicyKey, PolicyReasonKey } from '@share/api/ent/enterprise-original'
import {
  loadTree, treeNodes, treeHint, scopeLabel, scopeLoading,
  stageCode, files, statusBar, selected, filterTags, onlyUsable, tagOptions, tagNameMap,
  isBusy, queueProgress, queueLabel, startPolling, stopPolling,
  onNodeClick, loadTags, clearFilters, refresh, dataRevision,
  previewVisible, previewRow, previewUrl, previewKind, previewText, previewLoading, openPreview, closePreview,
  tagDraft,
  PURPOSE_SEGMENTS, purposeSaving,
  uploadVisible, uploadStage, planRows, planSummary, uploadProgress, openUpload, onFilesPicked, startUpload, closeUpload,
  formatSize,
  rootFiles, folderNodes,
  onDownload, onVersions, versionVisible, versions, currentVersion, onRestore,
  detailVisible, detailRow, detailLoading, detailPurpose,
  openDetail, closeDetail, saveDetail, initDetailDraft,
  quickIgnore, quickParticipate,
  policyVisible, policyValue, policyReason, openPolicy, submitPolicy,
  onDelete,
  queueVisible, queueDetail, queueLoading, openQueueDetail,
} from './logic'
import { FolderOpened, Document, Loading } from '@element-plus/icons-vue'
import OriginalFolderTree from './components/OriginalFolderTree.vue'
import type { OriginalFolderNode } from './components/OriginalFolderTree.vue'

/** ★ 上传格式白名单（与后端 AllowedExtensions 逐字一致）；之前没传 accept 导致任意类型都能选 */
const ACCEPT = '.doc,.docx,.xls,.xlsx,.ppt,.pptx,.rtf,.odt,.ods,.odp,.txt,.md,.csv,.pdf,.jpg,.jpeg,.png,.gif,.bmp,.webp'

/** ★ 专家语言：不是「是否参与识别」而是「这份文件要不要参与自动提取」 */
const POLICY_OPTIONS: Array<{ value: AnalyzePolicyKey; label: string }> = [
  { value: 'analyze', label: '参与识别（默认）' },
  { value: 'skip', label: '只留存，不参与识别' },
  { value: 'ignore', label: '不参与，留作证据' },
]
/** 根目录区（企业直接放在阶段根下的文件）用的同一套列 */
/** 与文件夹树内同一套列（总宽 ~950px，一屏放得下，⛔ 不用 fixed 列 —— 见组件内注释） */
const rootColumns: YzhTableColumn<any>[] = [
  { prop: 'FileName', label: '文件名', width: 200, slot: true },
  { prop: 'Tags', label: '标签', width: 140, slot: true },
  { prop: 'DocPurpose', label: '作用', minWidth: 180, slot: true },
  { prop: 'ExtractState', label: '提取', width: 92, slot: true },
  { prop: 'VersionNumber', label: '版本', width: 56, align: 'center', slot: true },
  { prop: 'Actions', label: '操作', width: 230, slot: true },
]

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
                共 {{ statusBar.Total }} 个文件
                <template v-if="statusBar.UsableCount">，{{ statusBar.UsableCount }} 个已就绪</template>
                <template v-if="statusBar.ConvertingCount || statusBar.AnalyzingCount">，正在处理 {{ statusBar.ConvertingCount + statusBar.AnalyzingCount }} 个</template>
                <template v-if="statusBar.FailedCount">，{{ statusBar.FailedCount }} 个需处理</template>
              </div>
            </div>
            <div class="eo-header__right">
              <el-button size="small" text @click="openQueueDetail">处理进度</el-button>
              <el-button v-if="stageCode" size="small" text @click="refresh">刷新</el-button>
              <el-tooltip
                v-if="stageCode"
                :disabled="!isBusy"
                :content="isBusy ? '上一批资料还在处理中，完成后才能继续上传' : ''"
                placement="bottom"
              >
                <span>
                  <el-button type="primary" :disabled="isBusy" @click="openUpload">
                    {{ isBusy ? '处理中，暂不能上传' : '上传文件' }}
                  </el-button>
                </span>
              </el-tooltip>
            </div>
          </div>

          <!-- ══════════ ★ 处理进度横幅（照抄标准文档管理的 queue-banner）══════════
               有队列在跑时：显示进度条 + 详情，并**禁用上传按钮**。
               2026-10-03 用户要求：「应该类似后台管理的标准文档管理，有队列的进度条，
               并卡住该阶段不允许继续上传，只有队列完成后才能继续上传，可以显示进度的详情，
               否则会造成误判」 -->
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
              placeholder="按标签筛选文件" style="width: 260px"
            >
              <el-option v-for="t in tagOptions" :key="t.TagCode" :label="t.TagName" :value="t.TagCode">
                <span>{{ t.TagName }}</span>
                <span class="eo-dim" style="float: right; font-size: 11px">{{ t.TagGroup }}</span>
              </el-option>
            </el-select>
            <el-checkbox v-model="onlyUsable">只看已就绪的</el-checkbox>
            <el-button v-if="filterTags.length || onlyUsable" size="small" text @click="clearFilters">清空筛选</el-button>

            <span class="eo-filterbar__spacer" />

            <el-checkbox
              :model-value="selected.length > 0 && selected.length === files.length"
              :indeterminate="selected.length > 0 && selected.length < files.length"
              @change="selectAll(!!$event)"
            >
              全选
            </el-checkbox>
            <el-button v-if="selected.length > 1" size="small" @click="openPolicy('batch')">
              批量设置（已选 {{ selected.length }}）
            </el-button>
          </div>

          <!-- ══════════ 文件夹树（照抄资料库布局）══════════
               每个文件夹 = 一张紧凑文件表；企业文件多，摊平/卡片都看不清归属。 -->
          <div v-if="!stageCode" class="eo-placeholder">
            <p>请在左侧选择「企业 › 阶段」，然后上传企业交来的原始资料。</p>
          </div>

          <el-empty v-else-if="folderNodes.length === 0 && rootFiles.length === 0" description="这里还没有文件">
            <el-button type="primary" :disabled="isBusy" @click="openUpload">上传文件</el-button>
          </el-empty>

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
                select-mode="multiple"
                row-key="Code"
                empty-text="暂无文件"
              >
                <template #column-FileName="{ row }">
                  <div class="f-name">
                    <el-icon class="f-name__icon"><Document /></el-icon>
                    <span class="f-name__text" :title="row.FileName">{{ row.FileName }}</span>
                  </div>
                </template>
                <template #column-Tags="{ row }">
                  <span v-if="row.IsNotSuggested" class="f-dim">—</span>
                  <div v-else-if="(row.Tags ?? []).length" class="f-tags">
                    <el-tag v-for="t in row.Tags" :key="t" size="small" effect="plain" class="f-tags__item"
                            :title="tagNameMap[t] || t"
                            @click="openDetail(row); initDetailDraft()">{{ tagNameMap[t] || t }}</el-tag>
                  </div>
                  <el-button v-else link type="primary" size="small" @click="openDetail(row); initDetailDraft()">加标签</el-button>
                </template>
                <template #column-DocPurpose="{ row }">
                  <span v-if="row.IsNotSuggested" class="f-dim">—</span>
                  <el-tooltip v-else-if="row.DocPurpose" :content="row.DocPurpose" placement="top" :show-after="300">
                    <span class="f-purpose" @click="openDetail(row); initDetailDraft()">{{ row.DocPurpose }}</span>
                  </el-tooltip>
                  <el-button v-else link type="primary" size="small" @click="openDetail(row); initDetailDraft()">填作用</el-button>
                </template>
                <template #column-ExtractState="{ row }">
                  <el-tag size="small" :type="row.IsUsableForFilling ? 'success' : 'info'">
                    {{ row.IsUsableForFilling ? '已提取' : (row.UnusableReason || '待提取') }}
                  </el-tag>
                </template>
                <template #column-VersionNumber="{ row }">
                  <span v-if="row.VersionNumber > 1">v{{ row.VersionNumber }}</span>
                  <span v-else class="f-dim">—</span>
                </template>
                <template #column-Actions="{ row }">
                  <el-button link type="primary" size="small" @click="openDetail(row); initDetailDraft()">详情</el-button>
                  <el-button link type="primary" size="small" @click="openPreview(row)">预览</el-button>
                  <el-button v-if="row.AnalyzePolicy === 'analyze'" link size="small" @click="quickIgnore(row)">忽略</el-button>
                  <el-button v-else link type="success" size="small" @click="quickParticipate(row)">恢复提取</el-button>
                  <el-button v-if="row.VersionNumber > 1" link size="small" @click="onVersions(row)">版本</el-button>
                  <el-button v-else link type="danger" size="small" @click="onDelete(row)">删除</el-button>
                </template>
              </YzhTable>
            </div>

            <!-- 文件夹树 -->
            <OriginalFolderTree
              v-if="folderNodes.length"
              :nodes="folderNodes as OriginalFolderNode[]"
              :data-revision="dataRevision"
              :tag-names="tagNameMap"
              @detail="openDetail"
              @preview="openPreview"
              @ignore="quickIgnore"
              @participate="quickParticipate"
              @versions="onVersions"
              @download="onDownload"
              @delete="onDelete"
              @tag="openDetail"
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
        <el-empty v-else-if="!previewLoading" description="这个格式暂时无法预览，请下载原件查看" />
      </div>
    </el-drawer>

    <!-- ═══════════ ★ 详情抽屉（点「详情」进来改标签/作用/预览）═══════════ -->
    <el-drawer
      :model-value="detailVisible" size="720px"
      :title="detailRow ? detailRow.FileName : '详情'"
      @update:model-value="(v: boolean) => { if (!v) closeDetail() }"
    >
      <div v-loading="detailLoading" class="eo-detail">
        <template v-if="detailRow">
          <!-- 基本信息 -->
          <div class="eo-detail__meta">
            <el-tag size="small" :type="detailRow.IsUsableForFilling ? 'success' : 'info'">
              {{ detailRow.IsUsableForFilling ? '已提取' : (detailRow.UnusableReason || '待提取') }}
            </el-tag>
            <span class="eo-dim">{{ formatSize(detailRow.FileSize) }}</span>
            <span class="eo-dim">{{ fileKindText(detailRow.FileType) }}</span>
            <span v-if="detailRow.RelFolderPath" class="eo-dim">{{ detailRow.RelFolderPath }}</span>
          </div>

          <!-- 预览内容 -->
          <div class="eo-detail__block">
            <div class="eo-detail__label">文件内容</div>
            <div class="eo-detail__preview">
              <el-button size="small" @click="openPreview(detailRow)">打开预览</el-button>
              <el-button size="small" text @click="onDownload(detailRow)">下载原件</el-button>
            </div>
          </div>

          <!-- ⛔ 「不建议提取」的文件不提供标签/作用编辑（营业执照、身份证等特定证件
               没有「体系文件作用」语义，硬打标签会污染召回词表） -->
          <el-alert v-if="detailRow.IsNotSuggested" type="info" :closable="false" show-icon class="eo-alert">
            这份文件已设置为<b>不参与提取</b>，因此不设标签和作用。
            如果这是误判，可在下方改为「参与识别」。
          </el-alert>

          <!-- 标签 -->
          <div v-else class="eo-detail__block">
            <div class="eo-detail__label">
              标签
              <span class="eo-dim">（决定这份资料被归到哪一类，只能从清单里选）</span>
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

          <!-- 作用（四段式） -->
          <div v-if="!detailRow.IsNotSuggested" class="eo-detail__block">
            <div class="eo-detail__label">
              作用
              <span class="eo-dim">（说明这份资料能证明什么，系统自动生成的可能不准）</span>
            </div>
            <el-form label-position="top">
              <el-form-item v-for="seg in PURPOSE_SEGMENTS" :key="seg" :label="seg.replace(/[【】]/g, '')">
                <el-input v-model="detailPurpose[seg]" type="textarea" :rows="2"
                          :placeholder="seg.replace(/[【】]/g, '') + '…'" />
              </el-form-item>
            </el-form>
          </div>

          <!-- AI 建议 -->
          <el-alert
            v-if="detailRow.PolicySource === 'ai' && detailRow.PolicyReason"
            type="warning" :closable="false" show-icon class="eo-alert"
          >
            系统建议这份文件「{{ detailRow.PolicyReason }}」，<b>尚未生效</b>，请人工确认
          </el-alert>
        </template>
      </div>

      <template #footer>
        <el-button @click="closeDetail">关闭</el-button>
        <el-button v-if="!detailRow?.IsNotSuggested" type="primary" :loading="purposeSaving" @click="saveDetail">保存</el-button>
      </template>
    </el-drawer>

    <!-- ═══════════ 上传抽屉 ═══════════ -->
    <el-drawer v-model="uploadVisible" title="上传文件" size="620px" @close="closeUpload">
      <el-alert type="info" :closable="false" show-icon class="eo-alert">
        会保留文件夹结构。已存在且内容相同的文件会自动跳过；内容不同的会作为新版本保留旧版。
      </el-alert>
      <el-alert type="warning" :closable="false" show-icon class="eo-alert">
        支持：Word / Excel / PPT / PDF / 图片 / 文本。压缩包和可执行文件不支持。
      </el-alert>

      <div class="eo-upload">
        <YzhFolderUpload :multiple="true" :accept="ACCEPT" @change="onFilesPicked" />
      </div>

      <div v-if="planRows.length > 0" class="eo-plan">
        <div class="eo-plan__summary">
          共 {{ planSummary.Total }} 个文件
          <template v-if="planSummary.ReplaceCount">，{{ planSummary.ReplaceCount }} 个是更新旧文件</template>
          <template v-if="planSummary.SkipCount">，{{ planSummary.SkipCount }} 个内容相同会跳过</template>
        </div>
        <ul class="eo-plan__list">
          <li v-for="r in planRows" :key="r.FileName">
            <span class="eo-plan__name">{{ r.FileName }}</span>
            <el-tag v-if="r.Blocked" size="small" type="danger">不支持</el-tag>
            <el-tag v-else-if="r.Action === 'skip'" size="small" type="info">内容相同，跳过</el-tag>
            <el-tag v-else-if="r.Action === 'replace'" size="small" type="warning">更新为新版本</el-tag>
            <el-tag v-else size="small" type="success">新增</el-tag>
          </li>
        </ul>
      </div>

      <div v-if="uploadStage === 'uploading'" class="eo-progress">{{ uploadProgress }}</div>
      <div v-else-if="uploadStage === 'done'" class="eo-progress eo-progress--done">{{ uploadProgress }}</div>

      <template #footer>
        <el-button @click="closeUpload">{{ uploadStage === 'done' ? '关闭' : '取消' }}</el-button>
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
        <el-button @click="policyVisible = false">取消</el-button>
        <el-button type="primary" @click="submitPolicy">保存</el-button>
      </template>
    </el-drawer>

    <!-- ═══════════ 处理进度抽屉 ═══════════ -->
    <el-drawer v-model="queueVisible" title="处理进度" size="720px">
      <div v-loading="queueLoading">
        <el-empty v-if="!queueLoading && (queueDetail?.Rows ?? []).length === 0" description="当前没有进行中的处理" />
        <div v-for="q in queueDetail?.Rows ?? []" :key="q.Code" class="eo-q">
          <div class="eo-q__head">
            <span>{{ q.QueueType === 'enterprise_original_analyze' ? '识别资料内容' : '读取文件内容' }}</span>
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
.eo-page { height: 100%; }
.eo-main { padding: 14px; display: flex; flex-direction: column; gap: 10px; min-height: 0; }

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
.f-tags { display: flex; gap: 3px; flex-wrap: wrap; }
.f-tags__item { cursor: pointer; }
.f-purpose { cursor: pointer; display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* ══════════ 详情抽屉 ══════════ */
.eo-detail { min-height: 200px; }
.eo-detail__meta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; font-size: 13px; }
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
  white-space: pre-wrap; word-break: break-word; font-size: 13px;
}
.eo-opt { display: flex; justify-content: space-between; gap: 10px; }
.eo-orig { margin-top: 14px; font-size: 12px; color: var(--el-text-color-secondary);
  display: flex; gap: 5px; flex-wrap: wrap; align-items: center; }
.eo-orig__label { margin-right: 4px; }

.eo-upload { margin: 12px 0; }
.eo-plan__summary { font-size: 13px; margin-bottom: 6px; }
.eo-plan__list { list-style: none; margin: 0; padding: 0; max-height: 260px; overflow: auto; }
.eo-plan__list li { display: flex; align-items: center; gap: 8px; padding: 4px 0; font-size: 13px; }
.eo-plan__name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.eo-progress { margin-top: 10px; font-size: 13px; }
.eo-progress--done { color: var(--el-color-success); }

.eo-q { padding: 8px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.eo-q__head { display: flex; align-items: center; gap: 10px; font-size: 13px; }
</style>
