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
 * <para>★ <b>面向专家的列设计</b>（用户明确要求）：
 * 文件名 · 标签 · <b>作用</b> · <b>是否提取成功</b> · 版本 · 操作按钮。
 * ⛔ 不出现 Markdown / 转换链 / Sha256 等技术词；⛔ 不用 emoji 图标（改用 Element Plus 图标）。</para>
 */
import { ref } from 'vue'
import { YzhTable } from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { FolderOpened, Document } from '@element-plus/icons-vue'
import type { OriginalFile } from '@share/api/ent/enterprise-original'

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
  /** 标签码 → 中文名（专家不该看到 RecordForm 这种英文码） */
  tagNames: Record<string, string>
}>()

/** ★ 标签显示中文名；字典里查不到才回退到码（总比空白好） */
function tagText(code: string): string {
  return props.tagNames[code] || code
}

const emit = defineEmits<{
  (e: 'detail', row: OriginalFile): void
  (e: 'preview', row: OriginalFile): void
  (e: 'ignore', row: OriginalFile): void
  (e: 'participate', row: OriginalFile): void
  (e: 'versions', row: OriginalFile): void
  (e: 'download', row: OriginalFile): void
  (e: 'delete', row: OriginalFile): void
  (e: 'tag', row: OriginalFile): void
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

// ⚠️ 总宽刻意控制在 ~950px：右区可用宽约 1000px，一屏放得下 ⇒ **不用 fixed 列**。
//    实测 `fixed: 'right'` 会把列渲染进 el-table 的独立 fixed 层，在本项目里
//    按钮宽度算成 0（点不动、截图里像是没有）。宁可让用户横向滚动，也不要固定列失效。
const columns: YzhTableColumn<OriginalFile>[] = [
  { prop: 'FileName', label: '文件名', width: 200, slot: true },
  { prop: 'Tags', label: '标签', width: 140, slot: true },
  { prop: 'DocPurpose', label: '作用', minWidth: 180, slot: true },
  { prop: 'ExtractState', label: '提取', width: 92, slot: true },
  { prop: 'VersionNumber', label: '版本', width: 56, align: 'center', slot: true },
  { prop: 'Actions', label: '操作', width: 230, slot: true },
]

function rowsLoader(node: OriginalFolderNode) {
  return () => Promise.resolve({ rows: node.Files, total: node.Files.length })
}

// ═══════════════ 专家语言的状态（从三态压成一个）═══════════════

const STATUS_TEXT: Record<string, { text: string; type: 'success' | 'info' | 'warning' | 'primary' | 'danger' }> = {
  done: { text: '已提取', type: 'success' },
  processing: { text: '提取中', type: 'primary' },
  failed: { text: '提取失败', type: 'danger' },
  manual: { text: '需人工填写', type: 'warning' },
  skipped: { text: '已忽略', type: 'info' },
  pending: { text: '待提取', type: 'info' },
  // ★ 策略为 skip/ignore（人工或 AI 建议）⇒ 不设标签/作用
  notSuggested: { text: '不建议提取', type: 'info' },
}

function statusOf(row: OriginalFile): { text: string; type: 'success' | 'info' | 'warning' | 'primary' | 'danger' } {
  if (row.IsUsableForFilling) return STATUS_TEXT.done
  if (row.IsNotSuggested) return STATUS_TEXT.notSuggested
  const cv = row.ConvertStatus
  const md = row.MarkdownStatus
  const an = row.AnalyzeStatus
  if (cv === 'converting' || md === 'converting' || an === 'analyzing') return STATUS_TEXT.processing
  if (cv === 'unsupported' || md === 'unsupported') return STATUS_TEXT.manual
  if (cv === 'failed' || md === 'failed' || an === 'failed') return STATUS_TEXT.failed
  if (an === 'skipped' || row.AnalyzePolicy === 'ignore' || row.AnalyzePolicy === 'skip') return STATUS_TEXT.skipped
  return STATUS_TEXT.pending
}

/** tooltip：说清「为什么是这个状态」，用专家看得懂的话 */
function statusTip(row: OriginalFile): string {
  const s = statusOf(row)
  if (s.text === '需人工填写') return '这个格式系统自动读不了内容，请人工填写下面的「作用」'
  if (s.text === '提取失败') return row.AnalyzeMessage || row.ConvertMessage || row.MarkdownMessage || '处理时出错，可删除后重新上传'
  if (s.text === '不建议提取') return '这份文件不参与标签和作用提取（可能是营业执照、身份证等特定证件）'
  if (s.text === '待提取') return row.UnusableReason || '还没开始处理，稍等片刻刷新看看'
  if (s.text === '已忽略') return '这份文件已设置为不参与提取'
  return ''
}

/** 作用摘要：只取第一段「是什么」，单行显示（⛔ 四段全塞进单元格会把行撑成十几行） */
function purposeOf(row: OriginalFile): string {
  const p = row.DocPurpose || ''
  if (!p) return ''
  const first = p.split('\n').map((s) => s.trim()).find(Boolean) || ''
  return first.replace(/^【[^】]*】/, '').trim()
}

/** tooltip 里显示四段全文（要点名分隔，让专家知道每一段讲什么） */
function fullPurpose(row: OriginalFile): string {
  const p = row.DocPurpose || ''
  if (!p) return ''
  return p.split('\n').map((s) => s.trim()).filter(Boolean).join('\n')
}

function formatSize(size?: number | null): string {
  if (!size) return ''
  if (size < 1024) return `${size} B`
  return size > 1024 * 1024 ? `${(size / 1024 / 1024).toFixed(1)} MB` : `${Math.round(size / 1024)} KB`
}

function extOf(name?: string): string {
  const i = (name || '').lastIndexOf('.')
  return i > 0 ? name!.slice(i + 1).toLowerCase() : ''
}
</script>

<template>
  <div class="folder-tree">
    <!-- 根目录（企业直接放在阶段根下的文件） -->
    <div v-if="nodes.length === 0" class="folder-empty">
      <el-empty description="该阶段下还没有文件" :image-size="70" />
    </div>

    <el-collapse v-else v-model="opened" class="folder-collapse">
      <el-collapse-item v-for="node in nodes" :key="pathKey(node)" :name="pathKey(node)">
        <template #title>
          <el-icon class="folder-icon"><FolderOpened /></el-icon>
          <span class="folder-title">{{ node.Name }}</span>
          <el-tag size="small" type="info">{{ node.Total }} 个文件</el-tag>
          <el-tag v-if="node.Done > 0" size="small" type="success">已提取 {{ node.Done }}</el-tag>
          <el-tag v-if="node.Failed > 0" size="small" type="danger">失败 {{ node.Failed }}</el-tag>
        </template>

        <!-- 本文件夹自身的文件 -->
        <YzhTable
          v-if="node.Files.length"
          :key="`${pathKey(node)}/${props.dataRevision}`"
          :columns="columns"
          :data-loader="rowsLoader(node)"
          :toolbar="false"
          :show-pagination="false"
          :select-mode="props.busy ? 'none' : 'multiple'"
          row-key="Code"
          empty-text="该文件夹暂无文件"
        >
          <!-- 文件名 + 大小 -->
          <template #column-FileName="{ row }">
            <div class="f-name">
              <el-icon class="f-name__icon"><Document /></el-icon>
              <span class="f-name__text" :title="row.FileName">{{ row.FileName }}</span>
              <el-tag v-if="row.PolicySource === 'ai' && row.PolicyReason" size="small" type="warning" effect="plain">
                待确认
              </el-tag>
            </div>
            <div class="f-sub">
              {{ formatSize(row.FileSize) }}
              <template v-if="extOf(row.FileName)"> · {{ extOf(row.FileName).toUpperCase() }}</template>
            </div>
          </template>

          <!-- 标签：受控词表，可点开编辑。
               ⛔ 「不建议提取」的文件**不给入口**（营业执照/身份证等特定证件不该进通用标签池） -->
          <template #column-Tags="{ row }">
            <span v-if="row.IsNotSuggested" class="f-dim">—</span>
            <div v-else-if="(row.Tags ?? []).length" class="f-tags">
              <el-tag
                v-for="t in row.Tags" :key="t" size="small" effect="plain"
                class="f-tags__item" :title="tagText(t)" @click="emit('tag', row)"
              >{{ tagText(t) }}</el-tag>
            </div>
            <el-button v-else link type="primary" size="small" @click="emit('tag', row)">
              {{ row.HasProfile ? '补标签' : '加标签' }}
            </el-button>
          </template>

          <!-- 作用：AI 生成，可点开改。⛔ 「不建议提取」的文件不给入口 -->
          <template #column-DocPurpose="{ row }">
            <span v-if="row.IsNotSuggested" class="f-dim">—</span>
            <el-tooltip v-else-if="purposeOf(row)" :content="fullPurpose(row)" placement="top" :show-after="300">
              <span class="f-purpose" @click="emit('detail', row)">{{ purposeOf(row) }}</span>
            </el-tooltip>
            <el-button v-else link type="primary" size="small" @click="emit('detail', row)">
              {{ row.HasProfile ? '补作用' : '填作用' }}
            </el-button>
          </template>

          <!-- 是否提取成功 -->
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

          <!-- ★ 操作：详情 / 预览 / 忽略 三个高频动作常驻，其余收进「更多」 -->
          <template #column-Actions="{ row }">
            <el-button link type="primary" size="small" @click="emit('detail', row)">详情</el-button>
            <el-button link type="primary" size="small" @click="emit('preview', row)">预览</el-button>
            <el-button
              v-if="row.AnalyzePolicy === 'analyze'"
              link size="small" @click="emit('ignore', row)"
            >忽略</el-button>
            <el-button v-else link type="success" size="small" @click="emit('participate', row)">恢复提取</el-button>
            <el-dropdown v-if="row.VersionNumber > 1" trigger="click" @command="(c: string) => {
              if (c === 'versions') emit('versions', row)
              else emit('download', row)
            }">
              <el-button link size="small">更多</el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="versions">历史版本</el-dropdown-item>
                  <el-dropdown-item command="download">下载原件</el-dropdown-item>
                  <el-dropdown-item command="delete" divided>删除</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
            <el-button v-else link type="danger" size="small" @click="emit('delete', row)">删除</el-button>
          </template>
        </YzhTable>

        <div v-else class="f-nofile">该文件夹下没有直接存放的文件</div>

        <!-- 子文件夹（递归） -->
        <OriginalFolderTree
          v-for="child in node.Children"
          :key="pathKey(child)"
          :nodes="[child]"
          :data-revision="props.dataRevision"
          :busy="props.busy"
          :tag-names="props.tagNames"
          @detail="(r) => emit('detail', r)"
          @preview="(r) => emit('preview', r)"
          @ignore="(r) => emit('ignore', r)"
          @participate="(r) => emit('participate', r)"
          @versions="(r) => emit('versions', r)"
          @download="(r) => emit('download', r)"
          @delete="(r) => emit('delete', r)"
          @tag="(r) => emit('tag', r)"
        />
      </el-collapse-item>
    </el-collapse>
  </div>
</template>

<style scoped>
.folder-tree { min-width: 0; }
.folder-collapse { border: none; }
.folder-collapse :deep(.el-collapse-item__header) {
  border: none; background: transparent; padding: 6px 0; font-weight: 500;
}
.folder-collapse :deep(.el-collapse-item__wrap) { border: none; }
.folder-collapse :deep(.el-collapse-item__content) { padding-bottom: 8px; }
.folder-icon { color: var(--el-color-warning); margin-right: 6px; vertical-align: -2px; }
.folder-title { margin-right: 8px; }
.folder-empty { padding: 20px 0; }

.f-name { display: flex; align-items: center; gap: 5px; }
.f-name__icon { color: var(--el-text-color-secondary); flex: none; }
.f-name__text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.f-sub { font-size: 11px; color: var(--el-text-color-placeholder); margin-top: 1px; }
.f-dim { color: var(--el-text-color-placeholder); }

.f-tags { display: flex; gap: 3px; flex-wrap: wrap; }
.f-tags__item { cursor: pointer; }

.f-purpose { cursor: pointer; display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.f-nofile { font-size: 12px; color: var(--el-text-color-placeholder); padding: 4px 0 8px; }
</style>