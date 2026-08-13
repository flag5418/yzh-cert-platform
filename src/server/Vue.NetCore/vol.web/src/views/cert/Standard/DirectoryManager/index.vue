<template>
  <div class="directory-manager">
    <!-- 上传队列顶部通知栏（只展示当前 机构+标准+阶段 的任务） -->
    <UploadQueueBanner
      ref="uploadQueueBannerRef"
      :tasks="activeUploadTasks"
      :queue="activeQueue"
      @cancel="handleCancelUploadTask"
      @clear-done="clearFinishedTasks"
    />

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
          prefix-icon="Search"
        />
      </div>
      <div class="tree-container">
        <div v-for="org in treeData" :key="org.id" class="tree-group">
          <!-- 机构 -->
          <div class="tree-node level-0" @click="toggleExpand(org)">
            <el-icon class="tree-toggle" :class="{ expanded: org.expanded }"
              ><IconForward
            /></el-icon>
            <el-icon class="tree-icon org"><IconOfficeBuilding /></el-icon>
            <span class="tree-label">{{ org.label }}</span>
            <el-badge :value="org.children ? org.children.length : 0" type="info" />
          </div>
          <!-- 标准 -->
          <template v-if="org.expanded && org.children">
            <template v-for="std in org.children" :key="std.id">
              <div class="tree-node level-1" @click="toggleExpand(std)">
                <el-icon class="tree-toggle" :class="{ expanded: std.expanded }"
                  ><IconForward
                /></el-icon>
                <el-icon class="tree-icon standard"><IconFile /></el-icon>
                <span class="tree-label">{{ std.label }}</span>
                <el-badge :value="std.children ? std.children.length : 0" type="info" />
              </div>
              <!-- 阶段 -->
              <div
                v-for="phase in std.children"
                :key="phase.id"
                class="tree-node level-2"
                :class="{ active: currentPhase && currentPhase.id === phase.id }"
                @click="selectPhase(phase)"
              >
                <el-icon class="tree-toggle" style="visibility: hidden"><IconForward /></el-icon>
                <el-icon class="tree-icon phase"><IconCalendar /></el-icon>
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
            <span class="clickable-breadcrumb" @click="navigateToRoot">{{
              currentPhase.standardCode
            }}</span>
          </el-breadcrumb-item>
          <el-breadcrumb-item>
            <span class="clickable-breadcrumb" @click="navigateToRoot">{{
              currentPhase.phaseCode
            }}</span>
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
        <!-- 队列执行中状态条 -->
        <div v-if="activeQueue?.exists" class="queue-status-bar">
          <el-icon class="is-spinning"><IconLoading /></el-icon>
          <span class="queue-name">{{ activeQueue.queueName || activeQueue.queueCode }}</span>
          <el-progress
            :percentage="activeQueue.progress || 0"
            :status="activeQueue.status === 'failed' ? 'exception' : activeQueue.status === 'completed' ? 'success' : ''"
            :stroke-width="6"
            style="flex:1;margin:0 12px;"
          />
          <span class="queue-count">{{ activeQueue.completedCount }}/{{ activeQueue.totalCount }}</span>
          <el-button link type="primary" size="small" @click="goToQueueMonitor">
            队列监控 →
          </el-button>
        </div>
      </div>

      <!-- 工具栏 -->
      <div class="toolbar" v-if="currentPhase">
        <el-badge :value="runningUploadCount" :hidden="runningUploadCount === 0">
          <el-button type="primary" size="small" @click="showUploadQueue" :disabled="isBusy">
            <el-icon><IconUpload /></el-icon> 上传队列
          </el-button>
        </el-badge>
        <el-button type="primary" size="small" @click="handleNewFolder" :disabled="isBusy">
          <el-icon><IconFolderAdd /></el-icon> 新建文件夹
        </el-button>
        <el-button size="small" @click="handleUpload" :disabled="isBusy">
          <el-icon><IconUpload /></el-icon> 上传
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" :disabled="isBusy" @click="handleRefresh">
          <el-icon><IconRefresh /></el-icon> 刷新节点
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" @click="handleExport" :disabled="selectedItems.size === 0 || isBusy">
          <el-icon><IconDownload /></el-icon> 导出打包
        </el-button>
        <el-divider direction="vertical" />
        <el-button size="small" :disabled="isBusy">全选</el-button>
        <el-button size="small" type="danger" plain :disabled="isBusy" @click="deleteSelected">
          <el-icon><IconDelete /></el-icon> 删除
        </el-button>
        <div style="flex: 1"></div>
        <el-button size="small" type="warning" plain :disabled="isBusy" @click="handleHelp">
          <el-icon><IconHelp /></el-icon> 使用帮助
        </el-button>
      </div>

      <!-- 文件列表 -->
      <div class="file-list-container" v-if="currentPhase">
        <table class="file-table">
          <thead>
            <tr>
              <th style="width: 40px">
                <el-checkbox v-model="allSelected" @change="toggleSelectAll" />
              </th>
              <th>名称</th>
              <th>大小</th>
              <th>上传状态</th>
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
                <el-icon class="folder-icon"><IconFolder /></el-icon>
                <span class="name-text folder-name">{{
                  folder.FolderName || folder.folderName
                }}</span>
              </td>
              <td class="size-cell">--</td>
              <td class="upload-status-cell">—</td>
              <td class="date-cell">{{ formatDate(folder.CreateDate || folder.createDate) }}</td>
              <td class="action-cell" :style="{ pointerEvents: isBusy ? 'none' : 'auto', opacity: isBusy ? 0.5 : 1 }">
                <el-button link type="primary" size="small" @click.stop="showRenameDialog(folder)"
                  >重命名</el-button
                >
                <el-button link type="danger" size="small" @click.stop="deleteItem(folder)"
                  >删除</el-button
                >
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
                <el-icon
                  class="file-type-icon"
                  :class="getFileIconClass(file.FileName || file.fileName)"
                  ><Document
                /></el-icon>
                <span class="name-text">{{ file.FileName || file.fileName }}</span>
                <!-- 上传中 / 队列锁定状态图标 -->
                <el-icon v-if="file.uploadStatus === 'uploading'" class="status-icon is-spinning" color="var(--yzh-color-primary)"><IconLoading /></el-icon>
                <el-icon v-else-if="file.uploadStatus === 'converting'" class="status-icon" color="var(--yzh-color-primary)"><IconFile /></el-icon>
                <el-icon v-else-if="isQueueRunning" class="status-icon is-spinning" color="var(--yzh-color-danger)"><IconLoading /></el-icon>
              </td>
              <td class="size-cell">{{ formatFileSize(file.FileSize || file.fileSize) }}</td>
              <td class="upload-status-cell">
                <el-icon v-if="file.uploadStatus === 'uploading'" color="var(--yzh-color-primary)" class="is-spinning"><IconLoading /></el-icon>
                <el-icon v-else-if="file.uploadStatus === 'uploaded' || file.uploadStatus === 'active'" color="var(--yzh-color-success)"><IconSuccess /></el-icon>
                <el-icon v-else-if="file.uploadStatus === 'converting'" color="var(--yzh-color-primary)"><IconFile /></el-icon>
                <el-icon v-else-if="file.uploadStatus === 'failed'" color="var(--yzh-color-danger)"><IconClose /></el-icon>
                <span v-if="file.uploadProgress > 0 && file.uploadProgress < 100" class="upload-percent">
                  {{ file.uploadProgress }}%
                </span>
                <span v-else class="upload-idle">—</span>
              </td>
              <td class="date-cell">{{ formatDate(file.CreateDate || file.createDate) }}</td>
              <td class="action-cell" :style="{ pointerEvents: isBusy ? 'none' : 'auto', opacity: isBusy ? 0.5 : 1 }">
                <el-button link type="primary" size="small" @click.stop="showRenameDialog(file)"
                  >重命名</el-button
                >
                <el-button link type="primary" size="small" @click.stop="replaceFile(file)"
                  >替换</el-button
                >
                <el-button link type="primary" size="small" @click.stop="downloadFile(file)"
                  >下载</el-button
                >
                <el-button link type="danger" size="small" @click.stop="deleteItem(file)"
                  >删除</el-button
                >
              </td>
            </tr>
          </tbody>
        </table>

        <!-- 空状态 -->
        <YzhEmptyState
          v-if="currentFolders.length === 0 && currentFiles.length === 0"
          :icon="IconFolder"
          title="暂无内容"
          description="该目录下暂无文件夹或文件"
          compact
        />
      </div>

      <!-- 未选中阶段：显示目录配置管理 -->
      <div v-if="!currentPhase" class="empty-state">
        <ConfigTab />
      </div>

      <!-- 状态栏 -->
      <CertStatusBar v-if="currentPhase">
        <span
          >共 {{ currentFolders.length + currentFiles.length }} 项 | 文件夹
          {{ currentFolders.length }} 个，文件 {{ currentFiles.length }} 个</span
        >
        <template #right><span>总大小 {{ totalSizeFormatted }}</span></template>
      </CertStatusBar>
    </div>

    <!-- 新建文件夹弹窗 -->
    <el-dialog v-model="showFolderDialog" title="新建文件夹" width="420px">
      <el-form :model="folderForm" label-width="90px" class="dialog-form">
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
          <div>
            文件夹编码: FD-{目录编码}|L{层级}|S{序号} → FD-SDC-ISO134852016|STAGE01|L02|S001
          </div>
          <div>
            文件编码: FL-{文件夹编码}|{文件名} →
            FL-FD-SDC-ISO134852016|STAGE01|L02|S001|营业执照.pdf
          </div>
        </div>
      </div>
      <template #footer>
        <el-button type="primary" @click="showHelpDialog = false">我知道了</el-button>
      </template>
    </el-dialog>

    <!-- 上传对话框 -->
    <el-dialog
      v-model="showUploadDialogFlag"
      title="上传文件"
      width="560px"
      :close-on-click-modal="false"
    >
      <div class="upload-dialog-body">
        <div class="upload-area">
          <input
            ref="fileInputRef"
            type="file"
            multiple
            style="display: none"
            @change="handleFileSelect"
          />
          <input
            ref="folderInputRef"
            type="file"
            webkitdirectory
            multiple
            style="display: none"
            @change="handleFolderSelect"
          />
          <div
            class="upload-trigger"
            :class="{ 'is-dragging': dragActive }"
            @click="triggerFileUpload"
            @dragover.prevent="dragActive = true"
            @dragleave="dragActive = false"
            @drop.prevent="handleDrop"
          >
            <el-icon class="upload-icon"><IconUpload /></el-icon>
            <div class="upload-text">
              点击选择文件或拖拽到此处
            </div>
            <div class="upload-hint">支持多个文件同时上传（文档/图片）</div>
          </div>
        </div>
        <div class="upload-secondary-btn">
          <el-button size="small" plain @click="triggerFolderUpload">
            <el-icon><IconFolderAdd /></el-icon> 或上传整个文件夹
          </el-button>
        </div>
        <div v-if="uploadFileList.length > 0" class="upload-file-list">
          <div class="file-list-header-sm">
            <span>待上传文件 ({{ uploadFileList.length }}个)</span>
            <el-button
              type="danger"
              link
              size="small"
              @click="clearUploadList"
              :disabled="uploading"
              >清空</el-button
            >
          </div>
          <div v-for="(file, index) in uploadFileList" :key="index" class="file-list-item-sm">
            <span class="file-item-name">{{ file.webkitRelativePath || file.name }}</span>
            <span class="file-item-size">{{ formatFileSize(file.size) }}</span>
            <el-button v-if="!uploading" type="danger" link size="small" @click="removeFile(index)">
              <el-icon><IconDelete /></el-icon>
            </el-button>
          </div>
        </div>
        <!-- 上传进度 -->
        <div v-if="uploading || uploadProgress.status === 'done'" class="upload-progress-area">
          <div class="progress-info">
            <span v-if="uploadProgress.status === 'uploading'">
              正在上传: {{ uploadProgress.currentFile }} ({{ uploadProgress.completed }}/{{
                uploadProgress.total
              }})
            </span>
            <span v-else-if="uploadProgress.failed > 0" class="text-danger">
              上传完成，{{ uploadProgress.failed }} 个文件失败
            </span>
            <span v-else class="text-success">
              全部 {{ uploadProgress.total }} 个文件上传成功
            </span>
          </div>
          <el-progress
            :percentage="
              uploadProgress.total > 0
                ? Math.round((uploadProgress.completed / uploadProgress.total) * 100)
                : 0
            "
            :status="
              uploadProgress.failed > 0
                ? 'exception'
                : uploadProgress.status === 'done' && uploadProgress.failed === 0
                  ? 'success'
                  : ''
            "
          />
        </div>
      </div>
      <template #footer>
        <el-button @click="cancelUpload">取消</el-button>
        <el-button
          type="primary"
          :disabled="uploadFileList.length === 0 || uploading"
          @click="submitUpload"
        >
          {{ uploading ? '上传中...' : '开始上传' }}
        </el-button>
      </template>
    </el-dialog>
    <!-- 文件转换进度面板 -->
    <ConvertProgressPanel ref="convertPanelRef" />
  </div>
</template>

<script setup>
import http from '@/api/http'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import {
  IconForward, IconCalendar, IconDelete, IconFile, IconDownload,
  IconFolder, IconFolderAdd, IconOfficeBuilding, IconHelp, IconUpload,
  IconLoading, IconSuccess, IconClose,
  YzhEmptyState
} from '@/yzh'
import { CertStatusBar } from '@/certcore'
import { formatFileSize, formatDate, downloadBlob, downloadBlobPost, fileNameOf } from '@/certcore'
import ConvertProgressPanel from './ConvertProgressPanel.vue'
import ConfigTab from './components/ConfigTab.vue'
import UploadQueueBanner from './components/UploadQueueBanner.vue'
import * as signalR from '@microsoft/signalr'
import { useYzhQueue } from '@/yzh'

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
const convertPanelRef = ref(null)
const showUploadPanel = ref(false)

const uploadProgress = reactive({
  total: 0,
  completed: 0,
  failed: 0,
  currentFile: '',
  status: 'idle'
})

// ========== 上传队列管理（SignalR 实时状态） ==========
const uploadTasks = ref([])  // 上传任务列表（含已完成，按 directoryCode 隔离）
const signalRConnection = ref(null)
const uploadQueueBannerRef = ref(null)

/**
 * 将 API 返回的 PascalCase 属性规范化为 camelCase
 * 同时补充 uploadStatus / uploadProgress 等前端计算属性
 */
const normalizeItem = (item, type) => {
  if (!item) return item
  const n = { ...item }
  // PascalCase → camelCase
  if (n.UploadStatus !== undefined) n.uploadStatus = n.UploadStatus
  if (n.FileName !== undefined) n.fileName = n.FileName
  if (n.FileSize !== undefined) n.fileSize = n.FileSize
  if (n.FolderName !== undefined) n.folderName = n.FolderName
  if (n.FolderCode !== undefined) n.folderCode = n.FolderCode
  if (n.FileCode !== undefined) n.fileCode = n.FileCode
  if (n.CreateDate !== undefined) n.createDate = n.CreateDate
  if (n.ModifyDate !== undefined) n.modifyDate = n.ModifyDate
  if (n.IsValid !== undefined) n.isValid = n.IsValid
  // 计算上传进度（从当前阶段的任务中查找，避免跨阶段串扰）
  if (type === 'file' && n.fileCode) {
    const task = uploadTasks.value.find(t => t.taskId && t.files && t.directoryCode === currentDirectoryCode.value)
    if (task) {
      const f = task.files.find(f => f.fileCode === n.fileCode)
      if (f) {
        n.uploadStatus = f.status
        n.uploadProgress = f.uploadProgress
      }
    }
  }
  return n
}

// 当前机构+标准+阶段 的目录编码（用于队列隔离）
const currentDirectoryCode = computed(() => (currentPhase.value ? buildDirectoryCode() : ''))

// 来自后端 yzh_queue 的当前目录运行中队列（轮询更新）
const activeQueue = ref(null)
let pollTimer = null

// 计算属性：是否正在上传（仅针对当前阶段，不影响其他 机构/标准/阶段 的文件管理）
const isUploading = computed(() =>
  uploadTasks.value.some(t => t.directoryCode === currentDirectoryCode.value && (t.status === 'uploading' || t.status === 'converting'))
)
// 计算属性：队列是否运行中（后端 yzh_queue + 本地 SignalR 任务）
const isQueueRunning = computed(() => activeQueue.value?.exists === true)
// 综合忙闲状态：上传中 或 队列执行中 都视为忙
const isBusy = computed(() => isUploading.value || isQueueRunning.value)
// 当前阶段的上传任务（含已完成，供横幅展示完成状态）
const activeUploadTasks = computed(() =>
  uploadTasks.value.filter(t => t.directoryCode === currentDirectoryCode.value)
)
// 当前阶段进行中的任务数（工具栏徽标）
const runningUploadCount = computed(() =>
  uploadTasks.value.filter(t => t.directoryCode === currentDirectoryCode.value && (t.status === 'uploading' || t.status === 'converting')).length
)

// 打开上传队列详情面板
const showUploadQueue = () => {
  uploadQueueBannerRef.value?.openPanel()
}

const goToQueueMonitor = () => {
  window.open('#/CertPlatform/ConvertQueueMonitor', '_blank')
}

// 清除当前阶段已结束的任务（保持队列整洁）
const clearFinishedTasks = () => {
  uploadTasks.value = uploadTasks.value.filter(
    t => t.directoryCode !== currentDirectoryCode.value || (t.status !== 'done' && t.status !== 'failed')
  )
}

// 初始化 SignalR 连接（只连一次）
const initSignalR = () => {
  if (signalRConnection.value) return
  const token = localStorage.getItem('user') ? JSON.parse(localStorage.getItem('user')).token : ''
  signalRConnection.value = new signalR.HubConnectionBuilder()
    .withUrl(`${window.ipAddress || 'http://localhost:9992/'}uploadHub${token ? `?access_token=${token}` : ''}`, {
      skipNegotiation: true,
      transport: signalR.HttpTransportType.WebSockets
    })
    .withAutomaticReconnect()
    .build()

  signalRConnection.value.on('ReceiveUploadProgress', (progress) => {
    updateTaskProgress(progress)
  })

  signalRConnection.value.start().catch(err => {
    console.warn('[SignalR] 连接失败，将使用轮询降级:', err.message)
  })
}

// 更新单个任务的进度
const updateTaskProgress = (progress) => {
  const task = uploadTasks.value.find(t => t.taskId === progress.taskId)
  if (!task) {
    // 任务不存在，创建新条目
    uploadTasks.value.push({
      taskId: progress.taskId,
      directoryCode: '',
      status: progress.status || 'uploading',
      totalFiles: progress.totalFiles || 0,
      uploadedFiles: progress.uploadedFiles || 0,
      pendingFiles: progress.pendingFiles || 0,
      percent: progress.percent || 0,
      failedFiles: 0,
      convertCount: 0,
      files: []
    })
    return
  }
  task.status = progress.status || task.status
  task.uploadedFiles = progress.uploadedFiles ?? task.uploadedFiles
  task.totalFiles = progress.totalFiles ?? task.totalFiles
  task.percent = progress.percent ?? task.percent
  task.updateTime = progress.updateTime
  if (task.status === 'completed') {
    task.status = 'done'
    ElMessage.success(`上传任务完成：${task.totalFiles} 个文件全部上传成功`)
    loadCurrentContent()
    showUploadPanel.value = false
  } else if (task.status === 'cancelled' || task.status === 'expired') {
    task.status = 'cancelled'
    ElMessage.warning('上传任务已取消')
    showUploadPanel.value = false
  }
}

// 开始监听某个任务的进度
const subscribeToTask = async (taskId) => {
  let conn = signalRConnection.value
  if (!conn) {
    initSignalR()
    conn = signalRConnection.value
  }
  if (!conn) return
  // 确保连接就绪后再订阅（小文件上传可能瞬间完成，延迟订阅会错过“已完成”事件）
  if (conn.state !== signalR.HubConnectionState.Connected) {
    try {
      await conn.start()
    } catch (err) {
      console.warn('[SignalR] 连接失败，无法订阅实时进度:', err.message)
      return
    }
  }
  try {
    await conn.invoke('BroadcastUploadProgress', taskId, {})
  } catch (err) {
    console.warn('[SignalR] 订阅失败:', err)
  }
}

// 取消任务
const handleCancelUploadTask = async (taskId) => {
  try {
    await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`)
    const task = uploadTasks.value.find(t => t.taskId === taskId)
    if (task) task.status = 'cancelled'
    uploadTasks.value = uploadTasks.value.filter(t => t.taskId !== taskId)
    ElMessage.success('已取消上传任务')
  } catch (e) {
    ElMessage.error('取消失败')
  }
}

// 获取文件上传状态文本
const getUploadStatusText = (status) => {
  const map = { uploading: '上传中', uploaded: '已上传', active: '已完成', converting: '转换中', converted: '已转换', failed: '失败', idle: '—', pending: '等待中', replacing: '替换中' }
  return map[status] || '—'
}
const getUploadStatusClass = (status) => {
  const cls = { uploading: 'uploading', uploaded: 'success', active: 'success', converting: 'converting', failed: 'failed' }
  return cls[status] || ''
}

// 计算属性
const totalSizeFormatted = computed(() => {
  const total = currentFiles.value.reduce(
    (sum, f) => sum + parseInt(f.FileSize || f.fileSize || 0),
    0
  )
  return formatFileSize(total)
})

// ========== 组织树 ==========
const loadTree = async () => {
  try {
    const res = await http.get('/api/standard-directory/organization-tree')
    if (res.Status === true || res.status === 0) {
      treeData.value = (res.Data || res.data || []).map((org) => ({
        ...org,
        expanded: true,
        children: (org.children || []).map((std) => ({
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
  activeQueue.value = null
  await loadCurrentContent()
  await refreshActiveQueue()
}

// ========== 队列轮询（当前目录） ==========
const { getActiveQueue } = useYzhQueue()

const refreshActiveQueue = async () => {
  const dc = currentDirectoryCode.value
  if (!dc) { activeQueue.value = null; return }
  try {
    activeQueue.value = await getActiveQueue(dc)
  } catch (e) {
    console.warn('[DirectoryManager] 队列查询失败:', e)
  }
}

const startPolling = () => {
  if (pollTimer) return
  pollTimer = setInterval(async () => {
    // 有运行中队列时轮询，无则静默
    try {
      const q = activeQueue.value
      if (!q?.exists) return
      await refreshActiveQueue()
      // 队列完成后停止轮询
      if (q.status === 'completed' || q.status === 'failed' || q.status === 'cancelled') {
        clearInterval(pollTimer)
        pollTimer = null
      }
    } catch {}
  }, 5000)
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
        // 根级别：取tree根节点的直接子节点（Depth=2的文件夹）
        // 如果根节点没有子节点（新建的根文件夹），直接用它
        const rootChildren = []
        for (const root of (Array.isArray(data) ? data : [data])) {
          if (root.Children && root.Children.length > 0) {
            rootChildren.push(...root.Children)
          } else if (!root.Children || root.Children.length === 0) {
            // 无子节点时，检查是否为新建的根文件夹（有FolderName）
            if (root.FolderName || root.folderName) {
              rootChildren.push(root)
            }
          }
        }
        currentFolders.value = rootChildren.map(f => normalizeItem(f, 'folder'))
        // 根级别：只加载在根文件夹下的文件（FolderCode以根文件夹code开头）
        const filesRes = await http.get(`/api/standard-directory/directory-files?directoryCode=${directoryCode}`)
        if (filesRes.Status === true || filesRes.status === 0) {
          const allFiles = filesRes.Data || filesRes.data || []
          // 根级别文件过滤：只保留在L01根文件夹下的文件（不包含子文件夹L02+中的文件）
          currentFiles.value = Array.isArray(allFiles)
            ? allFiles.filter((f) => {
                const fc = f.FolderCode || f.folderCode || ''
                const valid = f.IsValid !== false
                // L01表示根级别文件夹，L02+表示子文件夹
                const inRoot = !fc.includes('|L02|') && !fc.includes('|L03|') && !fc.includes('|L04|')
                return valid && inRoot
              }).map(f => normalizeItem(f, 'file'))
            : []
        }
      }
    } else {
      // 子文件夹级别：分别获取子文件夹列表和文件
      const foldersRes = await http.get(`/api/standard-directory/configs/${directoryCode}/folders`)
      if (foldersRes.Status === true || foldersRes.status === 0) {
        const allFolders = foldersRes.Data || foldersRes.data || []
        currentFolders.value = extractChildFolders(allFolders, currentFolderCode.value).map(f => normalizeItem(f, 'folder'))
      }
      const filesRes = await http.get(`/api/standard-directory/folders/${currentFolderCode.value}/files`)
      if (filesRes.Status === true || filesRes.status === 0) {
        const allItems = filesRes.Data || filesRes.data || []
        // folders/{folderCode}/files 返回该文件夹下的所有文件
        currentFiles.value = Array.isArray(allItems)
          ? allItems.filter((f) => f.IsValid !== false).map(f => normalizeItem(f, 'file'))
          : []
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
    for (const node of nodes || []) {
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
    for (const node of nodes || []) {
      const code = node.FolderCode || node.folderCode
      if (code === parentCode) {
        if (node.Children) result.push(...node.Children)
        return true
      }
      if (node.Children) {
        if (findAndExtract(node.Children)) return true
      }
    }
    return false
  }
  // 先检查根节点本身是否匹配，再检查其子节点
  if (Array.isArray(tree)) {
    for (const root of tree) {
      const code = root.FolderCode || root.folderCode
      if (code === parentCode) {
        if (root.Children) result.push(...root.Children)
      } else if (root.Children) {
        findAndExtract(root.Children)
      }
    }
  } else if (tree) {
    const code = tree.FolderCode || tree.folderCode
    if (code === parentCode) {
      if (tree.Children) result.push(...tree.Children)
    } else if (tree.Children) {
      findAndExtract(tree.Children)
    }
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
  allSelected.value = selectedItems.size === currentFolders.value.length + currentFiles.value.length
}

const toggleSelectAll = (val) => {
  selectedItems.clear()
  if (val) {
    currentFolders.value.forEach((f) => selectedItems.add(f.FolderCode || f.folderCode))
    currentFiles.value.forEach((f) => selectedItems.add(f.FileCode || f.fileCode))
  }
}

const selectAll = () => {
  currentFolders.value.forEach((f) => selectedItems.add(f.FolderCode || f.folderCode))
  currentFiles.value.forEach((f) => selectedItems.add(f.FileCode || f.fileCode))
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
    const res = await http.post(
      `/api/standard-directory/configs/${buildDirectoryCode()}/folders/create`,
      {
        folderName: folderForm.folderName,
        remark: folderForm.remark,
        // depth自动计算：根级别=1，子文件夹=父文件夹depth+1
        depth: currentFolderCode.value ? (currentFolderCode.value.includes('|L0') ? 2 : 1) : 1,
        parentCode: currentFolderCode.value || ''
      }
    )
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
  // 快照捕获新名称：ElMessageBox 确认框挂起期间，renameForm 可能被其他行的
  // showRenameDialog 覆盖，force 重试必须用本次的目标名称，而不是全局最新值。
  const newName = renameForm.newName
  // 类型判断：文件记录也有 FolderCode（所属文件夹），不能用 FolderCode 判文件夹！
  // 文件夹有 FolderName 且无 FileCode；文件有 FileName/FileCode。
  const isFolder = !!(item.FolderName || item.folderName) && !(item.FileCode || item.fileCode)
  const code = isFolder
    ? item.FolderCode || item.folderCode
    : item.FileCode || item.fileCode
  try {
    let res
    // 注意：后端 Newtonsoft 使用 CamelCase 契约（大小写不敏感），
    // 展开 item 时其 camelCase 字段（folderName/fileName）会覆盖 PascalCase 新值，
    // 必须剔除旧名字段，只保留新名字。
    const { folderName, fileName, ...rest } = item
    if (isFolder) {
      res = await http.post(`/api/standard-directory/folders/${code}`, {
        ...rest,
        FolderName: newName
      })
    } else {
      res = await http.post(`/api/standard-directory/files/${code}`, {
        ...rest,
        FileName: newName
      })
    }
    if (res.Status === true || res.status === 0) {
      ElMessage.success('重命名成功')
      showRenameDialogFlag.value = false
      await loadCurrentContent()
    } else if (res.Message?.includes('force=true')) {
      ElMessageBox.confirm(res.Message, '确认重命名', { type: 'warning' }).then(async () => {
        if (isFolder) {
          res = await http.post(`/api/standard-directory/folders/${code}`, {
            ...rest,
            FolderName: newName,
            Force: true
          })
        } else {
          res = await http.post(`/api/standard-directory/files/${code}`, {
            ...rest,
            FileName: newName,
            Force: true
          })
        }
        if (res.Status === true || res.status === 0) {
          ElMessage.success('重命名成功')
          showRenameDialogFlag.value = false
          await loadCurrentContent()
        } else {
          ElMessage.error(res.Message || '重命名失败')
        }
      }).catch(() => {})
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
  } catch {
    return
  }

  const isFolder = !!(item.FolderName || item.folderName) && !(item.FileCode || item.fileCode)
  const code = isFolder
    ? item.FolderCode || item.folderCode
    : item.FileCode || item.fileCode
  try {
    let res
    if (isFolder) res = await http.post(`/api/standard-directory/folders/${code}/delete`)
    else res = await http.post(`/api/standard-directory/files/${code}/delete`)
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
    await ElMessageBox.confirm(`确定要删除选中的 ${selectedItems.size} 个项目吗？`, '确认删除', {
      type: 'warning'
    })
  } catch {
    return
  }

  for (const code of [...selectedItems]) {
    const folder = currentFolders.value.find((f) => (f.FolderCode || f.folderCode) === code)
    const file = currentFiles.value.find((f) => (f.FileCode || f.fileCode) === code)
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

const triggerFileUpload = () => fileInputRef.value?.click()
const triggerFolderUpload = () => folderInputRef.value?.click()

// ========== 上传文件类型白名单 ==========
// 体系认证系统只允许上传文档/表格/图片等认证材料文件，
// 过滤掉 .DS_Store、临时文件等无关文件，避免后期文件比对出现问题。
const ALLOWED_UPLOAD_EXTS = [
  // 文档
  'pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'txt', 'rtf',
  // 图片
  'jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'tif', 'tiff'
]
const HIDDEN_FILE_PATTERN = /(^|\/)\.[^/]+$/

const isAllowedUploadFile = (file) => {
  const name = file.webkitRelativePath || file.name || ''
  // 过滤隐藏文件（如 .DS_Store、.gitignore）
  if (HIDDEN_FILE_PATTERN.test(name)) return false
  const ext = name.split('.').pop().toLowerCase()
  return ALLOWED_UPLOAD_EXTS.includes(ext)
}

const appendAllowedFiles = (files) => {
  const list = Array.from(files || [])
  const allowed = list.filter(isAllowedUploadFile)
  const rejected = list.filter(f => !isAllowedUploadFile(f))
  if (rejected.length > 0) {
    ElMessage.warning(`已过滤 ${rejected.length} 个不支持的文件（仅允许文档/图片）`)
  }
  uploadFileList.value = [...uploadFileList.value, ...allowed]
}

const handleFileSelect = (event) => {
  appendAllowedFiles(event.target.files)
  event.target.value = ''
}

const handleFolderSelect = (event) => {
  appendAllowedFiles(event.target.files)
  event.target.value = ''
}

const dragActive = ref(false)
const handleDrop = (event) => {
  dragActive.value = false
  appendAllowedFiles(event.dataTransfer?.files)
}

const removeFile = (index) => uploadFileList.value.splice(index, 1)
const clearUploadList = () => {
  uploadFileList.value = []
}
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
      // 拼接当前文件夹路径，确保文件上传到正确的子目录
      const fileName = file.name
      const rawPath = file.webkitRelativePath || fileName
      // 如果已在子文件夹中（webkitdirectory），保留原路径；否则拼上当前文件夹
      let relativePath = rawPath
      if (!rawPath.includes('/') && currentFolderCode.value) {
        // 需要查找当前文件夹的 FullPath 来构建相对路径
        const currentFolder = currentFolders.value.find(
          f => (f.FolderCode || f.folderCode) === currentFolderCode.value
        )
        if (currentFolder && (currentFolder.FullPath || currentFolder.fullPath)) {
          relativePath = `${currentFolder.FullPath || currentFolder.fullPath}/${fileName}`
        } else {
          // 回退：用 FolderCode 作为路径前缀
          relativePath = `${currentFolderCode.value}/${fileName}`
        }
      }

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
        relativePath,
        fileName: file.name,
        fileSize: file.size,
        mimeType: file.type || 'application/octet-stream'
      })
    }

    const initRes = await http.post('/api/standard-directory/upload-init', {
      directoryCode,
      // 机构编码来自组织树节点关系（cbCode），与登录人无关
      orgCode: currentPhase.value?.cbCode || currentPhase.value?.orgCode || '',
      folders,
      files
    })
    if (!initRes.Status && initRes.status !== 0) {
      throw new Error(initRes.Message || initRes.message || '预处理失败')
    }

    const manifest = initRes.Data || initRes.data
    taskId = manifest.TaskId || manifest.taskId
    const totalFiles = manifest.TotalFiles || manifest.totalFiles || 0
    const fileList = manifest.Files || manifest.files || []

    // 注册上传任务到队列
    const newTask = {
      taskId,
      directoryCode,
      name: currentPhase.value ? (currentPhase.value.label || currentPhase.value.phaseName || directoryCode) : directoryCode,
      status: 'uploading',
      totalFiles,
      uploadedFiles: 0,
      pendingFiles: totalFiles,
      percent: 0,
      failedFiles: 0,
      convertCount: 0,
      files: fileList.map(f => ({
        fileCode: f.FileCode || f.fileCode,
        fileName: f.FileName || f.fileName,
        status: 'pending',
        uploadProgress: 0
      }))
    }
    uploadTasks.value.push(newTask)
    // 尽早订阅实时进度（放在上传循环前，避免错过完成事件）
    subscribeToTask(taskId)

    let failed = false
    for (let i = 0; i < fileList.length; i++) {
      if (failed) break
      const enhancedFile = fileList[i]
      const localFile = uploadFileList.value[i]
      uploadProgress.currentFile = enhancedFile.FileName || enhancedFile.fileName
      uploadProgress.completed = i
      // 更新当前文件的上传进度
      const task = uploadTasks.value.find(t => t.taskId === taskId)
      if (task) {
        const f = task.files.find(f => f.fileCode === (enhancedFile.FileCode || enhancedFile.fileCode))
        if (f) f.uploadProgress = Math.round((i / fileList.length) * 100)
      }

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
          // 标记该文件为已上传
          if (task) {
            const f = task.files.find(f => f.fileCode === (enhancedFile.FileCode || enhancedFile.fileCode))
            if (f) { f.status = 'uploaded'; f.uploadProgress = 100 }
          }
        } else {
          failed = true
          uploadProgress.failed++
          if (task) {
            const f = task.files.find(f => f.fileCode === (enhancedFile.FileCode || enhancedFile.fileCode))
            if (f) f.status = 'failed'
          }
        }
      } catch {
        failed = true
        uploadProgress.failed++
        if (task) {
          const f = task.files.find(f => f.fileCode === (enhancedFile.FileCode || enhancedFile.fileCode))
          if (f) f.status = 'failed'
        }
      }
    }

    if (failed) {
      uploadProgress.status = 'done'
      await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`)
      ElMessage.error(`上传完成，${uploadProgress.failed} 个文件失败，已回滚`)
      // 无论 SignalR 是否送达，都同步更新队列状态
      const failedTask = uploadTasks.value.find(t => t.taskId === taskId)
      if (failedTask) {
        failedTask.status = 'failed'
        failedTask.failedFiles = uploadProgress.failed
      }
    } else {
      uploadProgress.completed = totalFiles
      uploadProgress.status = 'done'
      const confirmRes = await http.post(`/api/standard-directory/upload-confirm?taskId=${taskId}`)
      // 检查是否有文件需要转换（接口返回 PascalCase：Data.ConvertCount）
      const confirmData = confirmRes.Data || confirmRes.data
      const convertCount = confirmData && (confirmData.ConvertCount ?? confirmData.convertCount)
      if (convertCount > 0) {
        ElMessage.success(`上传成功，${convertCount} 个文件正在转换`)
        // 显示转换进度面板
        convertPanelRef.value?.start(taskId)
      } else {
        ElMessage.success(`全部 ${totalFiles} 个文件上传成功`)
      }
      // 兜底：确认成功后直接把队列任务标记为完成（不依赖 SignalR 是否送达）
      const doneTask = uploadTasks.value.find(t => t.taskId === taskId)
      if (doneTask) {
        doneTask.status = 'done'
        doneTask.uploadedFiles = doneTask.totalFiles
        doneTask.percent = 100
      }
      uploadFileList.value = []
      showUploadDialogFlag.value = false
      await loadCurrentContent()
      await refreshActiveQueue()
    }
  } catch (error) {
    console.error('上传流程异常:', error)
    uploadProgress.status = 'done'
    if (taskId) {
      try {
        await http.post(`/api/standard-directory/upload-cancel?taskId=${taskId}`)
      } catch {}
      const task = uploadTasks.value.find(t => t.taskId === taskId)
      if (task) { task.status = 'failed'; task.failedFiles = task.totalFiles }
    }
    ElMessage.error(error.message || '上传流程异常')
    await refreshActiveQueue()
  } finally {
    uploading.value = false
  }
}

// ========== 取消上传 ==========
const handleCancelUpload = async () => {
  if (!uploading.value) return
  try {
    await ElMessageBox.confirm('确定要取消当前上传任务吗？', '提示', { type: 'warning' })
    uploading.value = false
    uploadFileList.value = []
    uploadProgress.status = 'idle'
  } catch {}
}

// ========== 其他操作 ==========
const handleExport = async () => {
  if (!currentPhase.value) return
  if (selectedItems.size === 0) {
    ElMessage.warning('请先勾选需要导出的文件夹或文件')
    return
  }
  // 收集选中的文件夹编码和文件编码
  const folderCodes = []
  const fileCodes = []
  for (const code of selectedItems) {
    if (currentFolders.value.some((f) => (f.FolderCode || f.folderCode) === code)) {
      folderCodes.push(code)
    } else if (currentFiles.value.some((f) => (f.FileCode || f.fileCode) === code)) {
      fileCodes.push(code)
    }
  }
  try {
    await downloadBlobPost(
      `/api/standard-directory/configs/${buildDirectoryCode()}/export`,
      { folderCodes, fileCodes },
      `${buildDirectoryCode()}-export.zip`
    )
    ElMessage.success('导出成功')
  } catch (e) {
    ElMessage.error('导出失败：' + (e.message || e))
  }
}

const handleHelp = () => {
  showHelpDialog.value = true
}

const handleRefresh = async () => {
  await loadTree()
  if (currentPhase.value) {
    currentFolderCode.value = ''
    breadcrumbPath.value = []
    selectedItems.clear()
    allSelected.value = false
    await loadCurrentContent()
  }
  await refreshActiveQueue()
}

const replaceFile = (file) => {
  ElMessage.info('替换文件功能开发中')
}

const downloadFile = async (file) => {
  const storagePath = file.StoragePath || file.storagePath
  if (!storagePath) {
    ElMessage.warning('文件存储路径不存在')
    return
  }
  try {
    await downloadBlob(
      `/api/standard-directory/download?path=${encodeURIComponent(storagePath)}`,
      fileNameOf(file, 'download')
    )
  } catch (e) {
    ElMessage.error('下载失败：' + (e.message || e))
  }
}

// ========== 工具函数 ==========
const getFileIconClass = (fileName) => {
  if (!fileName) return 'file-default'
  const ext = fileName.split('.').pop().toLowerCase()
  if (['pdf'].includes(ext)) return 'file-pdf'
  if (['doc', 'docx'].includes(ext)) return 'file-doc'
  if (['xls', 'xlsx'].includes(ext)) return 'file-xls'
  if (['jpg', 'jpeg', 'png', 'gif', 'bmp'].includes(ext)) return 'file-image'
  return 'file-default'
}

onMounted(() => {
  loadTree()
  initSignalR()
  startPolling()
})

onUnmounted(() => {
  signalRConnection.value?.stop().catch(() => {})
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
})
</script>

<style scoped>
@import '@/yzh/styles/yzh.css';

.directory-manager {
  /* 与标准页面对齐：四周留白 + 浅灰背景，让 padding 一眼能看出来（纯白背景会视觉上吃掉 padding） */
  position: absolute;
  top: 16px;
  left: 24px;
  right: 24px;
  bottom: 16px;
  display: flex;
  overflow: hidden;
  background: var(--yzh-color-bg-page, #f5f7fa);
  gap: var(--yzh-space-gap, 16px);
  border-radius: var(--yzh-radius-sm, 4px);
}

/* 左侧面板：包一层白色卡片 + 圆角，与浅灰背景形成对比 */
.left-panel {
  width: 280px;
  height: 100%;
  overflow: hidden;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
}

.left-header {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  flex-shrink: 0;
}

.left-title {
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-text-primary, #303133);
  font-size: var(--yzh-font-size-md, 14px);
}

.search-box {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  flex-shrink: 0;
}

.tree-container {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  min-height: 0;
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

.tree-node:hover {
  background: var(--yzh-color-bg-hover, #f5f7fa);
}
.tree-node.active {
  background: var(--yzh-color-bg-active, #ecf5ff);
  color: var(--yzh-color-primary, #409eff);
}
.tree-node.level-0 {
  padding-left: 16px;
  font-weight: 500;
}
.tree-node.level-1 {
  padding-left: 36px;
}
.tree-node.level-2 {
  padding-left: 56px;
}

.tree-toggle {
  transition: transform 0.2s;
  color: var(--yzh-color-text-disabled, #c0c4cc);
}
.tree-toggle.expanded {
  transform: rotate(90deg);
}

.tree-icon {
  color: var(--yzh-color-text-secondary, #909399);
}
.tree-icon.org {
  color: var(--yzh-color-warning, #e6a23c);
}
.tree-icon.standard {
  color: var(--yzh-color-primary, #409eff);
}
.tree-icon.phase {
  color: var(--yzh-color-success, #67c23a);
}

.tree-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 右侧面板：同样包一层白色卡片 + 圆角 + 边框，与左卡片一致 */
.right-panel {
  flex: 1;
  height: 100%;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-card, #fff);
  min-width: 0;
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
}

/* 面包屑 */
.breadcrumb {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  flex-shrink: 0;
}

.clickable-breadcrumb {
  cursor: pointer;
  color: var(--yzh-color-primary, #409eff);
}
.clickable-breadcrumb:hover {
  text-decoration: underline;
}

/* 工具栏 */
.toolbar {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: nowrap;
  flex-shrink: 0;
}

/* 文件列表 - 填充剩余空间，溢出时滚动 */
.file-list-container {
  flex: 1;
  overflow-y: auto;
  min-height: 0;
}

.file-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

/* 固定宽度列 */
.file-table th:nth-child(1),
.file-table td:nth-child(1) {
  width: 44px;
}
.file-table th:nth-child(3),
.file-table td:nth-child(3) {
  width: 80px;
}
.file-table th:nth-child(4),
.file-table td:nth-child(4) {
  width: 140px;
}
.file-table th:nth-child(5),
.file-table td:nth-child(5) {
  width: 160px;
}
/* 名称列自动填充剩余空间 */
.file-table th:nth-child(2),
.file-table td:nth-child(2) {
  min-width: 0;
}

.file-table th {
  padding: 10px 16px;
  background: var(--yzh-color-bg-hover, #f5f7fa);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-text-regular, #606266);
  font-size: var(--yzh-font-size-sm, 13px);
  text-align: left;
  position: sticky;
  top: 0;
  z-index: 1;
}

.file-table td {
  padding: 0 16px;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  vertical-align: middle;
  height: 48px;
}

.file-table tr:hover {
  background: var(--yzh-color-bg-hover, #f5f7fa);
}
.file-table tr.selected {
  background: var(--yzh-color-bg-active, #ecf5ff);
}

.name-cell {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.name-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.folder-name {
  color: var(--yzh-color-text-primary, #303133);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.folder-icon {
  color: var(--yzh-color-warning, #e6a23c);
  font-size: 18px;
  flex-shrink: 0;
}

.file-type-icon {
  color: var(--yzh-color-text-secondary, #909399);
  font-size: 18px;
  flex-shrink: 0;
}
.file-type-icon.file-pdf {
  color: var(--yzh-color-danger, #f56c6c);
}
.file-type-icon.file-doc {
  color: var(--yzh-color-primary, #409eff);
}
.file-type-icon.file-xls {
  color: var(--yzh-color-success, #67c23a);
}
.file-type-icon.file-image {
  color: var(--yzh-color-text-secondary, #909399);
}

.size-cell,
.date-cell {
  color: var(--yzh-color-text-secondary, #909399);
}

.action-cell {
  white-space: nowrap;
}

/* 空状态（列表内部使用 compact 模式） */
.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 上传区域 */
.upload-tabs {
  display: flex;
  justify-content: center;
  margin-bottom: 20px;
}
.upload-secondary-btn {
  display: flex;
  justify-content: center;
  margin-top: 12px;
}

.upload-area {
  border: 2px dashed var(--yzh-color-border, #dcdfe6);
  border-radius: var(--yzh-radius-lg, 8px);
  padding: var(--yzh-space-8, 32px);
  text-align: center;
  cursor: pointer;
  margin-bottom: var(--yzh-space-4, 16px);
}
.upload-area:hover {
  border-color: var(--yzh-color-primary, #409eff);
  background: var(--yzh-color-bg-active, #ecf5ff);
}

.upload-trigger {
  display: flex;
  flex-direction: column;
  align-items: center;
}
.upload-trigger.is-dragging {
  transform: scale(1.02);
}
.upload-area:has(.is-dragging) {
  border-color: var(--yzh-color-primary, #409eff);
  background: var(--yzh-color-bg-active, #ecf5ff);
}
.upload-icon {
  font-size: 48px;
  color: var(--yzh-color-text-secondary, #909399);
  margin-bottom: var(--yzh-space-3, 12px);
}
.upload-text {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--yzh-color-text-primary, #303133);
  margin-bottom: var(--yzh-space-2, 8px);
}
.upload-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-disabled, #c0c4cc);
}

.upload-file-list {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-sm, 4px);
  max-height: 200px;
  overflow-y: auto;
}

.file-list-header-sm {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: var(--yzh-color-bg-hover, #fafafa);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.file-list-item-sm {
  display: flex;
  align-items: center;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border-lighter, #f0f0f0);
  font-size: var(--yzh-font-size-sm, 13px);
}

.file-item-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.file-item-size {
  color: var(--yzh-color-text-secondary, #909399);
  margin: 0 12px;
  font-size: var(--yzh-font-size-xs, 12px);
}

.upload-progress-area {
  margin-top: var(--yzh-space-4, 16px);
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-hover, #fafafa);
  border-radius: var(--yzh-radius-sm, 4px);
}
.progress-info {
  margin-bottom: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}
.text-success {
  color: var(--yzh-color-success, #67c23a);
}
.text-danger {
  color: var(--yzh-color-danger, #f56c6c);
}

/* 帮助内容 */
.help-content h4 {
  color: var(--yzh-color-text-primary, #303133);
  font-size: var(--yzh-font-size-md, 14px);
  margin: 16px 0 8px 0;
  padding-bottom: 6px;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.help-content h4:first-child {
  margin-top: 0;
}
.help-content p {
  color: var(--yzh-color-text-regular, #606266);
  line-height: var(--yzh-line-height-base, 1.6);
  margin-bottom: 8px;
}
.help-content ul {
  color: var(--yzh-color-text-regular, #606266);
  line-height: 1.8;
  padding-left: 20px;
  margin-bottom: 8px;
}
.help-content li {
  margin-bottom: 4px;
}
.help-content strong {
  color: var(--yzh-color-text-primary, #303133);
}

.code-example {
  background: var(--yzh-color-bg-page, #f5f7fa);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-sm, 4px);
  padding: var(--yzh-space-3, 12px);
  font-family: monospace;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  line-height: 1.8;
  margin-bottom: var(--yzh-space-3, 12px);
}

/* 队列执行中状态条 */
.queue-status-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
  padding: 6px 12px;
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  border: 1px solid var(--yzh-color-primary-light-7, #b3d8ff);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: 13px;
}
.queue-status-bar .queue-name {
  font-weight: 600;
  color: var(--yzh-color-primary, #409eff);
  white-space: nowrap;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
}
.queue-status-bar .queue-count {
  font-size: 12px;
  color: var(--yzh-color-text-secondary, #909399);
  white-space: nowrap;
}

/* 文件名后状态图标 */
.status-icon {
  margin-left: 4px;
  flex-shrink: 0;
}

/* 通用旋转动画 */
.is-spinning {
  animation: yzh-spin 1s linear infinite;
}
@keyframes yzh-spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}

/* 弹窗 — 不用 scoped，因为 el-dialog teleport 到 body */
</style>

<!-- 非 scoped 样式：dialog teleport 到 body，scoped 无法命中 -->
<style>
/* 标题栏内边距 */
.el-dialog__header {
  padding: 20px 20px 10px;
  margin-right: 0;
}
/* 内容区内边距 */
.el-dialog__body {
  padding: 10px 20px 20px;
}
/* 底部按钮栏内边距 */
.el-dialog__footer {
  padding: 0 20px 20px;
}

/* 弹窗表单：标签不折行 */
.dialog-form {
  .el-form-item__label {
    white-space: nowrap;
  }
}
</style>
