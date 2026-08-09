<template>
  <div class="directory-manager">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="left-header">
        <span class="left-title">目录结构</span>
      </div>
      <div class="search-box">
        <el-input v-model="searchText" placeholder="搜索..." size="small" clearable prefix-icon="Search" />
      </div>
      <div class="tree-container">
        <div v-for="org in treeData" :key="org.id" class="tree-group">
          <!-- 机构 -->
          <div class="tree-node level-0" @click="toggleExpand(org)">
            <el-icon class="tree-toggle" :class="{ expanded: org.expanded }"><ArrowRight /></el-icon>
            <el-icon class="tree-icon org"><OfficeBuilding /></el-icon>
            <span class="tree-label">{{ org.label }}</span>
            <el-badge :value="org.children ? org.children.length : 0" type="info" />
          </div>
          <!-- 标准 -->
          <template v-if="org.expanded && org.children">
            <template v-for="std in org.children" :key="std.id">
              <div class="tree-node level-1" @click="toggleExpand(std)">
                <el-icon class="tree-toggle" :class="{ expanded: std.expanded }"><ArrowRight /></el-icon>
                <el-icon class="tree-icon standard"><Document /></el-icon>
                <span class="tree-label">{{ std.label }}</span>
                <el-badge :value="std.children ? std.children.length : 0" type="info" />
              </div>
              <!-- 阶段 -->
              <div v-for="phase in std.children" :key="phase.id"
                class="tree-node level-2"
                :class="{ active: currentPhase && currentPhase.id === phase.id }"
                @click="selectPhase(phase)">
                <el-icon class="tree-toggle" style="visibility: hidden;"><ArrowRight /></el-icon>
                <el-icon class="tree-icon phase"><Calendar /></el-icon>
                <span class="tree-label">{{ phase.label }}</span>
              </div>
            </template>
          </template>
        </div>
      </div>
    </div>

    <!-- 右侧内容区 -->
    <div class="right-panel">
      <!-- 面包屑 -->
      <div class="breadcrumb" v-if="currentPhase">
        <el-breadcrumb separator="/">
          <el-breadcrumb-item>
            <span class="clickable-breadcrumb" @click="navigateToRoot">{{ currentPhase.standardCode }}</span>
          </el-breadcrumb-item>
          <el-breadcrumb-item>
            <span class="clickable-breadcrumb" @click="navigateToRoot">{{ currentPhase.phaseCode }}</span>
          </el-breadcrumb-item>
          <el-breadcrumb-item v-for="(crumb, index) in breadcrumbPath" :key="index">
            <span v-if="index < breadcrumbPath.length - 1" class="clickable-breadcrumb" @click="navigateToCrumb(index)">
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
          <el-icon><Upload /></el-icon> 上传文件
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" @click="handleExport">
          <el-icon><Download /></el-icon> 导出打包
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" @click="selectAll">全选</el-button>
        <el-button size="small" type="danger" plain @click="deleteSelected">
          <el-icon><Delete /></el-icon> 删除
        </el-button>
        <div style="flex: 1;"></div>
        <el-button size="small" type="warning" plain @click="handleHelp">
          <el-icon><QuestionFilled /></el-icon> 使用帮助
        </el-button>
      </div>

      <!-- 文件列表 -->
      <div class="file-list-container" v-if="currentPhase">
        <table class="file-table">
          <thead>
            <tr>
              <th width="40"><el-checkbox v-model="allSelected" @change="toggleSelectAll" /></th>
              <th>名称</th>
              <th width="100">大小</th>
              <th width="140">修改时间</th>
              <th width="120">操作</th>
            </tr>
          </thead>
          <tbody>
            <!-- 文件夹 -->
            <tr v-for="folder in currentFolders" :key="folder.FolderCode || folder.folderCode"
              :class="{ selected: selectedItems.has(folder.FolderCode || folder.folderCode) }"
              @click="toggleSelect(folder)"
              @dblclick="enterFolder(folder)">
              <td>
                <el-checkbox :model-value="selectedItems.has(folder.FolderCode || folder.folderCode)"
                  @click.stop="toggleSelect(folder)" />
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
            <tr v-for="file in currentFiles" :key="file.FileCode || file.fileCode"
              :class="{ selected: selectedItems.has(file.FileCode || file.fileCode) }"
              @click="toggleSelect(file)">
              <td>
                <el-checkbox :model-value="selectedItems.has(file.FileCode || file.fileCode)"
                  @click.stop="toggleSelect(file)" />
              </td>
              <td class="name-cell">
                <el-icon class="file-type-icon" :class="getFileIconClass(file.FileName || file.fileName)"><Document /></el-icon>
                <span class="name-text">{{ file.FileName || file.fileName }}</span>
              </td>
              <td class="size-cell">{{ formatFileSize(file.FileSize || file.fileSize) }}</td>
              <td class="date-cell">{{ formatDate(file.CreateDate || file.createDate) }}</td>
              <td class="action-cell">
                <el-button link type="primary" size="small" @click.stop="replaceFile(file)">替换</el-button>
                <el-button link type="primary" size="small" @click.stop="downloadFile(file)">下载</el-button>
                <el-button link type="danger" size="small" @click.stop="deleteItem(file)">删除</el-button>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- 空状态 -->
        <el-empty v-if="currentFolders.length === 0 && currentFiles.length === 0" description="暂无内容" />
      </div>

      <!-- 未选中阶段提示 -->
      <div v-if="!currentPhase" class="empty-state">
        <el-empty description="请在左侧选择阶段">
          <template #description>
            <div>选择 机构 > 标准 > 阶段 后，右侧将加载文件目录</div>
          </template>
        </el-empty>
      </div>

      <!-- 状态栏 -->
      <div class="status-bar" v-if="currentPhase">
        <span>共 {{ currentFolders.length + currentFiles.length }} 项 | 文件夹 {{ currentFolders.length }} 个，文件 {{ currentFiles.length }} 个</span>
        <span>总大小 {{ totalSizeFormatted }}</span>
      </div>
    </div>

    <!-- 新建文件夹弹窗 -->
    <el-dialog v-model="showFolderDialog" title="新建文件夹" width="400px">
      <el-form :model="folderForm" label-width="80px" class="dialog-form">
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
    <el-dialog v-model="showRenameDialogFlag" title="重命名" width="400px">
      <el-form :model="renameForm" label-width="80px" class="dialog-form">
        <el-form-item label="名称">
          <el-input v-model="renameForm.newName" placeholder="请输入新名称" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRenameDialogFlag = false">取消</el-button>
        <el-button type="primary" @click="confirmRename">确定</el-button>
      </template>
    </el-dialog>

    <!-- 使用帮助弹窗 -->
    <el-dialog v-model="showHelpDialog" title="使用帮助" width="600px">
      <div class="help-content">
        <h4>页面功能说明</h4>
        <p>本页面用于维护每个"机构+标准+阶段"组合下的标准文件目录结构。</p>
        <h4>右侧文件管理</h4>
        <ul>
          <li><strong>新建文件夹</strong>: 创建子文件夹，系统自动生成编码</li>
          <li><strong>上传文件</strong>: 支持文件/文件夹上传</li>
          <li><strong>双击文件夹</strong>: 进入该文件夹查看子内容</li>
          <li><strong>面包屑导航</strong>: 点击面包屑可返回上级目录</li>
        </ul>
        <h4>编码规则</h4>
        <div class="code-example">
          <div>目录编码: SDC-{标准}|{阶段} → SDC-ISO134852016|STAGE01</div>
          <div>文件夹编码: FD-{目录编码}|L{层级}|S{序号} → FD-SDC-ISO134852016|STAGE01|L02|S001</div>
          <div>文件编码: FL-{文件夹编码}|{文件名} → FL-FD-SDC-ISO134852016|STAGE01|L02|S001|营业执照.pdf</div>
        </div>
      </div>
      <template #footer>
        <el-button type="primary" @click="showHelpDialog = false">我知道了</el-button>
      </template>
    </el-dialog>

    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialogFlag" title="上传文件" width="560px" :close-on-click-modal="false">
      <div class="upload-dialog-body">
        <div class="upload-tabs">
          <el-radio-group v-model="uploadMode" size="small">
            <el-radio-button value="file">上传文件</el-radio-button>
            <el-radio-button value="folder">上传文件夹</el-radio-button>
          </el-radio-group>
        </div>
        <div class="upload-area">
          <input v-if="uploadMode === 'file'" ref="fileInputRef" type="file" multiple
            style="display: none" @change="handleFileSelect" />
          <input v-else ref="folderInputRef" type="file" webkitdirectory multiple
            style="display: none" @change="handleFolderSelect" />
          <div class="upload-trigger" @click="triggerUpload">
            <el-icon class="upload-icon"><Upload /></el-icon>
            <div class="upload-text">{{ uploadMode === 'file' ? '点击选择文件或拖拽到此处' : '点击选择文件夹' }}</div>
            <div class="upload-hint">支持多个文件同时上传</div>
          </div>
        </div>
        <div v-if="uploadFileList.length > 0" class="upload-file-list">
          <div class="file-list-header-sm">
            <span>待上传文件 ({{ uploadFileList.length }}个)</span>
            <el-button type="danger" link size="small" @click="clearUploadList" :disabled="uploading">清空</el-button>
          </div>
          <div v-for="(file, index) in uploadFileList" :key="index" class="file-list-item-sm">
            <span class="file-item-name">{{ file.webkitRelativePath || file.name }}</span>
            <span class="file-item-size">{{ formatFileSize(file.size) }}</span>
            <el-button v-if="!uploading" type="danger" link size="small" @click="removeFile(index)">
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
        </div>
        <!-- 上传进度 -->
        <div v-if="uploading || uploadProgress.status === 'done'" class="upload-progress-area">
          <div class="progress-info">
            <span v-if="uploadProgress.status === 'uploading'">
              正在上传: {{ uploadProgress.currentFile }} ({{ uploadProgress.completed }}/{{ uploadProgress.total }})
            </span>
            <span v-else-if="uploadProgress.failed > 0" class="text-danger">
              上传完成，{{ uploadProgress.failed }} 个文件失败
            </span>
            <span v-else class="text-success">
              全部 {{ uploadProgress.total }} 个文件上传成功
            </span>
          </div>
          <el-progress
            :percentage="uploadProgress.total > 0 ? Math.round((uploadProgress.completed / uploadProgress.total) * 100) : 0"
            :status="uploadProgress.failed > 0 ? 'exception' : (uploadProgress.status === 'done' && uploadProgress.failed === 0 ? 'success' : '')" />
        </div>
      </div>
      <template #footer>
        <el-button @click="cancelUpload">取消</el-button>
        <el-button type="primary" :disabled="uploadFileList.length === 0 || uploading" @click="submitUpload">
          {{ uploading ? '上传中...' : '开始上传' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  OfficeBuilding, Document, Folder, Calendar, ArrowRight,
  FolderAdd, Upload, Download, Delete, QuestionFilled, Search
} from '@element-plus/icons-vue'
import http from '@/api/http'

const searchText = ref('')
const treeData = ref([])
const currentPhase = ref(null)
const currentFolders = ref([])
const currentFiles = ref([])
const breadcrumbPath = ref([])
const currentFolderCode = ref('')
const allSelected = ref(false)
const selectedItems = reactive(new Set())

// 弹窗控制
const showFolderDialog = ref(false)
const showRenameDialogFlag = ref(false)
const showHelpDialog = ref(false)
const showUploadDialogFlag = ref(false)

const folderForm = reactive({ folderName: '', remark: '' })
const renameForm = reactive({ newName: '', item: null })
const uploadMode = ref('file')
const fileInputRef = ref(null)
const folderInputRef = ref(null)
const uploadFileList = ref([])
const uploading = ref(false)

const uploadProgress = reactive({
  total: 0, completed: 0, failed: 0, currentFile: '', status: 'idle'
})

// 计算属性
const totalSizeFormatted = computed(() => {
  const total = currentFiles.value.reduce((sum, f) => sum + parseInt(f.FileSize || f.fileSize || 0), 0)
  return formatFileSize(total)
})

// ========== 组织树 ==========
const loadTree = async () => {
  try {
    const res = await http.get('/api/standard-directory/organization-tree')
    if (res.Status === true || res.status === 0) {
      treeData.value = (res.Data || res.data || []).map(org => ({
        ...org,
        expanded: true,
        children: (org.children || []).map(std => ({
          ...std,
          expanded: false,
          children: std.children || []
        }))
      }))
    }
  } catch (error) {
    console.error('加载组织树失败:', error)
  }
}

const toggleExpand = (node) => {
  node.expanded = !node.expanded
}

const selectPhase = async (phase) => {
  currentPhase.value = phase
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  allSelected.value = false
  await loadCurrentContent()
}

// ========== 文件夹/文件加载 ==========
const loadCurrentContent = async () => {
  if (!currentPhase.value) return
  const directoryCode = buildDirectoryCode()
  try {
    if (!currentFolderCode.value) {
      const res = await http.get(`/api/standard-directory/configs/${directoryCode}/folders`)
      if (res.Status === true || res.status === 0) {
        const data = res.Data || res.data || []
        currentFolders.value = extractFoldersAtLevel(data, 2)
        currentFiles.value = []
      }
    } else {
      const [foldersRes, filesRes] = await Promise.all([
        http.get(`/api/standard-directory/configs/${directoryCode}/folders`),
        http.get(`/api/standard-directory/folders/${currentFolderCode.value}/files`)
      ])
      if (foldersRes.Status === true || foldersRes.status === 0) {
        const allFolders = foldersRes.Data || foldersRes.data || []
        currentFolders.value = extractChildFolders(allFolders, currentFolderCode.value)
      }
      if (filesRes.Status === true || filesRes.status === 0) {
        const allFiles = filesRes.Data || filesRes.data || []
        currentFiles.value = Array.isArray(allFiles) ? allFiles.filter(f => f.IsValid !== false) : []
      }
    }
    allSelected.value = false
  } catch (error) {
    console.error('加载内容失败:', error)
    currentFolders.value = []
    currentFiles.value = []
  }
}

const buildDirectoryCode = () => {
  return `SDC-${currentPhase.value.standardCode.replace(/[:\-\s]/g, '')}|${currentPhase.value.phaseCode.replace(/[:\-\s]/g, '')}`
}

const extractFoldersAtLevel = (tree, targetDepth) => {
  const result = []
  const traverse = (nodes) => {
    for (const node of (nodes || [])) {
      if (node.Depth === targetDepth || node.depth === targetDepth) {
        result.push(node)
      }
      if (node.Children && node.Children.length > 0) {
        traverse(node.Children)
      }
    }
  }
  if (Array.isArray(tree)) {
    for (const root of tree) {
      if (root.Children) traverse(root.Children)
    }
  } else if (tree && tree.Children) {
    traverse(tree.Children)
  }
  return result
}

const extractChildFolders = (tree, parentCode) => {
  const result = []
  const findAndExtract = (nodes) => {
    for (const node of (nodes || [])) {
      const code = node.FolderCode || node.folderCode
      if (code === parentCode) {
        if (node.Children) result.push(...node.Children)
        return true
      }
      if (node.Children && findAndExtract(node.Children)) return true
    }
    return false
  }
  if (Array.isArray(tree)) {
    for (const root of tree) findAndExtract(root.Children || [root])
  } else if (tree) {
    findAndExtract(tree.Children || [tree])
  }
  return result
}

// ========== 文件夹导航 ==========
const enterFolder = (folder) => {
  currentFolderCode.value = folder.FolderCode || folder.folderCode
  breadcrumbPath.value.push({
    code: currentFolderCode.value,
    name: folder.FolderName || folder.folderName
  })
  selectedItems.clear()
  loadCurrentContent()
}

const navigateToRoot = () => {
  currentFolderCode.value = ''
  breadcrumbPath.value = []
  selectedItems.clear()
  loadCurrentContent()
}

const navigateToCrumb = (index) => {
  breadcrumbPath.value = breadcrumbPath.value.slice(0, index + 1)
  currentFolderCode.value = breadcrumbPath.value[index].code
  selectedItems.clear()
  loadCurrentContent()
}

// ========== 选择操作 ==========
const toggleSelect = (item) => {
  const code = item.FolderCode || item.fileCode || item.FileCode
  if (selectedItems.has(code)) selectedItems.delete(code)
  else selectedItems.add(code)
  allSelected.value = selectedItems.size === (currentFolders.value.length + currentFiles.value.length)
}

const toggleSelectAll = (val) => {
  selectedItems.clear()
  if (val) {
    currentFolders.value.forEach(f => selectedItems.add(f.FolderCode || f.folderCode))
    currentFiles.value.forEach(f => selectedItems.add(f.FileCode || f.fileCode))
  }
}

const selectAll = () => {
  currentFolders.value.forEach(f => selectedItems.add(f.FolderCode || f.folderCode))
  currentFiles.value.forEach(f => selectedItems.add(f.FileCode || f.fileCode))
  allSelected.value = true
}

// ========== 新建文件夹 ==========
const handleNewFolder = () => {
  folderForm.folderName = ''
  folderForm.remark = ''
  showFolderDialog.value = true
}

const submitFolder = async () => {
  if (!folderForm.folderName) {
    ElMessage.warning('请输入文件夹名称')
    return
  }
  try {
    const res = await http.post(`/api/standard-directory/configs/${buildDirectoryCode()}/folders/create`, {
      folderName: folderForm.folderName,
      remark: folderForm.remark,
      depth: 1,
      parentCode: currentFolderCode.value || ''
    })
    if (res.Status === true || res.status === 0) {
      ElMessage.success('创建成功')
      showFolderDialog.value = false
      await loadCurrentContent()
    } else {
      ElMessage.error(res.Message || res.message || '创建失败')
    }
  } catch (error) {
    ElMessage.error('创建失败')
  }
}

// ========== 重命名 ==========
const showRenameDialog = (item) => {
  renameForm.item = item
  renameForm.newName = item.FolderName || item.folderName || item.FileName || item.fileName
  showRenameDialogFlag.value = true
}

const confirmRename = async () => {
  if (!renameForm.newName) {
    ElMessage.warning('请输入新名称')
    return
  }
  const item = renameForm.item
  const isFolder = !!(item.FolderCode || item.folderCode)
  const code = item.FolderCode || item.folderCode || item.FileCode || item.fileCode
  try {
    let res
    if (isFolder) {
      res = await http.put(`/api/standard-directory/folders/${code}`, { ...item, FolderName: renameForm.newName })
    } else {
      res = await http.put(`/api/standard-directory/files/${code}`, { ...item, FileName: renameForm.newName })
    }
    if (res.Status === true || res.status === 0) {
      ElMessage.success('重命名成功')
      showRenameDialogFlag.value = false
      await loadCurrentContent()
    } else {
      ElMessage.error(res.Message || '重命名失败')
    }
  } catch (error) {
    ElMessage.error('重命名失败')
  }
}

// ========== 删除 ==========
const deleteItem = async (item) => {
  const name = item.FolderName || item.folderName || item.FileName || item.fileName
  try {
    await ElMessageBox.confirm(`确定要删除 "${name}" 吗？`, '确认删除', { type: 'warning' })
  } catch { return }

  const isFolder = !!(item.FolderCode || item.folderCode)
  const code = item.FolderCode || item.folderCode || item.FileCode || item.fileCode
  try {
    let res
    if (isFolder) res = await http.delete(`/api/standard-directory/folders/${code}`)
    else res = await http.delete(`/api/standard-directory/files/${code}`)
    if (res.Status === true || res.status === 0) {
      ElMessage.success('删除成功')
      await loadCurrentContent()
    } else {
      ElMessage.error(res.Message || '删除失败')
    }
  } catch (error) {
    ElMessage.error('删除失败')
  }
}

const deleteSelected = async () => {
  if (selectedItems.size === 0) {
    ElMessage.warning('请先选择要删除的项目')
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除选中的 ${selectedItems.size} 个项目吗？`, '确认删除', { type: 'warning' })
  } catch { return }

  for (const code of [...selectedItems]) {
    const folder = currentFolders.value.find(f => (f.FolderCode || f.folderCode) === code)
    const file = currentFiles.value.find(f => (f.FileCode || f.fileCode) === code)
    if (folder) await deleteItem(folder)
    else if (file) await deleteItem(file)
  }
  selectedItems.clear()
  allSelected.value = false
}

// ========== 上传 ==========
const handleUpload = () => {
  uploadFileList.value = []
  uploadMode.value = 'file'
  uploadProgress.status = 'idle'
  uploadProgress.completed = 0
  uploadProgress.failed = 0
  showUploadDialogFlag.value = true
}

const triggerUpload = () => {
  if (uploadMode.value === 'file') fileInputRef.value?.click()
  else folderInputRef.value?.click()
}

const handleFileSelect = (event) => {
  uploadFileList.value = [...uploadFileList.value, ...Array.from(event.target.files)]
  event.target.value = ''
}

const handleFolderSelect = (event) => {
  uploadFileList.value = [...uploadFileList.value, ...Array.from(event.target.files)]
  event.target.value = ''
}

const removeFile = (index) => uploadFileList.value.splice(index, 1)
const clearUploadList = () => { uploadFileList.value = [] }
const cancelUpload = () => {
  showUploadDialogFlag.value = false
  uploadFileList.value = []
  uploading.value = false
}

const submitUpload = async () => {
  if (uploadFileList.value.length === 0 || !currentPhase.value) return

  uploading.value = true
  uploadProgress.total = uploadFileList.value.length
  uploadProgress.completed = 0
  uploadProgress.failed = 0
  uploadProgress.status = 'uploading'

  const directoryCode = buildDirectoryCode()
  let taskId = null

  try {
    const folderSet = new Set()
    const folders = []
    const files = []

    for (const file of uploadFileList.value) {
      const relativePath = file.webkitRelativePath || file.name
      const pathParts = relativePath.split('/')
      if (pathParts.length > 1) {
        for (let i = 1; i < pathParts.length; i++) {
          const folderPath = pathParts.slice(0, i).join('/')
          if (!folderSet.has(folderPath)) {
            folderSet.add(folderPath)
            folders.push({ path: folderPath })
          }
        }
      }
      files.push({
        relativePath, fileName: file.name, fileSize: file.size,
        mimeType: file.type || 'application/octet-stream'
      })
    }

    const initRes = await http.post('/api/standard-directory/upload-init', { directoryCode, folders, files })
    if (!initRes.Status && initRes.status !== 0) {
      throw new Error(initRes.Message || initRes.message || '预处理失败')
    }

    const manifest = initRes.Data || initRes.data
    taskId = manifest.TaskId || manifest.taskId
    const totalFiles = manifest.TotalFiles || manifest.totalFiles || 0
    const fileList = manifest.Files || manifest.files || []

    let failed = false
    for (let i = 0; i < fileList.length; i++) {
      if (failed) break
      const enhancedFile = fileList[i]
      const localFile = uploadFileList.value[i]
      uploadProgress.currentFile = enhancedFile.FileName || enhancedFile.fileName
      uploadProgress.completed = i

      const formData = new FormData()
      formData.append('file', localFile)
      formData.append('fileCode', enhancedFile.FileCode || enhancedFile.fileCode)
      formData.append('storagePath', enhancedFile.StoragePath || enhancedFile.storagePath)
      formData.append('taskId', taskId)

      try {
        const res = await http.post('/api/standard-directory/upload-file-v2', formData, null, {
          headers: { 'Content-Type': undefined }
        })
        if (res.Status === true || res.status === 0) uploadProgress.completed = i + 1
        else { failed = true; uploadProgress.failed++ }
      } catch {
        failed = true; uploadProgress.failed++
      }
    }

    if (failed) {
      uploadProgress.status = 'done'
      await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`)
      ElMessage.error(`上传完成，${uploadProgress.failed} 个文件失败，已回滚`)
    } else {
      uploadProgress.completed = totalFiles
      uploadProgress.status = 'done'
      await http.post(`/api/standard-directory/upload-confirm?taskId=${taskId}`)
      ElMessage.success(`全部 ${totalFiles} 个文件上传成功`)
      uploadFileList.value = []
      showUploadDialogFlag.value = false
      await loadCurrentContent()
    }
  } catch (error) {
    console.error('上传流程异常:', error)
    uploadProgress.status = 'done'
    if (taskId) {
      try { await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`) } catch {}
    }
    ElMessage.error(error.message || '上传流程异常')
  } finally {
    uploading.value = false
  }
}

// ========== 其他操作 ==========
const handleExport = () => {
  if (!currentPhase.value) return
  window.open(`/api/standard-directory/configs/${buildDirectoryCode()}/export`, '_blank')
}

const handleHelp = () => { showHelpDialog.value = true }

const replaceFile = (file) => {
  ElMessage.info('替换文件功能开发中')
}

const downloadFile = (file) => {
  const storagePath = file.StoragePath || file.storagePath
  if (storagePath) window.open(`/api/standard-directory/download?path=${encodeURIComponent(storagePath)}`, '_blank')
}

// ========== 工具函数 ==========
const formatFileSize = (bytes) => {
  if (!bytes || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const formatDate = (dateStr) => {
  if (!dateStr) return '--'
  return dateStr.substring(0, 10)
}

const getFileIconClass = (fileName) => {
  if (!fileName) return 'file-default'
  const ext = fileName.split('.').pop().toLowerCase()
  if (['pdf'].includes(ext)) return 'file-pdf'
  if (['doc', 'docx'].includes(ext)) return 'file-doc'
  if (['xls', 'xlsx'].includes(ext)) return 'file-xls'
  if (['jpg', 'jpeg', 'png', 'gif', 'bmp'].includes(ext)) return 'file-image'
  return 'file-default'
}

onMounted(() => { loadTree() })
</script>

<style scoped>
.directory-manager {
  display: flex;
  height: calc(100vh - 84px);
  background: #fff;
}

/* 左侧面板 */
.left-panel {
  width: 280px;
  background: #fff;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;
}

.left-header {
  padding: 12px 16px;
  border-bottom: 1px solid #ebeef5;
}

.left-title {
  font-weight: 500;
  color: #303133;
  font-size: 14px;
}

.search-box {
  padding: 12px 16px;
  border-bottom: 1px solid #ebeef5;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}

.tree-node {
  padding: 6px 16px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background 0.2s;
  font-size: 13px;
}

.tree-node:hover { background: #f5f7fa; }
.tree-node.active { background: #ecf5ff; color: #409eff; }
.tree-node.level-0 { padding-left: 16px; font-weight: 500; }
.tree-node.level-1 { padding-left: 36px; }
.tree-node.level-2 { padding-left: 56px; }

.tree-toggle {
  transition: transform 0.2s;
  color: #c0c4cc;
}
.tree-toggle.expanded { transform: rotate(90deg); }

.tree-icon { color: #909399; }
.tree-icon.org { color: #e6a23c; }
.tree-icon.standard { color: #409eff; }
.tree-icon.phase { color: #67c23a; }

.tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* 右侧面板 */
.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
  min-width: 0;
}

/* 面包屑 */
.breadcrumb {
  padding: 12px 20px;
  border-bottom: 1px solid #ebeef5;
}

.clickable-breadcrumb { cursor: pointer; color: #409eff; }
.clickable-breadcrumb:hover { text-decoration: underline; }

/* 工具栏 */
.toolbar {
  padding: 12px 20px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: nowrap;
}

/* 文件列表 */
.file-list-container {
  flex: 1;
  overflow-y: auto;
}

.file-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.file-table th {
  padding: 10px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #ebeef5;
  font-weight: 500;
  color: #606266;
  font-size: 13px;
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 1;
}

.file-table td {
  padding: 0 16px;
  border-bottom: 1px solid #ebeef5;
  font-size: 13px;
  color: #606266;
  vertical-align: middle;
  height: 48px;
}

.file-table tr:hover { background: #f5f7fa; }
.file-table tr.selected { background: #ecf5ff; }

.name-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  line-height: 48px;
}

.name-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.folder-name { color: #303133; font-weight: 500; }

.folder-icon { color: #e6a23c; font-size: 18px; flex-shrink: 0; }

.file-type-icon { color: #909399; font-size: 18px; flex-shrink: 0; }
.file-type-icon.file-pdf { color: #f56c6c; }
.file-type-icon.file-doc { color: #409eff; }
.file-type-icon.file-xls { color: #67c23a; }
.file-type-icon.file-image { color: #909399; }

.size-cell, .date-cell { color: #909399; }

.action-cell {
  white-space: nowrap;
}

/* 状态栏 */
.status-bar {
  padding: 10px 20px;
  border-top: 1px solid #ebeef5;
  background: #f5f7fa;
  font-size: 13px;
  color: #909399;
  display: flex;
  justify-content: space-between;
}

/* 空状态 */
.empty-state { flex: 1; display: flex; align-items: center; justify-content: center; }

/* 上传区域 */
.upload-tabs { display: flex; justify-content: center; margin-bottom: 20px; }

.upload-area {
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  padding: 32px;
  text-align: center;
  cursor: pointer;
  margin-bottom: 16px;
}
.upload-area:hover { border-color: #409eff; background: #ecf5ff; }

.upload-trigger { display: flex; flex-direction: column; align-items: center; }
.upload-icon { font-size: 48px; color: #909399; margin-bottom: 12px; }
.upload-text { font-size: 14px; color: #303133; margin-bottom: 8px; }
.upload-hint { font-size: 12px; color: #c0c4cc; }

.upload-file-list { border: 1px solid #ebeef5; border-radius: 4px; max-height: 200px; overflow-y: auto; }

.file-list-header-sm {
  display: flex; justify-content: space-between; align-items: center;
  padding: 10px 12px; background: #fafafa; border-bottom: 1px solid #ebeef5;
  font-size: 13px; color: #606266;
}

.file-list-item-sm {
  display: flex; align-items: center; padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0; font-size: 13px;
}

.file-item-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.file-item-size { color: #909399; margin: 0 12px; font-size: 12px; }

.upload-progress-area { margin-top: 16px; padding: 12px; background: #fafafa; border-radius: 4px; }
.progress-info { margin-bottom: 8px; font-size: 13px; color: #606266; }
.text-success { color: #67c23a; }
.text-danger { color: #f56c6c; }

/* 帮助内容 */
.help-content h4 { color: #303133; font-size: 14px; margin: 16px 0 8px 0; padding-bottom: 6px; border-bottom: 1px solid #ebeef5; }
.help-content h4:first-child { margin-top: 0; }
.help-content p { color: #606266; line-height: 1.6; margin-bottom: 8px; }
.help-content ul { color: #606266; line-height: 1.8; padding-left: 20px; margin-bottom: 8px; }
.help-content li { margin-bottom: 4px; }
.help-content strong { color: #303133; }

.code-example {
  background: #f5f7fa; border: 1px solid #ebeef5; border-radius: 4px;
  padding: 12px; font-family: monospace; font-size: 13px; color: #606266;
  line-height: 1.8; margin-bottom: 12px;
}

/* 弹窗表单 padding */
.dialog-form {
  padding: 10px 20px 0;
}
</style>
