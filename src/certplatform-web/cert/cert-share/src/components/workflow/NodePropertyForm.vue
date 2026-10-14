<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <div class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">基础信息</span>
          <el-tooltip v-if="nodeDescription" :content="nodeDescription" placement="left" :show-after="1000"><el-icon class="help-icon"><Warning /></el-icon></el-tooltip>
        </div>
        <div class="inspector-field"><div class="inspector-label">节点名称</div><el-input v-model="form.title" size="small" @change="applyChanges" /></div>
        <div class="inspector-field"><div class="inspector-label">节点描述</div><el-input v-model="form.description" type="textarea" :rows="2" size="small" placeholder="说明该节点在此工作流中的作用，供专家追溯时理解" @change="applyChanges" /></div>
        <div class="inspector-field"><div class="inspector-label">节点类型</div><div class="node-type-display"><span class="type-dot" :class="nodeTypeTag"></span><span class="type-name">{{ nodeTypeName }}</span></div></div>
      </div>

      <div v-if="visibleInputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">输入端口 (INPUTS)</span></div>
        <div v-for="port in visibleInputPorts" :key="port.name" class="inspector-field">
          <div class="inspector-label"><span>{{ port.label || port.name }}</span><el-tag v-if="port.required" size="small" type="danger" effect="plain">REQ</el-tag></div>
          <div v-if="port.description && port.description !== (port.label || port.name)" class="port-desc">{{ port.description }}</div>
          <PortControl :key="`port_${port.name}_${form._updateTick || 0}`" :model-value="getInputValue(port.name)" :bind-mode="port.bindMode || 'LinkOrConstant'" :enum-source="port.enumSource" :linkable-nodes="linkableNodes.map((n: any) => ({ id: n.id, label: n.title || n.id }))" :input-type="getInputType(port.name)" :placeholder="getPortPlaceholder(port)" @update:model-value="(v: any) => setInputValue(port.name, v)" @update:input-type="(v: string) => setInputType(port.name, v)" @link-node="({ sourceNodeId }: any) => onLinkNode(port.name, sourceNodeId)" />
        </div>
      </div>

      <div v-if="form.nodeType === 'branch'" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">分支逻辑</span></div>
        <div class="branch-hint"><el-icon><InfoFilled /></el-icon><span>连接上游比较节点的布尔输出。</span></div>
        <div class="inspector-field"><div class="inspector-label">成功分支目标</div><el-select :model-value="getBranchOutput('success')" placeholder="选择目标节点" size="small" style="width: 100%" @change="(v: string) => setBranchOutput('success', v)"><el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" /></el-select></div>
        <div class="inspector-field"><div class="inspector-label">失败分支目标</div><el-select :model-value="getBranchOutput('failure')" placeholder="选择目标节点" size="small" style="width: 100%" @change="(v: string) => setBranchOutput('failure', v)"><el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" /></el-select></div>
      </div>

      <div v-if="visiblePanelSchema.length > 0 && form.nodeType !== 'branch'" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">参数配置</span></div>
        <template v-for="field in visiblePanelSchema" :key="field.field">
          <div v-if="field.type !== 'promptWithRef'" class="inspector-field">
            <div class="inspector-label">{{ field.label }}</div>
            <el-input v-if="field.type === 'textarea'" v-model="panelFieldValues[field.field]" type="textarea" :rows="field.rows || 3" size="small" @change="applyPanelField(field)" />
            <template v-else-if="field.type === 'doc-select'">
              <el-select v-model="panelFieldValues[field.field]" size="small" style="width: 100%" @change="(v: string) => onDocSelect(field.field, v)"><el-option v-for="d in docList" :key="d.ruleCode || d.RuleCode" :label="d.fileName || d.FileName || d.standardFileCode || d.StandardFileCode" :value="d.ruleCode || d.RuleCode" /></el-select>
              <div v-if="!docList.length" class="panel-hint">{{ docEmptyHint || '暂无已配置提取规则的文档' }}</div>
            </template>
            <el-select v-else-if="field.type === 'field-select'" v-model="panelFieldValues[field.field]" size="small" style="width: 100%" :disabled="!panelFieldValues['config.ruleCode']" @change="applyPanelFields"><el-option v-for="f in fieldList" :key="f.fieldCode" :label="f.fieldName" :value="f.fieldCode" /></el-select>
            <!-- ★ 2026-10-11 补：table-select 此前**没有任何渲染分支** ⇒ docTable 的「表格」下拉只显示 label、选不了 -->
            <el-select v-else-if="field.type === 'table-select'" v-model="panelFieldValues[field.field]" size="small" style="width: 100%" :disabled="!panelFieldValues['config.ruleCode']" @change="applyPanelFields"><el-option v-for="t in tableList" :key="t.tableCode" :label="t.tableName" :value="t.tableCode" /></el-select>
            <!-- ★ 2026-10-14（用户裁定）：删除 std-select / stage-select 两个渲染分支。
                 is_std_file_missing 面板不再暴露「标准/阶段」—— 左树已经定义了它们，
                 且执行期 NodeExecutor.ExecuteStdFileMissingAsync 只读 standardFileCode。
                 ⛔ 不要因为「分支暂时没人用」就留着：留着 = 下次有人往 schema 加回这两个 type 时
                 又变成「让用户多选一次」而不自知。 -->
            <template v-else-if="field.type === 'std-file-select'">
              <el-select v-model="panelFieldValues[field.field]" size="small" style="width: 100%" placeholder="选择标准文件" filterable :disabled="!stdCtxReady" @change="applyPanelFields"><el-option v-for="f in stdFileOptions" :key="f.code" :label="f.name" :value="f.code" /></el-select>
              <div v-if="!stdCtxReady" class="panel-hint">请先在左侧选择阶段 —— 标准文件列表按当前「标准 + 阶段」加载</div>
            </template>
            <el-switch v-else-if="field.type === 'switch'" v-model="panelFieldValues[field.field]" @change="applyPanelFields" />
            <el-slider v-else-if="field.type === 'slider'" v-model="panelFieldValues[field.field]" :min="field.min || 0" :max="field.max || 1" :step="field.step || 0.1" @change="applyPanelFields" />
            <!-- ★ 2026-10-14（设计 §2.1 / 用户裁决 ①②④）：AI 节点「参数」表。
                 每一行 = 一个 {{参数名}} 占位符 = **一条依赖边**（sourceType=node 时
                 params[].nodeId → 本节点，引擎据此预执行来源节点，见 topology.implicitEdges）。
                 ⛔ 这里只编辑 `config.params`，不碰旧 `customParams`（那是兜底通道）。 -->
            <template v-else-if="field.type === 'ai-params'">
              <div v-for="(p, idx) in aiParams" :key="`aip_${idx}_${form._updateTick || 0}`" class="ai-param-item">
                <div class="ai-param-head">
                  <el-input v-model="p.name" size="small" class="ai-param-name" placeholder="p1" @change="commitAiParams" />
                  <el-input v-model="p.label" size="small" placeholder="中文说明（给人看也给模型看）" @change="commitAiParams" />
                  <el-button size="small" text type="danger" @click="removeAiParam(idx)"><el-icon><Delete /></el-icon></el-button>
                </div>
                <div class="ai-param-src">
                  <el-select v-model="p.nodeId" size="small" filterable placeholder="来源节点" @change="(v: string) => onAiParamNodeChange(idx, v)">
                    <el-option v-for="n in linkableNodes" :key="n.id" :label="n.title || n.id" :value="n.id" />
                  </el-select>
                  <el-select v-model="p.portName" size="small" class="ai-param-port" placeholder="端口" @change="commitAiParams">
                    <el-option v-for="pt in aiParamPorts(p)" :key="pt" :label="pt" :value="pt" />
                  </el-select>
                </div>
              </div>
              <el-button size="small" plain type="primary" class="ai-param-add" @click="addAiParam"><el-icon><Plus /></el-icon> 添加参数</el-button>
              <div class="panel-hint">提示词里写 <code>{{ paramHint }}</code> 引用；每一项同时是一条依赖边（保存时校验来源节点必须真实存在）</div>
            </template>
          </div>
          <div v-else class="inspector-field"><div class="inspector-label">{{ field.label }}</div><PromptRefEditor v-model="panelFieldValues[field.field]" :rows="field.rows || 6" :linkable-nodes="linkableNodes" @update:model-value="applyPanelField(field)" /></div>
        </template>
      </div>

      <div v-if="visibleOutputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">输出端口 (OUTPUTS)</span></div>
        <div v-for="out in visibleOutputPorts" :key="out.name" class="output-item"><span class="output-type">{{ out.type }}</span><span class="tree-label">{{ out.label || out.name }}</span></div>
      </div>

      <div class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">操作</span></div>
        <div v-if="testResult" class="test-result-wrapper"><div :class="['test-result', testResult.success ? 'test-success' : 'test-fail']"><div class="test-header"><el-icon><CircleCheck v-if="testResult.success" /><Warning v-else /></el-icon><span>{{ testResult.success ? '执行成功' : '执行失败' }}</span></div><div v-if="testResult.message" class="test-message">{{ testResult.message }}</div><!-- ★ 2026-10-14 第 0 批止血：未解析引用必须显式可见（原实现只写日志，界面完全看不见） --><el-alert v-if="unresolvedRefs.length" type="warning" :closable="false" show-icon :title="unresolvedRefs.length + ' 个提示词引用未解析'" :description="'以下占位符没取到值，已按原文送给大模型（模型可能照抄）：' + unresolvedRefs.join('、')" class="test-unresolved" /><pre v-if="testResult.data" class="test-output-json">{{ JSON.stringify(testResult.data, null, 2) }}</pre></div></div>
        <div class="action-row">
          <el-button v-if="form.nodeType === 'docField' || form.nodeType === 'docTable'" type="default" size="small" :loading="testLoading" @click="testDocExtract">测试提取</el-button>
          <el-button v-else-if="testable && form.nodeType !== 'start' && form.nodeType !== 'end'" type="primary" size="small" :loading="testLoading" @click="testNode">运行测试</el-button>
          <el-button type="danger" size="small" plain @click="deleteNode">删除节点</el-button>
        </div>
      </div>
    </div>

    <div v-else class="form-empty">
      <YzhEmptyState
        :icon="InfoFilled"
        title="点击画布节点配置属性"
        description="从左侧节点库拖动节点到画布，或点击节点查看和编辑属性"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { YzhEmptyState } from '@yzh-core'
import { getSpecialNode } from '@share/composables/workflow/specialNodes'
import { normalizePorts } from '@share/composables/workflow/portNormalize'
import { CircleCheck, Delete, InfoFilled, Plus, Warning } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import { parseAiParams, type AiParam } from '@share/composables/workflow/aiParams'
import PortControl from './panels/PortControl.vue'
import PromptRefEditor from './panels/PromptRefEditor.vue'

const props = defineProps<{
  selectedNode?: any; skills?: any[]; docRules?: any[]; docFields?: any[]; docTables?: any[]; canvasNodes?: any[]
  /** 「文档」下拉为空时的提示（父组件按是否已选阶段给不同文案；收窄后**必须**给，否则用户只看到空下拉） */
  docEmptyHint?: string
  /**
   * ★ 2026-10-14（用户裁定）：左树当前「标准 + 阶段」上下文（均为 **GUID** 口径）。
   *
   * <para>① `is_std_file_missing` 节点用它**自动带入**标准/阶段 —— 面板已删掉这两个下拉：
   * 左树已经定义了它们，再让用户选一次是重复负担；且执行期
   * <c>NodeExecutor.ExecuteStdFileMissingAsync</c> 只读 <c>standardFileCode</c>，
   * 注释明确「刻意不传 standard_code / stage_code」。</para>
   * <para>② `docField` / `docTable` 的「文档」下拉由父组件据此收窄（本组件只负责渲染）。</para>
   */
  stdContext?: { standardCode: string; stageCode: string }
  /** ★ is_std_file_missing 节点用：当前「标准 + 阶段」下的标准文件，每项 { code, name } */
  stdFiles?: any[]
}>()
const emit = defineEmits<{
  'update-node': [data: any]; 'delete-node': [nodeId: string]; 'load-doc-fields': [ruleCode: string]
  'link-node': [data: any]; 'test-node': [data: any]; 'test-workflow': [data: any]; 'test-doc-extract': [data: any]
  /** ★ 请求父组件按「标准 + 阶段」加载标准文件列表 */
  'load-std-files': [data: { standardCode: string; stageCode: string }]
}>()

const form = ref<any>({ nodeId: '', nodeType: 'skill', title: '', description: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [], inputTypes: {} })
const inputValues = ref<Record<string, any>>({})
const inputTypes = ref<Record<string, string>>({})
const panelFieldValues = ref<Record<string, any>>({})
const docList = ref<any[]>([]); const fieldList = ref<any[]>([]); const tableList = ref<any[]>([])
const testResult = ref<any>(null); const testLoading = ref(false)

/**
 * ★ 2026-10-14（第 0 批止血）：AI 节点测试返回的「未解析提示词引用」清单。
 *
 * <para><b>为什么必须在界面上显式展示</b>：配置期测试刻意不中断（搭图阶段上游本来可能就是空的），
 * 但「不中断」≠「不说」—— 后端原实现只写一条 <c>LogWarning</c>，用户在界面上完全看不见，
 * 于是以为「提示词没问题」，实际是占位符原文被送给了大模型、模型照抄回来。</para>
 *
 * <para>字段来源：<c>AiNodeDebugInfo.UnresolvedRefs</c>（由引擎 <c>AiNodeExecutor</c> 唯一产出）。
 * 本项目 MVC JSON 选项是 <c>PropertyNamingPolicy = null</c> ⇒ 响应为 <b>PascalCase</b>；
 * 这里同时兼容 camelCase，避免将来有人改了序列化策略时**静默失效**。</para>
 */
const unresolvedRefs = computed<string[]>(() => {
  const d = testResult.value?.data
  const list = d?.Debug?.UnresolvedRefs ?? d?.debug?.unresolvedRefs
  return Array.isArray(list) ? list : []
})

// ══════════════════════════════════════════════════════════════
// ★ AI 节点「参数」表（设计 §2.1 / 用户裁决 ①②④）
//   数据源 = `config.params`（数组或字符串化 JSON 均可能，读写统一走 parseAiParams）。
//   ⚠️ 参数表是**唯一权威**：旧 `customParams` 只作后端兜底，⛔ 这里不再编辑它。
// ══════════════════════════════════════════════════════════════
const aiParams = ref<AiParam[]>([])
/** 模板里给用户看的占位符示例（在 <template> 中直接写 {{…}} 会被 Vue 当插值，必须走绑定） */
const paramHint = '{{参数名}}'

/** 自动取下一个未占用的短参数名（p1 → p2 …，符合 §2.3「短 ASCII 标识符」） */
function nextParamName(): string {
  const used = new Set(aiParams.value.map((p) => p.name).filter(Boolean))
  for (let i = 1; i < 100; i++) {
    const n = `p${i}`
    if (!used.has(n)) return n
  }
  return `p${Date.now()}`
}

function addAiParam() {
  aiParams.value = [...aiParams.value, { name: nextParamName(), label: '', sourceType: 'node', nodeId: '', portName: 'result' }]
  commitAiParams()
}

function removeAiParam(idx: number) {
  aiParams.value = aiParams.value.filter((_, i) => i !== idx)
  commitAiParams()
}

/** 某参数可选的输出端口（取来源节点的可见端口；拿不到时兜底 `result`） */
function aiParamPorts(p: AiParam): string[] {
  const n = (props.canvasNodes || []).find((x: any) => (x?.nodeId || x?.id) === p?.nodeId)
  const ports = ((n?.outputPorts || []) as any[])
    .filter((x) => x?.display !== 'hidden' && x?.name)
    .map((x) => String(x.name))
  return ports.length ? ports : ['result']
}

/** 换来源节点后，端口必须跟着切 —— 留着上一个节点的端口名会静默取不到值（§2.7.4） */
function onAiParamNodeChange(idx: number, nodeId: string) {
  const p = aiParams.value[idx]
  if (!p) return
  p.nodeId = nodeId
  p.sourceType = 'node'
  const ports = aiParamPorts(p)
  if (!p.portName || !ports.includes(p.portName)) p.portName = ports[0] || 'result'
  commitAiParams()
}

/** 把参数表写回 `config.params` 并标记画布已修改（⛔ 只写这一个键，不整份覆盖 config） */
function commitAiParams() {
  form.value.config = { ...form.value.config, params: aiParams.value.map((p) => ({ ...p })) }
  applyChanges()
}

const specialMeta = computed(() => getSpecialNode(form.value.nodeType))
const nodeTypeName = computed(() => { if (specialMeta.value) return specialMeta.value.className; const sk = (props.skills || []).find((s: any) => s.skillCode === form.value.skillCode); return sk?.skillName || form.value.nodeType })
const nodeTypeTag = computed(() => ({ start: 'success', end: 'danger', branch: 'warning', skill: 'primary', ai_node: 'info', docField: 'success', docTable: 'warning' } as Record<string, string>)[form.value.nodeType] || 'info')
const nodeDescription = computed(() => { if (specialMeta.value) return specialMeta.value.description; return (props.skills || []).find((s: any) => s.skillCode === form.value.skillCode)?.description || '' })
const testable = computed(() => { if (specialMeta.value) return specialMeta.value.testable; return form.value.nodeType === 'skill' })
const visibleInputPorts = computed(() => (form.value.inputPorts || []).filter((p: any) => p.display !== 'hidden'))
const visibleOutputPorts = computed(() => (form.value.outputPorts || []).filter((p: any) => p.display !== 'hidden' && p.role !== 'anchor'))
// ★ 第 5 批：panelSchema 支持 `visibleWhen: { field, equals }` 条件显示 ——
//   例如 outputSchema 只在 outputType === 'json' 时出现，避免「配了 json 却看不到 Schema 框」。
//   ⛔ 不写 visibleWhen 的字段恒显示（向后兼容既有 schema）。
const visiblePanelSchema = computed(() => {
  const schema = specialMeta.value?.panelSchema || []
  return schema.filter((field: any) => {
    const w = field.visibleWhen
    if (!w || !w.field) return true
    return panelFieldValues.value[w.field] === w.equals
  })
})

// ── ★ is_std_file_missing：标准/阶段改由**左树上下文**带入（2026-10-14 用户裁定）──
//   为什么可以：执行期 NodeExecutor.ExecuteStdFileMissingAsync 只读 standardFileCode
//   （注释明确「刻意不传 standard_code / stage_code」：二次过滤一旦口径不符，
//     会把本来命中的行整批滤掉，静默退化成 not_configured）。
//   ⇒ 标准/阶段本就只是**配置期筛选器**，而左树（机构 → 标准 → 阶段 → 规则）已经选定它们，
//     再让用户在面板里选一次是纯重复负担。
//   ⚠️ 口径：上下文里的两个值都是 **GUID**（树节点 stdCode / phaseCode），
//     与 cert_standard_directory_file.StandardCode / StageCode 同源。
const stdCtxReady = computed(() => !!props.stdContext?.standardCode)
const stdFileOptions = computed(() => props.stdFiles || [])

/** 把左树上下文写进节点 config —— ⛔ 仅留痕：执行期不读这两个键，且**不触发** applyChanges
 *  （否则「点一下节点」就会把画布标成未保存，属假脏）。下次用户真实改动时会随整份 config 一起落库。 */
function syncStdContextToConfig() {
  const ctx = props.stdContext
  if (!ctx?.standardCode) return
  form.value.config = {
    ...form.value.config,
    standardCode: ctx.standardCode,
    stageCode: ctx.stageCode || '',
  }
}

/** 按左树上下文请求标准文件列表（父组件负责实际取数） */
function requestStdFiles() {
  const ctx = props.stdContext
  if (!ctx?.standardCode) return
  emit('load-std-files', { standardCode: ctx.standardCode, stageCode: ctx.stageCode || '' })
}

const linkableNodes = computed(() => {
  const excludeTypes = new Set(['start', 'end', 'branch'])
  const getOutputPorts = (nodeType: string) => getSpecialNode(nodeType)?.outputPorts?.filter((p: any) => p.display !== 'hidden') || [{ name: 'result', label: '结果', type: 'string' }]
  return (props.canvasNodes || []).filter((n: any) => n && n.id !== form.value.nodeId && !excludeTypes.has(n.nodeType)).map((n: any) => ({ id: n.id, title: n.title || n.id, nodeType: n.nodeType, color: getSpecialNode(n.nodeType)?.color || '', outputPorts: getOutputPorts(n.nodeType) }))
})

const branchTargetNodes = computed(() => (props.canvasNodes || []).filter((n: any) => n.id !== form.value.nodeId).map((n: any) => ({ id: n.id, label: `${n.title || n.id}` })))

function getBranchOutput(handle: string) {
  if (form.value._branchEdges?.length) { const edge = form.value._branchEdges.find((e: any) => e.handle === handle); if (edge) return edge.targetId }
  return form.value.config?.[`_${handle}Target`] || ''
}
function setBranchOutput(handle: string, targetNodeId: string) {
  const config = { ...form.value.config }; config[`_${handle}Target`] = targetNodeId; form.value.config = config
  emit('link-node', { portName: handle, sourceNodeId: targetNodeId ? form.value.nodeId : null, targetNodeId: targetNodeId || form.value.nodeId, sourceHandle: handle })
  applyChanges()
}
function getInputValue(portName: string) { return inputValues.value[portName] ?? '' }
function getInputType(portName: string) {
  if (inputTypes.value[portName]) return inputTypes.value[portName]
  const val = inputValues.value[portName]
  if (val && linkableNodes.value.some((n: any) => n.id === val)) return 'link'
  if (val) return 'constant'
  return 'link'
}
function setInputType(portName: string, type: string) {
  const prevValue = inputValues.value[portName]; const prevType = inputTypes.value[portName]
  inputTypes.value[portName] = type; form.value.inputTypes = { ...inputTypes.value }
  if (prevType === 'link' && type === 'constant' && prevValue) emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId })
  inputValues.value[portName] = ''; const inputs = { ...form.value.inputs }; delete inputs[portName]; form.value.inputs = inputs; applyChanges()
}
function setInputValue(portName: string, value: any) {
  inputValues.value[portName] = value; form.value.inputs = { ...form.value.inputs, [portName]: value }; applyChanges()
}
function onLinkNode(portName: string, sourceNodeId: string | null) {
  inputTypes.value[portName] = 'link'; form.value.inputTypes = { ...inputTypes.value }
  if (!sourceNodeId) { emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId }); inputValues.value[portName] = ''; const inputs = { ...form.value.inputs }; delete inputs[portName]; form.value.inputs = inputs }
  else { emit('link-node', { portName, sourceNodeId, targetNodeId: form.value.nodeId }); inputValues.value[portName] = sourceNodeId; form.value.inputs = { ...form.value.inputs, [portName]: sourceNodeId } }
  applyChanges()
}
function getPortPlaceholder(port: any) { if (port.bindMode === 'Link') return '请在画布上连线'; if (port.bindMode === 'Enum') return '请选择'; if (port.bindMode === 'ConstantOnly') return '输入常量值'; return '输入常量或 {{n1.portName}}' }
function applyPanelField(field: any) {
  const config = { ...form.value.config }; const parts = field.field.split('.')
  if (parts.length === 2 && parts[0] === 'config') config[parts[1]] = panelFieldValues.value[field.field]; else config[field.field] = panelFieldValues.value[field.field]
  form.value.config = config; applyChanges()
}
function applyPanelFields() {
  const config = { ...form.value.config }
  for (const [key, val] of Object.entries(panelFieldValues.value)) { const parts = key.split('.'); if (parts.length === 2 && parts[0] === 'config') config[parts[1]] = val }
  form.value.config = config; applyChanges()
}
function onDocSelect(field: string, ruleCode: string) {
  const config = { ...form.value.config }; const parts = field.split('.'); const key = parts.length === 2 ? parts[1] : field; config[key] = ruleCode
  if (key === 'ruleCode' || key === 'docCode') { config.fieldCode = ''; config.tableCode = ''; panelFieldValues.value['config.fieldCode'] = ''; panelFieldValues.value['config.tableCode'] = ''; testResult.value = null }
  form.value.config = config; emit('load-doc-fields', ruleCode); applyChanges()
}
function testNode() {
  testLoading.value = true; testResult.value = null
  emit('test-node', { nodeId: form.value.nodeId, nodeType: form.value.nodeType, title: form.value.title, skillCode: form.value.skillCode, config: form.value.config, inputs: form.value.inputs, inputTypes: form.value.inputTypes, inputPorts: form.value.inputPorts, outputPorts: form.value.outputPorts,
    onSuccess: (data: any) => { testResult.value = { success: true, data }; testLoading.value = false },
    onError: (msg: string, errorData: any) => { testResult.value = { success: false, message: msg, data: errorData }; testLoading.value = false }
  })
}
function deleteNode() { if (form.value.nodeId) emit('delete-node', form.value.nodeId) }
function testDocExtract() {
  const nodeType = form.value.nodeType; const config = form.value.config
  if (!config.ruleCode) { ElMessage.warning('请先选择文档'); return }
  if (nodeType === 'docField' && !config.fieldCode) { ElMessage.warning('请先选择字段'); return }
  if (nodeType === 'docTable' && !config.tableCode) { ElMessage.warning('请先选择表格'); return }
  testLoading.value = true; testResult.value = null
  const body = nodeType === 'docField' ? { ruleCode: config.ruleCode, fieldCode: config.fieldCode, docType: config.docType || 'standard' } : { ruleCode: config.ruleCode, tableCode: config.tableCode, docType: config.docType || 'standard' }
  // 注意：父组件传入的 data 是 DocExtractionRule 的原始 payload（{fieldCode,value,confidence,message}
  // 或 {tableCode,rows,confidence,message}），本身没有 success 字段，必须在此包裹后再交给模板判定
  emit('test-doc-extract', { nodeType, body, onSuccess: (data: any) => { testResult.value = { success: true, data }; testLoading.value = false; ElMessage.success(data?.message || '测试完成') }, onError: (msg: string, errorData?: any) => { testResult.value = { success: false, message: msg, data: errorData }; testLoading.value = false; ElMessage.error(msg) } })
}

let _lastNodeId: string | null = null; let _updateTick = 0
watch(() => props.selectedNode, (node: any) => {
  if (node) {
    const currentNodeId = node.nodeId || node.id
    if (_lastNodeId === currentNodeId && form.value.title === node.title) return
    _lastNodeId = currentNodeId; _updateTick++
    // ★ 端口字段名归一化（2026-10-11）：后端 metadata 返回 PascalCase（Name/Label/BindMode…），
    //   而本组件与 PortControl 读小写（port.name / port.label / port.bindMode）。
    //   不归一化 ⇒ 端口标题整片空白 + bindMode 失效（本该「仅常量」的参数被渲染成「选择上游节点」下拉）。
    //   详见 composables/workflow/portNormalize.ts 的长注释。
    form.value = { nodeId: currentNodeId, nodeType: node.nodeType || 'skill', title: node.title || '', description: node.description || '', skillCode: node.skillCode || '', inputs: { ...(node.inputs || {}) }, outputs: { ...(node.outputs || {}) }, config: { ...(node.config || {}) }, inputPorts: normalizePorts(node.inputPorts), outputPorts: normalizePorts(node.outputPorts), inputTypes: { ...(node.inputTypes || {}) }, _branchEdges: node.branchEdges || [], _updateTick }
    const vals: Record<string, any> = {}; const types: Record<string, string> = {}
    for (const port of form.value.inputPorts) {
      vals[port.name] = form.value.inputs[port.name] ?? ''
      if (form.value.inputTypes[port.name]) types[port.name] = form.value.inputTypes[port.name]
      else { const val = vals[port.name]; if (val && (props.canvasNodes || []).some((n: any) => n.id === val)) types[port.name] = 'link'; else if (val) types[port.name] = 'constant'; else types[port.name] = 'link' }
    }
    inputValues.value = vals; inputTypes.value = types
    const pfv: Record<string, any> = {}; const schema = specialMeta.value?.panelSchema || []
    for (const field of schema) { const parts = field.field.split('.'); if (parts.length === 2 && parts[0] === 'config') pfv[field.field] = form.value.config[parts[1]] ?? field.defaultValue ?? ''; else pfv[field.field] = form.value.config[field.field] ?? field.defaultValue ?? '' }
    panelFieldValues.value = pfv; docList.value = props.docRules || []; fieldList.value = props.docFields || []; tableList.value = props.docTables || []
    // ★ 参数表初始化：只在**切换节点**时做一次（上面有同节点早退，编辑中的改动不会被冲掉）
    aiParams.value = parseAiParams(form.value.config)
    if ((node.nodeType === 'docField' || node.nodeType === 'docTable') && (node.config?.ruleCode || node.config?.docCode)) emit('load-doc-fields', node.config.ruleCode || node.config.docCode)
    // ★ 选中「标准文件缺失」节点时按**左树上下文**加载标准文件下拉
    //   （否则已配好的文件显示为空；标准/阶段不再由本面板选择，见 stdContext 注释）
    if (node.nodeType === 'is_std_file_missing') {
      syncStdContextToConfig()
      requestStdFiles()
    }
  } else { _lastNodeId = null; form.value = { nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [], inputTypes: {} }; inputValues.value = {}; inputTypes.value = {}; panelFieldValues.value = {}; aiParams.value = [] }
}, { immediate: true })

watch(() => props.docRules, (rules) => { docList.value = rules || [] }, { immediate: true })
watch(() => props.docFields, (fields) => { fieldList.value = fields || [] }, { immediate: true })
watch(() => props.docTables, (tables) => { tableList.value = tables || [] }, { immediate: true })

// ★ 左树上下文变化（切叶子 / 切阶段）→ 若当前正停在「标准文件缺失」节点上，
//   重载文件下拉并同步留痕。不做这一步的话，换阶段后下拉仍停在上一个阶段的文件列表，
//   用户会选到一个**不属于当前阶段**的标准文件（且执行期不报错）。
watch(() => props.stdContext, () => {
  if (form.value.nodeType !== 'is_std_file_missing' || !form.value.nodeId) return
  syncStdContextToConfig()
  requestStdFiles()
}, { deep: true })

function applyChanges() {
  if (!form.value.nodeId) return
  emit('update-node', { nodeId: form.value.nodeId, nodeType: form.value.nodeType, classCode: form.value.nodeType, title: form.value.title, description: form.value.description, skillCode: form.value.skillCode, inputs: { ...inputValues.value }, inputTypes: { ...inputTypes.value }, outputs: form.value.outputs, config: form.value.config, inputPorts: form.value.inputPorts, outputPorts: form.value.outputPorts })
}
</script>

<style scoped lang="less">
.node-property-form { display: flex; flex-direction: column; height: 100%; overflow: hidden; background: var(--yzh-color-bg-container, #fff); }
.node-form { padding: 16px; overflow-y: auto; flex: 1; }
.inspector-section { margin-bottom: 16px; }
.inspector-section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; padding-bottom: 6px; border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9); }
.inspector-section-title { font-size: 13px; font-weight: 700; color: var(--yzh-color-text-body, #334155); }
.inspector-field { margin-bottom: 10px; }
.inspector-label { font-size: 12px; color: var(--yzh-color-text-muted, #64748b); margin-bottom: 4px; display: flex; align-items: center; gap: 6px; }
.node-type-display { display: flex; align-items: center; gap: 6px; }
.type-dot { width: 8px; height: 8px; border-radius: 50%; }
.type-dot.success { background: var(--yzh-color-success, #67C23A); }
.type-dot.danger { background: var(--yzh-color-danger, #F56C6C); }
.type-dot.warning { background: var(--yzh-color-warning, #E6A23C); }
.type-dot.primary { background: var(--yzh-color-primary, #409EFF); }
.type-dot.info { background: var(--yzh-color-text-tertiary, #909399); }
.port-desc { font-size: 11px; color: var(--yzh-color-primary-disabled, #94a3b8); margin-bottom: 4px; }
/* ★ 2026-10-14：下拉空态/前置条件提示（按样式法条 S01–S12 用令牌 + 兜底，⛔ 不写裸 hex/px） */
.panel-hint { font-size: var(--yzh-font-size-xs, 12px); color: var(--yzh-color-text-muted, #64748b); margin-top: var(--yzh-space-1, 4px); line-height: 1.5; }
.branch-hint { display: flex; gap: 8px; align-items: flex-start; padding: 12px; background: #fffbeb; border: 1px solid #fef3c7; border-radius: 2px; font-size: 12px; color: #92400e; line-height: 1.5; margin-bottom: 16px; .el-icon { margin-top: 2px; flex-shrink: 0; font-size: 14px; } }
.output-item { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; background: var(--yzh-color-bg-page, #f8fafc); padding: 8px 12px; border: 1px dashed var(--yzh-color-border, #e2e8f0); border-radius: 2px; }
.output-type { color: var(--yzh-color-primary); font-family: monospace; font-weight: 700; font-size: 11px; }
.help-icon { color: var(--yzh-color-primary-disabled, #94a3b8); cursor: pointer; font-size: 16px; &:hover { color: var(--yzh-color-primary); } }
.test-result-wrapper { margin-bottom: 12px; }
.test-result { padding: 12px; border-radius: 4px; font-size: 12px; border: 1px solid var(--yzh-color-border, #e2e8f0); }
.test-result.test-success { background: #f0fdf4; border-color: #dcfce7; color: #166534; }
.test-result.test-fail { background: #fef2f2; border-color: #fee2e2; color: #991b1b; }
.test-header { display: flex; align-items: center; gap: 6px; font-weight: 700; margin-bottom: 8px; }
.test-message { margin-bottom: 8px; line-height: 1.5; word-break: break-all; }
.test-output-json { background: var(--yzh-color-bg-page, #f5f7fa); padding: 8px; border-radius: 4px; font-size: 11px; font-family: monospace; max-height: 150px; overflow-y: auto; margin: 0; }
/* ① 按 S01–S02 归一为令牌（修 R18：本文件基线 37，回潮到 44 会拦）；
   ② 按 S04 补显式 type（el-button 必须显式声明语义色）。 */
.unresolved-ref-panel { margin-top: var(--yzh-space-2, 8px); }
.unresolved-ref-list { display: flex; flex-wrap: wrap; gap: var(--yzh-space-2, 8px); margin-top: var(--yzh-space-2, 8px); }
.unresolved-ref-tag { display: inline-block; padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px); border-radius: var(--yzh-radius-sm, 4px); font-size: var(--yzh-font-size-xs, 12px); font-weight: 700; color: var(--yzh-color-warning, #d97706); background: var(--yzh-color-warning-light-7, #faecd8); border: 1px solid var(--yzh-color-warning-light-8, #f5dab1); }
/* ★ 2026-10-14：AI 节点参数表 —— 按样式法条 S01–S12 令牌 + 兜底（⛔ 无裸 hex / 字面量 px） */
.ai-param-item { display: flex; flex-direction: column; gap: var(--yzh-space-1, 4px); padding: var(--yzh-space-2, 8px); margin-bottom: var(--yzh-space-2, 8px); background: var(--yzh-color-bg-page, #f8fafc); border: 1px dashed var(--yzh-color-border, #e2e8f0); border-radius: var(--yzh-radius-sm, 4px); }
.ai-param-head { display: flex; align-items: center; gap: var(--yzh-space-1, 4px); .ai-param-name { width: 88px; flex-shrink: 0; } }
.ai-param-src { display: flex; align-items: center; gap: var(--yzh-space-1, 4px); .el-select { flex: 1; min-width: 0; } .ai-param-port { width: 120px; flex-shrink: 0; } }
.ai-param-add { width: 100%; margin-top: var(--yzh-space-1, 4px); }
.action-row { display: flex; gap: 8px; margin-top: 12px; .el-button { flex: 1; height: 32px; border-radius: 2px; font-weight: 700; } }
.form-empty { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; padding: 32px; text-align: center; }
</style>
