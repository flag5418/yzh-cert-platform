<template>
  <div class="app-container directory-manager">
    <div class="left-panel">
      <div class="panel-header">
        <span>组织结构</span>
      </div>
      <div class="tree-container">
        <el-tree
          ref="treeRef"
          :data="treeData"
          :props="treeProps"
          :loading="treeLoading"
          node-key="id"
          highlight-current
          @node-click="handleNodeClick"
        >
          <template #default="{ node, data }">
            <span class="tree-node">
              <el-icon v-if="data.type === 'organization'"><OfficeBuilding /></el-icon>
              <el-icon v-else-if="data.type === 'standard'"><Document /></el-icon>
              <el-icon v-else><Folder /></el-icon>
              <span class="node-label">{{ data.label }}</span>
            </span>
          </template>
        </el-tree>
      </div>
    </div>

    <div class="right-panel">
      <div class="panel-header">
        <div class="breadcrumb" v-if="currentNode">
          <el-breadcrumb separator="/">
            <el-breadcrumb-item v-for="(item, index) in breadcrumbs" :key="index">
              {{ item }}
            </el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <div class="actions">
          <el-button type="primary" size="small" :disabled="!currentConfig" @click="handleUpload">
            <el-icon><Upload /></el-icon> 上传文件
          </el-button>
          <el-button size="small" :disabled="!currentConfig" @click="handleNewFolder">
            <el-icon><FolderAdd /></el-icon> 新建文件夹
          </el-button>
          <el-button size="small" :disabled="!currentConfig" @click="handleExport">
            <el-icon><Download /></el-icon> 导出打包
          </el-button>
          <el-button size="small" :disabled="!currentConfig" @click="handleHelp">
            <el-icon><QuestionFilled /></el-icon> 使用帮助
          </el-button>
        </div>
      </div>

      <div class="content-area" v-loading="contentLoading">
        <el-empty v-if="!currentConfig" description="请在左侧选择机构-标准-阶段" />
        
        <div v-else class="file-grid">
          <div 
            v-for="folder in folders" 
            :key="folder.folderCode" 
            class="file-item folder-item"
            @click="handleFolderClick(folder)"
            @contextmenu.prevent="showFolderMenu($event, folder)"
          >
            <el-icon class="file-icon folder"><Folder /></el-icon>
            <span class="file-name">{{ folder.folderName }}</span>
          </div>
          
          <div 
            v-for="file in files" 
            :key="file.fileCode" 
            class="file-item"
            @contextmenu.prevent="showFileMenu($event, file)"
          >
            <el-icon class="file-icon" :class="getFileClass(file.fileType)">
              <Document />
            </el-icon>
            <span class="file-name">{{ file.fileName }}</span>
            <span class="file-meta">{{ file.fileType }}</span>
          </div>
        </div>

        <el-empty v-if="currentConfig && folders.length === 0 && files.length === 0" description="暂无内容" />
      </div>
    </div>

    <!-- 上传对话框 -->
    <el-dialog v-model="uploadDialogVisible" title="上传文件" width="560px" :close-on-click-modal="false" class="upload-dialog">
      <div class="upload-dialog-body">
        <div class="upload-tabs">
          <el-radio-group v-model="uploadMode" size="small">
            <el-radio-button value="file">上传文件</el-radio-button>
            <el-radio-button value="folder">上传文件夹</el-radio-button>
          </el-radio-group>
        </div>

        <div class="upload-area">
          <input
            v-if="uploadMode === 'file'"
            ref="fileInputRef"
            type="file"
            multiple
            :accept="acceptTypes"
            style="display: none"
            @change="handleFileSelect"
          />
          <input
            v-else
            ref="folderInputRef"
            type="file"
            webkitdirectory
            multiple
            style="display: none"
            @change="handleFolderSelect"
          />
          <div 
            class="upload-trigger" 
            :class="{ 'is-dragover': isDragOver }"
            @click="triggerUpload"
            @dragover.prevent="isDragOver = true"
            @dragleave.prevent="isDragOver = false"
            @drop.prevent="handleDrop"
          >
            <el-icon class="upload-icon"><Upload /></el-icon>
            <div class="upload-text">{{ uploadMode === 'file' ? '点击选择文件或拖拽到此处' : '点击选择文件夹或拖拽文件夹到此处' }}</div>
            <div class="upload-hint">{{ uploadMode === 'file' ? '支持多个文件同时上传' : '将递归上传文件夹内所有文件' }}</div>
          </div>
        </div>

        <div v-if="uploadFileList.length > 0" class="upload-file-list">
          <div class="file-list-header">
            <span>待上传文件 ({{ uploadFileList.length }}个)</span>
            <el-button type="danger" link size="small" @click="clearUploadList" :disabled="uploading">清空</el-button>
          </div>
          <el-scrollbar max-height="200px">
            <div v-for="(file, index) in uploadFileList" :key="index" class="file-list-item">
              <el-icon class="file-type-icon"><Document /></el-icon>
              <span class="file-item-name" :title="file.webkitRelativePath || file.name">{{ file.webkitRelativePath || file.name }}</span>
              <span class="file-item-size">{{ formatFileSize(file.size) }}</span>
              <el-button v-if="!uploading" type="danger" link size="small" @click="removeFile(index)">
                <el-icon><Delete /></el-icon>
              </el-button>
            </div>
          </el-scrollbar>
        </div>

        <!-- 上传进度条 -->
        <div v-if="uploading || uploadProgress.status === 'done'" class="upload-progress">
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
            :status="uploadProgress.failed > 0 ? 'exception' : (uploadProgress.status === 'done' && uploadProgress.failed === 0 ? 'success' : '')"
          />
        </div>
      </div>
      <template #footer>
        <div class="dialog-footer">
          <el-button @click="uploadDialogVisible = false">取消</el-button>
          <el-button type="primary" :disabled="uploadFileList.length === 0" :loading="uploading" @click="submitUpload">
            {{ uploading ? '上传中...' : '开始上传' }}
          </el-button>
        </div>
      </template>
    </el-dialog>

    <!-- 新建文件夹对话框 -->
    <el-dialog v-model="folderDialogVisible" title="新建文件夹" width="400px">
      <el-form :model="folderForm" label-width="100px">
        <el-form-item label="文件夹名称">
          <el-input v-model="folderForm.folderName" placeholder="请输入文件夹名称" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="folderForm.remark" type="textarea" placeholder="可选备注" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="folderDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitFolder">确定</el-button>
      </template>
    </el-dialog>

    <!-- 使用帮助对话框 -->
    <el-dialog v-model="helpDialogVisible" title="使用帮助" width="600px">
      <div class="help-content">
        <h4>标准目录管理器使用说明</h4>
        <ul>
          <li><strong>左侧树形结构：</strong>显示机构-标准-阶段的层级关系</li>
          <li><strong>右侧文件区：</strong>显示选中阶段下的文件夹和文件</li>
          <li><strong>上传文件：</strong>点击"上传文件"按钮，选择本地文件上传</li>
          <li><strong>新建文件夹：</strong>点击"新建文件夹"按钮，创建新的文件夹</li>
          <li><strong>导出打包：</strong>点击"导出打包"按钮，将当前目录结构下载为ZIP文件</li>
          <li><strong>右键菜单：</strong>在文件或文件夹上右键，可进行重命名、删除等操作</li>
        </ul>
      </div>
      <template #footer>
        <el-button @click="helpDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { 
  OfficeBuilding, Document, Folder, Upload, Download, 
  FolderAdd, QuestionFilled, Delete 
} from '@element-plus/icons-vue'
import http from '@/api/http'

const treeRef = ref(null)
const treeData = ref([])
const treeLoading = ref(false)
const treeProps = {
  children: 'children',
  label: 'label'
}

const currentNode = ref(null)
const currentConfig = ref(null)
const folders = ref([])
const files = ref([])
const contentLoading = ref(false)

const breadcrumbs = computed(() => {
  if (!currentNode.value) return []
  const parts = currentNode.value.id.split('|')
  return parts
})

const uploadDialogVisible = ref(false)
const folderDialogVisible = ref(false)
const helpDialogVisible = ref(false)
const uploadMode = ref('file')
const fileInputRef = ref(null)
const folderInputRef = ref(null)
const uploadFileList = ref([])
const uploading = ref(false)
const isDragOver = ref(false)
const acceptTypes = '.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.zip,.rar,.7z,.png,.jpg,.jpeg,.gif,.bmp'

// 上传进度状态
const uploadProgress = ref({
  total: 0,
  completed: 0,
  failed: 0,
  currentFile: '',
  status: 'idle' // idle / uploading / done
})

const folderForm = ref({
  folderName: '',
  remark: ''
})

// 加载组织树
const loadTree = async () => {
  treeLoading.value = true
  try {
    const res = await http.get('/api/standard-directory/organization-tree')
    console.log('API Response:', res)
    // 兼容不同的响应格式
    if (res.Status === true || res.status === 0 || res.code === 0 || res.success) {
      treeData.value = res.Data || res.data || []
    } else if (Array.isArray(res)) {
      // 如果直接返回数组
      treeData.value = res
    }
  } catch (error) {
    console.error('加载组织树失败:', error)
    ElMessage.error('加载组织树失败')
  } finally {
    treeLoading.value = false
  }
}

// 处理节点点击
const handleNodeClick = async (data) => {
  currentNode.value = data
  
  if (data.type === 'phase') {
    await loadDirectoryConfig(data.cbCode, data.standardCode, data.phaseCode)
  } else {
    currentConfig.value = null
    folders.value = []
    files.value = []
  }
}

// 加载目录配置（不存在时自动创建）
const loadDirectoryConfig = async (cbCode, standardCode, phaseCode) => {
  contentLoading.value = true
  try {
    // 与后端GenerateDirectoryCode保持一致：清理特殊字符
    const cleanStd = standardCode.replace(/[:\-\s]/g, '')
    const cleanPhase = phaseCode.replace(/[:\-\s]/g, '')
    const directoryCode = `SDC-${cleanStd}|${cleanPhase}`
    console.log('loadDirectoryConfig:', { cbCode, standardCode, phaseCode, directoryCode })
    
    let res = await http.get(`/api/standard-directory/configs/${directoryCode}`)
    console.log('GET config result:', res)
    let config = (res.Status === true || res.status === 0) ? (res.Data || res.data) : null

    // 配置不存在，自动创建
    if (!config) {
      console.log('配置不存在，自动创建...')
      const createRes = await http.post(`/api/standard-directory/configs/create`, {
        directoryCode,
        standardCode,
        phaseCode,
        rootFolderName: `${standardCode} - ${phaseCode}`
      })
      console.log('创建结果:', createRes)
      if (createRes.Status === true || createRes.status === 0) {
        // 重新获取配置
        res = await http.get(`/api/standard-directory/configs/${directoryCode}`)
        console.log('重新GET config:', res)
        config = (res.Status === true || res.status === 0) ? (res.Data || res.data) : null
      }
    }

    if (config) {
      console.log('currentConfig 设置为:', config)
      // 统一属性名为 camelCase（API 返回 PascalCase，前端用 camelCase）
      currentConfig.value = {
        ...config,
        directoryCode: config.DirectoryCode || config.directoryCode,
        standardCode: config.StandardCode || config.standardCode,
        phaseCode: config.PhaseCode || config.phaseCode
      }
      await loadFolders(currentConfig.value.directoryCode)
    } else {
      console.error('配置加载失败')
      currentConfig.value = null
      folders.value = []
      files.value = []
    }
  } catch (error) {
    console.error('loadDirectoryConfig 异常:', error)
    currentConfig.value = null
    folders.value = []
    files.value = []
  } finally {
    contentLoading.value = false
  }
}

// 加载文件夹
const loadFolders = async (directoryCode) => {
  try {
    const res = await http.get(`/api/standard-directory/configs/${directoryCode}/folders`)
    console.log('Folders Response:', res)
    if (res.Status === true || res.status === 0) {
      folders.value = res.Data || res.data || []
    }
  } catch (error) {
    folders.value = []
  }
}

// 处理文件夹点击
const handleFolderClick = async (folder) => {
  await loadFiles(folder.folderCode)
}

// 加载文件
const loadFiles = async (folderCode) => {
  contentLoading.value = true
  try {
    const res = await http.get(`/api/standard-directory/folders/${folderCode}/files`)
    console.log('Files Response:', res)
    if (res.Status === true || res.status === 0) {
      files.value = res.Data || res.data || []
    }
  } catch (error) {
    files.value = []
  } finally {
    contentLoading.value = false
  }
}

// 上传文件
const handleUpload = () => {
  uploadFileList.value = []
  uploadMode.value = 'file'
  uploadDialogVisible.value = true
}

const triggerUpload = () => {
  if (uploadMode.value === 'file') {
    fileInputRef.value?.click()
  } else {
    folderInputRef.value?.click()
  }
}

const handleFileSelect = (event) => {
  const selectedFiles = Array.from(event.target.files)
  uploadFileList.value = [...uploadFileList.value, ...selectedFiles]
  event.target.value = ''
}

const handleFolderSelect = (event) => {
  const selectedFiles = Array.from(event.target.files)
  uploadFileList.value = [...uploadFileList.value, ...selectedFiles]
  event.target.value = ''
}

const handleDrop = (event) => {
  isDragOver.value = false
  if (uploading.value) return
  
  const droppedFiles = Array.from(event.dataTransfer.files)
  if (droppedFiles.length > 0) {
    uploadFileList.value = [...uploadFileList.value, ...droppedFiles]
  }
}

const removeFile = (index) => {
  uploadFileList.value.splice(index, 1)
}

const clearUploadList = () => {
  uploadFileList.value = []
}

const formatFileSize = (bytes) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

const submitUpload = async () => {
  if (uploadFileList.value.length === 0) return
  if (!currentConfig.value) {
    ElMessage.warning('请先在左侧选择一个阶段节点')
    return
  }
  
  uploading.value = true
  uploadProgress.value = {
    total: uploadFileList.value.length,
    completed: 0,
    failed: 0,
    currentFile: '',
    status: 'uploading'
  }
  
  const directoryCode = currentConfig.value.directoryCode
  let taskId = null
  
  try {
    // ===== Step 1: 构建基础清单 =====
    const folderSet = new Set()
    const folders = []
    const files = []
    
    for (const file of uploadFileList.value) {
      const relativePath = file.webkitRelativePath || file.name
      const pathParts = relativePath.split('/')
      
      // 提取文件夹路径（去掉文件名部分）
      if (pathParts.length > 1) {
        for (let i = 1; i < pathParts.length; i++) {
          const folderPath = pathParts.slice(0, i).join('/')
          if (!folderSet.has(folderPath)) {
            folderSet.add(folderPath)
            folders.push({ path: folderPath })
          }
        }
      }
      
      // 文件项
      files.push({
        relativePath: relativePath,
        fileName: file.name,
        fileSize: file.size,
        mimeType: file.type || 'application/octet-stream'
      })
    }
    
    const manifest = { directoryCode, folders, files }
    console.log('清单:', manifest)
    
    // ===== Step 2: 调用 upload-init =====
    const initRes = await http.post('/api/standard-directory/upload-init', manifest)
    console.log('upload-init 响应:', initRes)
    
    if (!initRes.Status && initRes.status !== 0) {
      throw new Error(initRes.Message || initRes.message || '预处理失败')
    }
    
    const enhancedManifest = initRes.Data || initRes.data
    taskId = enhancedManifest.taskId
    
    console.log('任务ID:', taskId, '文件数:', enhancedManifest.totalFiles)
    
    // ===== Step 3: 逐个上传文件 =====
    let failed = false
    
    for (let i = 0; i < enhancedManifest.files.length; i++) {
      if (failed) break
      
      const enhancedFile = enhancedManifest.files[i]
      const localFile = uploadFileList.value[i]
      
      uploadProgress.value.currentFile = enhancedFile.fileName
      uploadProgress.value.completed = i
      
      const formData = new FormData()
      formData.append('file', localFile)
      formData.append('fileCode', enhancedFile.fileCode)
      formData.append('storagePath', enhancedFile.storagePath)
      formData.append('taskId', taskId)
      
      try {
        const res = await http.post('/api/standard-directory/upload-file-v2', formData, null, {
          headers: { 'Content-Type': undefined }
        })
        if (res.Status === true || res.status === 0) {
          uploadProgress.value.completed = i + 1
        } else {
          failed = true
          uploadProgress.value.failed++
          console.warn(`上传失败 [${i+1}/${enhancedManifest.totalFiles}]:`, enhancedFile.fileName, res.Message)
        }
      } catch (error) {
        failed = true
        uploadProgress.value.failed++
        console.error(`上传异常 [${i+1}/${enhancedManifest.totalFiles}]:`, enhancedFile.fileName, error)
      }
    }
    
    // ===== Step 4: 确认或回滚 =====
    if (failed) {
      uploadProgress.value.status = 'done'
      await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`)
      ElMessage.error(`上传完成，${uploadProgress.value.failed} 个文件失败，已回滚`)
    } else {
      uploadProgress.value.completed = enhancedManifest.totalFiles
      uploadProgress.value.status = 'done'
      await http.post(`/api/standard-directory/upload-confirm?taskId=${taskId}`)
      ElMessage.success(`全部 ${enhancedManifest.totalFiles} 个文件上传成功`)
      uploadFileList.value = []
      uploadDialogVisible.value = false
      if (currentConfig.value) {
        loadFolders(currentConfig.value.directoryCode)
      }
    }
  } catch (error) {
    console.error('上传流程异常:', error)
    uploadProgress.value.status = 'done'
    // 尝试回滚
    if (taskId) {
      try { await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`) } catch {}
    }
    ElMessage.error(error.message || '上传流程异常')
  } finally {
    uploading.value = false
  }
}

// 新建文件夹
const handleNewFolder = () => {
  folderForm.value = {
    folderName: '',
    remark: ''
  }
  folderDialogVisible.value = true
}

const submitFolder = async () => {
  if (!folderForm.value.folderName) {
    ElMessage.warning('请输入文件夹名称')
    return
  }

  try {
    const res = await http.post(`/api/standard-directory/configs/${currentConfig.value.directoryCode}/folders/create`, {
      folderName: folderForm.value.folderName,
      remark: folderForm.value.remark,
      depth: 1,
      parentCode: ''
    })
    
    console.log('Create Folder Response:', res)
    if (res.Status === true || res.status === 0) {
      ElMessage.success('创建成功')
      folderDialogVisible.value = false
      await loadFolders(currentConfig.value.directoryCode)
    } else {
      ElMessage.error(res.Message || res.message || '创建失败')
    }
  } catch (error) {
    ElMessage.error('创建失败')
  }
}

// 导出打包
const handleExport = () => {
  if (!currentConfig.value) return
  
  const url = `/api/standard-directory/configs/${currentConfig.value.directoryCode}/export`
  window.open(url, '_blank')
}

// 使用帮助
const handleHelp = () => {
  helpDialogVisible.value = true
}

// 获取文件样式类
const getFileClass = (fileType) => {
  const typeMap = {
    'pdf': 'file-pdf',
    'doc': 'file-doc',
    'docx': 'file-doc',
    'xls': 'file-xls',
    'xlsx': 'file-xls',
    'jpg': 'file-image',
    'jpeg': 'file-image',
    'png': 'file-image'
  }
  return typeMap[fileType?.toLowerCase()] || 'file-default'
}

// 右键菜单
const showFolderMenu = (event, folder) => {
  // TODO: 实现右键菜单
}

const showFileMenu = (event, file) => {
  // TODO: 实现右键菜单
}

onMounted(() => {
  loadTree()
})
</script>

<style scoped>
.directory-manager {
  display: flex;
  height: calc(100vh - 84px);
  background: #fff;
}

.left-panel {
  width: 280px;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;
}

.panel-header {
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
  font-weight: 500;
  background: #fafafa;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
}

.node-label {
  font-size: 14px;
}

.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.actions {
  display: flex;
  gap: 8px;
}

.content-area {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.file-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 16px;
}

.file-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 16px 8px;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.file-item:hover {
  background: #f5f7fa;
  border-color: #409eff;
}

.folder-item {
  background: #fdf6ec;
  border-color: #e6a23c;
}

.folder-item:hover {
  background: #faecd8;
}

.file-icon {
  font-size: 48px;
  margin-bottom: 8px;
}

.file-icon.folder {
  color: #e6a23c;
}

.file-icon.file-pdf {
  color: #f56c6c;
}

.file-icon.file-doc {
  color: #409eff;
}

.file-icon.file-xls {
  color: #67c23a;
}

.file-icon.file-image {
  color: #909399;
}

.file-icon.file-default {
  color: #909399;
}

.file-name {
  font-size: 12px;
  text-align: center;
  word-break: break-all;
  line-height: 1.4;
}

.file-meta {
  font-size: 10px;
  color: #909399;
  margin-top: 4px;
}

.help-content h4 {
  margin-top: 0;
  margin-bottom: 16px;
}

.help-content ul {
  padding-left: 20px;
}

.help-content li {
  margin-bottom: 8px;
  line-height: 1.6;
}

/* 上传对话框样式 */
:deep(.upload-dialog) .el-dialog__header {
  padding: 20px 20px 16px;
  margin: 0;
  border-bottom: 1px solid #ebeef5;
}

:deep(.upload-dialog) .el-dialog__body {
  padding: 20px;
}

:deep(.upload-dialog) .el-dialog__footer {
  padding: 16px 20px 20px;
  border-top: 1px solid #ebeef5;
}

.upload-dialog-body {
  padding: 0;
}

.upload-tabs {
  display: flex;
  justify-content: center;
  margin-bottom: 20px;
}

.upload-area {
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  padding: 32px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s;
  margin-bottom: 16px;
}

.upload-area:hover {
  border-color: #409eff;
  background: #f5f7fa;
}

.upload-trigger {
  display: flex;
  flex-direction: column;
  align-items: center;
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  padding: 32px 20px;
  cursor: pointer;
  transition: all 0.3s;
}

.upload-trigger:hover {
  border-color: #409eff;
}

.upload-trigger.is-dragover {
  border-color: #409eff;
  background-color: #ecf5ff;
}

.upload-icon {
  font-size: 48px;
  color: #909399;
  margin-bottom: 12px;
}

.upload-text {
  font-size: 16px;
  color: #303133;
  margin-bottom: 8px;
}

.upload-hint {
  font-size: 13px;
  color: #909399;
}

.upload-file-list {
  border: 1px solid #ebeef5;
  border-radius: 4px;
  overflow: hidden;
}

.file-list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: #fafafa;
  border-bottom: 1px solid #ebeef5;
  font-size: 13px;
  color: #606266;
}

.file-list-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
  font-size: 13px;
}

.file-list-item:last-child {
  border-bottom: none;
}

.file-type-icon {
  color: #909399;
  margin-right: 8px;
  font-size: 16px;
}

.file-item-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #303133;
}

.file-item-size {
  color: #909399;
  margin: 0 12px;
  font-size: 12px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.upload-progress {
  margin-top: 16px;
  padding: 12px;
  background: #fafafa;
  border-radius: 4px;
  border: 1px solid #ebeef5;
}

.progress-info {
  margin-bottom: 8px;
  font-size: 13px;
  color: #606266;
}

.text-success {
  color: #67c23a;
}

.text-danger {
  color: #f56c6c;
}
</style>
