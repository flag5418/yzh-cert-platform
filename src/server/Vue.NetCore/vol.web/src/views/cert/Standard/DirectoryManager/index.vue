<template>
  <div class="directory-manager">
    <!-- 左侧面板 -->
    <div class="left-panel">
      <div class="left-header">
        <span class="left-title">目录结构</span>
      </div>
      <div class="search-box">
        <input type="text" class="search-input" placeholder="搜索..." v-model="searchText" />
      </div>
      <div class="tree-container">
        <div v-for="org in treeData" :key="org.id" class="tree-group">
          <!-- 机构 -->
          <div class="tree-node level-0" @click="toggleExpand(org)">
            <span class="tree-toggle" :class="{ expanded: org.expanded }">▶</span>
            <span class="tree-icon org">🏢</span>
            <span class="tree-label">{{ org.label }}</span>
            <span class="tree-badge">{{ org.children ? org.children.length : 0 }}</span>
          </div>
          <!-- 标准 -->
          <template v-if="org.expanded && org.children">
            <template v-for="std in org.children" :key="std.id">
              <div class="tree-node level-1" @click="toggleExpand(std)">
                <span class="tree-toggle" :class="{ expanded: std.expanded }">▶</span>
                <span class="tree-icon standard">📋</span>
                <span class="tree-label">{{ std.label }}</span>
                <span class="tree-badge">{{ std.children ? std.children.length : 0 }}</span>
              </div>
              <!-- 阶段 -->
              <div v-for="phase in std.children" :key="phase.id"
                class="tree-node level-2"
                :class="{ active: currentPhase && currentPhase.id === phase.id }"
                @click="selectPhase(phase)">
                <span class="tree-toggle" style="visibility: hidden;">▶</span>
                <span class="tree-icon phase">📅</span>
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
        <span class="breadcrumb-item" @click="navigateToRoot">{{ currentPhase.standardCode }}</span>
        <span class="breadcrumb-separator">/</span>
        <span class="breadcrumb-item" @click="navigateToRoot">{{ currentPhase.phaseCode }}</span>
        <template v-for="(crumb, index) in breadcrumbPath" :key="index">
          <span class="breadcrumb-separator">/</span>
          <span v-if="index < breadcrumbPath.length - 1" class="breadcrumb-item" @click="navigateToCrumb(index)">
            {{ crumb.name }}
          </span>
          <span v-else class="breadcrumb-current">{{ crumb.name }}</span>
        </template>
      </div>

      <!-- 工具栏 -->
      <div class="toolbar" v-if="currentPhase">
        <button class="btn btn-primary" @click="handleNewFolder">
          <span>📁</span> 新建文件夹
        </button>
        <button class="btn" @click="handleUpload">
          <span>⬆️</span> 上传文件
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn" @click="handleExport">
          <span>📦</span> 导出打包
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn" @click="selectAll">☑️ 全选</button>
        <button class="btn btn-danger" @click="deleteSelected">🗑️ 删除</button>
        <div style="flex: 1;"></div>
        <button class="btn btn-help" @click="handleHelp">❓ 使用帮助</button>
      </div>

      <!-- 文件列表 -->
      <div class="file-list-container" v-if="currentPhase">
        <div class="file-list-header">
          <div><input type="checkbox" class="file-checkbox" @change="toggleSelectAll" /></div>
          <div>名称</div>
          <div>大小</div>
          <div>修改时间</div>
          <div>操作</div>
        </div>

        <!-- 文件夹 -->
        <div v-for="folder in currentFolders" :key="folder.FolderCode || folder.folderCode"
          class="file-list-item"
          :class="{ selected: selectedItems.has(folder.FolderCode || folder.folderCode) }"
          @click="toggleSelect(folder)"
          @dblclick="enterFolder(folder)"
          @contextmenu.prevent="showContextMenu($event, folder, 'folder')">
          <div>
            <input type="checkbox" class="file-checkbox"
              :checked="selectedItems.has(folder.FolderCode || folder.folderCode)"
              @click.stop="toggleSelect(folder)" />
          </div>
          <div class="file-name">
            <span class="file-icon folder">📁</span>
            <span class="file-name-text folder-name">{{ folder.FolderName || folder.folderName }}</span>
          </div>
          <div class="file-size">--</div>
          <div class="file-date">{{ formatDate(folder.CreateDate || folder.createDate) }}</div>
          <div class="file-actions">
            <button class="action-btn" @click.stop="showRenameDialog(folder)">重命名</button>
            <button class="action-btn danger" @click.stop="deleteItem(folder)">删除</button>
          </div>
        </div>

        <!-- 文件 -->
        <div v-for="file in currentFiles" :key="file.FileCode || file.fileCode"
          class="file-list-item"
          :class="{ selected: selectedItems.has(file.FileCode || file.fileCode) }"
          @click="toggleSelect(file)"
          @contextmenu.prevent="showContextMenu($event, file, 'file')">
          <div>
            <input type="checkbox" class="file-checkbox"
              :checked="selectedItems.has(file.FileCode || file.fileCode)"
              @click.stop="toggleSelect(file)" />
          </div>
          <div class="file-name">
            <span class="file-icon" :class="getFileIconClass(file.FileName || file.fileName)">📄</span>
            <span class="file-name-text">{{ file.FileName || file.fileName }}</span>
          </div>
          <div class="file-size">{{ formatFileSize(file.FileSize || file.fileSize) }}</div>
          <div class="file-date">{{ formatDate(file.CreateDate || file.createDate) }}</div>
          <div class="file-actions">
            <button class="action-btn" @click.stop="replaceFile(file)">替换</button>
            <button class="action-btn" @click.stop="downloadFile(file)">下载</button>
            <button class="action-btn danger" @click.stop="deleteItem(file)">删除</button>
          </div>
        </div>

        <!-- 空状态 -->
        <div v-if="currentFolders.length === 0 && currentFiles.length === 0" class="empty-state">
          <div class="empty-icon">📂</div>
          <div class="empty-text">暂无内容</div>
          <div class="empty-hint">点击上方按钮上传文件或创建文件夹</div>
        </div>
      </div>

      <!-- 未选中阶段提示 -->
      <div v-if="!currentPhase" class="empty-state">
        <div class="empty-icon">👈</div>
        <div class="empty-text">请在左侧选择阶段</div>
        <div class="empty-hint">选择机构 > 标准 > 阶段后，右侧将加载文件目录</div>
      </div>

      <!-- 状态栏 -->
      <div class="status-bar" v-if="currentPhase">
        <span>共 {{ currentFolders.length + currentFiles.length }} 项 | 文件夹 {{ currentFolders.length }} 个，文件 {{ currentFiles.length }} 个</span>
        <span>总大小 {{ totalSizeFormatted }}</span>
      </div>
    </div>

    <!-- 新建文件夹弹窗 -->
    <div class="dialog-overlay" v-if="showFolderDialog" @click.self="showFolderDialog = false">
      <div class="dialog">
        <div class="dialog-header">
          <span class="dialog-title">新建文件夹</span>
          <span class="dialog-close" @click="showFolderDialog = false">×</span>
        </div>
        <div class="dialog-body">
          <div class="form-item">
            <label class="form-label">文件夹名称</label>
            <input type="text" class="form-input" v-model="folderForm.folderName" placeholder="请输入文件夹名称" />
          </div>
          <div class="form-item">
            <label class="form-label">备注</label>
            <input type="text" class="form-input" v-model="folderForm.remark" placeholder="可选备注" />
          </div>
        </div>
        <div class="dialog-footer">
          <button class="btn" @click="showFolderDialog = false">取消</button>
          <button class="btn btn-primary" @click="submitFolder">确定</button>
        </div>
      </div>
    </div>

    <!-- 重命名弹窗 -->
    <div class="dialog-overlay" v-if="showRenameDialogFlag" @click.self="showRenameDialogFlag = false">
      <div class="dialog">
        <div class="dialog-header">
          <span class="dialog-title">重命名</span>
          <span class="dialog-close" @click="showRenameDialogFlag = false">×</span>
        </div>
        <div class="dialog-body">
          <div class="form-item">
            <label class="form-label">名称</label>
            <input type="text" class="form-input" v-model="renameForm.newName" placeholder="请输入新名称" />
          </div>
        </div>
        <div class="dialog-footer">
          <button class="btn" @click="showRenameDialogFlag = false">取消</button>
          <button class="btn btn-primary" @click="confirmRename">确定</button>
        </div>
      </div>
    </div>

    <!-- 使用帮助弹窗 -->
    <div class="dialog-overlay" v-if="showHelpDialog" @click.self="showHelpDialog = false">
      <div class="dialog" style="width: 600px;">
        <div class="dialog-header">
          <span class="dialog-title">使用帮助</span>
          <span class="dialog-close" @click="showHelpDialog = false">×</span>
        </div>
        <div class="dialog-body">
          <div class="help-content">
            <h3>📌 页面功能说明</h3>
            <p>本页面用于维护每个"机构+标准+阶段"组合下的标准文件目录结构。</p>
            <h3>📌 右侧文件管理</h3>
            <ul>
              <li><strong>新建文件夹</strong>: 创建子文件夹，系统自动生成编码</li>
              <li><strong>上传文件</strong>: 支持拖拽上传或点击按钮选择文件</li>
              <li><strong>双击文件夹</strong>: 进入该文件夹查看子内容</li>
              <li><strong>面包屑导航</strong>: 点击面包屑可返回上级目录</li>
            </ul>
            <h3>📌 编码规则</h3>
            <div class="code-example">
              <div>目录编码: SDC-{标准}|{阶段} → SDC-ISO134852016|STAGE01</div>
              <div>文件夹编码: FD-{目录编码}|L{层级}|S{序号} → FD-SDC-ISO134852016|STAGE01|L02|S001</div>
              <div>文件编码: FL-{文件夹编码}|{文件名} → FL-FD-SDC-ISO134852016|STAGE01|L02|S001|营业执照.pdf</div>
            </div>
          </div>
        </div>
        <div class="dialog-footer">
          <button class="btn btn-primary" @click="showHelpDialog = false">我知道了</button>
        </div>
      </div>
    </div>

    <!-- 上传对话框 -->
    <div class="dialog-overlay" v-if="showUploadDialogFlag" @click.self="showUploadDialogFlag = false">
      <div class="dialog" style="width: 560px;">
        <div class="dialog-header">
          <span class="dialog-title">上传文件</span>
          <span class="dialog-close" @click="cancelUpload">×</span>
        </div>
        <div class="dialog-body">
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
              <div class="upload-icon-text">⬆️</div>
              <div class="upload-text">{{ uploadMode === 'file' ? '点击选择文件或拖拽到此处' : '点击选择文件夹' }}</div>
            </div>
          </div>
          <div v-if="uploadFileList.length > 0" class="upload-file-list">
            <div class="file-list-header-sm">
              <span>待上传文件 ({{ uploadFileList.length }}个)</span>
              <button class="action-btn danger" @click="clearUploadList" :disabled="uploading">清空</button>
            </div>
            <div v-for="(file, index) in uploadFileList" :key="index" class="file-list-item-sm">
              <span class="file-item-name">{{ file.webkitRelativePath || file.name }}</span>
              <span class="file-item-size">{{ formatFileSize(file.size) }}</span>
              <button v-if="!uploading" class="action-btn danger" @click="removeFile(index)">×</button>
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
        <div class="dialog-footer">
          <button class="btn" @click="cancelUpload">取消</button>
          <button class="btn btn-primary" :disabled="uploadFileList.length === 0 || uploading" @click="submitUpload">
            {{ uploading ? '上传中...' : '开始上传' }}
          </button>
        </div>
      </div>
    </div>

    <!-- 右键菜单 -->
    <div v-if="contextMenu.visible" class="context-menu"
      :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }">
      <div class="context-menu-item" @click="handleContextRename">✏️ 重命名</div>
      <div class="context-menu-divider"></div>
      <div class="context-menu-item danger" @click="handleContextDelete">🗑️ 删除</div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '@/api/http'

const searchText = ref('')
const treeData = ref([])
const currentPhase = ref(null)
const currentFolders = ref([])
const currentFiles = ref([])
const breadcrumbPath = ref([])
const currentFolderCode = ref('') // 当前浏览的文件夹编码，空=根目录

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

// 右键菜单
const contextMenu = reactive({ visible: false, x: 0, y: 0, item: null, type: '' })

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
  await loadCurrentContent()
}

// ========== 文件夹/文件加载 ==========
const loadCurrentContent = async () => {
  if (!currentPhase.value) return
  const directoryCode = `SDC-${currentPhase.value.standardCode.replace(/[:\-\s]/g, '')}|${currentPhase.value.phaseCode.replace(/[:\-\s]/g, '')}`
  try {
    if (!currentFolderCode.value) {
      // 加载根目录 - 获取文件夹树
      const res = await http.get(`/api/standard-directory/configs/${directoryCode}/folders`)
      if (res.Status === true || res.status === 0) {
        const data = res.Data || res.data || []
        // 从树结构中提取根文件夹的子文件夹（Depth=2的文件夹）
        currentFolders.value = extractFoldersAtLevel(data, 2)
        currentFiles.value = []
      }
    } else {
      // 加载指定文件夹的内容
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
  } catch (error) {
    console.error('加载内容失败:', error)
    currentFolders.value = []
    currentFiles.value = []
  }
}

// 从树结构中提取指定层级的文件夹
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
  // 树结构的根节点可能是数组或单个对象
  if (Array.isArray(tree)) {
    for (const root of tree) {
      if (root.Children) traverse(root.Children)
    }
  } else if (tree && tree.Children) {
    traverse(tree.Children)
  }
  return result
}

// 提取指定父文件夹下的子文件夹
const extractChildFolders = (tree, parentCode) => {
  const result = []
  const findAndExtract = (nodes) => {
    for (const node of (nodes || [])) {
      const code = node.FolderCode || node.folderCode
      if (code === parentCode) {
        if (node.Children) {
          result.push(...node.Children)
        }
        return true
      }
      if (node.Children && findAndExtract(node.Children)) {
        return true
      }
    }
    return false
  }
  if (Array.isArray(tree)) {
    for (const root of tree) {
      findAndExtract(root.Children || [root])
    }
  } else if (tree) {
    findAndExtract(tree.Children || [tree])
  }
  return result
}

// ========== 文件夹导航 ==========
const enterFolder = (folder) => {
  const code = folder.FolderCode || folder.folderCode
  const name = folder.FolderName || folder.folderName
  currentFolderCode.value = code
  breadcrumbPath.value.push({ code, name })
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
  if (selectedItems.has(code)) {
    selectedItems.delete(code)
  } else {
    selectedItems.add(code)
  }
}

const toggleSelectAll = (e) => {
  selectedItems.clear()
  if (e.target.checked) {
    currentFolders.value.forEach(f => selectedItems.add(f.FolderCode || f.folderCode))
    currentFiles.value.forEach(f => selectedItems.add(f.FileCode || f.fileCode))
  }
}

const selectAll = () => {
  currentFolders.value.forEach(f => selectedItems.add(f.FolderCode || f.folderCode))
  currentFiles.value.forEach(f => selectedItems.add(f.FileCode || f.fileCode))
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
  const directoryCode = `SDC-${currentPhase.value.standardCode.replace(/[:\-\s]/g, '')}|${currentPhase.value.phaseCode.replace(/[:\-\s]/g, '')}`
  try {
    const res = await http.post(`/api/standard-directory/configs/${directoryCode}/folders/create`, {
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
      res = await http.put(`/api/standard-directory/folders/${code}`, {
        ...item,
        FolderName: renameForm.newName
      })
    } else {
      res = await http.put(`/api/standard-directory/files/${code}`, {
        ...item,
        FileName: renameForm.newName
      })
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
    if (isFolder) {
      res = await http.delete(`/api/standard-directory/folders/${code}`)
    } else {
      res = await http.delete(`/api/standard-directory/files/${code}`)
    }
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

  // 逐个删除
  for (const code of selectedItems) {
    const folder = currentFolders.value.find(f => (f.FolderCode || f.folderCode) === code)
    const file = currentFiles.value.find(f => (f.FileCode || f.fileCode) === code)
    if (folder) await deleteItem(folder)
    else if (file) await deleteItem(file)
  }
  selectedItems.clear()
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

const removeFile = (index) => {
  uploadFileList.value.splice(index, 1)
}

const clearUploadList = () => {
  uploadFileList.value = []
}

const cancelUpload = () => {
  showUploadDialogFlag.value = false
  uploadFileList.value = []
  uploading.value = false
}

const submitUpload = async () => {
  if (uploadFileList.value.length === 0) return
  if (!currentPhase.value) {
    ElMessage.warning('请先在左侧选择一个阶段节点')
    return
  }

  uploading.value = true
  uploadProgress.total = uploadFileList.value.length
  uploadProgress.completed = 0
  uploadProgress.failed = 0
  uploadProgress.status = 'uploading'

  const directoryCode = `SDC-${currentPhase.value.standardCode.replace(/[:\-\s]/g, '')}|${currentPhase.value.phaseCode.replace(/[:\-\s]/g, '')}`
  let taskId = null

  try {
    // Step 1: 构建清单
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
        relativePath: relativePath,
        fileName: file.name,
        fileSize: file.size,
        mimeType: file.type || 'application/octet-stream'
      })
    }

    // Step 2: upload-init
    const initRes = await http.post('/api/standard-directory/upload-init', { directoryCode, folders, files })
    if (!initRes.Status && initRes.status !== 0) {
      throw new Error(initRes.Message || initRes.message || '预处理失败')
    }

    const enhancedManifest = initRes.Data || initRes.data
    taskId = enhancedManifest.TaskId || enhancedManifest.taskId
    const totalFiles = enhancedManifest.TotalFiles || enhancedManifest.totalFiles || 0
    const fileList = enhancedManifest.Files || enhancedManifest.files || []

    // Step 3: 逐个上传
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
        if (res.Status === true || res.status === 0) {
          uploadProgress.completed = i + 1
        } else {
          failed = true
          uploadProgress.failed++
        }
      } catch (error) {
        failed = true
        uploadProgress.failed++
      }
    }

    // Step 4: 确认或回滚
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
  const directoryCode = `SDC-${currentPhase.value.standardCode.replace(/[:\-\s]/g, '')}|${currentPhase.value.phaseCode.replace(/[:\-\s]/g, '')}`
  window.open(`/api/standard-directory/configs/${directoryCode}/export`, '_blank')
}

const handleHelp = () => {
  showHelpDialog.value = true
}

const replaceFile = (file) => {
  ElMessage.info('替换文件功能开发中')
}

const downloadFile = (file) => {
  const storagePath = file.StoragePath || file.storagePath
  if (storagePath) {
    window.open(`/api/standard-directory/download?path=${encodeURIComponent(storagePath)}`, '_blank')
  }
}

// ========== 右键菜单 ==========
const showContextMenu = (event, item, type) => {
  contextMenu.visible = true
  contextMenu.x = event.pageX
  contextMenu.y = event.pageY
  contextMenu.item = item
  contextMenu.type = type
}

const handleContextRename = () => {
  if (contextMenu.item) showRenameDialog(contextMenu.item)
  contextMenu.visible = false
}

const handleContextDelete = () => {
  if (contextMenu.item) deleteItem(contextMenu.item)
  contextMenu.visible = false
}

const hideContextMenu = () => {
  contextMenu.visible = false
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

// ========== 生命周期 ==========
onMounted(() => {
  loadTree()
  document.addEventListener('click', hideContextMenu)
})

onUnmounted(() => {
  document.removeEventListener('click', hideContextMenu)
})
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
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.left-title {
  font-weight: 500;
  color: #303133;
}

.search-box {
  padding: 12px 16px;
  border-bottom: 1px solid #ebeef5;
}

.search-input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  font-size: 13px;
  outline: none;
}

.search-input:focus {
  border-color: #409eff;
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
}

.tree-node:hover {
  background: #f5f7fa;
}

.tree-node.active {
  background: #ecf5ff;
  color: #409eff;
}

.tree-node.level-0 { padding-left: 16px; font-weight: 500; }
.tree-node.level-1 { padding-left: 36px; }
.tree-node.level-2 { padding-left: 56px; }

.tree-icon { width: 16px; height: 16px; display: flex; align-items: center; justify-content: center; }
.tree-icon.org { color: #e6a23c; }
.tree-icon.standard { color: #409eff; }
.tree-icon.phase { color: #67c23a; }

.tree-toggle {
  width: 16px; height: 16px; display: flex; align-items: center; justify-content: center;
  cursor: pointer; color: #c0c4cc; transition: transform 0.2s;
}
.tree-toggle.expanded { transform: rotate(90deg); }

.tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.tree-badge {
  background: #f0f2f5; color: #909399; font-size: 12px;
  padding: 1px 6px; border-radius: 10px;
}

/* 右侧面板 */
.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
}

/* 面包屑 */
.breadcrumb {
  padding: 12px 20px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #606266;
}

.breadcrumb-item { color: #409eff; cursor: pointer; }
.breadcrumb-item:hover { text-decoration: underline; }
.breadcrumb-separator { color: #c0c4cc; }
.breadcrumb-current { color: #303133; font-weight: 500; }

/* 工具栏 */
.toolbar {
  padding: 12px 20px;
  border-bottom: 1px solid #ebeef5;
  display: flex;
  align-items: center;
  gap: 12px;
}

.btn {
  padding: 8px 16px; border: 1px solid #dcdfe6; border-radius: 4px;
  cursor: pointer; font-size: 13px; display: flex; align-items: center;
  gap: 6px; transition: all 0.2s; background: #fff;
}
.btn:hover { border-color: #409eff; color: #409eff; }
.btn-primary { background: #409eff; color: #fff; border-color: #409eff; }
.btn-primary:hover { background: #66b1ff; border-color: #66b1ff; color: #fff; }
.btn-danger { color: #f56c6c; border-color: #f56c6c; }
.btn-danger:hover { background: #f56c6c; color: #fff; }
.btn-help { border-color: #e6a23c; color: #e6a23c; }
.btn-help:hover { background: #e6a23c; color: #fff; border-color: #e6a23c; }

.toolbar-divider { width: 1px; height: 24px; background: #e4e7ed; }

/* 文件列表 */
.file-list-container { flex: 1; overflow-y: auto; }

.file-list-header {
  display: grid; grid-template-columns: 40px 1fr 100px 140px 120px;
  padding: 10px 20px; background: #f5f7fa; border-bottom: 1px solid #ebeef5;
  font-weight: 500; color: #606266; font-size: 13px; position: sticky; top: 0;
}

.file-list-item {
  display: grid; grid-template-columns: 40px 1fr 100px 140px 120px;
  padding: 12px 20px; border-bottom: 1px solid #ebeef5;
  align-items: center; cursor: pointer; transition: background 0.2s;
}
.file-list-item:hover { background: #f5f7fa; }
.file-list-item.selected { background: #ecf5ff; }

.file-checkbox { width: 16px; height: 16px; cursor: pointer; }

.file-name { display: flex; align-items: center; gap: 10px; overflow: hidden; }

.file-icon { width: 32px; height: 32px; display: flex; align-items: center; justify-content: center; font-size: 24px; }
.file-icon.folder { color: #e6a23c; }
.file-icon.file-pdf { color: #f56c6c; }
.file-icon.file-doc { color: #409eff; }
.file-icon.file-xls { color: #67c23a; }
.file-icon.file-image { color: #909399; }
.file-icon.file-default { color: #909399; }

.file-name-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.file-name-text.folder-name { color: #303133; font-weight: 500; }

.file-size, .file-date { color: #909399; font-size: 13px; }

.file-actions { display: flex; gap: 8px; opacity: 0; transition: opacity 0.2s; }
.file-list-item:hover .file-actions { opacity: 1; }

.action-btn {
  padding: 4px 8px; border: none; background: none; cursor: pointer;
  color: #606266; font-size: 12px; border-radius: 4px;
}
.action-btn:hover { background: #ebeef5; color: #409eff; }
.action-btn.danger:hover { color: #f56c6c; }

/* 状态栏 */
.status-bar {
  padding: 10px 20px; border-top: 1px solid #ebeef5; background: #f5f7fa;
  font-size: 13px; color: #909399; display: flex; justify-content: space-between;
}

/* 空状态 */
.empty-state { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; color: #909399; }
.empty-icon { font-size: 64px; margin-bottom: 16px; color: #dcdfe6; }
.empty-text { font-size: 14px; margin-bottom: 8px; }
.empty-hint { font-size: 13px; color: #c0c4cc; }

/* 弹窗 */
.dialog-overlay {
  position: fixed; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0, 0, 0, 0.5); display: flex; align-items: center;
  justify-content: center; z-index: 1000;
}

.dialog {
  background: #fff; border-radius: 8px; width: 480px; max-width: 90%;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.dialog-header {
  padding: 16px 20px; border-bottom: 1px solid #ebeef5;
  display: flex; align-items: center; justify-content: space-between;
}

.dialog-title { font-size: 16px; font-weight: 500; color: #303133; }
.dialog-close { cursor: pointer; color: #909399; font-size: 20px; }
.dialog-close:hover { color: #303133; }
.dialog-body { padding: 20px; }

.form-item { margin-bottom: 16px; }
.form-label { display: block; margin-bottom: 8px; font-weight: 500; color: #606266; }

.form-input {
  width: 100%; padding: 10px 12px; border: 1px solid #dcdfe6;
  border-radius: 4px; font-size: 14px; outline: none;
}
.form-input:focus { border-color: #409eff; }

.dialog-footer {
  padding: 12px 20px; border-top: 1px solid #ebeef5;
  display: flex; justify-content: flex-end; gap: 12px;
}

/* 上传区域 */
.upload-tabs { display: flex; justify-content: center; margin-bottom: 20px; }

.upload-area {
  border: 2px dashed #dcdfe6; border-radius: 8px; padding: 32px;
  text-align: center; cursor: pointer; margin-bottom: 16px;
}
.upload-area:hover { border-color: #409eff; background: #ecf5ff; }

.upload-trigger { display: flex; flex-direction: column; align-items: center; }
.upload-icon-text { font-size: 48px; margin-bottom: 12px; }
.upload-text { font-size: 14px; color: #303133; }

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

/* 右键菜单 */
.context-menu {
  position: fixed; background: #fff; border: 1px solid #e4e7ed;
  border-radius: 4px; box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
  padding: 6px 0; min-width: 160px; z-index: 2000;
}

.context-menu-item {
  padding: 8px 16px; cursor: pointer; display: flex; align-items: center;
  gap: 8px; font-size: 13px; color: #606266;
}
.context-menu-item:hover { background: #f5f7fa; color: #409eff; }
.context-menu-item.danger { color: #f56c6c; }
.context-menu-item.danger:hover { background: #fef0f0; }

.context-menu-divider { height: 1px; background: #e4e7ed; margin: 6px 0; }

/* 帮助内容 */
.help-content h3 { color: #303133; font-size: 15px; margin: 20px 0 10px 0; padding-bottom: 8px; border-bottom: 1px solid #ebeef5; }
.help-content h3:first-child { margin-top: 0; }
.help-content p { color: #606266; line-height: 1.6; margin-bottom: 12px; }
.help-content ul { color: #606266; line-height: 1.8; padding-left: 20px; margin-bottom: 12px; }
.help-content li { margin-bottom: 4px; }
.help-content strong { color: #303133; }

.code-example {
  background: #f5f7fa; border: 1px solid #ebeef5; border-radius: 4px;
  padding: 12px; font-family: monospace; font-size: 13px; color: #606266;
  line-height: 1.8; margin-bottom: 12px;
}
</style>
