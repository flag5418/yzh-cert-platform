<script setup lang="ts">
/**
 * 企业资料管理（审核员端 /resources）
 *
 * 布局（01 分册 + 后台管理风格）：
 * - 整页 = 灰底（由布局层给）+ 两张白卡片：左「企业/阶段树」、右「阶段资料」
 * - 左卡片：**一棵树**（企业 → 阶段），企业为父节点、阶段为子节点，带搜索与计数徽标；
 *   专家未来会管多个企业，因此左侧只保留树本身，不再叠加下拉/单选组
 * - 右卡片：阶段头（企业/阶段名 + 上传入口）→ 汇总条 → 按标准分 Tab → 文件夹树 + 槽位表
 *
 * ★ 数据源（05 §二）：stage-tree（左树）→ stage-overview（汇总）→ standard-directory（每标准主数据）
 * ★ 上传（04 §一）：upload/plan 干跑预览 → upload/init → upload/file... → upload/confirm
 * ★ 表格一律 YzhTable（守卫 R6：页面禁内联 `<el-table>`）；清单换批靠 dataRevision 换 key 重挂载
 * ★ 字段大小写：DB 列名字段 PascalCase（Code/FileName/StoragePath/Status），接口拼装字段 camel
 */
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import { YzhTable, YzhEmptyState } from '@yzh-core'
import { Calendar, OfficeBuilding, Refresh, Search, Upload, UploadFilled, Files } from '@element-plus/icons-vue'
import { ResourcesLogic, type ResourceTreeNode } from './logic'
import type { FileSlot } from '@share/api'
import StandardFolderTree from './components/StandardFolderTree.vue'

const logic = new ResourcesLogic()

// ─── 左树（本地 UI 状态：搜索框 + el-tree 实例） ───
const treeRef = ref()
const treeFilter = ref('')

/** el-tree 搜索：父节点可见性由 el-tree 自身按「子节点有命中」推导，无需手写递归 */
function filterTreeNode(value: string, data: any): boolean {
  if (!value) return true
  return String(data?.Name ?? '').toLowerCase().includes(value.toLowerCase())
}

watch(treeFilter, (v) => treeRef.value?.filter(v))
/** 选中态以 logic.selectedKey 为唯一权威（单阶段企业会自动深入阶段节点） */
watch(() => logic.selectedKey.value, (key) => {
  if (key) treeRef.value?.setCurrentKey(key)
})

function onTreeNodeClick(node: ResourceTreeNode) {
  logic.selectTreeNode(node)
}

onMounted(async () => {
  try { await logic.loadEnterprises() } catch { /* silent */ }
})
onBeforeUnmount(() => {
  logic.stopPolling()
  logic.closePreview()
})

/** 文件夹选择（webkitdirectory，保留相对路径 ⇒ M0 文件夹路径强匹配才有输入） */
let folderInput: HTMLInputElement | null = null
function pickFolder() {
  if (!folderInput) {
    folderInput = document.createElement('input')
    folderInput.type = 'file'
    folderInput.multiple = true
    folderInput.setAttribute('webkitdirectory', 'true')
    folderInput.addEventListener('change', () => {
      const files = Array.from(folderInput?.files ?? [])
      if (!files.length) return
      if (logic.singleUploadVisible.value) logic.setSingleUploadFolderFiles(files)
      else logic.addBatchFiles(files)
      if (folderInput) folderInput.value = ''
    })
  }
  folderInput.click()
}

function onSinglePick(file: any) {
  if (file?.raw) logic.setSingleUploadFiles([file.raw])
}
function onBatchPick(file: any) {
  if (file?.raw) logic.addBatchFiles([file.raw])
}
function onReplacePick(file: any) {
  logic.setReplaceNewFile(file?.raw ?? null)
}
</script>

<template>
  <div class="resources-page">
    <div class="page-body">
      <!-- ─────────── 左：企业 → 阶段（一棵树） ─────────── -->
      <aside class="panel tree-panel">
        <div class="panel__header">
          <span>企业与阶段</span>
          <el-button link type="primary" size="small" :loading="logic.loadingEnterprises.value" @click="logic.loadEnterprises()">
            刷新
          </el-button>
        </div>

        <div class="tree-panel__search">
          <el-input v-model="treeFilter" placeholder="搜索企业 / 阶段" clearable size="small">
            <template #prefix>
              <el-icon><Search /></el-icon>
            </template>
          </el-input>
        </div>

        <div class="tree-panel__body">
          <el-tree
            v-if="logic.treeNodes.value.length"
            ref="treeRef"
            :data="logic.treeNodes.value"
            node-key="Key"
            :props="{ label: 'Name', children: 'Children' }"
            highlight-current
            :expand-on-click-node="false"
            default-expand-all
            :filter-node-method="filterTreeNode"
            @node-click="onTreeNodeClick"
          >
            <template #default="{ data }">
              <span class="tree-node" :class="{ 'is-disabled': data.Disabled }">
                <el-icon class="tree-node__icon" :class="`is-${data.Type}`">
                  <component :is="data.Type === 'enterprise' ? OfficeBuilding : Calendar" />
                </el-icon>
                <span class="tree-node__label" :title="data.Name">{{ data.Name }}</span>
                <span v-if="data.Type === 'enterprise'" class="tree-node__badge">{{ data.StageCount }} 阶段</span>
                <el-tag v-else size="small" :type="data.StandardCount ? 'success' : 'info'">
                  {{ data.StandardCount ? `${data.StandardCount} 标准` : '未关联' }}
                </el-tag>
              </span>
            </template>
          </el-tree>

          <el-skeleton v-else-if="logic.loadingEnterprises.value" :rows="4" animated />
          <YzhEmptyState v-else title="暂无企业数据" />
        </div>
      </aside>

      <!-- ─────────── 右：阶段资料 ─────────── -->
      <section class="panel content-panel">
        <template v-if="logic.selectedStage.value">
          <div class="panel__header content-panel__header">
            <div class="content-panel__title">
              <span class="content-panel__ent">{{ logic.selectedEnterpriseName.value }}</span>
              <span class="content-panel__sep">/</span>
              <span class="content-panel__stage">{{ logic.selectedStageName.value }}</span>
              <el-tag v-if="logic.uploading.value" type="primary" size="small">上传中 {{ logic.uploadProgress.value }}</el-tag>
              <el-tag v-else-if="logic.polling.value" type="warning" size="small">转换进行中，5s 自动刷新</el-tag>
            </div>
            <div class="content-panel__actions">
              <el-button type="primary" size="small" :icon="Upload" :disabled="logic.uploading.value"
                         @click="logic.openBatchUpload()">
                所有标准上传
              </el-button>
              <el-button size="small" :icon="Refresh" :loading="logic.loading.value" @click="logic.loadOverview()">
                刷新
              </el-button>
            </div>
          </div>

          <!-- 汇总条 -->
          <div v-if="logic.overviewSummary" class="overview">
            <div class="overview-item">
              <div class="overview-item__value">{{ logic.overviewSummary.standardCount }}</div>
              <div class="overview-item__label">标准</div>
            </div>
            <div class="overview-item">
              <div class="overview-item__value">{{ logic.overviewSummary.totalRequired }}</div>
              <div class="overview-item__label">应上传</div>
            </div>
            <div class="overview-item overview-item--success">
              <div class="overview-item__value">{{ logic.overviewSummary.totalLive }}</div>
              <div class="overview-item__label">已就位</div>
            </div>
            <div class="overview-item overview-item--warning">
              <div class="overview-item__value">{{ logic.overviewSummary.totalConverting }}</div>
              <div class="overview-item__label">转换中</div>
            </div>
            <div class="overview-item overview-item--danger">
              <div class="overview-item__value">{{ logic.overviewSummary.totalMissing }}</div>
              <div class="overview-item__label">缺失</div>
            </div>
          </div>

          <!-- 不可配置标准：Toast 会飘走，这里留一条常驻说明。★ 2026-09-30 精简：只留一句处置，去掉给管理员看的诊断 -->
          <div v-if="logic.unconfiguredStandards.value.length" class="unconfigured-bar">
            <el-alert type="warning" :closable="false" show-icon>
              <template #title>
                以下标准暂无资料目录（不会出现在下方 Tab 中）
              </template>
              <div v-for="(msg, i) in logic.unconfiguredStandards.value" :key="i" class="unconfigured-bar__item">{{ msg }}</div>
              <div class="unconfigured-bar__hint">目录模板就绪后，刷新本页即自动生成</div>
            </el-alert>
          </div>

          <div class="content-panel__body" v-loading="logic.loading.value">
            <div v-if="logic.loading.value && !logic.overview.value" class="content-panel__placeholder">
              <el-skeleton :rows="4" animated />
            </div>
            <div v-else-if="logic.tabStandards.length === 0" class="content-panel__placeholder">
              <YzhEmptyState :icon="Files" :title="logic.emptyHint" />
            </div>

            <el-tabs v-else v-model="logic.activeTab.value" type="card" class="standards-tabs">
              <el-tab-pane
                v-for="std in logic.tabStandards"
                :key="std.StandardCode"
                :name="std.StandardCode"
              >
                <template #label>
                  <span>{{ std.StandardName }}</span>
                  <el-tag v-if="std.Missing > 0" type="danger" size="small" class="tab-badge">{{ std.Missing }}</el-tag>
                </template>

                <div class="std-toolbar">
                  <div class="std-toolbar__summary">
                    <span class="std-no">{{ std.StandardNo }}</span>
                    <el-divider direction="vertical" />
                    <span>应上传 <b>{{ std.Required }}</b></span>
                    <el-divider direction="vertical" />
                    <span>已就位 <b class="std-live">{{ std.Live }}</b></span>
                    <el-divider direction="vertical" />
                    <span>转换中 <b>{{ std.Converting }}</b></span>
                    <el-divider direction="vertical" />
                    <span class="std-missing">缺失 <b>{{ std.Missing }}</b></span>
                  </div>
                  <div class="std-toolbar__actions">
                    <el-button size="small" :disabled="logic.queueBusy.value" @click="logic.cancelRunningQueue(std.StandardCode)">
                      取消队列
                    </el-button>
                    <el-button size="small" :icon="Upload" :disabled="logic.uploading.value" @click="pickFolder()">
                      上传文件夹
                    </el-button>
                    <el-button type="primary" size="small" :icon="Upload" :disabled="logic.uploading.value"
                               @click="logic.openSingleUpload(std.StandardCode)">
                      上传文件
                    </el-button>
                  </div>
                </div>

                <YzhEmptyState
                  v-if="logic.folderTreeOf(std.StandardCode).length === 0"
                  title="该标准暂无资料目录，模板就绪后刷新本页自动生成"
                 />
                <StandardFolderTree
                  v-else
                  :nodes="logic.folderTreeOf(std.StandardCode)"
                  :columns="logic.slotColumns"
                  :data-revision="logic.dataRevision.value"
                  :panel-height="(n: number) => logic.panelHeight(n)"
                  :busy="logic.queueBusy.value || logic.uploading.value"
                  @replace="(r: FileSlot) => logic.openReplaceDialog(r)"
                  @remove="(r: FileSlot) => logic.openDeleteDialog(r)"
                  @versions="(r: FileSlot) => logic.openVersions(r)"
                  @extract="(r: FileSlot) => logic.handleTriggerExtract(r)"
                  @result="(r: FileSlot) => logic.handleViewResult(r)"
                  @preview="(r: FileSlot) => logic.previewRow(r)"
                  @download="(r: FileSlot) => logic.downloadRow(r)"
                  @upload="(folderCode: string) => { logic.openSingleUpload(std.StandardCode, folderCode); }"
                />
              </el-tab-pane>
            </el-tabs>
          </div>
        </template>

        <div v-else class="content-panel__placeholder">
          <YzhEmptyState :icon="Files" :title="logic.emptyHint" />
        </div>
      </section>
    </div>

    <!-- ═══════════ 单标准上传（plan 干跑预览 → 四段式执行） ═══════════ -->
    <el-dialog v-model="logic.singleUploadVisible.value" title="上传到标准目录" width="820px">
      <el-form label-width="96px">
        <el-form-item label="目标标准">
          <el-tag>{{ logic.standardOf(logic.currentUploadStandardCode.value)?.StandardName }}</el-tag>
        </el-form-item>
        <el-form-item label="目标文件夹">
          <el-select v-model="logic.singleUploadFolderCode.value" style="width:320px" @change="logic.previewSingleUpload()">
            <el-option
              v-for="f in logic.folderOptionsOf(logic.currentUploadStandardCode.value)"
              :key="f.FolderCode" :label="f.Label" :value="f.FolderCode"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="选择方式">
          <el-button :icon="UploadFilled" @click="pickFolder()">选择文件夹（保留目录结构）</el-button>
          <el-upload class="inline-upload" :auto-upload="false" :show-file-list="false" multiple :on-change="onSinglePick">
            <el-button :icon="Upload">选择文件</el-button>
          </el-upload>
          <el-button size="small" :disabled="logic.singleUploadFiles.value.length === 0" @click="logic.clearSingleUploadFiles()">
            清空
          </el-button>
        </el-form-item>
      </el-form>

      <div v-if="logic.singleUploadFiles.value.length" class="file-chips">
        <el-tag v-for="(f, i) in logic.singleUploadFiles.value" :key="`${f.name}-${i}`" closable @close="logic.removeSingleUploadFile(i)">
          {{ f.name }}
        </el-tag>
      </div>

      <el-alert type="info" :closable="false" show-icon class="dialog-tip">
        按「文件夹路径（M0）→ 文件名精确（M1）→ 主词包含（M2）→ 目录+扩展名（M3）」的顺序自动匹配槽位。
        M3 是分不准时的兜底，<b>默认不勾选</b>，请你确认后再勾。
        没匹配上的文件不会进入任何标准。
      </el-alert>

      <el-button v-if="logic.singleUploadFiles.value.length" size="small" type="default"
                 :loading="logic.singlePlanLoading.value" @click="logic.previewSingleUpload()">
        重新匹配
      </el-button>

      <div v-if="logic.singlePlanRows.value.length" class="plan-block">
        <div class="plan-title">将上传 {{ logic.singlePlanRows.value.filter(r => !r.BlockReason).length }} 个文件</div>
        <!-- 预览行用普通列表：守卫 R6 禁页面内联表格组件，且此处数据在内存、无需分页取数 -->
        <div v-for="(row, i) in logic.singlePlanRows.value" :key="i" class="plan-row">
          <el-checkbox :model-value="row.Selected" :disabled="!!row.BlockReason"
                       @change="(v: any) => logic.toggleBatchRow(row, !!v)" />
          <span class="plan-row__file" :title="row.FileName">{{ row.FileName }}</span>
          <span class="plan-row__folder" :title="row.FolderPath">{{ row.FolderPath || '（根）' }}</span>
          <span class="plan-row__slot" :title="row.SlotFileName">→ {{ row.SlotFileName }}</span>
          <el-tag size="small" :type="row.Level === 'M3FolderExt' ? 'warning' : 'success'">{{ logic.levelText(row.Level) }}</el-tag>
          <el-tag v-if="row.BlockReason" size="small" type="danger">{{ row.BlockReason }}</el-tag>
          <el-tag v-else-if="row.NeedsConfirm" size="small" type="warning">需人工确认（默认不勾选）</el-tag>
          <el-tag v-else size="small" type="success">可上传</el-tag>
        </div>
      </div>

      <el-alert v-if="logic.singlePlanUnmatched.value.length" type="warning" :closable="false" show-icon class="dialog-tip">
        未命中槽位（默认不会上传）：{{ logic.singlePlanUnmatched.value.join('、') }}
        <div class="assign-hint">
          <el-checkbox v-model="logic.singleAssignUnmatched.value">
            把这些文件直接放入「{{ logic.folderFullPathOf(logic.currentUploadStandardCode.value, logic.singleUploadFolderCode.value) || '根目录' }}」
            （模板外文件，走人工落地，不占用标准槽位）
          </el-checkbox>
        </div>
      </el-alert>

      <template #footer>
        <el-button @click="logic.closeSingleUploadDialog()">取消</el-button>
        <el-button type="primary" :loading="logic.uploading.value"
                   :disabled="logic.singlePlanLoading.value
                     || (!logic.singlePlanRows.value.some(r => r.Selected && !r.BlockReason)
                       && !(logic.singleAssignUnmatched.value && logic.singleUploadFiles.value.length > 0))"
                   @click="logic.confirmSingleUpload()">
          确认上传
        </el-button>
      </template>
    </el-dialog>

    <!-- ═══════════ 所有标准上传（多标准分发，需求 3 核心） ═══════════ -->
    <el-dialog v-model="logic.batchUploadVisible.value" title="所有标准上传（多标准自动分发）" width="980px">
      <el-alert type="info" :closable="false" show-icon class="dialog-tip">
        目标阶段：<b>{{ logic.selectedStageName.value }}</b>；涉及标准：
        {{ logic.tabStandards.map(s => s.StandardName).join(' / ') || '—' }}
        <br />逐标准独立判定：只把<b>属于该标准</b>的文件放进去，未命中不进入该标准
      </el-alert>

      <div class="batch-picker">
        <el-button :icon="UploadFilled" @click="pickFolder()">选择文件夹</el-button>
        <el-upload class="inline-upload" :auto-upload="false" :show-file-list="false" multiple :limit="500" :on-change="onBatchPick">
          <el-button :icon="Upload">选择文件</el-button>
        </el-upload>
        <el-button size="small" :disabled="logic.batchFiles.value.length === 0" @click="logic.clearBatchFiles()">
          清空
        </el-button>
        <span class="batch-count">已选 {{ logic.batchFiles.value.length }} 个文件</span>
        <el-button type="primary" size="small" :loading="logic.batchPlanLoading.value"
                   :disabled="logic.batchFiles.value.length === 0" @click="logic.previewBatchUpload()">
          解析分发计划
        </el-button>
      </div>

      <template v-if="logic.batchPlanRows.value.length || logic.batchPlanUnmatched.value.length">
        <div v-for="g in logic.batchPlanRowGroups" :key="g.StandardCode" class="plan-block">
          <div class="plan-title">
            {{ g.StandardName }}
            <el-tag size="small" type="info">命中 {{ g.Rows.filter(r => !r.BlockReason).length }} / {{ g.Rows.length }}</el-tag>
          </div>
          <div v-for="(row, i) in g.Rows" :key="i" class="plan-row">
            <el-checkbox :model-value="row.Selected" :disabled="!!row.BlockReason"
                         @change="(v: any) => logic.toggleBatchRow(row, !!v)" />
            <span class="plan-row__file" :title="row.FileName">{{ row.FileName }}</span>
            <span class="plan-row__folder" :title="row.FolderPath">{{ row.FolderPath || '（根）' }}</span>
            <span class="plan-row__slot" :title="row.SlotFileName">→ {{ row.SlotFileName }}</span>
            <el-tag size="small" :type="row.Level === 'M3FolderExt' ? 'warning' : 'success'">{{ logic.levelText(row.Level) }}</el-tag>
            <el-tag v-if="row.BlockReason" size="small" type="danger">{{ row.BlockReason }}</el-tag>
            <el-tag v-else-if="row.NeedsConfirm" size="small" type="warning">需人工确认</el-tag>
            <el-tag v-else size="small" type="success">可上传</el-tag>
          </div>
        </div>

        <el-alert v-if="logic.batchPlanCross.value.length" type="warning" :closable="false" show-icon class="dialog-tip">
          <div v-for="c in logic.batchPlanCross.value" :key="c.FileName">
            同一文件命中多标准：{{ c.FileName }} → {{ c.Standards.join(' + ') }}（将各存一份，共 {{ c.Standards.length }} 份）
          </div>
        </el-alert>

        <el-alert v-if="logic.batchPlanConflicts.value.length" type="warning" :closable="false" show-icon class="dialog-tip">
          <div v-for="(c, i) in logic.batchPlanConflicts.value" :key="i">
            冲突：{{ c.StandardName }} · {{ c.SlotFileName }} ← {{ c.FileNames.join(' / ') }}（请在上表勾选保留哪一份）
          </div>
        </el-alert>

        <el-alert v-if="logic.batchPlanBlocked.value.length" type="error" :closable="false" show-icon class="dialog-tip">
          <div v-for="(r, i) in logic.batchPlanBlocked.value" :key="i">{{ r.FileName }} → {{ r.SlotFileName }}：{{ r.BlockReason }}</div>
        </el-alert>

        <!-- 未归属区：不自动丢弃（D4），也不自动塞入各标准（需求 3）——只能人工指派 -->
        <div v-if="logic.batchPlanUnmatched.value.length" class="plan-block">
          <div class="plan-title">
            未归属（{{ logic.batchPlanUnmatched.value.length }} 个，不会自动上传）
            <el-tag size="small" type="info">已指派 {{ logic.assignedUnmatched.length }}</el-tag>
          </div>
          <el-alert type="info" :closable="false" show-icon class="dialog-tip">
            这些文件不属于任何标准的资料目录（例如营业执照、体系证书这类通用文件）。
            需要存档的话，请指定「标准 + 文件夹」，会作为补充资料保存（不占用必传项）。
          </el-alert>
          <div v-for="u in logic.batchPlanUnmatched.value" :key="u.Key" class="plan-row assign-row">
            <span class="plan-row__file" :title="u.RelativePath || u.FileName">{{ u.FileName }}</span>
            <el-tag size="small" :type="u.Reason === 'ambiguous' ? 'warning' : 'info'">
              {{ logic.unmatchedReasonText(u.Reason) }}
            </el-tag>
            <el-select :model-value="logic.unmatchedAssignOf(u.Key).StandardCode" placeholder="指派到标准" size="small" style="width:150px"
                       @change="(v: any) => logic.setUnmatchedAssign(u.Key, { StandardCode: v })">
              <el-option v-for="s in logic.tabStandards" :key="s.StandardCode" :label="s.StandardName" :value="s.StandardCode" />
            </el-select>
            <el-select :model-value="logic.unmatchedAssignOf(u.Key).FolderCode" placeholder="指派到文件夹" size="small" style="width:210px"
                       :disabled="!logic.unmatchedAssignOf(u.Key).StandardCode"
                       @change="(v: any) => logic.setUnmatchedAssign(u.Key, { FolderCode: v })">
              <el-option v-for="f in logic.folderOptionsOf(logic.unmatchedAssignOf(u.Key).StandardCode)" :key="f.FolderCode" :label="f.Label" :value="f.FolderCode" />
            </el-select>
          </div>
        </div>
      </template>

      <template #footer>
        <el-button @click="logic.closeBatchUpload()">取消</el-button>
        <el-button v-if="logic.assignedUnmatched.length" :loading="logic.uploading.value"
                   @click="logic.confirmUnmatchedAssign()">
          指派上传（{{ logic.assignedUnmatched.length }} 个）
        </el-button>
        <el-button type="primary" :loading="logic.uploading.value" :disabled="logic.selectedBatchCount === 0"
                   @click="logic.confirmBatchUpload()">
          确认上传（{{ logic.selectedBatchCount }} 个文件）
        </el-button>
      </template>
    </el-dialog>

    <!-- ═══════════ 替换文件 ═══════════ -->
    <el-dialog v-model="logic.replaceDialogVisible.value" title="替换文件" width="520px">
      <el-alert type="warning" :closable="false" show-icon class="dialog-tip">
        替换后旧文件归档到同目录「_archive/原名.v{版本号}」，版本号递增，可在版本面板恢复
      </el-alert>
      <p class="replace-info">
        <strong>当前文件：</strong>{{ logic.replacingFile.value?.FileName }}
        <el-tag size="small">v{{ logic.replacingFile.value?.VersionNumber }}</el-tag>
      </p>
      <el-upload :auto-upload="false" :show-file-list="false" :limit="1" :on-change="onReplacePick">
        <el-button :icon="Upload">选择新文件</el-button>
      </el-upload>
      <span v-if="logic.replaceNewFile.value" class="selected-file">{{ logic.replaceNewFile.value.name }}</span>
      <el-form label-width="80px" class="reason-form">
        <el-form-item label="替换原因" required>
          <el-input v-model="logic.replaceReason.value" type="textarea" :rows="2" maxlength="200" show-word-limit
                    placeholder="必填：为何替换（写入操作留痕，供审计追溯）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="logic.closeReplaceDialog()">取消</el-button>
        <el-button type="primary" :disabled="!logic.replaceNewFile.value || !logic.replaceReason.value.trim()"
                   @click="logic.confirmReplace()">确认替换</el-button>
      </template>
    </el-dialog>

    <!-- ═══════════ 移除文件 ═══════════ -->
    <el-dialog v-model="logic.deleteDialogVisible.value" title="移除文件" width="460px">
      <el-alert type="warning" :closable="false" show-icon class="dialog-tip">
        移除只在清单上标记无效，存储对象与归档版本全部保留（证据保全），可在版本面板恢复
      </el-alert>
      <p class="replace-info"><strong>文件：</strong>{{ logic.deletingFile.value?.FileName }}</p>
      <el-form label-width="80px" class="reason-form">
        <el-form-item label="移除原因" required>
          <el-input v-model="logic.deleteReason.value" type="textarea" :rows="2" maxlength="200" show-word-limit
                    placeholder="必填：为何移除（写入操作留痕，供审计追溯）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="logic.closeDeleteDialog()">取消</el-button>
        <el-button type="danger" :disabled="!logic.deleteReason.value.trim()" @click="logic.confirmDelete()">确认移除</el-button>
      </template>
    </el-dialog>

    <!-- ═══════════ 版本面板 ═══════════ -->
    <el-drawer v-model="logic.versionsVisible.value" title="历史版本与操作留痕" direction="rtl" size="640px">
      <div v-if="logic.versionsFile.value" class="versions-head">
        <span class="versions-head__name">{{ logic.versionsFile.value.FileName }}</span>
        <el-tag size="small">当前 v{{ logic.versionsFile.value.VersionNumber }}</el-tag>
        <el-tag size="small" type="info">{{ logic.versionsFile.value.FolderPath || '根目录' }}</el-tag>
      </div>

      <div class="result-section__title">归档版本</div>
      <YzhTable
        :key="`${logic.versionsFile.value?.Code ?? ''}/${logic.versionsRev.value}`"
        :columns="logic.versionColumns"
        :data-loader="logic.versionRowsLoader()"
        :toolbar="false"
        :show-pagination="false"
        :height="logic.panelHeight(logic.versionRows.value.length)"
        row-key="Code"
        empty-text="暂无归档版本"
      >
        <template #column-VersionNumber="{ row }">v{{ row.VersionNumber }}</template>
        <template #column-FileSize="{ row }">
          {{ !row.FileSize ? '-' : row.FileSize < 1024 ? row.FileSize + ' B' : Math.round(row.FileSize / 1024) + ' KB' }}
        </template>
        <template #column-Restore="{ row }">
          <el-button link type="primary" size="small" @click="logic.downloadVersion(row)">下载</el-button>
          <el-button
            link type="primary" size="small"
            :loading="logic.restoringVersion.value === row.VersionNumber"
            @click="logic.handleRestore(row)"
          >恢复</el-button>
        </template>
      </YzhTable>

      <div class="result-section__title">操作时间线</div>
      <el-timeline v-if="logic.historyRows.value.length">
        <el-timeline-item
          v-for="(h, i) in logic.historyRows.value" :key="`${h.OpType}-${i}`"
          :timestamp="h.Time ? String(h.Time).replace('T', ' ').slice(0, 19) : '-'"
          placement="top"
        >
          <div class="timeline-line">
            <el-tag size="small" :type="h.OpType === 'delete' ? 'danger' : h.Source === 'version' ? 'info' : 'primary'">
              {{ logic.opTypeText(h) }}
            </el-tag>
            <span v-if="h.VersionNumber" class="timeline-ver">v{{ h.VersionNumber }}</span>
            <span v-if="h.CreateBy" class="timeline-by">{{ h.CreateBy }}</span>
          </div>
          <div v-if="h.Detail" class="timeline-detail">{{ h.Detail }}</div>
        </el-timeline-item>
      </el-timeline>
      <YzhEmptyState v-else title="暂无操作记录" />
    </el-drawer>

    <!-- ═══════════ PDF 预览（Blob → ObjectURL；裸链接无法带 Authorization） ═══════════ -->
    <el-drawer v-model="logic.previewVisible.value" :title="`预览：${logic.previewName.value}`" direction="rtl" size="70%" @closed="logic.closePreview()">
      <iframe v-if="logic.previewUrl.value" :src="logic.previewUrl.value" class="preview-frame" />
      <YzhEmptyState v-else title="暂无预览" />
    </el-drawer>

    <!-- ═══════════ 提取结果 ═══════════ -->
    <el-drawer v-model="logic.resultDrawerVisible.value" title="提取结果" direction="rtl" size="520px">
      <div v-if="logic.resultData.value" class="result-content">
        <el-descriptions :column="1" border size="small">
          <el-descriptions-item label="文件名">{{ logic.resultData.value?.FileName }}</el-descriptions-item>
          <el-descriptions-item label="提取状态">
            <el-tag>{{ logic.resultData.value?.ExtractStatus }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item v-if="logic.resultData.value?.ExtractMessage" label="提取信息">
            {{ logic.resultData.value?.ExtractMessage }}
          </el-descriptions-item>
          <el-descriptions-item v-if="logic.resultData.value?.MaxConfidence != null" label="最高置信度">
            {{ logic.resultData.value?.MaxConfidence }}
          </el-descriptions-item>
        </el-descriptions>
        <div v-if="logic.resultData.value?.Fields?.length" class="result-section">
          <div class="result-section__title">字段提取结果</div>
          <div v-for="f in logic.resultData.value.Fields" :key="f.FieldCode" class="result-field-row">
            <span class="rf-label">{{ f.FieldName }}</span>
            <span class="rf-value">{{ f.ExtractedValue ?? '-' }}</span>
            <el-tag v-if="f.Confidence" :type="f.Confidence >= 0.8 ? 'success' : f.Confidence >= 0.5 ? 'warning' : 'danger'" size="small">
              {{ f.Confidence }}
            </el-tag>
          </div>
        </div>
        <div v-if="logic.resultData.value?.Tables?.length" class="result-section">
          <div class="result-section__title">表格提取结果</div>
          <div v-for="t in logic.resultData.value.Tables" :key="t.TableCode" class="result-table-row">
            <span class="rt-label">{{ t.TableCode }}</span>
            <el-button link type="primary" size="small" @click="logic.previewExtractedJson(t.Rows ? JSON.stringify(t.Rows) : '')">查看</el-button>
          </div>
        </div>
      </div>
      <YzhEmptyState v-else title="暂无提取结果" />
    </el-drawer>
  </div>
</template>

<style scoped>
/* ═══════════ 页面骨架：灰底（布局层给）+ 两张白卡片 ═══════════ */
.resources-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  /* 布局层 el-main 已给 padding/灰底；此处只负责卡片间距 */
}

.page-body {
  flex: 1;
  display: flex;
  gap: 12px;
  min-height: 0;
}

/* 统一卡片外观（与 YzhCard / 后台管理列表页同款令牌） */
.panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  overflow: hidden;
}

.panel__header {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

/* ═══════════ 左：企业 → 阶段 树 ═══════════ */
.tree-panel {
  width: 280px;
  flex-shrink: 0;
}

.tree-panel__search {
  flex-shrink: 0;
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.tree-panel__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 8px;
}

.tree-panel__body :deep(.el-tree) {
  background: transparent;
  color: var(--el-text-color-regular);
}

.tree-panel__body :deep(.el-tree-node__content) {
  height: 34px;
  border-radius: 4px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
}

.tree-node__icon {
  font-size: 15px;
  flex-shrink: 0;
}

.tree-node__icon.is-enterprise {
  color: var(--el-color-warning);
}

.tree-node__icon.is-stage {
  color: var(--el-color-primary);
}

.tree-node__label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.tree-node__badge {
  flex-shrink: 0;
  font-size: 11px;
  line-height: 18px;
  padding: 0 6px;
  border-radius: 9px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color);
}

.tree-node.is-disabled .tree-node__label {
  color: var(--el-text-color-secondary);
}

/* ═══════════ 右：阶段资料 ═══════════ */
.content-panel {
  flex: 1;
  min-width: 0;
}

.content-panel__header {
  font-weight: 500;
}

.content-panel__title {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.content-panel__ent {
  font-size: 14px;
  font-weight: 600;
}

.content-panel__sep {
  color: var(--el-text-color-placeholder);
}

.content-panel__stage {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.content-panel__actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.content-panel__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 12px 16px 16px;
}

.content-panel__placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 24px;
}

/* 汇总条 */
.overview {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 12px 20px;
  background: var(--el-fill-color-lighter);
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.overview-item {
  flex: 1;
  padding-left: 20px;
  border-left: 1px solid var(--el-border-color-lighter);
}

.overview-item:first-child {
  padding-left: 0;
  border-left: none;
}

.overview-item__value {
  font-size: 20px;
  font-weight: 600;
  line-height: 1.3;
  font-variant-numeric: tabular-nums;
}

.overview-item__label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.overview-item--success .overview-item__value {
  color: var(--el-color-success);
}

.overview-item--warning .overview-item__value {
  color: var(--el-color-warning);
}

.overview-item--danger .overview-item__value {
  color: var(--el-color-danger);
}

/* 不可配置标准的常驻说明 */
.unconfigured-bar {
  flex-shrink: 0;
  padding: 10px 16px 0;
}

.unconfigured-bar__item {
  font-size: 13px;
  line-height: 1.8;
}

.unconfigured-bar__hint {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* 标准 Tab */
.standards-tabs :deep(.el-tabs__header) {
  margin-bottom: 12px;
}

.standards-tabs :deep(.el-tabs__content) {
  overflow: visible;
}

.std-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
  padding: 8px 12px;
  margin-bottom: 12px;
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
}

.std-toolbar__summary {
  display: flex;
  align-items: center;
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.std-toolbar__actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.std-no {
  color: var(--el-text-color-secondary);
}

.std-live {
  color: var(--el-color-success);
}

.std-missing {
  color: var(--el-color-danger);
}

.tab-badge {
  margin-left: 6px;
}

/* 弹窗内通用 */
.dialog-tip {
  margin: 8px 0 12px;
}

.batch-picker {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 8px;
}

.batch-count {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.inline-upload {
  display: inline-block;
}

.file-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  max-height: 120px;
  overflow: auto;
}

.plan-block {
  margin: 10px 0;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  padding: 8px 10px;
}

.plan-title {
  font-weight: 600;
  font-size: 13px;
  margin-bottom: 6px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.plan-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
  font-size: 13px;
  border-bottom: 1px dashed var(--el-border-color-lighter);
}

.plan-row__file {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plan-row__folder {
  width: 150px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-secondary);
}

.plan-row__slot {
  width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 未归属人工指派行 */
.assign-row {
  gap: 10px;
}

.assign-hint {
  margin-top: 6px;
  font-size: 12px;
}

.replace-info {
  margin: 8px 0;
  font-size: 13px;
}

.selected-file {
  display: block;
  margin-top: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.reason-form {
  margin-top: 12px;
}

.versions-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.versions-head__name {
  font-weight: 600;
}

.result-section__title {
  font-weight: 600;
  margin: 16px 0 8px;
}

.result-field-row,
.result-table-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 0;
  border-bottom: 1px dashed var(--el-border-color-lighter);
}

.rf-label {
  min-width: 120px;
  color: var(--el-text-color-secondary);
}

.rf-value {
  flex: 1;
}

.timeline-line {
  display: flex;
  align-items: center;
  gap: 8px;
}

.timeline-detail {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  word-break: break-all;
}

.preview-frame {
  width: 100%;
  height: 100%;
  min-height: 70vh;
  border: none;
}
</style>
