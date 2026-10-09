<script setup lang="ts">
/**
 * 原始资料文件夹树（递归）—— 36 号 T3.2
 *
 * <para><b>为什么照抄资料库的 <c>StandardFolderTree</c> 结构</b>（2026-10-03 用户裁决）：
 * 企业交上来的资料是<b>有文件夹层级</b>的（`4记录文件/技术类/…`），而且<b>文件很多</b>。
 * 卡片列表一屏放不下几份，信息密度极低 ⇒ 改为「文件夹树 + 每个文件夹一张紧凑文件表」。</para>
 *
 * <para>每个节点 = 该文件夹自身的文件表（<c>YzhTable</c>，守卫 R6）+ 子文件夹列表；
 * 计数按子树汇总（父目录的统计 = 自身 + 全部后代）。</para>
 *
 * <para>★ <b>面向专家的列设计</b>（2026-10-07 用户裁决精简）：
 * <b>文件名（含大小）</b> · <b>提取状态</b> · <b>版本</b> · 操作按钮。
 * ⛔ 删掉「标签」「作用」两列 —— 它们**按标准各有一份**（一个文件在 N 个标准下有 N 行画像），
 *    摊在列表里既占宽、又必须逐行写清「这是哪个标准的」；改到行操作「提取信息」里按当前标准编辑。
 * ⛔ 不出现 Markdown / 转换链 / Sha256 等技术词；⛔ 不用 emoji 图标（改用 Element Plus 图标）。</para>
 *
 * <para>★★ <b>2026-10-07 四轮：文件夹是一等公民</b>（用户逐字：「审核员……需要的只是通过这个进行
 * <b>管理</b>：可以预览，可以针对单个文件进行<b>替换</b>，可以删除，<b>可以删除整个文件夹，
 * 或针对整个文件夹进行更新</b>」）。⇒ 文件夹标题右侧挂一个「<b>操作 ▾</b>」下拉：
 * 更新本文件夹内容 / 排除提取 / 允许提取 / 删除整个文件夹。</para>
 *
 * <para>⛔ <b>本组件刻意不做的事</b>（用户裁决「不需要确认任何信息 / 不用做强制的审批」）：
 * 不再在文件名后挂「待确认」标签、不再要求审核员先处理 AI 建议才能往下走。
 * 系统给的是<b>参考</b>，采不采由人自己判断。</para>
 */
import { ref } from 'vue'
import { YzhTable, YzhEmptyState, type YzhAction } from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { FolderOpened, Document, ArrowDown } from '@element-plus/icons-vue'
import type { OriginalFile } from '@share/api/ent/enterprise-original'
// ★ 提取状态 + tooltip 与根目录表**共用同一份**（logic.ts）：
//   ⛔ 不再各判一套 —— 原来本组件判七态、根目录表只判两态，
//      同一份文件在页面上下两处显示不同状态，用户无法判断哪个是真的。
import { statusOf, statusTip } from '../logic'

/** 文件夹节点（由 logic.ts 按 RelFolderPath 构建） */
export interface OriginalFolderNode {
  /** 相对路径，根为 '' */
  Path: string
  Name: string
  Depth: number
  Files: OriginalFile[]
  Children: OriginalFolderNode[]
  /** 子树汇总 */
  Total: number
  Done: number
  Failed: number
}

const props = defineProps<{
  nodes: OriginalFolderNode[]
  dataRevision: number
  busy?: boolean
}>()

const emit = defineEmits<{
  /**
   * ★ <b>2026-10-07 四轮：原 'detail' + 'content' 合并为一条「提取信息」</b>。
   *
   * <para>用户逐字：「可以增加一个<b>提取信息的展示</b>」⇒ 要的是<b>一个</b>入口，
   * ⛔ 不是页面上并排的「详情」「提取内容」两条（它们打开的是同一份东西的两个切面）。
   * 合并后由父页在一个抽屉里用两个 Tab 呈现。</para>
   */
  (e: 'detail', row: OriginalFile): void
  (e: 'regenerate', row: OriginalFile): void
  (e: 'preview', row: OriginalFile): void
  /**
   * ★ 点击文件行（2026-10-09）。
   *
   * <para>本页原先**没有**行点击动作（只有行操作「操作 ▾」下拉），但用户对
   * 「不支持提取规则」的文件要求「**点击该文件，直接提示**该文件不支持提取规则」——
   * hover 才出的 tooltip 满足不了「点一下就知道」。</para>
   *
   * <para>本组件只**转发**：判据与文案在 `logic.ts`，提示在父页（与根目录表共用一份）。</para>
   */
  (e: 'row-click', row: OriginalFile): void
  /** ★ 单个文件换一份新的（走 `upload/*`，旧版本自动保留）—— 用户逐字：「可以针对单个文件进行替换」 */
  (e: 'replace', row: OriginalFile): void
  /** ★ 原 'ignore' —— 文案与语义都改成「排除提取」（用户裁决：「忽略」让人猜不到后果） */
  (e: 'exclude', row: OriginalFile): void
  /** ★ 原 'participate' —— 改成「允许提取」，与「排除提取」成对 */
  (e: 'allow', row: OriginalFile): void
  (e: 'versions', row: OriginalFile): void
  (e: 'download', row: OriginalFile): void
  (e: 'delete', row: OriginalFile): void

  // ══════════ ★ 文件夹级（2026-10-07 四轮）══════════
  // 用户逐字：「可以删除<b>整个文件夹</b>，或针对<b>整个文件夹进行更新</b>等操作」——
  // ⇒ 「文件夹」在这页是**一等公民**，不是一个只会展开/收起的壳。
  // ⚠️ 本组件**只负责发事件**：作用域（子树展开成哪些文件 Code）与确认文案都在 `logic.ts`，
  //    因为它需要 `enterpriseCode`（组件不持有），也需要和单文件操作共用同一套批处理口径。
  /** 更新本文件夹下的内容（等价于「以这个文件夹为落点再传一批」） */
  (e: 'folder-update', node: OriginalFolderNode): void
  /** 本文件夹（含子文件夹）下的文件全部不参与提取 */
  (e: 'folder-exclude', node: OriginalFolderNode): void
  /** 本文件夹（含子文件夹）下的文件恢复参与提取 */
  (e: 'folder-allow', node: OriginalFolderNode): void
  /** 删除整个文件夹（含子文件夹）—— 破坏性动作 */
  (e: 'folder-delete', node: OriginalFolderNode): void
}>()

/** 默认展开前两层（更多会太吵） */
const opened = ref<string[]>([])
watchInit()
function watchInit(): void {
  const walk = (list: OriginalFolderNode[], depth: number): void => {
    for (const n of list) {
      if (depth <= 1) opened.value.push(pathKey(n))
      walk(n.Children, depth + 1)
    }
  }
  walk(props.nodes, 0)
}
function pathKey(n: OriginalFolderNode): string {
  return n.Path || '__root__'
}

// ⚠️ 删掉「标签」「作用」两列后只剩 3 列，不再需要「总宽 ~950px 一屏放下」的约束。
//    ⛔ 仍然**不用 fixed 列** —— 实测 `fixed: 'right'` 会把列渲染进 el-table 的独立 fixed 层，
//       在本项目里按钮宽度算成 0（点不动、截图里像是没有）。宁可横向滚动，也不要固定列失效。
const columns: YzhTableColumn<OriginalFile>[] = [
  { prop: 'FileName', label: '文件名', minWidth: 260, slot: true },
  { prop: 'ExtractState', label: '提取状态', width: 110, slot: true },
  { prop: 'VersionNumber', label: '版本', width: 60, align: 'center', slot: true }
  // ⛔ 操作列不再手写（2026-10-07）：YzhTable 内置行动作列 + action-dropdown-only
  //    列宽按 actionColWidth 自适应；action-fixed=false 见上方 fixed 层的坑
]

function rowsLoader(node: OriginalFolderNode) {
  return () => Promise.resolve({ rows: node.Files, total: node.Files.length })
}

/**
 * 行操作（纯下拉：一个「操作 ▾」装下全部按钮）。
 *
 * ★ 2026-10-07 三轮修正：
 *   ① 「忽略 / 恢复提取」→「**排除提取 / 允许提取**」（用户裁决：原文案让人猜不到会发生什么）。
 *   ② 原先「下载原件 / 历史版本 / 重新生成内容」只在 `VersionNumber > 1` 时才出现
 *      ⇒ **没被替换过的文件下载不了原件**，也点不到重新生成；改为恒定可见。
 *   ③ **四轮**：「详情」+「提取内容」合并为一条「**提取信息**」；新增「**替换**」。
 *      ⚠️ 这份清单必须与父页根目录表（`index.vue` 的 `rootRowActions`）**逐条一致** ——
 *      同一个文件在「根目录」和「某个文件夹」里能做的事不一样，是用户最先察觉的那种不一致。
 *      ⇒ 这里刻意**不放**「重新生成内容」：它已在「提取信息」抽屉里，放这儿只会让下拉更长。
 */
function rowActions(row: OriginalFile): YzhAction[] {
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

/** ★ 点击文件行 → 转发（2026-10-09）。`YzhTable` 的 row-click 还带 index，父页用不到，只转 row。 */
function onRowClick(row: OriginalFile) {
  emit('row-click', row)
}

/** YzhTable @row-action(key,row) → 本组件既有 emit（转发链不变） */
function onRowAction(key: string, row: OriginalFile) {
  switch (key) {
    case 'info': return emit('detail', row)
    case 'preview': return emit('preview', row)
    case 'download': return emit('download', row)
    case 'versions': return emit('versions', row)
    case 'replace': return emit('replace', row)
    case 'exclude': return emit('exclude', row)
    case 'allow': return emit('allow', row)
    case 'delete': return emit('delete', row)
  }
}

/** ★ 文件夹标题上的「操作 ▾」下拉（2026-10-07 四轮新增）—— 与行操作同一个分发形态 */
function onFolderAction(cmd: string, node: OriginalFolderNode) {
  switch (cmd) {
    case 'update': return emit('folder-update', node)
    case 'exclude': return emit('folder-exclude', node)
    case 'allow': return emit('folder-allow', node)
    case 'delete': return emit('folder-delete', node)
  }
}

// ═══════════════ 展示辅助 ═══════════════
// ★ statusOf / statusTip 已提到 logic.ts（与根目录表共用一份口径）

function formatSize(size?: number | null): string {
  if (!size) return ''
  if (size < 1024) return `${size} B`
  return size > 1024 * 1024 ? `${(size / 1024 / 1024).toFixed(1)} MB` : `${Math.round(size / 1024)} KB`
}

function extOf(name?: string): string {
  const i = (name || '').lastIndexOf('.')
  return i > 0 ? name!.slice(i + 1).toLowerCase() : ''
}

/** ★ 文件夹表头徽标口径：**本层文件数**（⛔ 不再用子树汇总当主数字，见模板注释） */
function ownCount(node: OriginalFolderNode): number {
  return node.Files.length
}
</script>

<template>
  <div class="folder-tree">
    <!-- 根目录（企业直接放在阶段根下的文件） -->
    <div v-if="nodes.length === 0" class="folder-empty">
      <YzhEmptyState title="该阶段下还没有文件" />
    </div>

    <el-collapse v-else v-model="opened" class="folder-collapse">
      <el-collapse-item v-for="node in nodes" :key="pathKey(node)" :name="pathKey(node)">
        <!-- ★ 徽标口径（2026-10-07 修正）：原来主数字用 `node.Total`（**子树汇总**），
             但本文件夹的表格里只有 `node.Files` 这几行 ⇒ 父级显示「101 个文件」而它自己的表只有 5 行，
             用户以为数字错了（也是「显示的信息不正确」的一种）。
             现在：主数字 = **本层文件数**；有子文件夹时另给一句「含子文件夹共 N」。
             ⛔ 不再拿 `node.Total` 当主数字 —— 那等于把子树重复算进父级。 -->
        <template #title>
          <!-- ★ 2026-10-07 四轮：
               ① 原来 4 个裸 `el-tag`（`N 个文件` / 已提取 / 失败）改成**纯文字**：
                  它们是「说明」不是「状态徽标」，用彩色标签反而抢走注意力，
                  而且法条 S08（状态标签用 YzhStatusBadge）的基线**只准减不准增**。
               ② 「已提取 N」直接去掉 —— 顺利的那部分不需要在标题上喊，
                  审核员真正需要一眼看到的是「哪几份出问题了」（失败 N）。
               ③ 新增「**操作 ▾**」下拉：更新本文件夹内容 / 排除提取 / 允许提取 / 删除整个文件夹。
                  用户逐字：「可以删除整个文件夹，或针对整个文件夹进行更新」。 -->
          <div class="folder-head">
            <el-icon class="folder-icon"><FolderOpened /></el-icon>
            <span class="folder-title">{{ node.Name }}</span>
            <span class="folder-sub">
              {{ ownCount(node) }} 个文件<template v-if="node.Children.length">（含子文件夹 {{ node.Total }}）</template>
            </span>
            <span v-if="node.Failed > 0" class="folder-bad" :title="`含子文件夹合计 ${node.Failed} 份处理失败`">
              失败 {{ node.Failed }}
            </span>
            <span class="folder-head__spacer" />
            <!-- ⚠️ `@click.stop` 不可省：折叠面板的标题整条是「点击切换展开」的热区，
                 不拦住冒泡 ⇒ 点「操作」的同时把文件夹收起来了（看起来像「点了没反应」）。
                 ★ 刻意把 `.stop` 挂在**外层 span** 而不是按钮上：`el-dropdown` 会把触发器事件
                   合并进它的唯一子节点，挂在按钮上等于赌「合并时保留了我的修饰符」；
                   挂在 span 上则是纯粹的 DOM 冒泡拦截 —— 下拉先弹出、事件再被拦下，两者互不干扰。 -->
            <span class="folder-head__ops" @click.stop>
              <el-dropdown trigger="click" @command="(c: string) => onFolderAction(c, node)">
                <el-button size="small" text type="primary" class="folder-ops">
                  操作<el-icon class="folder-ops__caret"><ArrowDown /></el-icon>
                </el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item command="update">更新本文件夹内容</el-dropdown-item>
                    <el-dropdown-item command="exclude">排除提取</el-dropdown-item>
                    <el-dropdown-item command="allow">允许提取</el-dropdown-item>
                    <el-dropdown-item command="delete" divided>删除整个文件夹</el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </span>
          </div>
        </template>

        <!-- 本文件夹自身的文件 -->
        <YzhTable
          v-if="node.Files.length"
          :key="`${pathKey(node)}/${props.dataRevision}`"
          :columns="columns"
          :data-loader="rowsLoader(node)"
          :toolbar="false"
          :show-pagination="false"
          :row-action-buttons="rowActions"
          :action-dropdown-only="true"
          :action-fixed="false"
          :select-mode="props.busy ? 'none' : 'multiple'"
          row-key="Code"
          empty-text="该文件夹暂无文件"
          @row-action="onRowAction"
          @row-click="onRowClick"
        >
          <!-- 文件名 + 大小 -->
          <!-- ⛔ 原「待确认」`el-tag` 已于 2026-10-07 四轮删除：
               用户逐字「审核员不需要确认任何信息……针对提取的 markdown、标签、文档作用
               **不用做强制的审批**」—— 在文件名后面挂一个「待确认」正是那句被取消的口气，
               它把「系统的一条建议」伪装成「你必须处理的一件事」。
               现在 AI 建议只在「提取信息」抽屉里以中性一行呈现（且标注「仅供参考」）。 -->
          <template #column-FileName="{ row }">
            <div class="f-name">
              <el-icon class="f-name__icon"><Document /></el-icon>
              <span class="f-name__text" :title="row.FileName">{{ row.FileName }}</span>
            </div>
            <div class="f-sub">
              {{ formatSize(row.FileSize) }}
              <template v-if="extOf(row.FileName)"> · {{ extOf(row.FileName).toUpperCase() }}</template>
            </div>
          </template>

          <!-- ⛔ 原 #column-Tags（标签）与 #column-DocPurpose（作用）两个插槽已于 2026-10-07 删除：
               这两列按标准各有一份（一个文件在 N 个标准下有 N 行画像），
               摊在列表里必须逐行标注标准才不会串；用户裁决改为「只在行操作『详情』里按当前标准编辑」。 -->

          <!-- 提取状态（口径来自 logic.statusOf，与根目录表**共用一份**） -->
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

          <!-- ★ 操作改由 YzhTable 内置列渲染（rowActions + action-dropdown-only），
               原 #column-Actions 手写插槽 + 手写「更多」下拉已于 2026-10-07 删除。
               顺带修掉旧下拉的命令分发 bug：`@command` 只判了 'versions'，
               其余（regenerate/delete）全部误走 `emit('download')`。 -->
        </YzhTable>

        <div v-else class="f-nofile">该文件夹下没有直接存放的文件</div>

        <!-- 子文件夹（递归）
             ★ 2026-10-07 补回 `folder-tree--child`（缩进 + 左侧竖线）：
               原先漏了这个 class ⇒ 二级文件夹与一级**同缩进**，树看起来是平铺的，
               用户无法判断「技术类」到底属于哪个上级目录（照抄源 `StandardFolderTree` 有它）。 -->
        <OriginalFolderTree
          v-for="child in node.Children"
          class="folder-tree--child"
          :key="pathKey(child)"
          :nodes="[child]"
          :data-revision="props.dataRevision"
          :busy="props.busy"
          @detail="(r) => emit('detail', r)"
          @regenerate="(r) => emit('regenerate', r)"
          @preview="(r) => emit('preview', r)"
          @row-click="(r) => emit('row-click', r)"
          @replace="(r) => emit('replace', r)"
          @exclude="(r) => emit('exclude', r)"
          @allow="(r) => emit('allow', r)"
          @versions="(r) => emit('versions', r)"
          @download="(r) => emit('download', r)"
          @delete="(r) => emit('delete', r)"
          @folder-update="(n) => emit('folder-update', n)"
          @folder-exclude="(n) => emit('folder-exclude', n)"
          @folder-allow="(n) => emit('folder-allow', n)"
          @folder-delete="(n) => emit('folder-delete', n)"
        />
      </el-collapse-item>
    </el-collapse>
  </div>
</template>

<style scoped>
.folder-tree { min-width: 0; }
/* ★ 子文件夹缩进 + 左侧竖线：让「层级」可见（照抄源 `StandardFolderTree` 同款）——
   ⛔ 缺了它，二级文件夹与一级同缩进，树看起来是平铺的（2026-10-07 修复） */
.folder-tree--child {
  padding-left: var(--yzh-space-5, 20px);
  border-left: 2px solid var(--el-border-color-lighter);
}
.folder-collapse { border: none; }
.folder-collapse :deep(.el-collapse-item__header) {
  border: none; background: transparent; padding: 6px 0; font-weight: 500;
}
.folder-collapse :deep(.el-collapse-item__wrap) { border: none; }
.folder-collapse :deep(.el-collapse-item__content) { padding-bottom: 8px; }
.folder-icon { color: var(--el-color-warning); margin-right: 6px; vertical-align: -2px; }
.folder-title { margin-right: 8px; }
/* 「N 个文件（含子文件夹 M）」—— 说明主数字为什么小于它，⛔ 不再让父子数字互相矛盾 */
.folder-sub { font-size: var(--yzh-font-size-xs, 12px); color: var(--el-text-color-placeholder); margin-right: var(--yzh-space-2, 8px); }
.folder-empty { padding: 20px 0; }

/* ══════════ ★ 文件夹标题行（2026-10-07 四轮）══════════
   左：图标 + 名称 + 计数 + 「失败 N」；右：「操作 ▾」下拉。
   `flex: 1` 让它在折叠面板标题里占满，spacer 把下拉推到最右（箭头左侧）。 */
.folder-head { display: flex; align-items: center; gap: var(--yzh-space-2, 8px); flex: 1; min-width: 0; }
.folder-head__spacer { flex: 1; }
/* 「失败 N」—— 纯文字 + 危险色（⛔ 不用 el-tag：法条 S08 基线只准减不准增；
   顺利的那部分不喊，出问题的才要一眼看见） */
.folder-bad { font-size: var(--yzh-font-size-xs, 12px); color: var(--el-color-danger); }
/* 下拉的拦截外壳（`@click.stop` 挂在这里，见模板注释） */
.folder-head__ops { flex: none; display: inline-flex; align-items: center; }
.folder-ops { flex: none; }
.folder-ops__caret { margin-left: var(--yzh-space-1, 4px); vertical-align: -2px; }

.f-name { display: flex; align-items: center; gap: 5px; }
.f-name__icon { color: var(--el-text-color-secondary); flex: none; }
.f-name__text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.f-sub { font-size: 11px; color: var(--el-text-color-placeholder); margin-top: 1px; }
.f-dim { color: var(--el-text-color-placeholder); }

.f-nofile { font-size: 12px; color: var(--el-text-color-placeholder); padding: 4px 0 8px; }
</style>