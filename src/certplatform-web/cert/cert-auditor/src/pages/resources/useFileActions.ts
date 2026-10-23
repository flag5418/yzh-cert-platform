/**
 * `resources` 页面 · **文件操作**（删除 / 版本与时间线 / 预览 / 提取结果）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 与 `useUpload.ts` 的分界（2026-10-19 · 计划 §3.11 ③）
 *
 *   · `useUpload.ts`  = **写**（plan → init → file → confirm；含替换、未归属指派）
 *   · 本文件          = **单个已就位文件的读/改**（删除、版本恢复、预览、看提取结果）
 *
 * 两者都通过 `@share/composables/useFileOps`（机制族）落地，**不各写一套请求**；
 * 本文件只做「页面状态 + 用户话术」，机制细节留在 `fileOps`。
 *
 * ★ 三条用户视角的约定（⛔ 别改成技术词）
 *   · 「删除」就是**真删**（2026-10-20 用户裁决）：存储对象、历史版本、提取结果一并清除，
 *     **不可恢复**。是否保留企业资料属于专家的判断 —— 系统替专家留存「已撤回」的文件，
 *     只会把不该留的资料留在库里（涉诉时举证责任反落在机构身上）。
 *   · 预览失败时给的是**原因**（格式不支持 / 文件已不在），不是异常堆栈。
 *   · 提取结果是「系统认出来的东西」，只读展示，⛔ 不做强制确认（用户裁决 2026-10-07）。
 * ════════════════════════════════════════════════════════════════════════
 */
import { ref } from 'vue'
import { confirmOrFalse } from '@yzh-core'
import { ElMessage } from 'element-plus'
import {
  deleteFile, restoreFile, getVersions, getHistory, triggerExtract, getExtractionResult, previewBlob,
  slotModifyTime,
} from '@share/api'
import type { FileSlot, FileVersionRow, HistoryItem } from '@share/api'
import { isSlotLive, isSlotRemoved } from '@share/api'

/** 宿主能力（由 `ResourcesLogic` 提供；依赖收窄到 4 个成员） */
export interface FileActionsHost {
  selectedEnterprise: { value: string }
  activeTab: { value: string }
  fileOps: {
    download(ctx: { storagePath: string; fileName: string }): Promise<void>
  }
  reloadStandard(standardCode: string): Promise<void>
}

/** 文件操作状态（每次调用新建 ⇒ 随页面实例生灭） */
export function createFileActionsState() {
  // ─── 删除（真删：对象 + 版本 + 提取结果，不可恢复） ───
  const deleteDialogVisible = ref(false)
  const deletingFile = ref<FileSlot | null>(null)
  const deleteReason = ref('')

  // ─── 版本面板 ───
  const versionsVisible = ref(false)
  const versionsFile = ref<FileSlot | null>(null)
  const versionRows = ref<FileVersionRow[]>([])
  const historyRows = ref<HistoryItem[]>([])
  const restoringVersion = ref(0)
  const versionsRev = ref(0)

  /** 预览抽屉（PDF Blob → ObjectURL） */
  const previewVisible = ref(false)
  const previewUrl = ref('')
  const previewName = ref('')

  /** 提取结果抽屉 */
  const resultDrawerVisible = ref(false)
  const resultData = ref<any>(null)

  return {
    deleteDialogVisible,
    deletingFile,
    deleteReason,
    versionsVisible,
    versionsFile,
    versionRows,
    historyRows,
    restoringVersion,
    versionsRev,
    previewVisible,
    previewUrl,
    previewName,
    resultDrawerVisible,
    resultData
  }
}

/** 文件操作动作（`ResourcesLogic` 逐个转发 ⇒ 调用面仍是 `logic.xxx`） */
export function createFileActions(host: FileActionsHost, s: ReturnType<typeof createFileActionsState>) {
  function openDeleteDialog(file: FileSlot) {
    s.deletingFile.value = file
    s.deleteReason.value = ''
    s.deleteDialogVisible.value = true
  }

  function closeDeleteDialog() {
    s.deleteDialogVisible.value = false
    s.deletingFile.value = null
    s.deleteReason.value = ''
  }

  async function confirmDelete() {
    const target = s.deletingFile.value
    if (!target) return
    if (!s.deleteReason.value.trim()) { ElMessage.warning('请填写删除原因'); return }
    try {
      await deleteFile(
        target.Code, host.selectedEnterprise.value,
        s.deleteReason.value.trim(), slotModifyTime(target)
      )
      // ⛔ 措辞不要出现「可在版本面板恢复」—— 真删后没有版本可恢复
      ElMessage.success('已删除（含历史版本与提取结果，不可恢复）')
      closeDeleteDialog()
      await host.reloadStandard(host.activeTab.value)
    } catch (e: any) {
      // 后端在对象删除失败时会「不做任何变更」并回报原因 ⇒ 此处提示可重试
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  // ═══════════════════ 下载 / 预览 ═══════════════════

  async function downloadRow(row: FileSlot) {
    if (!row.StoragePath) { ElMessage.warning('该文件尚未上传'); return }
    try {
      await host.fileOps.download({ storagePath: row.StoragePath, fileName: row.FileName })
    } catch (e: any) {
      ElMessage.error(e?.message ?? '下载失败')
    }
  }

  async function downloadVersion(row: FileVersionRow) {
    try {
      await host.fileOps.download({ storagePath: row.StoragePath, fileName: row.FileName })
    } catch (e: any) {
      ElMessage.error(e?.message ?? '下载失败')
    }
  }

  /** 预览：一律取 Blob 再用 ObjectURL（Authorization 头无法随裸链接发送） */
  async function previewRow(row: FileSlot) {
    try {
      const blob = await previewBlob(row.Code, host.selectedEnterprise.value)
      if (s.previewUrl.value) URL.revokeObjectURL(s.previewUrl.value)
      s.previewUrl.value = URL.createObjectURL(blob)
      s.previewName.value = row.FileName
      s.previewVisible.value = true
    } catch (e: any) {
      ElMessage.error(e?.message ?? '预览失败（可能尚未生成预览产物）')
    }
  }

  function closePreview() {
    s.previewVisible.value = false
    if (s.previewUrl.value) { URL.revokeObjectURL(s.previewUrl.value); s.previewUrl.value = '' }
  }

  // ═══════════════════ 版本 / 时间线 / 恢复 ═══════════════════

  async function openVersions(file: FileSlot) {
    s.versionsFile.value = file
    s.versionRows.value = []
    s.historyRows.value = []
    s.restoringVersion.value = 0
    s.versionsVisible.value = true
    try {
      const [versions, history] = await Promise.all([
        getVersions(file.Code, host.selectedEnterprise.value),
        getHistory(file.Code, host.selectedEnterprise.value)
      ])
      s.versionRows.value = versions ?? []
      s.historyRows.value = history?.Timeline ?? []
      s.versionsRev.value++
    } catch (e: any) {
      ElMessage.error(e?.message ?? '版本信息加载失败')
    }
  }

  function closeVersions() {
    s.versionsVisible.value = false
    s.versionsFile.value = null
  }

  async function handleRestore(row: FileVersionRow) {
    const file = s.versionsFile.value
    if (!file || s.restoringVersion.value > 0) return
    const ok = await confirmOrFalse(
      `用归档版本 v${row.VersionNumber}（${row.FileName}）恢复？当前文件会先归档，版本号继续递增。`,
      '恢复确认', { type: 'warning', confirmButtonText: '恢复' }
    )
    if (!ok) return

    s.restoringVersion.value = row.VersionNumber
    try {
      await restoreFile(file.Code, host.selectedEnterprise.value, row.VersionNumber, '从版本面板恢复')
      ElMessage.success(`已恢复 v${row.VersionNumber}，重新进入转换队列`)
      closeVersions()
      await host.reloadStandard(host.activeTab.value)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '恢复失败')
    } finally { s.restoringVersion.value = 0 }
  }

  // ═══════════════════ 提取（真链，偏差 D10） ═══════════════════

  async function handleTriggerExtract(file: FileSlot) {
    try {
      const isRetry = file.ExtractState === 'failed'
      await triggerExtract(file.Code, host.selectedEnterprise.value)
      ElMessage.success(isRetry ? '已重新提交提取' : '提取已入队')
      await host.reloadStandard(host.activeTab.value)
    } catch (e: any) { ElMessage.error(e?.message ?? '触发提取失败') }
  }

  async function handleViewResult(file: FileSlot) {
    s.resultDrawerVisible.value = true
    s.resultData.value = null
    try { s.resultData.value = await getExtractionResult(file.Code, host.selectedEnterprise.value) }
    catch { /* 无结果抽屉会显示空态 */ }
  }

  function previewExtractedJson(json: string) {
    try { ElMessage.info(JSON.stringify(JSON.parse(json), null, 2).slice(0, 400) + '...') }
    catch { ElMessage.info(json?.slice(0, 400) + '...') }
  }

  function opTypeText(item: HistoryItem): string {
    const map: Record<string, string> = {
      upload: '上传', replace: '替换', delete: '删除', restore: '恢复',
      extract_trigger: '触发提取', extract_done: '提取完成', archive: '归档'
    }
    return map[item.OpType] ?? item.OpType
  }

  /** 匹配层级 → 中文（分发预览用） */
  function levelText(level: string): string {
    const map: Record<string, string> = {
      M0Path: 'M0 路径强匹配', M1Exact: 'M1 文件名精确', M2Contains: 'M2 主词包含', M3FolderExt: 'M3 分类+扩展名（需确认）'
    }
    return map[level] ?? level
  }

  /** 行是否展示「就位」相关操作 */
  function isLive(row: FileSlot): boolean { return isSlotLive(row) }
  /** ⚠️ 仅用于**历史遗留**行（旧「移除」语义留下的 `Status='removed'`）；新删除会把行复位为 `missing` */
  function isRemoved(row: FileSlot): boolean { return isSlotRemoved(row) }

  return {
    openDeleteDialog, closeDeleteDialog, confirmDelete,
    downloadRow, downloadVersion,
    previewRow, closePreview,
    openVersions, closeVersions, handleRestore,
    handleTriggerExtract, handleViewResult, previewExtractedJson,
    opTypeText, levelText, isLive, isRemoved
  }
}

export type FileActions = ReturnType<typeof createFileActions>
