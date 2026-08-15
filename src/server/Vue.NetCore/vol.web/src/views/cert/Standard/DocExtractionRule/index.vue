<template>
  <div class="doc-extraction-rule">
    <!-- 三栏布局：目录树 / 预览 / 操作区 -->
    <div class="main-container">
      <!-- 左侧：文件目录树（certcore 通用件，全局复用） -->
      <div class="left-panel" :style="{ width: leftPanelWidth + 'px' }">
        <CertDirectoryTree
          ref="treeRef"
          :selected-file="currentFile"
          mode="file"
          :show-convert-badge="true"
          :show-rule-status="true"
          filterable
          @select="onFileSelect"
          @lock-warning="onFileLockWarning"
        >
          <template #header-actions>
            <el-button
              size="small"
              type="primary"
              plain
              :loading="retrying"
              title="将转换失败/未转换的 doc、xls 文件重新加入转换队列"
              @click.stop="onRetryFailed"
            >
              <el-icon style="margin-right: 4px"><IconRefresh /></el-icon>重试失败转换
            </el-button>
          </template>
        </CertDirectoryTree>
      </div>

      <!-- 左侧拖拽调整宽度 -->
      <div class="resize-handle resize-handle-left" @mousedown.prevent="startResizeLeft">
        <div class="resize-bar"></div>
      </div>

      <!-- 中间：文档预览 -->
      <div class="center-panel">
        <DocPreview v-if="currentFile && !lockReason" :file="currentFile" />
        <div v-else-if="lockReason" class="lock-state-panel">
          <YzhLockStatus
            title="文件处理中，暂不可操作"
            :description="lockReason"
            tag-text="队列执行期间锁定"
          />
        </div>
        <div v-else class="empty-preview">
          <YzhEmptyState :icon="IconFile" title="请选择左侧文档进行预览" description="从目录树中选择一份文档查看内容" />
        </div>
      </div>

      <!-- 右侧：操作区 -->
      <div class="right-panel" v-loading="analyzing" element-loading-text="AI 分析中，请稍候…" element-loading-background="rgba(255,255,255,0.7)">
        <!-- 状态栏 -->
        <div class="status-bar" v-if="currentFile">
          <div class="status-item">
            <span class="label">规则状态：</span>
            <YzhStatusBadge :type="ruleStatusType" :text="ruleStatusText" />
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
          <el-tab-pane name="analysis">
            <template #label>
              <span class="tab-label"><el-icon><IconAnalyze /></el-icon>自动分析</span>
            </template>
            <AIAnalysisTab
              :fields="analysisFields"
              :tables="analysisTables"
              :raw-json="rawJsonDisplay"
              :analyzing="analyzing"
              @analyze="onAIAnalyze"
              @update:fields="onFieldsUpdate"
              @update:tables="onTablesUpdate"
            />
          </el-tab-pane>
          <el-tab-pane name="prompt">
            <template #label>
              <span class="tab-label"><el-icon><IconPrompt /></el-icon>提示词与验证</span>
            </template>
            <PromptVerifyTab
              :prompt="generatedPrompt"
              :verify-result="verifyResult"
              :generating="generating"
              :verifying="verifying"
              @generate="onGeneratePrompt"
              @verify="onVerifyPrompt"
              @update:prompt="onPromptUpdate"
            />
          </el-tab-pane>
        </el-tabs>

        <!-- 底部操作（仅在提示词Tab显示） -->
        <div class="bottom-actions" v-if="activeTab === 'prompt'">
          <el-button @click="cancel">取消</el-button>
          <el-button type="primary" @click="saveRule" :loading="saving">保存规则</el-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertDirectoryTree } from '@/certcore'
import { YzhEmptyState, YzhStatusBadge, YzhLockStatus } from '@/yzh'
import { IconFile, IconAnalyze, IconPrompt, IconRefresh, IconLoading } from '@/yzh'
import { useYzhQueue } from '@/yzh/composables/useYzhQueue'
import { aiAnalyzeDocument, getExtractionRule, generatePrompt, verifyPrompt, saveExtractionRule } from './api'
import AIAnalysisTab from './components/AIAnalysisTab.vue'
import DocPreview from './components/DocPreview.vue'
import PromptVerifyTab from './components/PromptVerifyTab.vue'

const router = useRouter()
const treeRef = ref(null)
const { retryFailedConversions } = useYzhQueue()

// 状态
const activeTab = ref('analysis')
const leftPanelWidth = ref(280) // 左侧面板默认宽度
const currentFile = ref(null)
const saving = ref(false)
const retrying = ref(false)
const analyzing = ref(false) // AI 分析全局等待态（右侧操作区 v-loading）
const lockReason = ref('') // 当前文件锁定原因（队列处理中 / 上传中）

// AI分析结果
const analysisFields = ref([])
const analysisTables = ref([])

// Prompt和验证结果
const generatedPrompt = ref('')
const verifyResult = ref(null)
// 最近一次验证的原始提取数据 { Fields: {code:value}, Tables: {code:rows} }（不经 mapVerify 转换）
// 保存规则时随请求提交 → 后端落 B-08/B-09（YZH 标准企业），供工作流验证
const verifyRawData = ref(null)
const rawJsonDisplay = ref('')
const generating = ref(false)
const verifying = ref(false)
const verifiedIsValid = ref(false)  // 最近一次验证是否通过（saveRule 时推断 isValid）

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
const onFileSelect = async (file) => {
  console.log('[DocExtractionRule] ✅ onFileSelect 触发:',
    { id: file?.id, name: file?.name, type: file?.type, storagePath: file?.storagePath, mimeType: file?.mimeType })
  currentFile.value = file
  lockReason.value = ''
  // 重置状态
  analysisFields.value = []
  analysisTables.value = []
  generatedPrompt.value = ''
  verifyResult.value = null
  verifyRawData.value = null
  activeTab.value = 'analysis'
  // 若该文档已保存过提取规则，自动分析应回显已有字段/表格/Prompt，而不是全部清空
  await loadExistingRule(file)
}

// 加载该文档已保存的提取规则（字段/表格/Prompt/状态），未保存过则保持空状态
const loadExistingRule = async (file) => {
  if (!file?.fileCode) return
  try {
    const res = await getExtractionRule(file.fileCode)
    // 规则不存在时后端返回 { success: false, message } 且无 data
    const data = res?.Data ?? res?.data
    if (!data) return
    // 防止快速切换文件时旧请求的响应覆盖新选择的文件
    if (currentFile.value?.fileCode !== file.fileCode) return
    if (data.fields || data.Fields) analysisFields.value = mapFields(data)
    if (data.tables || data.Tables) analysisTables.value = mapTables(data)
    const prompt = pick(data, 'Prompt', 'prompt')
    if (prompt) generatedPrompt.value = prompt
    const status = pick(data, 'Status', 'status')
    if (status) ruleStatus.value = ['configured', 'failed'].includes(status) ? status : 'none'
    // 回显验证状态
    const isValid = pick(data, 'IsValid', 'isValid')
    verifiedIsValid.value = isValid ?? false
    // 已有 Prompt 的文件自动切到提示词与验证 Tab
    if (prompt) {
      activeTab.value = 'prompt'
    }
  } catch (err) {
    // 加载失败不影响页面使用，保持空状态
    console.warn('[DocExtractionRule] 加载已有规则失败:', err?.message ?? err)
  }
}

const onFileLockWarning = (info) => {
  const reason = info.queueCode === 'uploading'
    ? `文件「${info.fileName}」正在上传处理中，请稍后再试`
    : `文件「${info.fileName}」正被队列 ${info.queueCode} 处理中，请稍后再试`
  lockReason.value = reason
  ElMessage.warning(reason)
}

const onAIAnalyze = async () => {
  if (!currentFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  const fileCode = currentFile.value.fileCode
  // skill 由后端按文件扩展名权威推导（单一约束原则：后端唯一控制），前端不再推断
  const skill = 'word'

  console.log('[DocExtractionRule] 🔍 开始分析:', { fileCode, skill: '(后端推导)' })
  analyzing.value = true
  try {
    const res = await aiAnalyzeDocument({ fileCode, skill })
    console.log('[DocExtractionRule] 📦 analyze 响应:', JSON.stringify(res, null, 2))

    // 解析响应数据（兼容后端 PascalCase Fields/Tables 与 camelCase/snake_case 各键名）
    const data = res?.Data ?? res?.data ?? res
    if (data?.fields || data?.Fields) {
      analysisFields.value = mapFields(data)
    }
    if (data?.tables || data?.Tables) {
      analysisTables.value = mapTables(data)
    }
    // 原始JSON展示
    rawJsonDisplay.value = JSON.stringify(data ?? res, null, 2)
    // 后端返回的 Message 可能包含明确原因（如文件转换中/转换失败/不支持的类型），必须展示给用户
    const message = data?.Message ?? data?.message
    if (message && message !== 'AI分析完成') {
      ElMessage.warning(message)
    } else {
      ElMessage.success('分析完成')
    }
  } catch (err) {
    console.error('[DocExtractionRule] ❌ 分析失败:', err)
    ElMessage.error('AI分析失败: ' + (err?.message ?? '未知错误'))
  } finally {
    analyzing.value = false
  }
}

// 兼容不同大小写的字段取值（PascalCase / camelCase / snake_case）
const pick = (obj, ...keys) => {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] !== undefined && obj[k] !== null) return obj[k]
  }
  return undefined
}

// 字段/表格列表映射（AI 分析结果与已保存规则共用；NameEn 缺失时回退到 Code，保证英文名能回显）
const mapFields = (data) => (data.fields || data.Fields || []).map(f => ({
  name: pick(f, 'Name', 'name', 'fieldName', 'field_name_cn', 'field_name') ?? '',
  nameEn: pick(f, 'NameEn', 'nameEn', 'field_name_en') ?? pick(f, 'Code', 'code', 'field_code') ?? '',
  code: pick(f, 'Code', 'code', 'field_code') ?? '',
  dataType: pick(f, 'DataType', 'dataType', 'field_type') ?? 'string',
  description: pick(f, 'Description', 'description') ?? '',
  isRequired: pick(f, 'IsRequired', 'isRequired', 'is_required') ?? false,
  isManual: pick(f, 'IsManual', 'isManual') ?? false,
  isAiRecommended: pick(f, 'IsAiRecommended', 'isAiRecommended') ?? true,
  extractedValue: pick(f, 'ExtractedValue', 'extractedValue', 'extracted_value') ?? ''
}));

const mapTables = (data) => (data.tables || data.Tables || []).map(t => ({
  name: pick(t, 'Name', 'name', 'tableName', 'table_name_cn', 'table_name') ?? '',
  nameEn: pick(t, 'NameEn', 'nameEn', 'table_name_en') ?? pick(t, 'Code', 'code', 'table_code') ?? '',
  code: pick(t, 'Code', 'code', 'table_code') ?? '',
  description: pick(t, 'Description', 'description') ?? '',
  sheetName: pick(t, 'SheetName', 'sheetName', 'sheet_name') ?? '',
  // 提取数据预览行（后端 ExtractedData 是「列名→值」字典数组，必须透传否则表格数据不显示）
  extractedData: pick(t, 'ExtractedData', 'extractedData', 'extracted_data') ?? [],
  isAiRecommended: pick(t, 'IsAiRecommended', 'isAiRecommended') ?? true,
  columns: (t.columns ?? t.Columns ?? []).map(c => ({
    name: pick(c, 'Name', 'name', 'columnName', 'column_name_cn', 'column_name') ?? '',
    nameEn: pick(c, 'NameEn', 'nameEn', 'column_name_en') ?? pick(c, 'Code', 'code', 'column_code') ?? '',
    code: pick(c, 'Code', 'code', 'column_code') ?? '',
    dataType: pick(c, 'DataType', 'dataType', 'column_type') ?? 'string',
    isRequired: pick(c, 'IsRequired', 'isRequired', 'column_is_required') ?? false
  }))
}));

// 验证结果映射：字段 code→中文名（只展示当前 analysisFields 中存在的字段，过滤已删除的）
const mapVerifyFields = (rawData) => {
  const innerData = rawData?.Data ?? rawData?.data ?? rawData
  const rawFields = innerData?.Fields ?? innerData?.fields ?? {}
  // 构建 code→中文名 映射表（只包含当前字段列表中的字段）
  const codeToName = {}
  const validCodes = new Set()
  analysisFields.value.forEach(f => {
    const code = f.nameEn || f.code
    if (code) {
      codeToName[code] = f.name
      validCodes.add(code)
    }
  })
  const result = {}
  for (const [key, val] of Object.entries(rawFields)) {
    // 只展示当前字段列表中存在的字段，已删除的字段不展示
    if (!validCodes.has(key)) continue
    const displayName = codeToName[key] || key
    result[displayName] = val
  }
  return result
}

// 验证结果映射：表格 tableCode→中文表名 + 列名 code→中文列名
// 只展示当前 analysisTables 中存在的表格和列，已删除的表格/列不展示
const mapVerifyTables = (rawData) => {
  const innerData = rawData?.Data ?? rawData?.data ?? rawData
  const rawTables = innerData?.Tables ?? innerData?.tables ?? {}
  // 构建 tableCode→中文表名 + 列名 code→中文列名 映射表
  const tableCodeToName = {}
  // 同时构建 每个表的 columnCode→columnName 映射 + 有效列集合
  const tableColMap = {}  // { tableCode: { colCode: colNameCn } }
  const tableValidCols = {}  // { tableCode: Set<colCode> }
  analysisTables.value.forEach(t => {
    const code = t.nameEn || t.code
    if (code) {
      tableCodeToName[code] = t.name
      // 构建该表的列名映射 + 有效列集合
      const colMap = {}
      const validCols = new Set()
      if (t.columns) {
        t.columns.forEach(c => {
          const colCode = c.nameEn || c.code
          if (colCode) {
            colMap[colCode] = c.name
            validCols.add(colCode)
          }
        })
      }
      tableColMap[code] = colMap
      tableValidCols[code] = validCols
    }
  })
  const result = {}
  for (const [key, rows] of Object.entries(rawTables)) {
    // 只展示当前表格列表中存在的表格，已删除的表格不展示
    if (!tableCodeToName[key]) continue
    const displayName = tableCodeToName[key]
    const colMap = tableColMap[key] || {}
    const validCols = tableValidCols[key] || new Set()
    // 将每行数据的 key 从英文 code 替换为中文列名，只展示当前列定义中存在的列
    const mappedRows = (rows || []).map(row => {
      const newRow = {}
      for (const [colKey, colVal] of Object.entries(row)) {
        // 只展示当前列定义中存在的列，已删除的列不展示
        if (!validCols.has(colKey)) continue
        const colName = colMap[colKey] || colKey
        newRow[colName] = colVal
      }
      return newRow
    })
    result[displayName] = mappedRows
  }
  return result
}

const onFieldsUpdate = (fields) => {
  analysisFields.value = fields
  // 字段变更后，验证结果失效
  if (verifyResult.value) {
    verifyResult.value = null
    verifiedIsValid.value = false
  }
  // 字段变更后，旧 Prompt 已过期（包含已删除的字段），必须重新生成
  if (generatedPrompt.value) {
    generatedPrompt.value = ''
  }
}

const onTablesUpdate = (tables) => {
  analysisTables.value = tables
  // 表格变更后，验证结果失效
  if (verifyResult.value) {
    verifyResult.value = null
    verifiedIsValid.value = false
  }
  // 表格变更后，旧 Prompt 已过期（包含已删除的表格/列），必须重新生成
  if (generatedPrompt.value) {
    generatedPrompt.value = ''
  }
}

const onGeneratePrompt = async () => {
  // 校验：至少有一个字段或一个表格
  const aiFields = analysisFields.value.filter(f => f.isAiRecommended !== false)
  const aiTables = analysisTables.value.filter(t => t.isAiRecommended !== false)
  if (aiFields.length === 0 && aiTables.length === 0) {
    ElMessage.warning('请先在「自动分析」页签添加至少一个字段或表格')
    return
  }
  generating.value = true
  try {
    const res = await generatePrompt({
      fileCode: currentFile.value.fileCode,
      fields: analysisFields.value,
      tables: analysisTables.value
    })
    const data = res?.Data ?? res?.data ?? res
    const prompt = data?.Prompt ?? data?.prompt
    if (prompt) {
      generatedPrompt.value = prompt
      verifyResult.value = null  // 清空旧验证结果
      verifyRawData.value = null
      verifiedIsValid.value = false
      ElMessage.success('Prompt 生成成功')
    } else {
      ElMessage.warning('生成失败：未返回 Prompt 内容')
    }
  } catch (err) {
    ElMessage.error('生成失败: ' + (err?.message ?? '未知错误'))
  } finally {
    generating.value = false
  }
}

const onVerifyPrompt = async () => {
  if (!generatedPrompt.value) {
    ElMessage.warning('请先生成 Prompt')
    return
  }
  verifying.value = true
  try {
    const res = await verifyPrompt({
      fileCode: currentFile.value.fileCode,
      prompt: generatedPrompt.value
    })
    // 后端返回 { Success, Message, Data: { Fields, Tables, Message } }
    // success/message 在顶层，Fields/Tables 在 Data 内层
    const success = res?.Success ?? res?.success ?? false
    const message = res?.Message ?? res?.message ?? ''
    const innerData = res?.Data ?? res?.data ?? {}
    // 保留原始提取数据（code→value / code→rows），保存规则时提交给后端落 B-08/B-09
    verifyRawData.value = {
      Fields: innerData?.Fields ?? innerData?.fields ?? {},
      Tables: innerData?.Tables ?? innerData?.tables ?? {}
    }
    // 映射验证结果：字段 code→中文名、表格 tableCode→中文表名 + 列名 code→中文列名
    const result = {
      success,
      message,
      data: {
        fields: mapVerifyFields(innerData),
        tables: mapVerifyTables(innerData)
      }
    }
    verifyResult.value = result
    verifiedIsValid.value = success
    if (success) {
      ElMessage.success('验证通过')
    } else {
      ElMessage.warning(message || '验证失败')
    }
  } catch (err) {
    ElMessage.error('验证失败: ' + (err?.message ?? '未知错误'))
  } finally {
    verifying.value = false
  }
}

const onPromptUpdate = (prompt) => {
  generatedPrompt.value = prompt
  // Prompt 被修改后，验证结果失效
  if (verifyResult.value) {
    verifyResult.value = null
    verifiedIsValid.value = false
  }
}

const saveRule = async () => {
  if (!currentFile.value?.fileCode) {
    ElMessage.warning('请先选择一个文件')
    return
  }
  // 允许未验证直接保存，isValid 取最近验证结果（未验证则为 false）
  const isValid = verifiedIsValid.value
  // skill 由后端按文件扩展名权威推导（单一约束原则），前端不再推断
  const skill = 'word'

  // 组装提取数据（供后端落 B-08/B-09 工作流验证数据）：验证结果优先，分析预览值兜底
  let extractionData = null
  const raw = verifyRawData.value
  if (raw && (Object.keys(raw.Fields || {}).length > 0 || Object.keys(raw.Tables || {}).length > 0)) {
    extractionData = { Fields: raw.Fields || {}, Tables: raw.Tables || {} }
  } else {
    const fields = {}
    analysisFields.value.forEach(f => {
      const code = f.nameEn || f.code
      if (code && f.extractedValue) fields[code] = f.extractedValue
    })
    const tables = {}
    analysisTables.value.forEach(t => {
      const code = t.nameEn || t.code
      if (code && t.extractedData?.length > 0) tables[code] = t.extractedData
    })
    if (Object.keys(fields).length > 0 || Object.keys(tables).length > 0) {
      extractionData = { Fields: fields, Tables: tables }
    }
  }

  saving.value = true
  try {
    const res = await saveExtractionRule({
      fileCode: currentFile.value.fileCode,
      skill,
      fields: analysisFields.value,
      tables: analysisTables.value,
      prompt: generatedPrompt.value,
      isValid,
      extractionData
    })
    const data = res?.Data ?? res?.data ?? res
    const success = data?.success ?? data?.Success ?? false
    if (success) {
      ruleStatus.value = isValid ? 'configured' : 'failed'
      ElMessage.success('规则保存成功')
      // 刷新目录树，让文件节点的规则状态标签（已配置/未配置）立即更新
      treeRef.value?.refresh?.()
    } else {
      ElMessage.error(data?.message ?? data?.Message ?? '保存失败')
    }
  } catch (err) {
    ElMessage.error('保存失败: ' + (err?.message ?? '未知错误'))
  } finally {
    saving.value = false
  }
}

const cancel = () => {
  router.back()
}

/* 重试失败的文档转换：把转换失败/未转换的 doc、xls 重新入队，队列完成后树自动刷新 */
const onRetryFailed = async () => {
  try {
    await ElMessageBox.confirm(
      '将把转换失败或未转换的 doc、xls 文件重新加入转换队列（文件会在转换期间暂时隐藏，完成后自动恢复）。确定继续吗？',
      '重试失败转换',
      { type: 'warning', confirmButtonText: '开始重试', cancelButtonText: '取消' }
    )
  } catch {
    return
  }

  retrying.value = true
  try {
    const { ok, message, data } = await retryFailedConversions()
    if (!ok) {
      ElMessage.error(message || '重试失败')
      return
    }
    const enqueued = data?.enqueued ?? 0
    if (enqueued > 0) {
      ElMessage.success(`${message}（${enqueued} 个文件）`)
      // 文件已置为隐藏，立即刷新树
      treeRef.value?.refresh?.()
    } else {
      ElMessage.info(message || '没有需要重试的文件')
    }
  } catch (err) {
    console.error('[DocExtractionRule] 重试失败转换出错:', err)
    ElMessage.error('重试失败：' + (err?.message ?? '未知错误'))
  } finally {
    retrying.value = false
  }
}

// ====== 左侧面板拖拽调整宽度 ======
const startResizeLeft = (e) => {
  const startX = e.clientX
  const startWidth = leftPanelWidth.value

  const onMouseMove = (e) => {
    const delta = e.clientX - startX
    // 限制最小200px，最大600px
    leftPanelWidth.value = Math.max(200, Math.min(600, startWidth + delta))
  }

  const onMouseUp = () => {
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
</script>

<style scoped>
/* 令牌引用：yzh 设计令牌 + certcore 业务令牌 */
@import '@/yzh/styles/yzh.css';
@import '@/certcore/styles/cert-tokens.css';

.doc-extraction-rule {
  /* 认证平台统一页面容器：16/24/16/24 留白 + 灰底 */
  position: absolute;
  top: 16px;
  left: 24px;
  right: 24px;
  bottom: 16px;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border-radius: var(--yzh-radius-sm, 4px);
  overflow: hidden;
}

.main-container {
  flex: 1;
  min-height: 0;
  display: flex;
  overflow: hidden;
}

/* 左侧面板：独立白色卡片 */
.left-panel {
  min-width: 200px;
  max-width: 600px;
  flex-shrink: 0;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
  overflow: hidden;
  box-sizing: border-box;
}

/* 拖拽调整宽度手柄 */
.resize-handle {
  width: var(--yzh-space-1, 4px);
  flex-shrink: 0;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
}

.resize-handle:hover,
.resize-handle:active {
  background: var(--yzh-color-primary-light-7, #c6e2ff);
}

.resize-bar {
  width: 2px;
  height: 40px;
  background: var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-full, 9999px);
  transition: all 0.2s;
}

.resize-handle:hover .resize-bar {
  background: var(--yzh-color-primary, #409eff);
  height: 50px;
}

/* 中间面板：预览卡片 */
.center-panel {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.empty-preview {
  flex: 1;
  min-height: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
}

/* 右侧面板：独立白色卡片 */
.right-panel {
  width: 480px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-card, #fff);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 4px rgba(0, 0, 0, 0.04));
  box-sizing: border-box;
  overflow: hidden;
}

/* 状态栏 */
.status-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-8, 32px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  background: var(--yzh-color-bg-page, #f5f7fa);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.status-item {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
}

.status-item .label {
  color: var(--yzh-color-text-regular, #606266);
}

.status-item .value {
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

/* Tab */
.right-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.right-tabs :deep(.el-tabs__header) {
  margin: 0;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.right-tabs :deep(.el-tabs__item) {
  height: 44px;
  line-height: 44px;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  padding: 0 var(--yzh-space-5, 20px);
}

/* EP 内置「首/末 Tab 去内边距」规则特异性更高，直接给 nav-wrap 加内边距更可靠，且与内容区 20px 对齐 */
.right-tabs :deep(.el-tabs__nav-wrap) {
  padding: 0 var(--yzh-space-5, 20px);
}

.right-tabs :deep(.el-tabs__item.is-active) {
  color: var(--yzh-color-primary, #409eff);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.right-tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-5, 20px);
}

/* 底部操作栏 */
.bottom-actions {
  flex-shrink: 0;
  display: flex;
  justify-content: space-between;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  background: var(--yzh-color-bg-page, #f5f7fa);
}
</style>
