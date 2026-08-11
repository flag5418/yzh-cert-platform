<template>
  <div class="doc-extraction-rule">
    <!-- 顶部导航 -->
    <div class="page-header">
      <div class="page-title">
        <el-icon><Document /></el-icon>
        <span>文档提取规则管理</span>
      </div>
      <div class="page-actions">
        <el-button @click="goBack">返回列表</el-button>
        <el-button type="primary" @click="saveRule" :loading="saving">保存规则</el-button>
      </div>
    </div>

    <!-- 三栏布局 -->
    <div class="main-container">
      <!-- 左侧：文件树 -->
      <div class="left-panel" :style="{ width: leftPanelWidth + 'px' }">
        <FileTree
          :data="fileTreeData"
          :current-file="currentFile"
          @select="onFileSelect"
          @load-phase="onLoadPhase"
        />
      </div>

      <!-- 左侧拖拽调整宽度 -->
      <div class="resize-handle resize-handle-left" @mousedown.prevent="startResizeLeft">
        <div class="resize-bar"></div>
      </div>

      <!-- 中间：文档预览 -->
      <div class="center-panel">
        <DocPreview v-if="currentFile" :file="currentFile" />
        <div v-else class="empty-preview">
          <el-icon :size="64"><Document /></el-icon>
          <p>请选择左侧文档进行预览</p>
        </div>
      </div>

      <!-- 右侧：操作区 -->
      <div class="right-panel">
        <!-- 状态栏 -->
        <div class="status-bar" v-if="currentFile">
          <div class="status-item">
            <span class="label">规则状态：</span>
            <el-tag :type="ruleStatusType">{{ ruleStatusText }}</el-tag>
          </div>
          <div class="status-item">
            <span class="label">字段数：</span>
            <span class="value">{{ fieldCount }}个</span>
          </div>
          <div class="status-item">
            <span class="label">表格数：</span>
            <span class="value">{{ tableCount }}个</span>
          </div>
        </div>

        <!-- Tab 切换 -->
        <el-tabs v-model="activeTab" class="right-tabs">
          <el-tab-pane label="🔍 自动分析" name="analysis">
            <AIAnalysisTab
              :fields="analysisFields"
              :tables="analysisTables"
              :raw-json="rawJsonDisplay"
              @analyze="onAIAnalyze"
              @update:fields="onFieldsUpdate"
              @update:tables="onTablesUpdate"
            />
          </el-tab-pane>
          <el-tab-pane label="⚡ 提示词与验证" name="prompt">
            <PromptVerifyTab
              :prompt="generatedPrompt"
              :verify-result="verifyResult"
              @generate="onGeneratePrompt"
              @verify="onVerifyPrompt"
              @update:prompt="onPromptUpdate"
            />
          </el-tab-pane>
        </el-tabs>

        <!-- 底部操作（仅在提示词Tab显示） -->
        <div class="bottom-actions" v-if="activeTab === 'prompt'">
          <el-button @click="cancel">取消</el-button>
          <el-button type="primary" @click="saveRule" :loading="saving"> 保存规则 </el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import http from '@/api/http'
import { Document } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import AIAnalysisTab from './components/AIAnalysisTab.vue'
import { aiAnalyzeDocument } from './api'
import DocPreview from './components/DocPreview.vue'
import FileTree from './components/FileTree.vue'
import PromptVerifyTab from './components/PromptVerifyTab.vue'

const router = useRouter()

// 状态
const activeTab = ref('analysis')
const leftPanelWidth = ref(280) // 左侧面板默认宽度
const isResizing = ref(false) // 是否正在拖拽调整宽度
const currentFile = ref(null)
const saving = ref(false)

// 文件树数据（从现有接口获取）
const fileTreeData = ref([])

// AI分析结果
const analysisFields = ref([])
const analysisTables = ref([])

// Prompt和验证结果
const generatedPrompt = ref('')
const verifyResult = ref(null)
const rawJsonDisplay = ref('')

// 规则状态
const ruleStatus = ref('none') // none, configured, failed

// 计算属性
const ruleStatusType = computed(() => {
  const map = { none: 'info', configured: 'success', failed: 'danger' }
  return map[ruleStatus.value] || 'info'
})

const ruleStatusText = computed(() => {
  const map = { none: '未配置', configured: '已配置', failed: '配置失败' }
  return map[ruleStatus.value] || '未知'
})

const fieldCount = computed(() => analysisFields.value.length)
const tableCount = computed(() => analysisTables.value.length)

// 方法
const goBack = () => {
  router.back()
}

const onFileSelect = (file) => {
  console.info('[DocExtractionRule] ✅ onFileSelect 触发:',
    { id: file?.id, name: file?.name, type: file?.type, storagePath: file?.storagePath, mimeType: file?.mimeType })
  currentFile.value = file
  // 重置状态
  analysisFields.value = []
  analysisTables.value = []
  generatedPrompt.value = ''
  verifyResult.value = null
  activeTab.value = 'analysis'
}

/**
 * 点击阶段节点时触发
 * @param {Object} phase - 阶段节点数据，包含 id, name, directoryCode 等
 */
const onLoadPhase = async (phase) => {
  console.log('🎯 点击阶段:', phase.name, phase)

  // 防止重复加载
  if (phase._loaded) {
    console.log('⏭️ 已加载过，跳过')
    return
  }

  // 标记加载中
  phase._loading = true

  try {
    await loadStageFiles(phase)
    phase._loaded = true
  } catch (error) {
    console.error('❌ 加载阶段文件失败:', error)
    ElMessage.error('加载文件列表失败')
  } finally {
    phase._loading = false
  }
}

const onAIAnalyze = async () => {
  if (!currentFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  const fileCode = currentFile.value.fileCode
  // 根据 mimeType 推断 skill
  const mimeType = currentFile.value.mimeType || ''
  let skill = 'word'
  if (mimeType.includes('excel') || fileCode.toLowerCase().endsWith('.xlsx') || fileCode.toLowerCase().endsWith('.xls'))
    skill = 'excel'
  else if (mimeType.includes('pdf') || fileCode.toLowerCase().endsWith('.pdf'))
    skill = 'pdf'

  console.log('[DocExtractionRule] 🔍 开始分析:', { fileCode, skill })
  ElMessage.info('AI分析中...')

  try {
    const res = await aiAnalyzeDocument({ fileCode, skill })
    console.log('[DocExtractionRule] 📦 analyze 响应:', JSON.stringify(res, null, 2))

    // 解析响应数据
    const data = res?.Data ?? res?.data ?? res
    if (data?.fields) {
      analysisFields.value = data.fields.map(f => ({
        name: f.fieldName ?? f.name ?? f.field_code ?? '',
        dataType: f.dataType ?? 'string',
        description: f.description ?? '',
        isManual: f.isManual ?? false
      }))
    }
    if (data?.tables) {
      analysisTables.value = data.tables.map(t => ({
        name: t.tableName ?? t.name ?? t.table_code ?? '',
        description: t.description ?? '',
        columns: (t.columns ?? []).map(c => ({
          name: c.columnName ?? c.name ?? c.column_code ?? '',
          dataType: c.dataType ?? 'string'
        }))
      }))
    }
    // 原始JSON展示
    rawJsonDisplay.value = JSON.stringify(data ?? res, null, 2)
    ElMessage.success('分析完成')
  } catch (err) {
    console.error('[DocExtractionRule] ❌ 分析失败:', err)
    ElMessage.error('AI分析失败: ' + (err?.message ?? '未知错误'))
  }
}

const onFieldsUpdate = (fields) => {
  analysisFields.value = fields
}

const onTablesUpdate = (tables) => {
  analysisTables.value = tables
}

const onGeneratePrompt = async () => {
  // TODO: 调用后端生成Prompt接口
  ElMessage.info('生成Prompt中...')
}

const onVerifyPrompt = async () => {
  // TODO: 调用后端验证接口
  ElMessage.info('验证中...')
}

const onPromptUpdate = (prompt) => {
  generatedPrompt.value = prompt
}

const saveRule = async () => {
  saving.value = true
  try {
    // TODO: 调用后端保存接口
    ElMessage.success('规则保存成功')
    ruleStatus.value = 'configured'
  } finally {
    saving.value = false
  }
}

const cancel = () => {
  router.back()
}

// ====== 左侧面板拖拽调整宽度 ======
const startResizeLeft = (e) => {
  isResizing.value = true
  const startX = e.clientX
  const startWidth = leftPanelWidth.value

  const onMouseMove = (e) => {
    const delta = e.clientX - startX
    // 限制最小200px，最大600px
    const newWidth = Math.max(200, Math.min(600, startWidth + delta))
    leftPanelWidth.value = newWidth
  }

  const onMouseUp = () => {
    isResizing.value = false
    document.removeEventListener('mousemove', onMouseMove)
    document.removeEventListener('mouseup', onMouseUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }

  document.addEventListener('mousemove', onMouseMove)
  document.addEventListener('mouseup', onMouseUp)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

// 初始化加载文件树
const loadFileTree = async () => {
  try {
    const res = await http.get('/api/standard-directory/organization-tree')
    if (res.Status === true || res.status === 0) {
      // 转换数据格式为组件需要的结构
      const data = res.Data || res.data || []
      fileTreeData.value = transformTreeData(data)
    } else {
      ElMessage.error(res.Message || '加载文件树失败')
    }
  } catch (error) {
    console.error('加载文件树失败:', error)
    ElMessage.error('加载文件树失败')
  }
}

/**
 * 转换组织树数据（机构→标准→阶段）
 * 阶段节点的 children 为空，等待用户点击时懒加载
 */
const transformTreeData = (data) => {
  return data.map((org) => ({
    id: org.id,
    name: org.label || org.name,
    type: 'organization',
    children: (org.children || []).map((std) => ({
      id: std.id,
      name: std.label || std.name,
      type: 'standard',
      // 提取标准编码，用于构建目录编码
      standardCode: std.code || std.id,
      children: (std.children || []).map((phase) => ({
        id: phase.id,
        name: phase.label || phase.name,
        type: 'stage',
        // 构建目录编码，用于请求阶段文件
        directoryCode: extractDirectoryCode(phase.id),
        children: [], // 初始为空，点击时懒加载
        _loaded: false, // 是否已加载
        _loading: false // 是否正在加载
      }))
    }))
  }))
}

/**
 * 加载阶段的完整文件树（单次请求）
 * 后端返回包含文件夹、文件及其规则属性的完整JSON
 *
 * @param {Object} stageNode - 阶段节点（会被直接修改以添加 children）
 */
const loadStageFiles = async (stageNode) => {
  // 从 stageNode 获取目录编码
  const directoryCode = stageNode.directoryCode || extractDirectoryCode(stageNode.id)

  if (!directoryCode) {
    throw new Error('无法获取目录编码')
  }

  console.log('📡 请求阶段文件树:', directoryCode)
  console.log(
    '📡 stageNode:',
    JSON.stringify({
      id: stageNode.id,
      name: stageNode.name,
      directoryCode: stageNode.directoryCode
    })
  )

  // 单次请求获取完整的文件夹+文件树（含规则属性）
  const res = await http.get(`/api/standard-directory/stage-files/${directoryCode}`)

  console.log('📡 接口响应:', res)

  if (!(res.Status === true || res.status === 0)) {
    throw new Error(res.Message || '加载失败')
  }

  const data = res.Data || res.data
  console.log('✅ 收到完整文件树，folders数量:', data?.Folders?.length || data?.length || 0)

  // 转换并挂载到阶段节点下
  const treeData = transformStageFileTree(data.Folders || data || [])
  console.log('✅ 转换后的树数据:', treeData)

  // 使用 splice 触发 Vue 响应式更新
  if (Array.isArray(stageNode.children)) {
    stageNode.children.splice(0, stageNode.children.length, ...treeData)
  } else {
    stageNode.children = treeData
  }

  console.log(`📁 阶段 [${stageNode.name}] 已加载 ${countNodes(treeData)} 个节点`)
}

/**
 * 从阶段 ID 中提取目录编码
 * 支持格式: "CB001|ISO 13485:2016|STAGE-01" 或直接传入 directoryCode
 *
 * 注意：阶段代码可能包含横杠（STAGE-01）或不包含（STAGE01）
 * 需要统一移除特殊字符以匹配后端存储格式
 */
const extractDirectoryCode = (stageId) => {
  if (!stageId) return null

  // 如果已经是目录编码格式（SDC-开头），直接返回
  if (stageId.startsWith('SDC-')) {
    return stageId
  }

  // 尝试从 "机构|标准|阶段" 格式解析
  const parts = stageId.split('|')
  if (parts.length >= 3) {
    // 标准代码：移除冒号、横杠、空格
    const standardCode = parts[1].replace(/[:\-\s]/g, '')
    // 阶段代码：移除横杠、空格（STAGE-01 → STAGE01）
    const phaseCode = parts[2].replace(/[\-\s]/g, '')
    return `SDC-${standardCode}|${phaseCode}`
  }

  return null
}

/**
 * 转换后端返回的文件树为前端组件格式
 * 后端 StageFolderNode 结构:
 * {
 *   Code, Name, ParentCode, Depth, SortOrder,
 *   Children: StageFolderNode[],  // 子文件夹
 *   Files: StageFileNode[]        // 该文件夹下的文件
 * }
 */
const transformStageFileTree = (folderNodes, depth = 0) => {
  if (!Array.isArray(folderNodes)) return []

  const result = []

  for (const folder of folderNodes) {
    // 文件夹节点
    const folderNode = {
      id: folder.Code || `folder-${depth}-${result.length}`,
      name: folder.Name || `文件夹${result.length + 1}`,
      type: 'folder',
      // 规则相关属性
      ruleStatus: 'none', // 文件夹本身没有规则状态
      // 原始数据
      _raw: folder,
      children: [] // 子节点（子文件夹 + 文件混合）
    }

    // 1. 递归处理子文件夹
    if (folder.Children && folder.Children.length > 0) {
      const childFolders = transformStageFileTree(folder.Children, depth + 1)
      folderNode.children.push(...childFolders)
    }

    // 2. 处理该文件夹下的文件
    if (folder.Files && folder.Files.length > 0) {
      const fileNodes = folder.Files.map((file, idx) => ({
        id: file.FileCode || `file-${depth}-${idx}`,
        fileCode: file.FileCode || `file-${depth}-${idx}`,
        name: file.FileName || `文件${idx + 1}`,
        type: 'file',
        // 规则相关属性（后端返回）
        ruleStatus: file.RuleStatus || 'none',
        extractFieldCount: file.ExtractFieldCount || 0,
        tableDefCount: file.TableDefCount || 0,
        // 文件属性
        storagePath: file.StoragePath,
        convertedStoragePath: file.ConvertedStoragePath,
        mimeType: file.MimeType,
        fileSize: file.FileSize,
        // 原始数据
        _raw: file
      }))
      folderNode.children.push(...fileNodes)
    }

    result.push(folderNode)
  }

  return result
}

/** 统计树节点数量 */
const countNodes = (nodes) => {
  if (!Array.isArray(nodes)) return 0
  return nodes.reduce((sum, node) => {
    return sum + 1 + countNodes(node.children)
  }, 0)
}

// 转换文件夹树为树形组件需要的格式
const transformFolderTree = (nodes, depth = 0) => {
  return (nodes || []).map((node, index) => {
    // 判断是否为文件：优先看 FileName 是否存在（文件一定有文件名）
    // 其次看 FileCode 是否存在
    const hasFileName = !!(node.FileName || node.fileName)
    const hasFileCode = !!(node.FileCode || node.fileCode)
    const hasChildren = Array.isArray(node.Children) && node.Children.length > 0

    // 判断逻辑：有文件名或有FileCode且无子节点的为文件
    const isFile = hasFileName || (hasFileCode && !hasChildren)

    // 获取名称 - 根据类型选择字段
    let name
    if (isFile) {
      // 文件使用 FileName
      name = node.FileName || node.fileName || `文件${index + 1}`
    } else {
      // 文件夹使用 Name 或 Code
      name = node.Name || node.name || node.Code || node.code || `文件夹${index + 1}`
    }

    // 获取 ID - 根据类型选择字段
    let id
    if (isFile) {
      id = node.FileCode || node.fileCode || node.Code || node.code || `file-${depth}-${index}`
    } else {
      id =
        node.FolderCode || node.folderCode || node.Code || node.code || `folder-${depth}-${index}`
    }

    console.log(`[深度${depth}] 节点:`, {
      name,
      isFile,
      id,
      hasFileName,
      hasFileCode,
      hasChildren,
      Children数量: node.Children?.length || 0
    })

    const baseData = {
      id: id,
      name: name,
      type: isFile ? 'file' : 'folder',
      fileCode: node.FileCode || node.fileCode,
      folderCode: node.FolderCode || node.folderCode,
      ruleStatus: node.RuleStatus || node.ruleStatus || 'none'
    }

    // 如果有子节点，递归转换（一次性加载所有层级）
    if (hasChildren) {
      baseData.children = transformFolderTree(node.Children, depth + 1)
    }

    return baseData
  })
}

// ====== 以下方法已废弃，保留供参考 ======
// updatePhaseFiles - 已被 loadStageFiles 替代
// transformFolderTree - 已被 transformStageFileTree 替代
// ======================================

onMounted(() => {
  loadFileTree()
})
</script>

<style scoped>
.doc-extraction-rule {
  /* Vol 框架 el-scrollbar__view 等父链元素 flex 子项无高度 → height:100% 失效。
     用 absolute + 四周 16/24/16/24 定位直接填充满外层分配容器，与 DirectoryManager 一致。*/
  position: absolute;
  top: 16px;
  left: 24px;
  right: 24px;
  bottom: 16px;
  display: flex;
  flex-direction: column;
  background: #f5f7fa;
  gap: 12px;
  border-radius: 4px;
  overflow: hidden;
}

/* 顶部导航 - 更精致的阴影和层次 */
.page-header {
  flex-shrink: 0;
  height: 56px;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.04);
  z-index: 10;
  border-radius: 4px 4px 0 0;
}

.page-title {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
}

.page-title :deep(.el-icon) {
  font-size: 20px;
  color: #409eff;
}

.main-container {
  /* 在 absolute 填充满的根容器下，flex:1 + min-height:0 确保剩余高度严格等分 */
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
  gap: 0;
}

/* 左侧面板 - 更细腻的背景 */
.left-panel {
  min-width: 200px;
  max-width: 600px;
  height: 100%; /* 与 main-container 严格对齐 */
  background: #fff;
  border: 1px solid #e4e7ed;
  border-right: none;
  border-radius: 0;
  overflow-y: auto;
  box-shadow: 2px 0 8px rgba(0, 0, 0, 0.02);
  flex-shrink: 0;
  box-sizing: border-box;
}

/* 拖拽调整宽度手柄 */
.resize-handle {
  width: 6px;
  height: 100%;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  z-index: 5;
  transition: background-color 0.2s;
  background: #fff;
  border-top: 1px solid #e4e7ed;
  border-bottom: 1px solid #e4e7ed;
}

.resize-handle:hover,
.resize-handle:active {
  background-color: rgba(64, 158, 255, 0.15);
}

.resize-bar {
  width: 3px;
  height: 40px;
  background: #dcdfe6;
  border-radius: 3px;
  transition: all 0.2s;
}

.resize-handle:hover .resize-bar,
.resize-handle:active .resize-bar {
  background: #409eff;
  height: 50px;
}

/* 中间面板 - 更柔和的背景 */
.center-panel {
  flex: 1;
  min-width: 0;
  height: 100%;
  background: #f5f7fa;
  display: flex;
  flex-direction: column;
  padding: 0 20px; /* 左右留白 20；上下留白由外层根容器的 top/bottom 16 + page-header 统一管，避免上下重复加 padding */
  box-sizing: border-box;
  overflow: hidden;
}

.empty-preview {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #909399;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-radius: 0;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.04);
}

/* 右侧面板 - 更现代的卡片效果 */
.right-panel {
  width: 480px;
  height: 100%;
  background: #fff;
  border: 1px solid #e4e7ed;
  border-left: none;
  display: flex;
  flex-direction: column;
  box-shadow: -2px 0 8px rgba(0, 0, 0, 0.02);
  flex-shrink: 0;
  box-sizing: border-box;
}

/* 状态栏 - 更精致的设计 */
.status-bar {
  flex-shrink: 0;
  padding: 14px 20px;
  background: linear-gradient(to right, #f5f7fa, #fff);
  border-bottom: 1px solid #e4e7ed;
  display: flex;
  gap: 32px;
}

.status-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.status-item .label {
  color: #606266;
}

.status-item .value {
  font-weight: 600;
  color: #303133;
}

/* Tab 样式优化 */
.right-tabs {
  flex: 1;
  min-height: 0; /* 与根容器 min-height:0 链衔接，允许 Tabs 内容区收缩 */
  display: flex;
  flex-direction: column;
}

.right-tabs :deep(.el-tabs__header) {
  margin: 0;
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
}

.right-tabs :deep(.el-tabs__nav) {
  padding: 0 12px;
}

.right-tabs :deep(.el-tabs__item) {
  height: 44px;
  line-height: 44px;
  font-size: 13px;
  color: #606266;
  padding: 0 20px;
  transition: all 0.3s;
}

.right-tabs :deep(.el-tabs__item:hover) {
  color: #409eff;
}

.right-tabs :deep(.el-tabs__item.is-active) {
  color: #409eff;
  font-weight: 500;
  background: #fff;
  border-bottom: 2px solid #409eff;
}

.right-tabs :deep(.el-tabs__content) {
  flex: 1;
  overflow: auto;
  padding: 20px;
}

/* 底部操作栏 */
.bottom-actions {
  flex-shrink: 0;
  padding: 14px 20px;
  border-top: 1px solid #e4e7ed;
  display: flex;
  justify-content: space-between;
  background: #f5f7fa;
}
</style>
