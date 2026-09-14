<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
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
} from '@element-plus/icons-vue'
import { YzhFolderUpload } from '@share/components'
import { useFileTree, type TreeNode } from '@share/composables/useFileTree'
import {
  getFolders,
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
} from '@share/composables/useDirectoryApi'

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

// ========================================================
// 树操作
// ========================================================

function toggleExpand(node: any) {
  node.expanded = !node.expanded
}

function selectPhase(phase: TreeNode) {
  currentPhase.value = phase
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  loadCurrentContent()
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
        getFolders(directoryCode),
        getRootFiles(directoryCode),
      ])
      currentFolders.value = folders || []
      currentFiles.value = rootFiles || []
    } else {
      // 子文件夹级别：加载子文件夹和文件
      const [folders, files] = await Promise.all([
        getFolders(directoryCode),
        getFiles(currentFolderCode.value),
      ])
      
      // 过滤出当前文件夹的子文件夹
      const parentCode = currentFolderCode.value
      currentFolders.value = (folders || []).filter(
        (f: any) => f.ParentCode === parentCode
      )
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
  loadCurrentContent()
}

function navigateToRoot() {
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  loadCurrentContent()
}

function navigateToCrumb(index: number) {
  breadcrumbPath.value = breadcrumbPath.value.slice(0, index + 1)
  currentFolderCode.value = breadcrumbPath.value[index]?.code || ''
  selectedItems.clear()
  loadCurrentContent()
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
    await createFolder(currentPhase.value!.directoryCode!, {
      DirectoryCode: currentPhase.value!.directoryCode,
      FolderName: folderForm.folderName,
      ParentCode: currentFolderCode.value || undefined,
      Remark: folderForm.remark,
    })
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

function handleRefresh() {
  loadCurrentContent()
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

async function deleteSelected() {
  if (selectedItems.size === 0) return
  
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedItems.size} 项吗？`, '删除确认', { type: 'warning' })
  } catch { return }
  
  // TODO: 实现批量删除
  ElMessage.success('删除成功')
  selectedItems.clear()
  loadCurrentContent()
}

// ========================================================
// 文件操作
// ========================================================

function showRenameDialog(item: any) {
  renameForm.item = item
  renameForm.newName = item.FolderName || item.folderName || item.FileName || item.fileName || ''
  showRenameDialogVisible.value = true
}

async function confirmRename() {
  if (!renameForm.newName.trim()) {
    ElMessage.warning('请输入新名称')
    return
  }
  
  const item = renameForm.item
  const isFolder = !!(item.FolderCode || item.folderCode)
  
  try {
    if (isFolder) {
      await updateFolder(item.FolderCode || item.folderCode, { FolderName: renameForm.newName })
    } else {
      // TODO: 文件重命名
    }
    ElMessage.success('重命名成功')
    showRenameDialogVisible.value = false
    loadCurrentContent()
  } catch (e: any) {
    ElMessage.error(e?.message || '重命名失败')
  }
}

async function deleteItem(item: any) {
  const isFolder = !!(item.FolderCode || item.folderCode)
  const name = item.FolderName || item.folderName || item.FileName || item.fileName
  
  try {
    await ElMessageBox.confirm(`确定删除「${name}」吗？`, '删除确认', { type: 'warning' })
  } catch { return }
  
  try {
    if (isFolder) {
      await deleteFolder(item.FolderCode || item.folderCode)
    } else {
      await deleteFile(item.FileCode || item.fileCode)
    }
    ElMessage.success('删除成功')
    loadCurrentContent()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
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
    const directoryCode = currentPhase.value!.directoryCode!
    const { taskId, fileMap } = await uploadInit(directoryCode, filteredFiles)
    
    for (let i = 0; i < filteredFiles.length; i++) {
      const file = filteredFiles[i]
      uploadProgress.value.currentFile = file.name
      uploadProgress.value.completed = i + 1
      
      const relPath = (file as any).webkitRelativePath || file.name
      const mapped = fileMap[relPath]
      if (!mapped?.fileCode) continue
      await uploadFile(taskId, mapped.fileCode, file)
    }
    
    await uploadConfirm(taskId)
    ElMessage.success('上传成功')
    showUploadDialog.value = false
    loadCurrentContent()
  } catch (e: any) {
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
  const ext = fileName.split('.').pop()?.toLowerCase() || ''
  if (['pdf'].includes(ext)) return 'file-pdf'
  if (['doc', 'docx'].includes(ext)) return 'file-word'
  if (['xls', 'xlsx'].includes(ext)) return 'file-excel'
  if (['ppt', 'pptx'].includes(ext)) return 'file-ppt'
  if (['jpg', 'jpeg', 'png', 'gif', 'bmp'].includes(ext)) return 'file-image'
  return 'file-default'
}

// ========================================================
// 初始化
// ========================================================

onMounted(() => {
  loadTree()
})
</script>

<template>
  <div class="directory-manager">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="left-header">
        <span class="left-title">目录结构</span>
      </div>
      <div class="search-box">
        <el-input
          v-model="searchText"
          placeholder="搜索..."
          size="small"
          clearable
          :prefix-icon="Search"
        />
      </div>
      <div class="tree-container" v-loading="treeLoading">
        <div v-for="org in fileTreeData" :key="org.id" class="tree-group">
          <!-- 机构 -->
          <div class="tree-node level-0" @click="toggleExpand(org)">
            <el-icon class="tree-toggle" :class="{ expanded: org.expanded }">
              <Folder />
            </el-icon>
            <el-icon class="tree-icon org"><OfficeBuilding /></el-icon>
            <span class="tree-label">{{ org.name }}</span>
            <el-badge :value="org.children?.length || 0" type="info" />
          </div>
          <!-- 标准 -->
          <template v-if="org.expanded && org.children">
            <template v-for="std in org.children" :key="std.id">
              <div class="tree-node level-1" @click="toggleExpand(std)">
                <el-icon class="tree-toggle" :class="{ expanded: std.expanded }">
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
        <el-empty v-if="!treeLoading && fileTreeData.length === 0" description="暂无目录数据" :image-size="80" />
      </div>
    </div>

    <!-- 右侧内容区 -->
    <div class="right-panel">
      <!-- 面包屑 -->
      <div class="breadcrumb" v-if="currentPhase">
        <el-breadcrumb separator="/">
          <el-breadcrumb-item>
            <span class="clickable-breadcrumb" @click="navigateToRoot">
              {{ currentPhase.directoryCode }}
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
      <div class="toolbar" v-if="currentPhase">
        <el-button type="primary" size="small" @click="handleNewFolder">
          <el-icon><FolderAdd /></el-icon> 新建文件夹
        </el-button>
        <el-button size="small" @click="handleUpload">
          <el-icon><Upload /></el-icon> 上传
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" @click="handleRefresh">
          <el-icon><Refresh /></el-icon> 刷新
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" @click="selectAll">全选</el-button>
        <el-button size="small" type="danger" plain :disabled="selectedItems.size === 0" @click="deleteSelected">
          <el-icon><Delete /></el-icon> 删除
        </el-button>
      </div>

      <!-- 文件列表 -->
      <div class="file-list-container" v-if="currentPhase" v-loading="detailLoading">
        <table class="file-table">
          <thead>
            <tr>
              <th style="width: 40px">
                <el-checkbox v-model="allSelected" @change="selectAll" />
              </th>
              <th>名称</th>
              <th>大小</th>
              <th>修改时间</th>
              <th>操作</th>
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
                <span class="name-text folder-name">{{ folder.FolderName || folder.folderName }}</span>
              </td>
              <td class="size-cell">--</td>
              <td class="date-cell">{{ formatDate(folder.CreateDate || folder.createDate) }}</td>
              <td class="action-cell">
                <el-button link type="primary" size="small" @click.stop="showRenameDialog(folder)">重命名</el-button>
                <el-button link type="danger" size="small" @click.stop="deleteItem(folder)">删除</el-button>
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
              <td class="date-cell">{{ formatDate(file.CreateDate || file.createDate) }}</td>
              <td class="action-cell">
                <el-button link type="primary" size="small" @click.stop="showRenameDialog(file)">重命名</el-button>
                <el-button link type="primary" size="small" @click.stop="downloadItem(file)">下载</el-button>
                <el-button link type="danger" size="small" @click.stop="deleteItem(file)">删除</el-button>
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

      <!-- 未选中阶段 -->
      <div v-if="!currentPhase" class="empty-state">
        <el-empty description="请在左侧选择一个阶段" :image-size="120" />
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
        <el-button type="primary" @click="confirmRename">确定</el-button>
      </template>
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
        <!-- 当前位置提示 -->
        <div class="current-location-hint">
          <el-icon><Folder /></el-icon>
          <span>当前位置：<strong>{{ currentFolderCode ? breadcrumbPath.map(b => b.name).join(' / ') || '根目录' : '根目录' }}</strong></span>
          <span class="location-tip">（文件将上传到此位置）</span>
        </div>

        <!-- 文件上传组件 -->
        <YzhFolderUpload
          v-model="uploadFiles"
          :uploading="uploading"
          :max-files="500"
          :max-size="50 * 1024 * 1024"
        />

        <!-- 上传进度 -->
        <div v-if="uploading" class="upload-progress-area">
          <div class="progress-info">
            <span>正在上传: {{ uploadProgress.currentFile }} ({{ uploadProgress.completed }}/{{ uploadProgress.total }})</span>
          </div>
          <el-progress :percentage="Math.round((uploadProgress.completed / uploadProgress.total) * 100)" :stroke-width="8" />
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
  height: 100%;
  background: #fff;
}

/* 左侧面板 */
.left-panel {
  width: 280px;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;
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
  overflow-y: auto;
}

.file-table {
  width: 100%;
  border-collapse: collapse;
}

.file-table th,
.file-table td {
  padding: 12px 16px;
  text-align: left;
  border-bottom: 1px solid #ebeef5;
}

.file-table th {
  background: #fafafa;
  font-weight: 600;
  color: #606266;
  position: sticky;
  top: 0;
  z-index: 1;
}

.file-table tr:hover {
  background: #f5f7fa;
}

.file-table tr.selected {
  background: #ecf5ff;
}

.name-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.folder-icon {
  color: #e6a23c;
  font-size: 18px;
}

.file-type-icon {
  font-size: 18px;
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
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.folder-name {
  font-weight: 500;
}

.size-cell {
  color: #909399;
  font-size: 13px;
  width: 100px;
}

.date-cell {
  color: #909399;
  font-size: 13px;
  width: 160px;
}

.action-cell {
  width: 200px;
  white-space: nowrap;
}

.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 上传弹窗 */
.upload-dialog-body {
  max-height: 65vh;
  overflow-y: auto;
}

.current-location-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 4px;
  margin-bottom: 16px;
  font-size: 14px;
  color: #606266;
}

.location-tip {
  color: #909399;
  font-size: 12px;
}

.upload-progress-area {
  margin-top: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 4px;
}

.progress-info {
  margin-bottom: 8px;
  font-size: 13px;
  color: #606266;
}
</style>
