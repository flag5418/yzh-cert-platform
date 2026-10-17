<script setup lang="ts">
/**
 * 企业资料审核（专家端 · 路由 `/enterprise-review`）
 *
 * 规格 `docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：本页是「第二个页面」
 *
 * 用户原话：「第二个页面是左树右表，树的结构，企业 + 阶段 + 任务，
 * 右侧是勾选的所有的标准化文档，可以针对已经完成标准文档规范的进行审核」＋
 * 「可以按企业的阶段，统一导出所有的文档」＋
 * 「导出的也是已经规范化的文档」。
 *
 * ⇒ 左树三级（企业 › 阶段 › 任务）｜右表 = 当前范围的**已规范化文档**｜
 *   工具栏「导出…」把产物打成 ZIP（★ 文案随范围变，见 `exportText()`）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 追加需求（用户原话）
 *
 * 「可以增加一个导出，**可以按一个任务导出所有的文档**」＋
 * 「我们右侧的表格，**应该也是树表结构，应该显示每个文档的层次结构**」。
 *
 * ⇒ ① 导出：功能本就在（任务级已按清单导），把按钮**文案随范围变**说破范围；
 *    ② 右表：从平铺改成**树表**，按标准目录的文件夹层次分层
 *       （`tree.ts` 纯函数构建，`el-table` 树模式渲染）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 显示口径（用户原话）
 *
 * 「我们当前就不用理会所有的标准文档，我们只考虑标准文档中已经设置规则的」＋
 * 「我们不应该在界面上显示什么是标准化文件数量，哪些是已经定义了填写规则的
 * 文档数量，这样，让审核员非常奇怪，不理解」。
 *
 * ⇒ 本页只呈现**任务与文档的结果**，⛔ 不出现任何「标准域共 N 份」类内部计数。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ⛔ 本页**不使用** `useSingleTable`：左树是三级**动态拼装**的（不是后端树），
 *    右表数据来自「按范围聚合 + 去重」而不是 `/filter`。硬套内核两边都会变形。
 */
import { nextTick, onActivated, onMounted, reactive, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { Document, FolderOpened } from '@element-plus/icons-vue'
import type { Page, YzhAction, YzhTableColumn, YzhTreeNode } from '@yzh-core'
import { YzhEmptyState, YzhStatusBadge, YzhTable, YzhTreeTableLayout } from '@yzh-core'
import { formatDateTime } from '@share/utils/format'
import {
  pathFileName,
  TASK_FILE_STATUS_TEXT,
  TASK_FILE_STATUS_TYPE,
  toPercent,
} from '@share/api/ent/enterprise-normalize'
import { EnterpriseReviewLogic, type ReviewFileTreeNode } from './logic'
import ReviewLedgerDrawer from './components/ReviewLedgerDrawer.vue'

const route = useRoute()

const logic = new EnterpriseReviewLogic()

/**
 * 左树外壳的 ref —— **只为高亮**。
 *
 * ★ `YzhTreeTableLayout` 的 `defineExpose` 里透出了 `treeRef`（内含 `setCurrentNode`），
 *   所以这里不必、也 ⛔ 不该去摸 el-tree 的内部实例。
 */
const treeLayoutRef = ref<any>(null)

/**
 * 首次进入：拉树 +（若带 `?queueCode=`）定位并选中那一条。
 *
 * ★★ 2026-10-11 复核缺陷修复：任务列表页的「审核」按钮一直是
 *   `router.push({ name: 'EnterpriseReview', query: { queueCode } })`，
 *   但本页从来没读过 `route.query` ⇒ 点完落在一张**未选中的空页**上，
 *   用户得从最多 200 条任务的树里自己再找一遍。路由和注释都承诺了「带任务号定位」，
 *   实现没接上 —— 现在补上，并且**命中后要高亮**（只选不亮等于没定位）。
 */
onMounted(async () => {
  const raw = route.query.queueCode
  const queueCode = typeof raw === 'string' ? raw : undefined

  const hit = await logic.init(queueCode)
  if (!hit) return
  // ⚠️ 必须等树渲染完再设高亮：`setCurrentNode` 走 el-tree 的 store，
  //    节点还没进 store 时设了会被丢掉（不报错）。
  await nextTick()
  treeLayoutRef.value?.treeRef?.setCurrentNode(hit.Code)
})

/**
 * 回到本页时刷新（与 `tasks/index.vue`、`enterprise-normalize/index.vue` 同构）。
 *
 * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件 ⇒ 从向导
 *    `/enterprise-normalize/create` 开完任务返回时，本组件被**复用**、`onMounted`
 *    ⛔ 不会再跑 ⇒ 树和右表都是**缓存里的旧数据**（新开的任务在树上根本看不见）。
 *    本页是审核页，看到旧结果比看到空白更危险（用户会照着旧状态下判断）。
 *
 * ⚠️ 第一次 `onActivated` 与 `onMounted` 同时发生（首载已经拉过），
 *    用标记跳过，避免刚进页面就发两次请求。
 *
 * ⛔ 本页**没有**自动刷新定时器（审核是「看快照」，不是「盯进度」）——
 *   要看进度请回任务列表页，那里才有轮询。
 */
async function refreshOnReturn(): Promise<void> {
  await logic.loadTree()
  // 复用上次实际查过的批次（⛔ 不重新按树算，见 `reloadFiles` 的注释）
  await logic.reloadFiles()
}

let activatedOnce = false
onActivated(() => {
  if (!activatedOnce) {
    activatedOnce = true
    return
  }
  void refreshOnReturn()
})

// ══════════════════════════════════════════════════════════════════════
// 右表：树形（目录层次）
//
// ★★ 2026-10-11 用户需求：「右侧的表格应该也是树表结构，应该显示每个文档的
//    层次结构」⇒ 数据由 `logic.treeRows` 按 `FolderPath` 折好，这里只负责渲染。
//
// ⚠️ **关分页**（`show-pagination=false`）：分页是按**行**切的，而树里一行可能是
//    一个文件夹 —— 切在第 20 行正好落在文件夹和它的第一个子项之间时，
//    子项会凭空消失（el-table 的树只认「父在这一页里」）。
//    数据量本来就小（单任务 1~几十份，阶段级聚合上限 20 个批次）⇒ 全量渲染。
// ══════════════════════════════════════════════════════════════════════

const fileTableRef = ref<any>(null)

async function fileLoader(): Promise<Page<ReviewFileTreeNode>> {
  const all = logic.treeRows.value
  return { rows: all, total: all.length }
}

/**
 * ★ 清单是**异步填进 logic** 的（切节点 / 改完账本重拉），而 `YzhTable` 只在挂载时
 *   自己加载一次 ⇒ 必须显式 refresh，否则用户切了节点表格还是上一份数据。
 */
watch(
  () => logic.fileRows.value,
  async () => {
    await nextTick()
    fileTableRef.value?.refresh?.()
  },
)

const fileColumns: YzhTableColumn<ReviewFileTreeNode>[] = [
  // ★ 首列承载树：el-table 的展开箭头固定渲染在**第一列**，所以它必须是「名字」列
  { prop: 'Name', label: '标准化文档', minWidth: 300, slot: true },
  { prop: 'Status', label: '结果', minWidth: 210, slot: true },
  { prop: 'Completion', label: '完成率', width: 150, slot: true },
  { prop: 'PendingCount', label: '待办', width: 120, slot: true },
  { prop: 'OutputPath', label: '产物', minWidth: 200, slot: true },
  { prop: 'FillTime', label: '最近执行', width: 170, slot: true },
]

/**
 * 结果列要不要显示解释文字。
 *
 * ⛔ **不要无条件显示**：`success` 行的 `Message` 恒为「全部锚点写入且自验收通过」，
 *   那是后端给机器看的措辞，逐行铺开只会把表格撑高、把真正要看的东西淹掉。
 * ★ 真正需要它的是**非成功**的行 —— 尤其 `failed`：
 *   此时 `Pendings` 为空（压根没跑到出待办那一步），`Message` 是**唯一**的解释来源。
 *   只给一个红徽标，审核员第一反应是「系统坏了」，而不是「哪一步该我处理」。
 */
function showMessage(row: ReviewFileTreeNode): boolean {
  return !row.IsFolder && row.Status !== 'success' && !!row.Message
}

/**
 * 行按钮。
 *
 * ★「审核」只在**有留痕**（`FillLogCode` 非空）时出现 —— 账本按「企业 × 文件 × 留痕」
 *   定位，没有留痕就没有可审核的对象（点开只会是一句「还没规范化过」的空抽屉，
 *   比不摆更让人困惑）。跑失败的行看结果列里的原因即可。
 * ⚠️ 文件夹行没有留痕 ⇒ 判据天然把它挡掉，⛔ 不必再单独判 `IsFolder`
 *   （多写一个条件 = 多一个会忘记同步的地方）。
 */
function auditActions(row: ReviewFileTreeNode): YzhAction[] {
  return [
    { key: 'custom:audit', text: '审核', type: 'primary', visible: !!row.FillLogCode },
  ]
}

/**
 * 导出按钮文案**随范围变**。
 *
 * ★★ 2026-10-11 用户需求：「可以增加一个导出，**可以按一个任务导出所有的文档**」。
 *   实际上任务级导出**早就实现了**（`exportZip()` 在 `level === 'task'` 时
 *   把 `filledRows` 的清单传给后端）—— 缺的不是功能，是**文案没跟着范围变**：
 *   按钮恒写「导出已规范化文档」，用户在任务级点下去根本不知道导的是这一个任务
 *   还是整个阶段 ⇒ 只能自己去猜，或者干脆不用。
 *   ⇒ 用文案把「你现在导出的是什么范围」说破。
 */
function exportText(): string {
  return logic.selection.value?.level === 'task'
    ? '导出本任务的全部文档'
    : '导出本阶段全部已规范化文档'
}

function onNodeClick(node: YzhTreeNode): void {
  void logic.onNodeClick(node as never)
}

function fmt(v?: string | null): string {
  return v ? formatDateTime(v) : '—'
}

// ══════════════════════════════════════════════════════════════════════
// 审核抽屉（逐锚点改值 / 钉住）
// ══════════════════════════════════════════════════════════════════════

const ledger = reactive({
  visible: false,
  enterpriseCode: '',
  fileName: '',
  standardFileCode: '',
  fillLogCode: '',
})

function openLedger(row: ReviewFileTreeNode): void {
  const s = logic.selection.value
  // ⚠️ 文件夹行不该走到这里（按钮已 `visible:false`），但**必须**有兜底：
  //    没有它，一个漏判就会把空 Code 塞进抽屉 ⇒ 抽屉按空码查账本 ⇒
  //    弹一句「还没规范化过」，用户完全不知道自己做错了什么。
  if (!s || row.IsFolder || !row.FillLogCode) return
  ledger.enterpriseCode = s.EnterpriseCode
  ledger.fileName = row.FileName ?? row.Name
  ledger.standardFileCode = row.StandardFileCode ?? ''
  ledger.fillLogCode = row.FillLogCode
  ledger.visible = true
}

/** 账本被改过 ⇒ 重拉当前范围的清单（状态 / 待办数可能已变） */
function onLedgerChanged(): void {
  void logic.reloadFiles()
}
</script>

<template>
  <div class="rv-page">
    <YzhTreeTableLayout
      ref="treeLayoutRef"
      :tree-data="logic.treeNodes.value"
      :tree-width="340"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      @tree-node-click="onNodeClick"
    >
      <!-- 树底部：截断 / 加载失败时的兜底提示 -->
      <template #treeFooter>
        <div v-if="logic.treeHint.value" class="rv-tree-hint">{{ logic.treeHint.value }}</div>
      </template>

      <template #default>
        <div class="rv-main">
          <!-- ① 还没选 -->
          <YzhEmptyState
            v-if="!logic.selection.value"
            title="请在左侧选择「阶段」或「任务」"
            description="左树是「企业 › 阶段 › 任务」。选中阶段可以看该阶段全部文档并统一导出；选中任务可以看这一个批次的结果。"
          />

          <!-- ② 只选到企业：企业级只是分组，没有「企业级的文档」这回事 -->
          <YzhEmptyState
            v-else-if="logic.selection.value.level === 'enterprise'"
            title="请继续展开并选择具体的阶段或任务"
            description="企业本身不承载文档 —— 文档按「阶段」归属，导出也是按阶段导出的。"
          />

          <!-- ③ 主体 -->
          <template v-else>
            <!-- 范围条 + 导出 -->
            <div class="rv-bar">
              <div class="rv-bar__left">
                <span class="rv-bar__label">当前范围</span>
                <span class="rv-bar__value">{{ logic.selectionLabel.value }}</span>
                <span class="rv-bar__count">
                  已规范化 <b>{{ logic.filledRows.value.length }}</b> 份 / 共
                  {{ logic.fileRows.value.length }} 份
                </span>
              </div>
              <el-button
                type="primary"
                :loading="logic.exporting.value"
                :disabled="logic.fileRows.value.length === 0"
                @click="logic.exportZip()"
              >
                {{ exportText() }}
              </el-button>
            </div>

            <!--
              聚合口径说明（阶段级聚合了多个任务时）

              ⚠️ **⛔ 不要在这里写 Markdown 的 `**加粗**`** —— Vue 插值/文本节点
                **不解析 Markdown**，星号会**原样显示**（真机实测：
                「同一份文档只显示**最近一次**的执行结果」把两个星号都印了出来）。
                要加粗就用标签。
            -->
            <div v-if="logic.aggregatedQueueCodes.value.length > 1" class="rv-note">
              本清单聚合了 {{ logic.aggregatedQueueCodes.value.length }} 个任务的结果，
              同一份文档只显示<b>最近一次</b>的执行结果。
            </div>

            <!-- ★ 上限截断必须说破（⛔ 不静默） -->
            <div v-if="logic.skippedQueueCount.value > 0" class="rv-note rv-note--warn">
              还有 {{ logic.skippedQueueCount.value }} 个更早的任务没有纳入本次清单（单次最多聚合
              20 个）。要看更早的结果，请到左侧选中对应的任务。
            </div>

            <div class="rv-table">
              <YzhTable
                ref="fileTableRef"
                :columns="fileColumns"
                :data-loader="fileLoader"
                :show-pagination="false"
                :default-expand-all="true"
                :tree-props="{ children: 'Children' }"
                row-key="Code"
                empty-text="这个范围里还没有文档记录。请先到「企业资料规范化」开启一次任务。"
                :row-action-buttons="auditActions"
                @row-action="(_key, row) => openLedger(row)"
              >
                <!--
                  首列 = 树列（展开箭头由 el-table 固定渲染在第一列）。
                  两种行长得**必须不一样** —— 文件夹与文档在同一个视觉通道里，
                  只靠缩进区分会让用户以为「文件夹也是一份文档」。
                -->
                <template #column-Name="{ row }">
                  <span v-if="row.IsFolder" class="rv-folder">
                    <el-icon class="rv-folder__icon"><FolderOpened /></el-icon>
                    <span class="rv-folder__name" :title="row.Name">{{ row.Name }}</span>
                    <span class="rv-folder__count">{{ row.Count }} 份</span>
                  </span>
                  <span v-else class="rv-file">
                    <el-icon class="rv-file__icon"><Document /></el-icon>
                    <span class="rv-file__name" :title="row.Name">{{ row.Name }}</span>
                  </span>
                </template>

                <!-- 结果：徽标 + （非成功时）后端给的解释原文 -->
                <template #column-Status="{ row }">
                  <div v-if="!row.IsFolder" class="rv-status">
                    <YzhStatusBadge
                      :type="TASK_FILE_STATUS_TYPE[row.Status] || 'info'"
                      :text="TASK_FILE_STATUS_TEXT[row.Status] || row.Status"
                    />
                    <!--
                      ⚠️ 文本可能很长（失败时直接是数据库异常原文）⇒ 截断到两行 + 悬浮看全文，
                        ⛔ 不整段铺开 —— 一条 200 字的报错会把整行拉成一堵墙。
                    -->
                    <el-tooltip
                      v-if="showMessage(row)"
                      :content="row.Message"
                      placement="top"
                      popper-class="rv-msg-pop" :show-after="1000"
                    >
                      <span class="rv-status__msg">{{ row.Message }}</span>
                    </el-tooltip>
                  </div>
                </template>

                <!-- 完成率 -->
                <template #column-Completion="{ row }">
                  <el-progress
                    v-if="!row.IsFolder"
                    :percentage="toPercent(row.Completion)"
                    :stroke-width="10"
                    :status="row.Status === 'failed' ? 'exception' : undefined"
                  />
                </template>

                <!--
                  待办 —— ⛔ 只给一个数字等于没说。
                  同一个数字背后有四种原因（模板没配数据源 / 参数未在后台定义 /
                  企业没填值 / 企业档案字段为空），指向四个**不同**的修复动作，
                  所以把后端的归因原文摊开。

                  ★ 文件夹行给的是**合计**（纯文本，⛔ 不做成可点链接）：
                    合计不是任何一份文档的待办，点开没有对应明细可看 ——
                    做成链接会把人引到一个「点不开的空弹层」。
                -->
                <template #column-PendingCount="{ row }">
                  <span v-if="row.IsFolder" :class="{ 'rv-muted': !row.PendingTotal }">
                    {{ row.PendingTotal ? `合计 ${row.PendingTotal} 处` : '—' }}
                  </span>
                  <span v-else-if="row.PendingCount === 0" class="rv-muted">—</span>
                  <el-popover
                    v-else
                    placement="left"
                    :width="460"
                    trigger="click"
                    popper-class="rv-why-pop"
                  >
                    <template #reference>
                      <el-button type="primary" link size="small">
                        待办 {{ row.PendingCount }} 处
                      </el-button>
                    </template>

                    <div class="rv-why">
                      <div class="rv-why__hd">每条都写清了「去哪儿修」</div>
                      <ul class="rv-why__list">
                        <li v-for="(p, i) in row.Pendings" :key="i" class="rv-why__item">
                          <span class="rv-why__token">{{ p.Token }}</span>
                          <span v-if="p.Location" class="rv-why__loc">（{{ p.Location }}）</span>
                          <div class="rv-why__reason">{{ p.Reason }}</div>
                        </li>
                      </ul>
                    </div>
                  </el-popover>
                </template>

                <!-- 产物：★ 只显示文件名（完整路径含 GUID 段，撑爆整行且中段无意义） -->
                <template #column-OutputPath="{ row }">
                  <span v-if="row.IsFolder" />
                  <span v-else-if="row.OutputPath" :title="row.OutputPath">
                    {{ pathFileName(row.OutputPath) }}
                  </span>
                  <span v-else class="rv-muted">未生成</span>
                </template>

                <template #column-FillTime="{ row }">
                  <span v-if="!row.IsFolder" :class="{ 'rv-muted': !row.FillTime }">
                    {{ fmt(row.FillTime) }}
                  </span>
                </template>
              </YzhTable>
            </div>
          </template>
        </div>
      </template>
    </YzhTreeTableLayout>

    <!-- 审核抽屉：逐锚点改值 / 钉住 -->
    <ReviewLedgerDrawer
      v-model="ledger.visible"
      :enterprise-code="ledger.enterpriseCode"
      :file-name="ledger.fileName"
      :standard-file-code="ledger.standardFileCode"
      :fill-log-code="ledger.fillLogCode"
      @changed="onLedgerChanged"
    />
  </div>
</template>

<style scoped>
/*
 * ⛔ 不要动这两个类 —— `YzhTreeTableLayout` 的右面板是 `overflow:hidden`，
 *    页面必须自己给滚动容器，否则内容被裁到面板高且**整页滚不动**
 *    （症状会伪装成「展不开 / 点了没反应 / 下面的看不到」）。
 *    样板 = `pages/enterprise-normalize/create.vue`。
 */
.rv-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  overflow: hidden;
}

.rv-main {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  background: var(--yzh-color-bg-container, #fff);
}

/* ★ 第二坑：子项默认被 flex 压缩 ⇒ 必须显式 none，否则会被挤成一条线 */
.rv-main > * {
  flex: none;
}

.rv-tree-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.6;
  color: var(--yzh-color-warning, #d97706);
}

.rv-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  flex-wrap: wrap;
  padding-bottom: var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
}

.rv-bar__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  min-width: 0;
}

.rv-bar__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, #909399);
  flex-shrink: 0;
}

.rv-bar__value {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.rv-bar__count {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}

.rv-note {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  color: var(--yzh-color-text-tertiary, #909399);
}

.rv-note--warn {
  color: var(--yzh-color-warning, #d97706);
}

.rv-table {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: 4px;
}

/* ★ YzhTable 占满 .rv-table 的剩余高度 */
.rv-table :deep(.el-table) {
  flex: 1;
  min-height: 0;
}

.rv-table :deep(.el-table__inner-wrapper) {
  height: 100%;
}

.rv-muted {
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

/* ============================================================
 * 首列：文件夹行 / 文档行
 *
 * ★ 两种行**必须一眼可辨**：它们在同一个视觉通道里（同一列、同一缩进体系），
 *   只靠缩进区分会让人以为「文件夹也是一份文档」，然后去找它的「结果」。
 *   所以文件夹给主色图标 + 加粗 + 份数，文档给灰图标 + 常规字重。
 *
 * ⚠️ `min-width: 0` 是 flex 里让 `text-overflow: ellipsis` 生效的**必需前置**
 *   （flex 子项默认 `min-width: auto` ⇒ 不肯收缩 ⇒ 省略号永不出现）。
 * ============================================================ */
.rv-folder,
.rv-file {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  min-width: 0;
  max-width: 100%;
}

.rv-folder__icon {
  flex-shrink: 0;
  color: var(--yzh-color-primary, #1e3a8a);
}

.rv-folder__name {
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rv-folder__count {
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.rv-file__icon {
  flex-shrink: 0;
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.rv-file__name {
  color: var(--yzh-color-text-regular, #606266);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 结果列：徽标在上、解释在下（竖排，否则长文本会把徽标挤走） */
.rv-status {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--yzh-space-1, 4px);
  min-width: 0;
}

/*
 * ★ 两行截断（⛔ 不引第三方 clamp 组件）。
 *   `display:-webkit-box` 是 `-webkit-line-clamp` 的必需前置，⛔ 删不得。
 */
.rv-status__msg {
  display: -webkit-box;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  overflow: hidden;
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-tertiary, #909399);
  word-break: break-all;
  cursor: help;
}
</style>

<style>
/* ============================================================
 * ★ 待办原因弹层（`popper-class="rv-why-pop"`）
 *
 * 【为什么必须是非 scoped】`el-popover` 默认 `teleport` 到 `body` —— 弹层内容
 *   不在本组件 DOM 树里，`scoped` 的 `data-v-*` 选择器够不着 ⇒ 样式**永远不生效**。
 * 【为什么坚持 teleport】原因文本可能很长（如「参数曾定义、已被移除…」），
 *   留在表格行内会把整行撑变形。
 * ⚠️ 颜色 / 间距仍**全部用令牌** —— 守卫 `R18` 会扫（禁裸 hex）。
 * ============================================================ */
.rv-why-pop {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-3, 14px);
}

.rv-why__hd {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.rv-why__list {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.rv-why__item {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.rv-why__token {
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-primary, #303133);
}

.rv-why__loc {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.rv-why__reason {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-regular, #606266);
}

/* ============================================================
 * ★ 结果列的悬浮全文（`popper-class="rv-msg-pop"`）
 *
 * 【为什么必须是非 scoped】同上：`el-tooltip` 的弹层 `teleport` 到 `body`，
 *   `scoped` 的 `data-v-*` 够不着 ⇒ 不写在这里等于没写。
 * 【为什么限宽】后端失败原因常是数据库异常原文（单行 200+ 字），
 *   ⛔ 不限宽会拉出一条横贯全屏的细条，反而读不了。
 * ============================================================ */
.rv-msg-pop {
  max-width: 460px;
  line-height: var(--yzh-line-height-base, 1.6);
  word-break: break-all;
}
</style>
