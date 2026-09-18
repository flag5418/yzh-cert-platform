<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Folder,
  Document,
  OfficeBuilding,
  Upload,
  FolderAdd,
  Refresh,
  Delete,
  Search,
  Download,
  Loading,
  CircleCheck,
  CircleClose,
  MagicStick,
  QuestionFilled,
} from '@element-plus/icons-vue'
import { yzhApi } from '@yzh-core/api/client'
import { YzhFolderUpload, CertStatusBar } from '@share/components'
import { useFileTree, type TreeNode } from '@share/composables/useFileTree'
import {
  getFoldersFlat,
  getFiles,
  getRootFiles,
  createFolder,
  updateFolder,
  deleteFolder,
  updateFile,
  deleteFile,
  downloadFile,
  uploadInit,
  uploadFile,
  uploadConfirm,
  filterIgnoredFiles,
  getActiveQueue,
  cancelConvert,
} from '@share/composables/useDirectoryApi'
import { getRuleDetail, verifyPrompt } from '@share/api/workflow/doc-extraction-rule'
import { getPromptList, type PromptTemplate } from '@share/api/workflow/prompt-template'

const router = useRouter()
const { fileTreeData, loading: treeLoading, loadTree } = useFileTree()

// ========================================================
// 状态
// ========================================================

const searchText = ref('')
const currentPhase = ref<TreeNode | null>(null)
const currentFolders = ref<any[]>([])
const currentFiles = ref<any[]>([])
const currentFolderCode = ref('')
const breadcrumbPath = ref<{ code: string; name: string }[]>([])
const detailLoading = ref(false)

// 文件夹弹窗
const showFolderDialog = ref(false)
const folderForm = reactive({ folderName: '', remark: '' })

// 重命名弹窗
const showRenameDialogVisible = ref(false)
const renameForm = reactive({ newName: '', item: null as any })

// 上传相关
const showUploadDialog = ref(false)
const uploadFiles = ref<File[]>([])
const uploading = ref(false)
const uploadProgress = ref({ status: 'idle', currentFile: '', completed: 0, total: 0 })

// 选中项
const selectedItems = reactive(new Set<string>())
const allSelected = ref(false)

// 转换队列（顶部状态条）
const activeQueue = ref<any>(null)
let queueTimer: any = null

// 使用帮助
const showHelpDialog = ref(false)

// AI 分析
const showAiDialog = ref(false)
const aiLoading = ref(false)
const aiFile = ref<any>(null)
const aiPrompt = ref('')
const aiTemplates = ref<PromptTemplate[]>([])
const aiResult = ref<{ success: boolean; message: string; fields: Record<string, unknown>; tables: Record<string, any[]> } | null>(null)

// ========================================================
// 目录树：搜索过滤
// ========================================================

/**
 * 搜索过滤（历史项目搜索框的行为）：
 * 命中名称的机构/标准/阶段保留；父节点命中时保留其整棵子树。
 * 过滤态下强制展开，便于直接看到匹配结果。
 */
const filteredTree = computed(() => {
  const q = searchText.value.trim().toLowerCase()
  if (!q) return fileTreeData.value
  const hit = (n: any) => String(n?.name || '').toLowerCase().includes(q)

  const orgs: any[] = []
  for (const org of fileTreeData.value) {
    const stds: any[] = []
    for (const std of org.children || []) {
      const phases = (std.children || []).filter(hit)
      if (hit(std)) {
        stds.push({ ...std, expanded: true })
      } else if (phases.length) {
        stds.push({ ...std, children: phases, expanded: true })
      }
    }
    if (hit(org)) {
      orgs.push({ ...org, expanded: true })
    } else if (stds.length) {
      orgs.push({ ...org, children: stds, expanded: true })
    }
  }
  return orgs
})

function toggleExpand(node: any) {
  node.expanded = !node.expanded
}

function selectPhase(phase: TreeNode) {
  currentPhase.value = phase
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  allSelected.value = false
  loadCurrentContent()
  refreshActiveQueue()
}

// ========================================================
// 文件内容加载
// ========================================================

async function loadCurrentContent() {
  if (!currentPhase.value?.directoryCode) return

  detailLoading.value = true
  try {
    const directoryCode = currentPhase.value.directoryCode

    if (!currentFolderCode.value) {
      // 根级别：加载文件夹 + 根级文件
      const [folders, rootFiles] = await Promise.all([
        getFoldersFlat(directoryCode),
        getRootFiles(directoryCode),
      ])
      currentFolders.value = folders || []
      currentFiles.value = rootFiles || []
    } else {
      // 子文件夹级别：加载子文件夹和文件
      const [folders, files] = await Promise.all([
        getFoldersFlat(directoryCode),
        getFiles(currentFolderCode.value),
      ])
      const parentCode = currentFolderCode.value
      currentFolders.value = (folders || []).filter((f: any) => f.ParentCode === parentCode)
      currentFiles.value = files || []
    }
  } catch (e: any) {
    ElMessage.error('加载内容失败：' + (e?.message || ''))
  } finally {
    detailLoading.value = false
  }
}

function enterFolder(folder: any) {
  const folderCode = folder.FolderCode || folder.folderCode
  currentFolderCode.value = folderCode
  breadcrumbPath.value.push({ code: folderCode, name: folder.FolderName || folder.folderName })
  selectedItems.clear()
  allSelected.value = false
  loadCurrentContent()
}

function navigateToRoot() {
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  allSelected.value = false
  loadCurrentContent()
}

function navigateToCrumb(index: number) {
  breadcrumbPath.value = breadcrumbPath.value.slice(0, index + 1)
  currentFolderCode.value = breadcrumbPath.value[index]?.code || ''
  selectedItems.clear()
  allSelected.value = false
  loadCurrentContent()
}

// ========================================================
// 转换队列状态（历史项目：面包屑下状态条 + 队列监控入口）
// ========================================================

async function refreshActiveQueue() {
  const directoryCode = currentPhase.value?.directoryCode
  if (!directoryCode) {
    activeQueue.value = null
    return
  }
  try {
    const res: any = await getActiveQueue(directoryCode)
    const queue = res?.data ?? res
    activeQueue.value = queue && (queue.QueueCode || queue.queueCode) ? queue : null
  } catch {
    activeQueue.value = null
  }
}

function startQueuePolling() {
  stopQueuePolling()
  queueTimer = setInterval(refreshActiveQueue, 5000)
}

function stopQueuePolling() {
  if (queueTimer) clearInterval(queueTimer)
  queueTimer = null
}

function goToQueueMonitor() {
  router.push('/business/queue-monitor')
}

async function cancelActiveQueue() {
  const queueCode = activeQueue.value?.QueueCode || activeQueue.value?.queueCode
  if (!queueCode) return
  try {
    await ElMessageBox.confirm('确定取消当前转换队列吗？未完成的文件将不再转换。', '取消队列', { type: 'warning' })
  } catch {
    return
  }
  try {
    // 该端点 HTTP 恒 200，成败在 body.code（「队列不存在」等要如实提示）
    const res = await cancelConvert(queueCode)
    if (res?.code === 200) {
      ElMessage.success('已取消队列')
      refreshActiveQueue()
      loadCurrentContent()
    } else {
      ElMessage.error('取消失败：' + (res?.msg || res?.message || '未知错误'))
    }
  } catch (e: any) {
    ElMessage.error('取消失败：' + (e?.message || ''))
  }
}

// ========================================================
// 工具栏操作
// ========================================================

function handleNewFolder() {
  folderForm.folderName = ''
  folderForm.remark = ''
  showFolderDialog.value = true
}

async function submitFolder() {
  if (!folderForm.folderName.trim()) {
    ElMessage.warning('请输入文件夹名称')
    return
  }

  try {
    const res = await createFolder(currentPhase.value!.directoryCode!, {
      DirectoryCode: currentPhase.value!.directoryCode,
      FolderName: folderForm.folderName,
      ParentCode: currentFolderCode.value || undefined,
      Remark: folderForm.remark,
    })
    if (res?.code !== 200) throw new Error(res?.msg || res?.message || '创建失败')
    ElMessage.success('创建成功')
    showFolderDialog.value = false
    loadCurrentContent()
  } catch (e: any) {
    ElMessage.error(e?.message || '创建失败')
  }
}

function handleUpload() {
  uploadFiles.value = []
  uploadProgress.value = { status: 'idle', currentFile: '', completed: 0, total: 0 }
  showUploadDialog.value = true
}

async function handleRefresh() {
  await loadTree()
  if (currentPhase.value) {
    currentFolderCode.value = ''
    breadcrumbPath.value = []
    selectedItems.clear()
    allSelected.value = false
    await loadCurrentContent()
    await refreshActiveQueue()
  }
}

function selectAll() {
  if (allSelected.value) {
    selectedItems.clear()
  } else {
    currentFolders.value.forEach((f: any) => selectedItems.add(f.FolderCode || f.folderCode))
    currentFiles.value.forEach((f: any) => selectedItems.add(f.FileCode || f.fileCode))
  }
  allSelected.value = !allSelected.value
}

function toggleSelect(item: any) {
  const code = item.FolderCode || item.folderCode || item.FileCode || item.fileCode
  if (selectedItems.has(code)) {
    selectedItems.delete(code)
  } else {
    selectedItems.add(code)
  }
}

/** 批量删除：统一确认一次，逐项删除不再重复弹窗 */
async function deleteSelected() {
  if (selectedItems.size === 0) {
    ElMessage.warning('请先选择要删除的项目')
    return
  }

  try {
    await ElMessageBox.confirm(`确定要删除选中的 ${selectedItems.size} 个项目吗？`, '确认删除', { type: 'warning' })
  } catch {
    return
  }

  let failed = 0
  for (const code of [...selectedItems]) {
    const folder = currentFolders.value.find((f: any) => (f.FolderCode || f.folderCode) === code)
    const file = currentFiles.value.find((f: any) => (f.FileCode || f.fileCode) === code)
    try {
      const res = folder ? await deleteFolder(code) : file ? await deleteFile(code) : null
      if (res && res.code !== 200) failed++
    } catch {
      failed++
    }
  }
  if (failed) ElMessage.warning(`删除完成，${failed} 项失败`)
  else ElMessage.success('删除成功')

  selectedItems.clear()
  allSelected.value = false
  loadCurrentContent()
}

/** 导出打包：选中项（文件夹+文件）打 zip 下载 */
async function handleExport() {
  if (!currentPhase.value?.directoryCode) return
  if (selectedItems.size === 0) {
    ElMessage.warning('请先勾选需要导出的文件夹或文件')
    return
  }

  const folderCodes: string[] = []
  const fileCodes: string[] = []
  for (const code of selectedItems) {
    if (currentFolders.value.some((f: any) => (f.FolderCode || f.folderCode) === code)) folderCodes.push(code)
    else if (currentFiles.value.some((f: any) => (f.FileCode || f.fileCode) === code)) fileCodes.push(code)
  }

  const dirCode = currentPhase.value.directoryCode
  try {
    await yzhApi.download(
      `/api/Workflow/StandardDirectory/configs/${encodeURIComponent(dirCode)}/export`,
      { folderCodes, fileCodes },
      `${dirCode}-export.zip`
    )
    ElMessage.success('导出成功')
  } catch (e: any) {
    ElMessage.error('导出失败：' + (e?.message || ''))
  }
}

// ========================================================
// 文件操作
// ========================================================

function showRenameDialog(item: any) {
  renameForm.item = item
  renameForm.newName = item.FolderName || item.folderName || item.FileName || item.fileName || ''
  showRenameDialogVisible.value = true
}

/** 文件夹判定：有 FolderName 且无 FileCode（文件同样带 FolderCode，不能用 FolderCode 判定） */
function isFolderItem(item: any) {
  return !!(item?.FolderName || item?.folderName) && !(item?.FileCode || item?.fileCode)
}

async function confirmRename(force = false) {
  if (!renameForm.newName.trim()) {
    ElMessage.warning('请输入新名称')
    return
  }

  const item = renameForm.item
  const newName = renameForm.newName
  const folder = isFolderItem(item)
  const code = folder ? item.FolderCode || item.folderCode : item.FileCode || item.fileCode

  try {
    let res
    if (folder) {
      res = await updateFolder(code, {
        FolderName: newName,
        Remark: item.Remark || item.remark,
        ...(force ? { Force: true } : {}),
      } as any)
    } else {
      // ⚠️ 后端 UpdateFileAsync 会同时写入 Description，必须回传原值，否则重命名会清空备注
      res = await updateFile(code, {
        FileName: newName,
        Description: item.Description ?? item.description ?? null,
        ...(force ? { Force: true } : {}),
      } as any)
    }
    if (res?.code !== 200) throw new Error(res?.msg || res?.message || '重命名失败')
    ElMessage.success('重命名成功')
    showRenameDialogVisible.value = false
    loadCurrentContent()
  } catch (e: any) {
    const msg = e?.message || ''
    // 后端重名保护：返回 force=true 提示时需要用户二次确认后强制改名
    if (msg.includes('force=true')) {
      try {
        await ElMessageBox.confirm(msg.replace(/force=true/gi, '').trim() || '名称已存在，是否强制重命名？', '确认重命名', { type: 'warning' })
        await confirmRename(true)
      } catch {
        /* 用户取消 */
      }
      return
    }
    ElMessage.error(msg || '重命名失败')
  }
}

async function deleteItem(item: any, options: { skipConfirm?: boolean } = {}) {
  const name = item.FolderName || item.folderName || item.FileName || item.fileName
  if (!options.skipConfirm) {
    try {
      await ElMessageBox.confirm(`确定删除「${name}」吗？`, '删除确认', { type: 'warning' })
    } catch {
      return
    }
  }

  try {
    const res = isFolderItem(item)
      ? await deleteFolder(item.FolderCode || item.folderCode)
      : await deleteFile(item.FileCode || item.fileCode)
    if (res?.code !== 200) throw new Error(res?.msg || res?.message || '删除失败')
    ElMessage.success('删除成功')
    loadCurrentContent()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

function replaceFile() {
  ElMessage.info('替换文件功能开发中（历史项目同为占位）')
}

async function downloadItem(item: any) {
  const storagePath = item.StoragePath || item.storagePath
  if (!storagePath) {
    ElMessage.warning('文件未上传，无法下载')
    return
  }

  try {
    const blob = await downloadFile(storagePath)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = item.FileName || item.fileName
    a.click()
    URL.revokeObjectURL(url)
  } catch (e: any) {
    ElMessage.error('下载失败：' + (e?.message || ''))
  }
}

// ========================================================
// AI 分析（历史项目：针对单个文件跑提取规则/提示词）
// ========================================================

async function handleAiAnalyze(file: any) {
  aiFile.value = file
  aiResult.value = null
  aiPrompt.value = ''
  showAiDialog.value = true

  const fileCode = file.FileCode || file.fileCode

  // 1. 该文件已保存的提取规则 → 预填 Prompt
  try {
    const res: any = await getRuleDetail(fileCode)
    if (res?.data?.prompt) aiPrompt.value = res.data.prompt
  } catch {
    /* 未配置规则：保持空 */
  }

  // 2. 提示词模板列表（可选，用于替换 Prompt）
  try {
    const list: any = await getPromptList({ page: 1, rows: 100 })
    aiTemplates.value = (list?.data || list || []) as PromptTemplate[]
  } catch {
    aiTemplates.value = []
  }
}

async function runAiAnalyze() {
  const fileCode = aiFile.value?.FileCode || aiFile.value?.fileCode
  if (!fileCode) return
  if (!aiPrompt.value.trim()) {
    ElMessage.warning('请先输入或选择提示词')
    return
  }

  aiLoading.value = true
  try {
    const res: any = await verifyPrompt({ fileCode, prompt: aiPrompt.value })
    const data = res?.data
    aiResult.value = {
      success: !!data?.success,
      message: data?.message || (data?.success ? '提取成功' : '提取失败'),
      fields: (data?.data?.fields || {}) as Record<string, unknown>,
      tables: (data?.data?.tables || {}) as Record<string, any[]>,
    }
    if (data?.success) ElMessage.success('AI 提取完成')
    else ElMessage.warning(data?.message || 'AI 提取失败')
  } catch (e: any) {
    aiResult.value = { success: false, message: e?.message || 'AI 提取异常', fields: {}, tables: {} }
    ElMessage.error('AI 提取失败：' + (e?.message || ''))
  } finally {
    aiLoading.value = false
  }
}

// ========================================================
// 上传
// ========================================================

async function onUploadSubmit() {
  if (uploadFiles.value.length === 0) {
    ElMessage.warning('请选择文件')
    return
  }

  uploading.value = true
  const filteredFiles = filterIgnoredFiles(uploadFiles.value)
  if (filteredFiles.length === 0) {
    ElMessage.warning('所有文件均被过滤（如 .DS_Store），无可上传文件')
    uploading.value = false
    return
  }
  uploadProgress.value = { status: 'uploading', currentFile: '', completed: 0, total: filteredFiles.length }

  try {
    const directoryCode = currentPhase.value?.directoryCode || ''
    if (!directoryCode) {
      ElMessage.error('请先选择左侧的阶段节点')
      uploading.value = false
      return
    }

    const { taskId, fileMap } = await uploadInit(directoryCode, filteredFiles)
    if (!taskId) {
      ElMessage.error('上传初始化失败：未获取到 TaskId')
      uploading.value = false
      return
    }

    for (let i = 0; i < filteredFiles.length; i++) {
      const file = filteredFiles[i]
      uploadProgress.value.currentFile = file.name
      uploadProgress.value.completed = i + 1

      const relPath = (file as any).webkitRelativePath || file.name
      const mapped = fileMap[relPath]
      if (!mapped?.fileCode) {
        console.warn(`[onUploadSubmit] Skip file ${file.name}: no fileCode for relPath "${relPath}"`)
        continue
      }
      await uploadFile(taskId, mapped.fileCode, file)
    }

    const { queueCode } = await uploadConfirm(taskId)
    ElMessage.success('上传成功，已进入转换队列')
    showUploadDialog.value = false
    await loadCurrentContent()
    if (queueCode) {
      await refreshActiveQueue()
      startQueuePolling()
    }
  } catch (e: any) {
    console.error('[onUploadSubmit] Error:', e)
    ElMessage.error('上传失败：' + (e?.message || ''))
  } finally {
    uploading.value = false
  }
}

// ========================================================
// 辅助方法
// ========================================================

function formatFileSize(bytes: number) {
  if (!bytes) return '--'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return `${(bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0)} ${units[i]}`
}

function formatDate(dateStr: string) {
  if (!dateStr) return '--'
  return new Date(dateStr).toLocaleString('zh-CN')
}

function getFileIconClass(fileName: string) {
  const ext = (fileName || '').split('.').pop()?.toLowerCase() || ''
  if (['pdf'].includes(ext)) return 'file-pdf'
  if (['doc', 'docx'].includes(ext)) return 'file-word'
  if (['xls', 'xlsx'].includes(ext)) return 'file-excel'
  if (['ppt', 'pptx'].includes(ext)) return 'file-ppt'
  if (['jpg', 'jpeg', 'png', 'gif', 'bmp'].includes(ext)) return 'file-image'
  return 'file-default'
}

/** 文件转换/上传状态（历史项目「上传状态」列） */
function fileStatus(file: any): string {
  const convert = String(file.ConvertStatus || file.convertStatus || '').toLowerCase()
  if (convert === 'converting') return 'converting'
  if (convert === 'failed') return 'failed'
  if (convert === 'completed') return 'completed'
  if (convert === 'pending') return 'pending'
  const upload = String(file.UploadStatus || file.uploadStatus || '').toLowerCase()
  if (upload === 'uploading') return 'uploading'
  if (upload === 'active' || upload === 'uploaded') return 'uploaded'
  return 'none'
}

const STATUS_TEXT: Record<string, string> = {
  uploading: '上传中',
  uploaded: '已上传',
  pending: '待转换',
  converting: '转换中',
  completed: '已就绪',
  failed: '转换失败',
  none: '—',
}

const totalSize = computed(() =>
  currentFiles.value.reduce((sum: number, f: any) => sum + (f.FileSize || f.fileSize || 0), 0)
)
const totalSizeFormatted = computed(() => formatFileSize(totalSize.value))

const isBusy = computed(() => uploading.value || !!activeQueue.value)

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await loadTree()
  // 默认展开机构层，便于快速定位
  fileTreeData.value.forEach((org: any) => {
    org.expanded = true
  })
  startQueuePolling()
})

onUnmounted(() => {
  stopQueuePolling()
})
</script>

<template>
  <div class="directory-manager">
    <!-- 转换队列状态条（历史项目位于面包屑下方，此处作为整页顶部通知） -->
    <div v-if="activeQueue" class="queue-banner">
      <el-icon class="is-spinning"><Loading /></el-icon>
      <span class="queue-name">{{ activeQueue.QueueName || activeQueue.queueName || activeQueue.QueueCode || activeQueue.queueCode }}</span>
      <el-progress
        :percentage="activeQueue.Progress ?? activeQueue.progress ?? 0"
        :status="activeQueue.Status === 'failed' ? 'exception' : activeQueue.Status === 'completed' ? 'success' : ''"
        :stroke-width="6"
        class="queue-progress"
      />
      <span class="queue-count">
        {{ activeQueue.CompletedCount ?? activeQueue.completedCount ?? 0 }}/{{ activeQueue.TotalCount ?? activeQueue.totalCount ?? 0 }}
      </span>
      <el-button link type="primary" size="small" @click="goToQueueMonitor">队列监控 →</el-button>
      <el-button link type="danger" size="small" @click="cancelActiveQueue">取消队列</el-button>
    </div>

    <!-- 主体：左目录树 + 右内容区 -->
    <div class="main-row">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="left-header">
        <span class="left-title">目录结构</span>
      </div>
      <div class="search-box">
        <el-input
          v-model="searchText"
          placeholder="搜索机构 / 标准 / 阶段..."
          size="small"
          clearable
          :prefix-icon="Search"
        />
      </div>
      <div class="tree-container" v-loading="treeLoading">
        <div v-for="org in filteredTree" :key="org.id" class="tree-group">
          <!-- 机构 -->
          <div class="tree-node level-0" @click="toggleExpand(org)">
            <el-icon class="tree-toggle" :class="{ expanded: (org as any).expanded }">
              <Folder />
            </el-icon>
            <el-icon class="tree-icon org"><OfficeBuilding /></el-icon>
            <span class="tree-label">{{ org.name }}</span>
            <el-badge :value="org.children?.length || 0" type="info" />
          </div>
          <!-- 标准 -->
          <template v-if="(org as any).expanded && org.children">
            <template v-for="std in org.children" :key="std.id">
              <div class="tree-node level-1" @click="toggleExpand(std)">
                <el-icon class="tree-toggle" :class="{ expanded: (std as any).expanded }">
                  <Folder />
                </el-icon>
                <el-icon class="tree-icon standard"><Document /></el-icon>
                <span class="tree-label">{{ std.name }}</span>
                <el-badge :value="std.children?.length || 0" type="info" />
              </div>
              <!-- 阶段 -->
              <div
                v-for="phase in std.children"
                :key="phase.id"
                class="tree-node level-2"
                :class="{ active: currentPhase?.id === phase.id }"
                @click="selectPhase(phase)"
              >
                <el-icon class="tree-toggle" style="visibility: hidden">
                  <Folder />
                </el-icon>
                <el-icon class="tree-icon phase"><Document /></el-icon>
                <span class="tree-label">{{ phase.name }}</span>
              </div>
            </template>
          </template>
        </div>
        <el-empty
          v-if="!treeLoading && filteredTree.length === 0"
          :description="searchText ? '未匹配到目录' : '暂无目录数据'"
          :image-size="80"
        />
      </div>
    </div>

    <!-- 右侧内容区 -->
    <div class="right-panel">
      <template v-if="currentPhase">
        <!-- 面包屑 -->
        <div class="breadcrumb">
          <el-breadcrumb separator="/">
            <el-breadcrumb-item>
              <span class="clickable-breadcrumb" @click="navigateToRoot">
                {{ currentPhase.standardCode || currentPhase.name }}
              </span>
            </el-breadcrumb-item>
            <el-breadcrumb-item>
              <span class="clickable-breadcrumb" @click="navigateToRoot">
                {{ currentPhase.phaseCode || currentPhase.name }}
              </span>
            </el-breadcrumb-item>
            <el-breadcrumb-item v-for="(crumb, index) in breadcrumbPath" :key="index">
              <span
                v-if="index < breadcrumbPath.length - 1"
                class="clickable-breadcrumb"
                @click="navigateToCrumb(index)"
              >
                {{ crumb.name }}
              </span>
              <span v-else>{{ crumb.name }}</span>
            </el-breadcrumb-item>
          </el-breadcrumb>
        </div>

        <!-- 工具栏 -->
        <div class="toolbar">
          <el-button type="primary" size="small" :disabled="isBusy" @click="handleNewFolder">
            <el-icon><FolderAdd /></el-icon> 新建文件夹
          </el-button>
          <el-button size="small" :disabled="isBusy" @click="handleUpload">
            <el-icon><Upload /></el-icon> 上传
          </el-button>
          <el-divider direction="vertical" />
          <el-button size="small" @click="handleRefresh">
            <el-icon><Refresh /></el-icon> 刷新
          </el-button>
          <el-divider direction="vertical" />
          <el-button size="small" :disabled="selectedItems.size === 0 || isBusy" @click="handleExport">
            <el-icon><Download /></el-icon> 导出打包
          </el-button>
          <el-divider direction="vertical" />
          <el-button size="small" @click="selectAll">全选</el-button>
          <el-button size="small" type="danger" plain :disabled="selectedItems.size === 0 || isBusy" @click="deleteSelected">
            <el-icon><Delete /></el-icon> 删除
          </el-button>
          <div style="flex: 1"></div>
          <el-button size="small" type="warning" plain @click="showHelpDialog = true">
            <el-icon><QuestionFilled /></el-icon> 使用帮助
          </el-button>
        </div>

        <!-- 文件列表 -->
        <div class="file-list-container" v-loading="detailLoading">
          <table class="file-table">
            <thead>
              <tr>
                <th style="width: 40px">
                  <el-checkbox v-model="allSelected" @change="selectAll" />
                </th>
                <th>名称</th>
                <th style="width: 100px">大小</th>
                <th style="width: 120px">状态</th>
                <th style="width: 180px">修改时间</th>
                <th style="width: 300px">操作</th>
              </tr>
            </thead>
            <tbody>
              <!-- 文件夹 -->
              <tr
                v-for="folder in currentFolders"
                :key="folder.FolderCode || folder.folderCode"
                :class="{ selected: selectedItems.has(folder.FolderCode || folder.folderCode) }"
                @click="toggleSelect(folder)"
                @dblclick="enterFolder(folder)"
              >
                <td>
                  <el-checkbox
                    :model-value="selectedItems.has(folder.FolderCode || folder.folderCode)"
                    @click.stop="toggleSelect(folder)"
                  />
                </td>
                <td class="name-cell">
                  <el-icon class="folder-icon"><Folder /></el-icon>
                  <span class="name-text folder-name" @dblclick.stop="enterFolder(folder)">
                    {{ folder.FolderName || folder.folderName }}
                  </span>
                </td>
                <td class="size-cell">--</td>
                <td class="status-cell">—</td>
                <td class="date-cell">{{ formatDate(folder.CreateDate || folder.createDate) }}</td>
                <td class="action-cell">
                  <el-button link type="primary" size="small" :disabled="isBusy" @click.stop="showRenameDialog(folder)">重命名</el-button>
                  <el-button link type="danger" size="small" :disabled="isBusy" @click.stop="deleteItem(folder)">删除</el-button>
                </td>
              </tr>
              <!-- 文件 -->
              <tr
                v-for="file in currentFiles"
                :key="file.FileCode || file.fileCode"
                :class="{ selected: selectedItems.has(file.FileCode || file.fileCode) }"
                @click="toggleSelect(file)"
              >
                <td>
                  <el-checkbox
                    :model-value="selectedItems.has(file.FileCode || file.fileCode)"
                    @click.stop="toggleSelect(file)"
                  />
                </td>
                <td class="name-cell">
                  <el-icon class="file-type-icon" :class="getFileIconClass(file.FileName || file.fileName)">
                    <Document />
                  </el-icon>
                  <span class="name-text">{{ file.FileName || file.fileName }}</span>
                </td>
                <td class="size-cell">{{ formatFileSize(file.FileSize || file.fileSize) }}</td>
                <td class="status-cell">
                  <el-icon v-if="fileStatus(file) === 'converting' || fileStatus(file) === 'uploading'" class="is-spinning" color="#409eff">
                    <Loading />
                  </el-icon>
                  <el-icon v-else-if="fileStatus(file) === 'completed' || fileStatus(file) === 'uploaded'" color="#67c23a">
                    <CircleCheck />
                  </el-icon>
                  <el-icon v-else-if="fileStatus(file) === 'failed'" color="#f56c6c">
                    <CircleClose />
                  </el-icon>
                  <span :class="['status-text', 'is-' + fileStatus(file)]">{{ STATUS_TEXT[fileStatus(file)] }}</span>
                </td>
                <td class="date-cell">{{ formatDate(file.CreateDate || file.createDate) }}</td>
                <td class="action-cell">
                  <el-button link type="primary" size="small" :disabled="isBusy" @click.stop="showRenameDialog(file)">重命名</el-button>
                  <el-button link type="primary" size="small" :disabled="isBusy" @click.stop="replaceFile()">替换</el-button>
                  <el-button link type="primary" size="small" @click.stop="downloadItem(file)">下载</el-button>
                  <el-button link type="success" size="small" @click.stop="handleAiAnalyze(file)">
                    <el-icon><MagicStick /></el-icon> AI 分析
                  </el-button>
                  <el-button link type="danger" size="small" :disabled="isBusy" @click.stop="deleteItem(file)">删除</el-button>
                </td>
              </tr>
            </tbody>
          </table>

          <!-- 空状态 -->
          <el-empty
            v-if="currentFolders.length === 0 && currentFiles.length === 0"
            description="暂无内容"
            :image-size="80"
          />
        </div>

        <!-- 底部状态栏 -->
        <CertStatusBar class="directory-status-bar">
          <span>
            共 {{ currentFolders.length + currentFiles.length }} 项 | 文件夹 {{ currentFolders.length }} 个，文件
            {{ currentFiles.length }} 个
          </span>
          <span>总大小 {{ totalSizeFormatted }}</span>
        </CertStatusBar>
      </template>

      <!-- 未选中阶段：提示选择 -->
      <div v-else class="empty-state">
        <el-empty description="请从左侧目录树选择一个阶段" :image-size="120" />
      </div>
    </div>
    </div>

    <!-- 新建文件夹弹窗 -->
    <el-dialog v-model="showFolderDialog" title="新建文件夹" width="420px">
      <el-form :model="folderForm" label-width="90px">
        <el-form-item label="文件夹名称">
          <el-input v-model="folderForm.folderName" placeholder="请输入文件夹名称" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="folderForm.remark" placeholder="可选备注" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showFolderDialog = false">取消</el-button>
        <el-button type="primary" @click="submitFolder">确定</el-button>
      </template>
    </el-dialog>

    <!-- 重命名弹窗 -->
    <el-dialog v-model="showRenameDialogVisible" title="重命名" width="400px">
      <el-form :model="renameForm" label-width="80px">
        <el-form-item label="名称">
          <el-input v-model="renameForm.newName" placeholder="请输入新名称" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRenameDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmRename(false)">确定</el-button>
      </template>
    </el-dialog>

    <!-- 使用帮助 -->
    <el-dialog v-model="showHelpDialog" title="使用帮助" width="600px">
      <div class="help-content">
        <h4>页面功能说明</h4>
        <p>本页面用于维护每个「机构 + 标准 + 阶段」组合下的标准文件目录结构。</p>
        <h4>右侧文件管理</h4>
        <ul>
          <li><strong>新建文件夹</strong>：创建子文件夹，系统自动生成编码</li>
          <li><strong>上传</strong>：支持文件 / 文件夹上传，上传后自动进入转换队列</li>
          <li><strong>双击文件夹</strong>：进入该文件夹查看子内容</li>
          <li><strong>面包屑导航</strong>：点击面包屑可返回上级目录</li>
          <li><strong>导出打包</strong>：勾选文件夹 / 文件后打包为 ZIP 下载</li>
        </ul>
        <h4>编码规则</h4>
        <div class="code-example">
          <div>目录编码：SDC-{标准}|{阶段} → SDC-ISO134852016|STAGE01</div>
          <div>文件夹编码：FD-{目录编码}|L{层级}|S{序号} → FD-SDC-ISO134852016|STAGE01|L02|S001</div>
          <div>文件编码：FL-{文件夹编码}|{文件名} → FL-FD-...|S001|营业执照.pdf</div>
        </div>
      </div>
      <template #footer>
        <el-button type="primary" @click="showHelpDialog = false">我知道了</el-button>
      </template>
    </el-dialog>

    <!-- AI 分析 -->
    <el-dialog v-model="showAiDialog" title="AI 提取分析" width="720px" top="6vh">
      <div class="ai-dialog">
        <div class="ai-file">
          <el-icon><Document /></el-icon>
          <span>{{ aiFile?.FileName || aiFile?.fileName }}</span>
        </div>
        <el-select
          v-if="aiTemplates.length"
          v-model="aiPrompt"
          filterable
          placeholder="选择提示词模板（或直接编辑下方提示词）"
          style="width: 100%; margin-bottom: 8px"
          @change="(v: any) => (aiPrompt = v)"
        >
          <el-option
            v-for="tpl in aiTemplates"
            :key="tpl.promptCode"
            :label="tpl.promptName"
            :value="tpl.template"
          />
        </el-select>
        <el-input
          v-model="aiPrompt"
          type="textarea"
          :rows="10"
          placeholder="输入提取提示词（默认载入该文件已保存的规则 Prompt）"
        />
        <div class="ai-actions">
          <el-button type="primary" size="small" :loading="aiLoading" @click="runAiAnalyze">
            <el-icon><MagicStick /></el-icon> 开始分析
          </el-button>
          <span class="ai-hint">分析将使用当前提示词对该文件执行一次提取</span>
        </div>

        <div v-if="aiResult" class="ai-result">
          <el-alert
            :type="aiResult.success ? 'success' : 'warning'"
            :title="aiResult.message"
            :closable="false"
            show-icon
          />
          <template v-if="aiResult.success">
            <h5>提取字段</h5>
            <el-descriptions v-if="Object.keys(aiResult.fields).length" :column="1" border size="small">
              <el-descriptions-item v-for="(val, key) in aiResult.fields" :key="String(key)" :label="String(key)">
                {{ val }}
              </el-descriptions-item>
            </el-descriptions>
            <el-empty v-else description="未提取到字段" :image-size="60" />
            <template v-for="(rows, tableKey) in aiResult.tables" :key="String(tableKey)">
              <h5>提取表格：{{ tableKey }}</h5>
              <el-table :data="rows" size="small" border max-height="240">
                <el-table-column
                  v-for="col in Object.keys((rows && rows[0]) || {})"
                  :key="col"
                  :prop="col"
                  :label="col"
                  min-width="100"
                  show-overflow-tooltip
                />
              </el-table>
            </template>
          </template>
        </div>
      </div>
    </el-dialog>

    <!-- 上传对话框 -->
    <el-dialog
      v-model="showUploadDialog"
      title="上传文件"
      width="680px"
      :close-on-click-modal="false"
      top="5vh"
    >
      <div class="upload-dialog-body">
        <div class="current-location-hint">
          <el-icon><Folder /></el-icon>
          <span>
            当前位置：<strong>{{ currentFolderCode ? breadcrumbPath.map((b) => b.name).join(' / ') : '根目录' }}</strong>
          </span>
          <span class="location-tip">（文件将上传到此位置）</span>
        </div>

        <YzhFolderUpload
          v-model="uploadFiles"
          :uploading="uploading"
          :max-files="500"
          :max-size="50 * 1024 * 1024"
        />

        <div v-if="uploading" class="upload-progress-area">
          <div class="progress-info">
            <span>正在上传: {{ uploadProgress.currentFile }} ({{ uploadProgress.completed }}/{{ uploadProgress.total }})</span>
          </div>
          <el-progress
            :percentage="uploadProgress.total ? Math.round((uploadProgress.completed / uploadProgress.total) * 100) : 0"
            :stroke-width="8"
          />
        </div>
      </div>

      <template #footer>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <el-button type="primary" :loading="uploading" :disabled="uploadFiles.length === 0" @click="onUploadSubmit">
          {{ uploading ? '上传中...' : '开始上传' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.directory-manager {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #fff;
}

/* 转换队列状态条 */
.queue-banner {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 16px;
  background: #ecf5ff;
  border-bottom: 1px solid #d9ecff;
  font-size: 13px;
  color: #409eff;
}
.queue-name {
  font-weight: 500;
}
.queue-progress {
  flex: 1;
  min-width: 120px;
}
.queue-count {
  font-variant-numeric: tabular-nums;
}

.main-row {
  flex: 1;
  min-height: 0;
  display: flex;
}

/* 左侧面板：窄屏收窄（原固定 280px 在 1000px 级窗口会挤掉操作列） */
.left-panel {
  width: clamp(180px, 20vw, 280px);
  flex-shrink: 0;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.left-header {
  padding: 16px;
  border-bottom: 1px solid #e4e7ed;
}

.left-title {
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

.search-box {
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}

.tree-group {
  margin-bottom: 4px;
}

.tree-node {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  cursor: pointer;
  transition: background 0.2s;
  user-select: none;
}

.tree-node:hover {
  background: #f5f7fa;
}

.tree-node.active {
  background: #ecf5ff;
  color: #409eff;
}

.tree-node.level-0 {
  padding-left: 16px;
}

.tree-node.level-1 {
  padding-left: 36px;
}

.tree-node.level-2 {
  padding-left: 56px;
}

.tree-toggle {
  margin-right: 8px;
  transition: transform 0.2s;
  color: #909399;
}

.tree-toggle.expanded {
  transform: rotate(90deg);
}

.tree-icon {
  margin-right: 8px;
  font-size: 16px;
}

.tree-icon.org {
  color: #e6a23c;
}

.tree-icon.standard {
  color: #409eff;
}

.tree-icon.phase {
  color: #67c23a;
}

.tree-label {
  flex: 1;
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 右侧面板 */
.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
}

.breadcrumb {
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
  background: #fafafa;
}

.clickable-breadcrumb {
  cursor: pointer;
  color: #409eff;
}

.clickable-breadcrumb:hover {
  text-decoration: underline;
}

.toolbar {
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.file-list-container {
  flex: 1;
  /* 横向 + 纵向滚动：列宽合计超出面板时靠横向滚动，不压扁列 */
  overflow: auto;
}

.file-table {
  width: 100%;
  /* 最小宽度 = 各列合理宽度之和（勾选40+名称自适应+大小100+状态120+修改时间180+操作300）
     低于此宽度时表体横向滚动，避免名称列被压成竖排文字 */
  min-width: 760px;
  border-collapse: collapse;
}

.file-table th,
.file-table td {
  padding: 10px 16px;
  text-align: left;
  border-bottom: 1px solid #ebeef5;
}

.file-table th {
  background: #fafafa;
  font-weight: 500;
  color: #606266;
  font-size: 13px;
  position: sticky;
  top: 0;
  z-index: 1;
}

.file-table tbody tr:hover {
  background: #f5f7fa;
}

.file-table tbody tr.selected {
  background: #ecf5ff;
}

.name-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.folder-icon {
  color: #e6a23c;
}

.file-type-icon {
  color: #909399;
}

.file-type-icon.file-pdf {
  color: #f56c6c;
}
.file-type-icon.file-word {
  color: #409eff;
}
.file-type-icon.file-excel {
  color: #67c23a;
}
.file-type-icon.file-ppt {
  color: #e6a23c;
}
.file-type-icon.file-image {
  color: #909399;
}

.name-text {
  font-size: 13px;
  color: #303133;
  /* 名称单行省略，不随列宽换行（旧实现为 el-table show-overflow-tooltip） */
  display: inline-block;
  max-width: 340px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}
.folder-name {
  cursor: pointer;
  color: #409eff;
}

.size-cell,
.date-cell,
.status-cell {
  font-size: 13px;
  color: #909399;
}

.status-cell {
  display: flex;
  align-items: center;
  gap: 4px;
}
.status-text.is-failed {
  color: #f56c6c;
}
.status-text.is-completed {
  color: #67c23a;
}
.status-text.is-converting,
.status-text.is-uploading {
  color: #409eff;
}

.action-cell {
  white-space: nowrap;
  /* 操作列吸附右侧：窄屏横向滚动时「重命名/替换/下载/AI 分析/删除」始终可见
     （对齐历史项目 el-table fixed="right"，修复「看不到 AI 分析按钮」） */
  position: sticky;
  right: 0;
  background: #fff;
  /* 用阴影画分隔线：sticky 列与 border-collapse 共用时 border 会丢失 */
  box-shadow: -1px 0 0 0 #ebeef5;
}

.file-table tbody tr:hover .action-cell {
  background: #f5f7fa;
}

.file-table tbody tr.selected .action-cell {
  background: #ecf5ff;
}

/* 表头的操作列同样吸附，并保持高于表体内容与表头其他单元格 */
.file-table th:last-child {
  position: sticky;
  right: 0;
  background: #fafafa;
  box-shadow: -1px 0 0 0 #ebeef5;
  z-index: 3;
}

.empty-state {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

/* 上传弹窗 */
.upload-dialog-body {
  max-height: 62vh;
  overflow-y: auto;
}
.current-location-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  margin-bottom: 12px;
  background: #f5f7fa;
  border-radius: 4px;
  font-size: 13px;
  color: #606266;
}
.location-tip {
  color: #909399;
  font-size: 12px;
}
.upload-progress-area {
  margin-top: 12px;
}
.progress-info {
  font-size: 13px;
  color: #606266;
  margin-bottom: 6px;
}

/* 帮助 */
.help-content h4 {
  margin: 12px 0 6px;
  font-size: 14px;
}
.help-content p,
.help-content li {
  font-size: 13px;
  color: #606266;
  line-height: 1.8;
}
.code-example {
  background: #f5f7fa;
  padding: 10px 12px;
  border-radius: 4px;
  font-size: 12px;
  color: #606266;
  line-height: 1.9;
}

/* 底部状态栏：左右分栏 */
.directory-status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
  gap: 12px;
}

/* AI 分析弹窗 */
.ai-file {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 12px;
  font-size: 13px;
  color: #303133;
}
.ai-actions {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 12px;
}
.ai-hint {
  font-size: 12px;
  color: #909399;
}
.ai-result {
  margin-top: 16px;
}
.ai-result h5 {
  margin: 12px 0 6px;
  font-size: 13px;
  font-weight: 500;
}
</style>
