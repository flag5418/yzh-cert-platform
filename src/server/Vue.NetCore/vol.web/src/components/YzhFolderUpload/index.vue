<template>
  <div
    class="yzh-folder-upload"
    @dragover.prevent="onDragOver"
    @dragenter.prevent="onDragEnter"
    @dragleave.prevent="onDragLeave"
    @drop.prevent="onDrop"
    :class="{ 'is-active': isDragging, 'is-disabled': disabled }"
  >
    <!-- 拖拽区域 -->
    <div class="upload-drop-zone" :class="{ 'is-dragging': isDragging }">
      <el-icon class="upload-icon">
        <IconUpload />
      </el-icon>
      <div class="upload-text">
        <template v-if="isDragging">
          <span class="drag-hint">✨ 松开鼠标以上传</span>
        </template>
        <template v-else>
          将文件或<em>文件夹</em>拖到此处，或<em>点击下方按钮选择</em>
        </template>
      </div>
      <div class="upload-hint">支持多文件、多文件夹同时拖拽上传</div>
    </div>

    <!-- 操作按钮组 -->
    <div class="upload-actions">
      <el-button-group class="action-buttons">
        <el-button size="small" @click="triggerFileSelect" :disabled="disabled || uploading">
          <el-icon><IconDocument /></el-icon> 选择文件
        </el-button>
        <el-button size="small" type="primary" @click="triggerFolderSelect" :disabled="disabled || uploading">
          <el-icon><IconFolderAdd /></el-icon> 选择文件夹
        </el-button>
      </el-button-group>
    </div>

    <!-- 隐藏的 input 元素 -->
    <input
      ref="fileInputRef"
      type="file"
      multiple
      :accept="accept"
      style="display: none"
      @change="onFileSelect"
    />
    <input
      ref="folderInputRef"
      type="file"
      webkitdirectory
      multiple
      style="display: none"
      @change="onFolderSelect"
    />

    <!-- 文件列表 -->
    <div v-if="internalFileList.length > 0" class="file-list-container">
      <div class="file-list-header">
        <div class="header-left">
          <el-icon><IconFile /></el-icon>
          <span>已选择 <strong>{{ internalFileList.length }}</strong> 个文件</span>
        </div>
        <el-button type="danger" link size="small" @click="clearFiles" :disabled="uploading">
          <el-icon><IconDelete /></el-icon> 清空
        </el-button>
      </div>

      <div class="file-list" :style="{ maxHeight: listMaxHeight + 'px', overflowY: 'auto' }">
        <div v-for="(file, index) in internalFileList" :key="index" class="file-item">
          <div class="file-info">
            <el-icon class="file-icon"><IconDocument /></el-icon>
            <div class="file-details">
              <div class="file-name" :title="file.webkitRelativePath || file.name">
                {{ file.name }}
              </div>
              <div class="file-meta">
                <span class="file-size">{{ formatFileSize(file.size) }}</span>
                <span v-if="file.webkitRelativePath && file.webkitRelativePath.includes('/')" class="file-path-badge">
                  📁 {{ file.webkitRelativePath.substring(0, file.webkitRelativePath.lastIndexOf('/')) }}
                </span>
              </div>
            </div>
          </div>
          <el-button type="danger" link size="small" @click="removeFile(index)" :disabled="uploading">
            <el-icon><IconClose /></el-icon>
          </el-button>
        </div>
      </div>

      <div class="file-stats-bar">
        <span>总大小：{{ formatFileSize(totalSize) }}</span>
        <el-tag size="small" type="info">{{ internalFileList.length }} / {{ maxFiles }}</el-tag>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { ElMessage } from 'element-plus'
import {
  IconUpload,
  IconDocument,
  IconFolderAdd,
  IconFile,
  IconDelete,
  IconClose,
} from '@/yzh'

const props = defineProps({
  accept: { type: String, default: '' },
  maxSize: { type: Number, default: 50 * 1024 * 1024 },
  maxFiles: { type: Number, default: 100 },
  disabled: { type: Boolean, default: false },
  uploading: { type: Boolean, default: false },
  listMaxHeight: { type: Number, default: 250 },
  modelValue: { type: Array, default: () => [] }
})

const emit = defineEmits(['update:modelValue', 'change', 'exceed', 'error'])

const fileInputRef = ref(null)
const folderInputRef = ref(null)
const isDragging = ref(false)

const internalFileList = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

const totalSize = computed(() => 
  internalFileList.value.reduce((sum, f) => sum + (f.size || 0), 0)
)

// 拖拽事件
const onDragOver = (e) => { if (!props.disabled) e.dataTransfer.dropEffect = 'copy' }
const onDragEnter = (e) => { if (!props.disabled) isDragging.value = true }
const onDragLeave = (e) => { 
  if (!e.currentTarget.contains(e.relatedTarget)) isDragging.value = false 
}

const onDrop = async (event) => {
  if (props.disabled) return
  isDragging.value = false
  
  const items = event.dataTransfer?.items
  if (!items?.length) {
    ElMessage.warning('未检测到有效的文件')
    return
  }

  const files = []
  
  for (let i = 0; i < items.length; i++) {
    const item = items[i]
    if (item.kind === 'file') {
      const entry = item.webkitGetAsEntry?.()
      if (entry?.isDirectory) {
        await traverseDirectory(entry, entry.name + '/', files)
      } else {
        const file = item.getAsFile()
        if (file) files.push(file)
      }
    }
  }

  if (files.length > 0) {
    addFiles(files)
    ElMessage.success(`已添加 ${files.length} 个文件`)
  }
}

// 递归遍历文件夹
const traverseDirectory = async (entry, path, fileList) => {
  return new Promise((resolve) => {
    const reader = entry.createReader()
    const readEntries = () => {
      reader.readEntries(async (entries) => {
        if (!entries.length) { resolve(); return }
        for (const e of entries) {
          if (e.isFile) {
            e.file(f => { 
              if (path) Object.defineProperty(f, 'webkitRelativePath', { value: path + f.name, writable: false })
              fileList.push(f) 
            }, () => {})
          } else if (e.isDirectory) {
            await traverseDirectory(e, path + e.name + '/', fileList)
          }
        }
        readEntries()
      }, resolve)
    }
    readEntries()
  })
}

// 文件选择
const triggerFileSelect = () => { if (!props.disabled && !props.uploading) fileInputRef.value?.click() }
const triggerFolderSelect = () => { if (!props.disabled && !props.uploading) folderInputRef.value?.click() }

const onFileSelect = (e) => {
  addFiles(Array.from(e.target.files || []))
  e.target.value = ''
}

const onFolderSelect = (e) => {
  const files = Array.from(e.target.files || [])
  if (files.length) {
    addFiles(files)
    ElMessage.success(`已添加 ${files.length} 个文件`)
  }
  e.target.value = ''
}

// 文件列表管理
const addFiles = (files) => {
  const available = props.maxFiles - internalFileList.value.length
  if (available <= 0) {
    ElMessage.warning(`已达到最大文件数限制 (${props.maxFiles})`)
    return
  }

  const valid = files.filter(f => f.size <= props.maxSize).slice(0, available)
  if (valid.length) {
    internalFileList.value = [...internalFileList.value, ...valid]
    emit('change', internalFileList.value)
  }
}

const removeFile = (index) => {
  if (index >= 0 && index < internalFileList.value.length) {
    const list = internalFileList.value.filter((_, i) => i !== index)
    internalFileList.value = list
    emit('change', list)
  }
}

const clearFiles = () => {
  if (internalFileList.value.length) {
    internalFileList.value = []
    emit('change', [])
  }
}

// 工具方法
const formatFileSize = (bytes) => {
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return `${(bytes / Math.pow(1024, i)).toFixed(i > 0 ? 1 : 0)} ${units[i]}`
}

defineExpose({ clearFiles, removeFile, triggerFileSelect, triggerFolderSelect })
</script>

<style scoped>
.yzh-folder-upload { width: 100%; border-radius: 8px; }
.yzh-folder-upload.is-disabled { opacity: 0.6; pointer-events: none; }

.upload-drop-zone {
  border: 2px dashed #dcdfe6;
  border-radius: 8px;
  padding: 40px 20px;
  text-align: center;
  background: #fafafa;
  cursor: pointer;
  transition: all 0.3s ease;
}
.upload-drop-zone:hover { border-color: #a0cfff; background: #ecf5ff; }
.upload-drop-zone.is-dragging {
  border-color: #409eff;
  background: linear-gradient(135deg, #ecf5ff 0%, #d9ecff 100%);
  transform: scale(1.02);
}
.upload-icon { font-size: 48px; color: #909399; margin-bottom: 16px; transition: all 0.3s; }
.upload-drop-zone.is-dragging .upload-icon { color: #409eff; transform: translateY(-8px); }
.upload-text { font-size: 15px; color: #303133; margin-bottom: 8px; }
.upload-text em { color: #409eff; font-style: normal; font-weight: 500; }
.drag-hint { color: #409eff; font-weight: 500; animation: pulse 1.5s infinite; }
@keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }
.upload-hint { font-size: 13px; color: #909399; margin-top: 8px; }

.upload-actions { margin-top: 20px; text-align: center; }
.action-buttons { display: inline-flex; }

.file-list-container { margin-top: 20px; border: 1px solid #e4e7ed; border-radius: 4px; overflow: hidden; background: #fff; }
.file-list-header {
  display: flex; justify-content: space-between; align-items: center;
  padding: 12px 16px; background: #f5f7fa; border-bottom: 1px solid #e4e7ed;
}
.header-left { display: flex; align-items: center; gap: 8px; font-size: 14px; color: #606266; }
.header-left strong { color: #409eff; }

.file-list { padding: 8px 0; }
.file-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 10px 16px; transition: background 0.2s; border-bottom: 1px solid #f0f0f0;
}
.file-item:hover { background: #f5f7fa; }
.file-item:last-child { border-bottom: none; }

.file-info { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 0; }
.file-icon { font-size: 24px; color: #909399; flex-shrink: 0; }
.file-details { flex: 1; min-width: 0; }
.file-name { font-size: 14px; color: #303133; font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.file-meta { display: flex; gap: 12px; margin-top: 4px; font-size: 12px; color: #909399; }
.file-size { font-family: monospace; }
.file-path-badge {
  display: inline-flex; align-items: center; gap: 4px; padding: 2px 8px;
  background: #f5f7fa; border-radius: 10px; font-size: 11px; max-width: 200px;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}

.file-stats-bar {
  display: flex; justify-content: space-between; align-items: center;
  padding: 10px 16px; background: #f5f7fa; border-top: 1px solid #e4e7ed; font-size: 13px; color: #606266;
}
</style>
