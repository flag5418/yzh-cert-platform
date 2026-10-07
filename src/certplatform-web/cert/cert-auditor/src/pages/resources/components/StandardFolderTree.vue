<script setup lang="ts">
/**
 * 标准文件夹树（递归）—— 01 分册 §3.2 卡体
 *
 * <para>为什么要递归组件：模板目录是真三级（`4记录文件/技术类` 这种），
 * 早期实现把 11 个文件夹平铺成一个列表，三级文件夹看起来跟一级同级，
 * 用户无法判断「技术类」到底属于哪个上级目录。</para>
 *
 * <para>每个节点 = 该文件夹自身的槽位表（YzhTable，守卫 R6）+ 子节点列表；
 * 计数按子树汇总（父目录的「已就位/缺失」= 自身 + 全部后代）。</para>
 */
import { ref } from 'vue'
import { YzhTable, YzhEmptyState, type YzhAction } from '@yzh-core'
import { FolderOpened } from '@element-plus/icons-vue'
import { SLOT_STATUS_TEXT } from '@share/api'
import type { FileSlot } from '@share/api'
import type { FolderNode } from '../logic'

const props = defineProps<{
  nodes: FolderNode[]
  columns: any[]
  dataRevision: number
  panelHeight: (count: number) => number
  /** 转换/提取进行中：禁用写入类操作 */
  busy?: boolean
}>()

const emit = defineEmits<{
  (e: 'replace', row: FileSlot): void
  (e: 'remove', row: FileSlot): void
  (e: 'versions', row: FileSlot): void
  (e: 'extract', row: FileSlot): void
  (e: 'result', row: FileSlot): void
  (e: 'preview', row: FileSlot): void
  (e: 'download', row: FileSlot): void
  (e: 'upload', folderCode: string): void
}>()

/** 默认展开一级/二级，折叠三级（与 01 §3.2 一致） */
const opened = ref<string[]>(props.nodes.filter(n => n.Depth <= 2).map(n => n.Code))

/** 本地分组数据一次性回传（分页关闭）——YzhTable 只在挂载时跑 dataLoader */
function rowsLoader(node: FolderNode) {
  return () => Promise.resolve({ rows: node.Files, total: node.Files.length })
}

/**
 * 行操作按钮（2026-10-07 起改由 YzhTable 内置列渲染，原 #column-Actions 插槽已删）
 *
 * <para>列宽由 `actionColWidth` 按实际文案估宽 + `action-dropdown-only`
 * 收敛到下限 88px —— 原先 `width: 320` 固定像素不随按钮数收缩。</para>
 *
 * <para>⚠️ 原实现在两处禁用「提取」上挂了 `el-tooltip`（not_configured /
 * 转换后无正文）。`YzhAction` 不支持 tooltip，2026-10-07 用户裁决**丢弃**——
 * 解释改由「提取状态」列自身的 tooltip（`extractTip`）承担。</para>
 */
function rowActions(row: FileSlot): YzhAction[] {
  if (row.Status === 'removed') return [{ key: 'versions', text: '恢复' }]
  if (row.Status === 'missing') return [{ key: 'upload', text: '上传', disabled: props.busy }]

  const list: YzhAction[] = [
    { key: 'preview', text: '预览' },
    { key: 'download', text: '下载' },
    { key: 'replace', text: '替换', disabled: props.busy },
    { key: 'versions', text: '版本' }
  ]

  const canExtract = row.ConvertStatus === 'completed' && !!row.MarkdownPath && row.ExtractState !== 'not_configured'
  const noBody = row.ConvertStatus === 'completed' && !row.MarkdownPath
  if (canExtract) {
    list.push({
      key: 'extract',
      text: row.ExtractState === 'failed' ? '重试' : '提取',
      type: row.ExtractState === 'failed' ? 'danger' : 'primary',
      disabled: props.busy
    })
  } else if (row.ExtractState === 'not_configured' || noBody) {
    list.push({ key: 'extract', text: '提取', type: 'default', disabled: true })
  }
  // else：转换中/未完成且非 not_configured ⇒ 不出「提取」（与原实现 v-if 链一致）

  list.push({ key: 'result', text: '结果' })
  list.push({ key: 'remove', text: '移除', type: 'danger', disabled: props.busy })
  return list
}

/** YzhTable @row-action(key,row) → 本组件既有 emit（转发链不变，index.vue 零改动） */
function onRowAction(key: string, row: FileSlot) {
  switch (key) {
    case 'preview': return emit('preview', row)
    case 'download': return emit('download', row)
    case 'replace': return emit('replace', row)
    case 'versions': return emit('versions', row)
    case 'extract': return emit('extract', row)
    case 'result': return emit('result', row)
    case 'remove': return emit('remove', row)
    case 'upload': return emit('upload', row.FolderCode)
  }
}

/**
 * ★ 提取状态五态文案/色（与后端 ExtractStateRule.Of 同口径）
 *
 * <para>2026-09-30 用户裁决新增 `failed`：**有提取规则、但执行时失败**
 * （文档内容与标准目录结构完全不同 / 该文档不能解析 / AI 未提取到任何字段表格）。
 * 必须与 `not_configured`（无规则，不算失败）区分开 —— 旧实现把 failed 抹成 none，
 * 页面显示「未提取」，用户以为根本没跑。</para>
 */
const EXTRACT_STATE_TEXT: Record<string, { text: string; type: 'success' | 'info' | 'warning' | 'primary' | 'danger' }> = {
  extracted: { text: '已提取', type: 'success' },
  failed: { text: '提取失败', type: 'danger' },
  pending: { text: '未提取', type: 'warning' },
  not_configured: { text: '未配置', type: 'info' },
  queued: { text: '提取中', type: 'primary' }
}

/** 提取列 tooltip：说清「为什么是这个状态」，用专家看得懂的话（不指向管理员） */
function extractTip(row: FileSlot): string {
  const msg = row.ExtractMessage?.trim()
  switch (row.ExtractState) {
    case 'failed':
      return msg || '该文档有提取规则但执行未成功（内容与规则不匹配或无法解析），可点「重试」'
    case 'not_configured':
      return '该文档不需要提取：规则库未为它配置可用规则（不算失败）'
    case 'pending':
      return msg || '尚未提取'
    default:
      return msg || ''
  }
}

function statusType(row: FileSlot) {
  const t = SLOT_STATUS_TEXT[row.Status]?.type ?? 'info'
  return t === 'primary' ? 'primary' : t
}
function formatSize(size?: number | null) {
  if (!size) return '-'
  if (size < 1024) return `${size} B`
  return size > 1024 * 1024 ? `${(size / 1024 / 1024).toFixed(1)} MB` : `${Math.round(size / 1024)} KB`
}
</script>

<template>
  <div class="folder-tree">
    <el-collapse v-model="opened" class="folder-collapse">
      <el-collapse-item v-for="node in nodes" :key="node.Code" :name="node.Code">
        <template #title>
          <el-icon class="folder-icon"><FolderOpened /></el-icon>
          <span class="folder-title">{{ node.FolderName }}</span>
          <el-tag size="small" type="info">{{ node.Live }} / {{ node.Required }}</el-tag>
          <el-tag v-if="node.Converting > 0" size="small" type="warning">转换中 {{ node.Converting }}</el-tag>
          <el-tag v-if="node.Missing > 0" size="small" type="danger">缺失 {{ node.Missing }}</el-tag>
          <el-button
            link type="primary" size="small" class="folder-upload"
            @click.stop="emit('upload', node.Code)"
          >上传到本目录</el-button>
        </template>

        <!-- 本文件夹自身的槽位 -->
        <YzhTable
          v-if="node.Files.length"
          :key="`${node.Code}/${dataRevision}`"
          :columns="columns"
          :data-loader="rowsLoader(node)"
          :toolbar="false"
          :show-pagination="false"
          :height="panelHeight(node.Files.length)"
          :row-action-buttons="rowActions"
          :action-dropdown-only="true"
          row-key="Code"
          empty-text="该文件夹暂无标准槽位"
          @row-action="onRowAction"
        >
          <template #column-FileName="{ row }">
            <div class="slot-name">
              <span>{{ row.FileName }}</span>
              <el-tag v-if="!row.IsRequired" size="small" type="info">选传</el-tag>
            </div>
            <div v-if="row.FileSize" class="slot-sub">{{ formatSize(row.FileSize) }}</div>
          </template>
          <template #column-Status="{ row }">
            <el-tooltip
              v-if="row.Status === 'markdownFailed'"
              content="预览正常，但正文转换失败（该格式不支持），提取与 NC 检查拿不到内容，建议重传为 docx"
              placement="top"
            >
              <span><el-tag size="small" :type="statusType(row)">
                {{ SLOT_STATUS_TEXT[row.Status as keyof typeof SLOT_STATUS_TEXT]?.text ?? row.Status }}
              </el-tag></span>
            </el-tooltip>
            <el-tag v-else size="small" :type="statusType(row)">
              {{ SLOT_STATUS_TEXT[row.Status as keyof typeof SLOT_STATUS_TEXT]?.text ?? row.Status }}
            </el-tag>
          </template>
          <template #column-ExtractState="{ row }">
            <el-tooltip v-if="extractTip(row)" :content="extractTip(row)" placement="top">
              <span><el-tag
                size="small"
                :type="EXTRACT_STATE_TEXT[row.ExtractState ?? '']?.type ?? 'info'"
              >{{ EXTRACT_STATE_TEXT[row.ExtractState ?? '']?.text ?? '未提取' }}</el-tag></span>
            </el-tooltip>
            <el-tag
              v-else
              size="small"
              :type="EXTRACT_STATE_TEXT[row.ExtractState ?? '']?.type ?? 'info'"
            >{{ EXTRACT_STATE_TEXT[row.ExtractState ?? '']?.text ?? '未提取' }}</el-tag>
          </template>
          <template #column-VersionNumber="{ row }">
            <span v-if="row.StoragePath">v{{ row.VersionNumber }}</span>
            <span v-else>-</span>
          </template>
          <!-- 操作列改由 YzhTable 内置列渲染（:row-action-buttons + :action-dropdown-only），
               原 #column-Actions 手写插槽已于 2026-10-07 删除，按钮集见 script 的 rowActions() -->
        </YzhTable>

        <!-- 子文件夹（递归；层级缩进由 .folder-tree--child 的内边距表达） -->
        <StandardFolderTree
          v-if="node.Children.length"
          class="folder-tree--child"
          :nodes="node.Children"
          :columns="columns"
          :data-revision="dataRevision"
          :panel-height="panelHeight"
          :busy="busy"
          @replace="(r: FileSlot) => emit('replace', r)"
          @remove="(r: FileSlot) => emit('remove', r)"
          @versions="(r: FileSlot) => emit('versions', r)"
          @extract="(r: FileSlot) => emit('extract', r)"
          @result="(r: FileSlot) => emit('result', r)"
          @preview="(r: FileSlot) => emit('preview', r)"
          @download="(r: FileSlot) => emit('download', r)"
          @upload="(c: string) => emit('upload', c)"
        />

        <YzhEmptyState
          v-if="!node.Files.length && !node.Children.length"
          title="该文件夹暂无标准槽位"
         />
      </el-collapse-item>
    </el-collapse>
  </div>
</template>

<style scoped>
.folder-collapse :deep(.el-collapse-item__header) { height: auto; min-height: 40px; }
.folder-icon { margin-right: 6px; }
.folder-title { margin-right: 8px; font-weight: 500; }
.folder-upload { margin-left: 12px; }
.folder-tree--child { padding-left: 20px; border-left: 2px solid var(--el-border-color-lighter); }
.slot-name { display: flex; align-items: center; gap: 6px; }
.slot-sub { margin-top: 2px; font-size: 12px; color: var(--el-text-color-secondary); }
</style>
