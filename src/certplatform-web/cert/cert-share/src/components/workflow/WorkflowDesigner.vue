<!--
  WorkflowDesigner.vue — 通用工作流设计器组件

  Props:
    - title: 页面标题（如 "NC 规则配置" / "报告内容设计"）
    - treeConfig: 树加载配置
    - saveConfig: 保存配置
    - workflowType: 'validation' | 'report'
    - executeConfig: 执行验证配置（enabled=false 时不显示运行按钮）

  Emits:
    - save-success: 保存成功后触发
    - execute-success: 执行成功后触发（含结果数据）
-->
<template>
  <div class="workflow-designer-page studio-layout">
    <div class="page-header">
      <div class="header-left">
        <el-icon><Setting /></el-icon>
        <span class="header-title">{{ title }}</span>
      </div>
    </div>

    <div class="page-body">
      <!-- ===== 左栏：统一4级树（可折叠） ===== -->
      <aside class="left-panel" v-show="leftPanelVisible">
        <div class="panel-header">
          <span>{{ treeLabel }}</span>
          <el-button link size="small" @click="refreshTree"><el-icon><Refresh /></el-icon></el-button>
        </div>
        <div class="tree-search">
          <el-input v-model="searchText" placeholder="搜索..." size="small" clearable :prefix-icon="Search" />
        </div>
        <div class="tree-container">
          <template v-for="org in treeData" :key="org.id">
            <div v-if="org.visible" class="tree-group">
              <div class="tree-node level-0" :class="{ expanded: org.expanded }" @click="toggleExpand(org)">
                <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                <el-icon class="type-icon org"><OfficeBuilding /></el-icon>
                <span class="tree-label">{{ org.label }}</span>
                <el-badge v-if="org.children?.length" :value="org.children.length" type="info" />
              </div>
              <template v-if="org.expanded && org.children">
                <template v-for="std in org.children" :key="std.id">
                  <div class="tree-node level-1" :class="{ expanded: std.expanded }" @click="toggleExpand(std)">
                    <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                    <el-icon class="type-icon standard"><Document /></el-icon>
                    <span class="tree-label">{{ std.label }}</span>
                    <el-badge v-if="std.children?.length" :value="std.children.length" type="info" />
                  </div>
                  <template v-if="std.expanded && std.children && std.visible">
                    <template v-for="phase in std.children" :key="phase.id">
                      <div class="tree-node level-2" :class="{ expanded: phase.expanded }" @click="togglePhase(phase, std, org)">
                        <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                        <el-icon class="type-icon phase"><Calendar /></el-icon>
                        <span class="tree-label">{{ phase.label }}</span>
                        <el-badge v-if="phase.children?.length" :value="phase.children.length" type="info" />
                      </div>
                      <div v-if="phase.visible && phase.expanded && phase.leafLoaded">
                        <template v-for="leaf in phase.children" :key="leaf.id">
                          <div
                            v-if="leaf.visible !== false"
                            class="tree-node level-3"
                            :class="{ active: currentLeaf?.id === leaf.id, configured: hasWorkflow(leaf) }"
                            @click="selectLeaf(leaf, phase)"
                          >
                            <el-icon class="type-icon rule" :class="{ configured: hasWorkflow(leaf) }">
                              <CircleCheck v-if="hasWorkflow(leaf)" /><Document v-else />
                            </el-icon>
                            <span class="tree-label">{{ leaf._Label || leaf[treeConfig.textField] || leaf[treeConfig.codeField] || leaf.id }}</span>
                          </div>
                        </template>
                      </div>
                    </template>
                  </template>
                </template>
              </template>
            </div>
          </template>
        </div>
      </aside>

      <div class="panel-toggle left-toggle" @click="toggleLeftPanel" :title="leftPanelVisible ? '收起规则树' : '展开规则树'">
        <el-icon><ArrowRight /></el-icon>
      </div>

      <main class="main-content">
        <div class="canvas-toolbar">
          <span class="canvas-title">{{ currentLeaf ? `工作流：${currentLeaf[treeConfig.textField] || currentLeaf[treeConfig.codeField] || '未命名'}` : '请选择项目' }}</span>
          <div class="toolbar-actions">
            <el-button size="small" @click="autoLayout"><el-icon><Grid /></el-icon> 布局</el-button>
            <el-button size="small" type="danger" plain @click="handleClearCanvas"><el-icon><Delete /></el-icon> 清空</el-button>
            <el-button size="small" @click="validateGraph"><el-icon><CircleCheck /></el-icon> 校验</el-button>
            <el-button size="small" :disabled="!currentLeaf" @click="historyVisible = true"><el-icon><Clock /></el-icon> 测试历史</el-button>
            <el-button v-if="executeConfig.enabled" type="success" size="small" :loading="executing" :disabled="!currentLeaf" @click="handleExecuteTest">
              <el-icon><VideoPlay /></el-icon> 运行
            </el-button>
            <el-button type="primary" size="small" :disabled="!currentLeaf || !store.state.dirty" @click="handleSave">
              <el-icon><Download /></el-icon> 保存
            </el-button>
          </div>
        </div>
        <ExecutionResultPanel v-if="executionResult" :result="executionResult" @close="executionResult = null" />
        <ExecutionHistoryDrawer
          v-model="historyVisible"
          :rule-code="currentRuleCode"
          :test-scope="'FULL'"
        />
        <div ref="canvasRef" class="canvas-container" @dragover.prevent="onCanvasDragOver" @drop.prevent="onCanvasDrop"></div>
        <div class="canvas-footer">
          <span>节点: {{ store.state.nodes.length }} | 边: {{ store.state.edges.length }} | 状态: {{ store.state.dirty ? '未保存' : '已保存' }}</span>
          <span v-if="currentLeaf" class="rule-code-text">{{ currentLeaf[treeConfig.codeField] }}</span>
        </div>
      </main>

      <div class="panel-toggle right-toggle" @click="toggleRightPanel" :title="rightPanelVisible ? '收起节点库/属性' : '展开节点库/属性'">
        <el-icon><ArrowRight /></el-icon>
      </div>

      <aside class="right-panel" v-show="rightPanelVisible">
        <el-tabs v-model="activeRightTab" class="studio-tabs">
          <el-tab-pane label="节点库" name="nodes">
            <div class="nodes-panel"><SkillPanel :skills="skills" :categories="categories" @add-node="handleAddNode" /></div>
          </el-tab-pane>
          <el-tab-pane label="属性" name="props">
            <div class="prop-panel-wrapper">
              <NodePropertyForm
                :key="`panel_${forceRefreshTick}`"
                :selected-node="selectedNode"
                :skills="skills"
                :doc-rules="docRules"
                :doc-fields="currentDocFields"
                :doc-tables="currentDocTables"
                :canvas-nodes="canvasNodesForPanel"
                @update-node="handleUpdateNode"
                @delete-node="handleDeleteNode"
                @load-doc-fields="onNodeDocChange"
                @link-node="handleLinkNode"
                @test-node="handleTestNode"
                @test-workflow="handleTestWorkflow"
                @test-doc-extract="handleTestDocExtract"
              />
            </div>
          </el-tab-pane>
        </el-tabs>
      </aside>
    </div>
  </div>
</template>

<script setup lang="ts">
import { yzhApi } from '@yzh-core/api/client'
import ExecutionResultPanel from './ExecutionResultPanel.vue'
import ExecutionHistoryDrawer from './ExecutionHistoryDrawer.vue'
import NodePropertyForm from './NodePropertyForm.vue'
import SkillPanel from './SkillPanel.vue'
import { analyzeWorkflowTopology, nodeStyle, setLogEnabled, setLogLevel } from '@share/composables/workflow/compiler'
import { deserialize, extractLayout, serialize } from '@share/composables/workflow/serializer'
import { useWorkflowStore } from '@share/composables/workflow/useWorkflowStore'
import { installLogicFlowPatch } from '@share/utils/logicflow-patch'
import {
  Calendar, CircleCheck, Clock, Delete, Document, Download, Grid,
  OfficeBuilding, Refresh, Search, Setting, VideoPlay, ArrowRight
} from '@element-plus/icons-vue'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'

installLogicFlowPatch()
setLogEnabled(true)
setLogLevel('INFO')

// ==================== Props ====================
/**
 * 树配置
 *
 * ⚠️ 命名铁律（项目全局规则 §16.9）：textField / codeField 必须填写后端实体的
 *    PascalCase 属性名（与 DB 列名、C# 属性名逐字一致），禁止写成 camelCase。
 *    字段名写错时本组件会输出控制台错误 + 页面提示（见 loadLeavesForPhase）。
 */
interface TreeConfig {
  loadApi: string
  loadMethod?: 'get' | 'post'
  loadBodyBuilder?: (filter: any) => Record<string, any>
  /** 详情接口前缀；选中叶子后 GET `${detailApi}/${Code}` 拉取最新详情（含工作流/布局大字段） */
  detailApi?: string
  textField: string
  codeField: string
}
interface SaveConfig {
  api: string
  getUrl?: (ctx: any) => string
  buildPayload?: (ctx: any) => Record<string, any>
}
interface ExecuteConfig {
  enabled?: boolean
  runApi?: string
}
interface Props {
  title: string
  treeConfig: TreeConfig
  saveConfig: SaveConfig
  workflowType?: string
  executeConfig?: ExecuteConfig
}
const props = withDefaults(defineProps<Props>(), {
  title: '工作流设计',
  workflowType: () => 'validation',
  executeConfig: () => ({ enabled: true, runApi: '/api/Workflow/test/run' })
})
const emit = defineEmits<{
  'save-success': [result: any]
  'execute-success': [result: any]
}>()

// ==================== State ====================
const activeRightTab = ref('nodes')
const canvasRef = ref<HTMLDivElement | null>(null)
const diagram = ref<any>(null)
let _resizeObserver: ResizeObserver | null = null
const leftPanelVisible = ref(true)
const rightPanelVisible = ref(true)
const store = useWorkflowStore() as any
const treeData = ref<any[]>([])
const searchText = ref('')
const currentLeaf = ref<any>(null)
/** 当前叶子所属阶段节点（保留引用，用于保存后回写树缓存） */
const currentPhase = ref<any>(null)
/** 当前树上下文过滤值 —— 与实体字段同名，PascalCase（§16.9 铁律） */
const currentFilter = reactive({ OrgCode: '', StandardCode: '', PhaseCode: '' })
const skills = ref<any[]>([])
const categories = ref<any[]>([])
const selectedNode = ref<any>(null)
const forceRefreshTick = ref(0)
const docRules = ref<any[]>([])
const currentDocFields = ref<any[]>([])
const currentDocTables = ref<any[]>([])
const savedTip = ref('')
const executing = ref(false)
const executionResult = ref<any>(null)
/** 测试历史抽屉可见性（阶段四） */
const historyVisible = ref(false)
const selectedEdgeId = ref<string | null>(null)
const _renamingNodeId = ref<string | null>(null)
const _renamingTimestamp = ref(0)
const RENAMING_GUARD_MS = 1000

/**
 * 当前选中叶子节点的规则编码 —— 传给测试历史抽屉作预置筛选。
 * 与 handleTestNode 里取 meta.RuleCode 的口径保持一致（先 Code 再 treeConfig.codeField）。
 */
const currentRuleCode = computed<string>(() => {
  const leaf = currentLeaf.value
  if (!leaf) return ''
  return leaf.Code || leaf[props.treeConfig.codeField] || ''
})

const treeLabel = computed(() => {
  const field = props.treeConfig.textField
  return field === 'SectionName' ? '机构 / 标准 / 阶段 / 报告章节' : '机构 / 标准 / 阶段 / NC检查项'
})
const canvasNodesForPanel = computed(() =>
  store.state.nodes.map((n: any) => ({ id: n.id, title: n.title || n.id, text: n.title || n.id, nodeType: n.nodeType }))
)

/**
 * 该叶子是否已配置工作流
 * 兼容两套实体字段：ValidationRule.RuleJson / ReportSection.WorkflowConfig（均为 PascalCase）
 */
function hasWorkflow(leaf: any): boolean {
  if (!leaf) return false
  return !!(leaf.WorkflowConfig || leaf.RuleJson)
}

/** 取叶子的工作流配置串 */
function getWorkflowConfig(leaf: any): string | null {
  if (!leaf) return null
  return leaf.WorkflowConfig || leaf.RuleJson || null
}

/** 取叶子的布局串 */
function getLayoutJson(leaf: any): string | null {
  if (!leaf) return null
  return leaf.LayoutJson || null
}

// ==================== Lifecycle ====================
onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  await nextTick()
  initDiagram()
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeyDown)
  // 卸载前最后一道防线：若此刻画布与 store 已经脱节，说明有交互路径绕过了
  // onNodeMoved / syncPositionsFromDiagram，坐标将在下次打开时回滚。仅开发构建告警。
  assertCanvasStoreConsistency('组件卸载前')
  _resizeObserver?.disconnect()
  diagram.value?.clearData?.()
  diagram.value = null
})

// ==================== Data Loading ====================
// 说明：实体接口（PagedResult<V>）返回 PascalCase —— ApiResponse 外层 data 为 camelCase，
//       其内的分页对象属性名为 Items/TotalCount/PageIndex/PageSize（§16.9 铁律）。
async function loadSkills() {
  try {
    const res = await yzhApi.post('/api/Workflow/WfSkill/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || []
    // skillCode/skillName/category/skillType 是节点模型内部字段（workflow schema），
    // 由实体字段 Code/Name/CategoryCode/SkillType 派生，属组件内部模型而非实体字段。
    skills.value = items.map((s: any) => ({
      ...s,
      skillCode: s.Code,
      skillName: s.Name,
      category: s.CategoryCode || '_default',
      skillType: s.SkillType || 'manual'
    }))
  } catch {}
}
async function loadCategories() {
  try {
    const res = await yzhApi.post('/api/Workflow/WfSkillCategory/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || []
    categories.value = items.map((c: any) => ({
      ...c,
      categoryCode: c.Code,
      categoryName: c.Name,
      color: c.Color || '#409EFF',
      sortOrder: c.SortOrder ?? 99
    }))
  } catch {}
}
async function loadTree() {
  try {
    const res = await yzhApi.get('/api/Workflow/StandardDirectory/organization-tree')
    const raw = res?.data?.Data || res?.data || res?.Data || []
    treeData.value = (raw as any[]).map((org: any) => ({
      ...org, expanded: true, visible: true,
      children: (org.children || []).map((std: any) => ({
        ...std, expanded: false, visible: true,
        children: (std.children || []).map((phase: any) => ({ ...phase, expanded: false, visible: true, children: [], leafLoading: false, leafLoaded: false }))
      }))
    }))
    applySearchFilter()
  } catch {}
}
async function loadDocRules() {
  try {
    const res = await yzhApi.get('/api/Workflow/DocExtractionRule/configured-rules')
    docRules.value = res?.data || res?.Data || []
  } catch {}
}
async function loadFieldsAndTables(ruleCode: string) {
  if (!ruleCode) return
  try {
    const res = await yzhApi.get(`/api/Workflow/DocExtractionRule/${ruleCode}/fields-tables`)
    const d = res?.data || res?.Data
    if (d) {
      currentDocFields.value = d.fields || d.Fields || []
      currentDocTables.value = d.tables || d.Tables || []
    }
  } catch {}
}

// ==================== Tree Operations ====================
/**
 * 搜索过滤（补齐历史项目遗漏的叶子级过滤）
 *
 * 过滤层级：机构 → 标准 → 阶段 → NC检查项/报告章节
 * - 上级命中：展示其下全部子树
 * - 叶子命中：反向上卷，逐级点亮祖先并自动展开阶段
 * - 叶子仅在「已加载」（phase.leafLoaded）时参与匹配；未展开阶段的叶子不预加载，避免搜索触发全量请求
 */
function applySearchFilter() {
  const kw = searchText.value?.toLowerCase().trim() || ''
  const { textField, codeField } = props.treeConfig

  treeData.value.forEach((org: any) => {
    const orgHit = !!kw && String(org.label ?? '').toLowerCase().includes(kw)
    let orgHasMatch = !kw || orgHit

    ;(org.children || []).forEach((std: any) => {
      const stdHit = !!kw && String(std.label ?? '').toLowerCase().includes(kw)
      // 上级（机构/标准）命中 → 该标准整棵子树放行；否则由叶子命中反向上卷
      const ancestorHit = !kw || orgHit || stdHit
      let stdHasMatch = !kw || stdHit

      ;(std.children || []).forEach((phase: any) => {
        const phaseHit = !!kw && String(phase.label ?? '').toLowerCase().includes(kw)
        // 注意：passThrough 只取「自身/祖先」命中，不能取兄弟阶段的命中结果，
        //       否则第一个命中阶段之后的所有兄弟阶段会被连带放行，把无关叶子也显示出来。
        const passThrough = ancestorHit || phaseHit
        let leafHit = false

        ;(phase.children || []).forEach((leaf: any) => {
          if (passThrough) { leaf.visible = true; return }
          const label = String(leaf._Label ?? leaf[textField] ?? leaf[codeField] ?? leaf.id ?? '')
          const code = String(leaf[codeField] ?? '')
          const hit = label.toLowerCase().includes(kw) || code.toLowerCase().includes(kw)
          leaf.visible = hit
          if (hit) leafHit = true
        })

        phase.visible = passThrough || leafHit
        if (leafHit) {
          phase.expanded = true   // 命中叶子时自动展开，否则用户看不到结果
          stdHasMatch = true
          orgHasMatch = true
        }
      })

      std.visible = ancestorHit || stdHasMatch
      if (stdHasMatch) orgHasMatch = true
    })

    org.visible = orgHasMatch
  })
}
watch(searchText, () => applySearchFilter())
function toggleExpand(node: any) { node.expanded = !node.expanded }
function toggleLeftPanel() { leftPanelVisible.value = !leftPanelVisible.value }
function toggleRightPanel() { rightPanelVisible.value = !rightPanelVisible.value }
async function togglePhase(phase: any, std: any, org: any) {
  // 切换阶段会重建画布 —— 存在未保存改动时先确认，避免静默丢失
  if (store.state.dirty) {
    try {
      await ElMessageBox.confirm('当前工作流有未保存的改动，切换阶段后将丢失。是否继续？', '未保存提示', {
        type: 'warning', confirmButtonText: '继续切换', cancelButtonText: '留在当前'
      })
    } catch { return }
  }
  phase.expanded = !phase.expanded
  // 读：组织树是服务层手写 DTO（camelCase，已登记例外）
  // 写：currentFilter 的键为实体字段名（PascalCase，§16.9 铁律）
  Object.assign(currentFilter, {
    OrgCode: org.cbCode || org.id,
    StandardCode: std.stdCode || phase.stdCode || std.standardCode,
    PhaseCode: phase.phaseCode
  })
  currentLeaf.value = null
  currentPhase.value = null
  selectedNode.value = null
  clearCanvas()
  if (phase.expanded && !phase.leafLoaded) await loadLeavesForPhase(phase)
}
async function loadLeavesForPhase(phase: any) {
  phase.leafLoading = true
  try {
    const filter = currentFilter
    let res: any
    if (props.treeConfig.loadMethod === 'post') {
      const body = props.treeConfig.loadBodyBuilder ? props.treeConfig.loadBodyBuilder(filter) : { Page: 1, PageSize: 200, Filters: [] }
      res = await yzhApi.post(props.treeConfig.loadApi, body)
    } else {
      // 查询串键名对应后端 C# 形参名（[FromQuery] string orgCode…），与 C# 侧逐字一致
      const params = new URLSearchParams({ orgCode: filter.OrgCode, standardCode: filter.StandardCode, phaseCode: filter.PhaseCode }).toString()
      res = await yzhApi.get(`${props.treeConfig.loadApi}?${params}`)
    }
    const items: any[] = res?.data?.Items || res?.data || []
    const { textField, codeField } = props.treeConfig
    // ── 契约校验 ──
    // 字段名写错（典型：实体是 PascalCase 却填了 camelCase）会让叶子渲染成空行，
    // 历史上该故障排查成本极高，此处主动暴露。
    if (items.length > 0 && items[0][textField] === undefined) {
      console.error(
        `[WorkflowDesigner] treeConfig.textField="${textField}" 在接口 ${props.treeConfig.loadApi} 返回数据中不存在。` +
        `依据 YZH 命名铁律（DB列名 = C#属性名 = TS字段名，PascalCase），请修正 treeConfig。`,
        '实际返回字段：', Object.keys(items[0])
      )
      ElMessage.error(`树配置字段「${textField}」与后端返回不一致，请按 PascalCase 修正 treeConfig`)
    }
    phase.children = items.map((item: any) => ({
      ...item,
      id: item.Code || item[codeField],
      _Label: item[textField],
      visible: true
    }))
    phase.leafLoaded = true
    applySearchFilter()   // 新加载的叶子立即套用当前搜索条件
  } catch { phase.children = [] } finally { phase.leafLoading = false }
}
const refreshTree = () => { loadTree() }

async function selectLeaf(leaf: any, phase: any) {
  // 切走会覆盖画布 —— 存在未保存改动时先确认
  if (store.state.dirty && currentLeaf.value?.id !== leaf.id) {
    try {
      await ElMessageBox.confirm('当前工作流有未保存的改动，切换后将丢失。是否继续？', '未保存提示', {
        type: 'warning', confirmButtonText: '继续切换', cancelButtonText: '留在当前'
      })
    } catch { return }
  }
  // ★ 引用同一性：currentLeaf 必须指向 phase.children 中的那个对象。
  //   若写成 { ...leaf } 浅拷贝，保存后的回写会落到副本上，
  //   切走再切回时读到树里未更新的旧值 → 表现为「保存的布局没有保存」。
  currentLeaf.value = leaf
  currentPhase.value = phase
  savedTip.value = ''
  currentFilter.OrgCode = phase.cbCode || currentFilter.OrgCode
  currentFilter.StandardCode = phase.stdCode || phase.standardCode || currentFilter.StandardCode
  currentFilter.PhaseCode = phase.phaseCode || currentFilter.PhaseCode

  // 先用列表数据渲染（列表已含工作流/布局字段时秒开）
  renderWorkflow(getWorkflowConfig(leaf), getLayoutJson(leaf))

  // 再按需拉详情覆盖（历史项目 selectRule 的等价能力，补齐后保证拿到最新大字段）
  const code = leaf.Code || leaf[props.treeConfig.codeField]
  if (props.treeConfig.detailApi && code) {
    try {
      const before = JSON.stringify([getWorkflowConfig(leaf), getLayoutJson(leaf)])
      const res = await yzhApi.get(`${props.treeConfig.detailApi}/${code}`)
      const d = res?.data
      if (d) {
        Object.assign(leaf, d)                              // 回写树里的原对象
        leaf._Label = leaf[props.treeConfig.textField]
        if (JSON.stringify([getWorkflowConfig(leaf), getLayoutJson(leaf)]) !== before) {
          renderWorkflow(getWorkflowConfig(leaf), getLayoutJson(leaf))
        }
      }
    } catch { /* 详情接口不可用时沿用列表数据，不阻断编辑 */ }
  }
}

// ==================== Diagram Initialization ====================
function initDiagram() {
  const ensureSizeThenInit = (attempt = 0) => {
    const el = canvasRef.value
    if (el && el.clientWidth > 50 && el.clientHeight > 50) { createLogicFlowInstance(); return }
    if (attempt >= 20) { console.warn('[WorkflowDesigner] 画布容器尺寸异常'); createLogicFlowInstance(); return }
    setTimeout(() => ensureSizeThenInit(attempt + 1), 100)
  }
  ensureSizeThenInit()
}
function createLogicFlowInstance() {
  const container = canvasRef.value
  if (!container) return
  diagram.value = new LogicFlow({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    container: container as any, grid: { size: 20, visible: true, type: 'mesh' }, background: { backgroundColor: '#fafbfc' },
    edgeType: 'polyline', allowResize: true, allowRotate: false, isSilentMode: false,
    stopScrollGraph: false, stopZoomGraph: false, stopMoveGraph: false, snapline: false,
    keyboard: { enabled: false }, textEditMode: false
  })
  try { diagram.value.render({ nodes: [], edges: [] }) } catch (e) { console.warn('[WorkflowDesigner] 初始空图渲染失败:', e) }

  diagram.value.on('node:click', ({ data }: any) => {
    const props_ = data.properties || {}
    const storeNode = store.getNodeById(data.id)
    const { inputs: syncedInputs, inputTypes: syncedInputTypes } = computeNodeInputsFromEdges(data.id, storeNode?.inputs || props_.inputs || {}, storeNode?.inputTypes || props_.inputTypes || {})
    if (JSON.stringify(syncedInputs) !== JSON.stringify(storeNode?.inputs || {}) || JSON.stringify(syncedInputTypes) !== JSON.stringify(storeNode?.inputTypes || {})) {
      store.setInputValue(data.id, null, null)
      if (storeNode) { storeNode.inputs = syncedInputs; storeNode.inputTypes = syncedInputTypes }
    }
    const nodeData: any = {
      nodeId: data.id, nodeType: props_.nodeType || 'skill', classCode: props_.classCode || props_.nodeType || 'skill',
      title: storeNode?.title || props_.title || data.text || '', skillCode: props_.skillCode || '',
      config: storeNode?.config || props_.config || {}, inputs: syncedInputs, inputTypes: syncedInputTypes,
      outputs: storeNode?.outputs || props_.outputs || {}, inputPorts: storeNode?.inputPorts || props_.inputPorts || [],
      outputPorts: storeNode?.outputPorts || props_.outputPorts || []
    }
    if (nodeData.nodeType === 'branch') {
      const gd = diagram.value?.getGraphData()
      const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === data.id)
      nodeData.branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
    }
    selectedNode.value = nodeData
    activeRightTab.value = 'props'
  })

  diagram.value.on('node:dbclick', ({ data }: any) => {
    const nodeType = data.properties?.nodeType || data.properties?.classCode
    if (nodeType === 'start') return
    promptEditNodeName(data.id, data.properties?.title || data.text || '')
  })

  diagram.value.on('edge:click', ({ data }: any) => {
    if (_renamingNodeId.value) return
    selectedNode.value = null
    selectedEdgeId.value = data.id
  })
  diagram.value.on('blank:click', () => {
    if (_renamingNodeId.value && Date.now() - _renamingTimestamp.value < RENAMING_GUARD_MS) return
    selectedNode.value = null
    selectedEdgeId.value = null
  })
  diagram.value.on('edge:add', (_event: any) => { autoSetBranchHandle(_event.data); onEdgeChange() })
  diagram.value.on('edge:delete', (_event: any) => { onEdgeChange() })

  // ── 节点拖拽结束 → 坐标同步回 store（补齐历史项目遗漏的能力）──
  // LogicFlow 2.2.5 事件：node:dragstart / node:drag / node:drop（payload = { e, data }）
  // 不监听会同时引发两个故障：
  //   ① store.nodes[].x/y 与画布脱节 → 保存的 LayoutJson 是拖动前的旧坐标
  //   ② store.dirty 不置位 → 「保存」按钮恒为禁用，用户无法保存自己排好的布局
  const onNodeMoved = ({ data }: any) => {
    if (!data?.id) return
    const storeNode = store.getNodeById(data.id)
    if (!storeNode) return
    if (storeNode.x === data.x && storeNode.y === data.y) return
    store.moveNode(data.id, data.x, data.y)
  }
  diagram.value.on('node:drop', onNodeMoved)
  diagram.value.on('node:drag', onNodeMoved)

  document.addEventListener('keydown', handleKeyDown)

  _resizeObserver = new ResizeObserver(() => {
    if (diagram.value && canvasRef.value) {
      const { clientWidth, clientHeight } = canvasRef.value
      if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight)
    }
  })
  if (canvasRef.value) _resizeObserver.observe(canvasRef.value)

  nextTick(() => {
    if (diagram.value && canvasRef.value) {
      const { clientWidth, clientHeight } = canvasRef.value
      if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight)
    }
  })
}

function handleKeyDown(e: KeyboardEvent) {
  const target = e.target as HTMLElement | null
  if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') return
  if (e.key !== 'Delete' && e.key !== 'Backspace') return
  e.preventDefault()
  if (selectedEdgeId.value && diagram.value) { diagram.value.deleteEdge(selectedEdgeId.value); selectedEdgeId.value = null; return }
  if (selectedNode.value?.nodeId && diagram.value) handleDeleteNode(selectedNode.value.nodeId)
}

function computeNodeInputsFromEdges(nodeId: string, currentInputs: any = {}, currentInputTypes: any = {}) {
  if (!diagram.value) return { inputs: currentInputs, inputTypes: currentInputTypes }
  const gd = diagram.value.getGraphData()
  const inEdges = (gd.edges || []).filter((e: any) => e.targetNodeId === nodeId)
  const nodeProps = gd.nodes.find((n: any) => n.id === nodeId)?.properties || {}
  const inputPorts = nodeProps.inputPorts || []
  const newInputs: any = {}
  const newTypes: any = {}
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const edge = inEdges.find((e: any) => { const h = e.properties?.targetHandle; return !h || h === port.name })
    if (edge) {
      if (currentInputTypes[port.name] === 'constant' && currentInputs[port.name]) continue
      newInputs[port.name] = edge.sourceNodeId
      newTypes[port.name] = 'link'
    }
  }
  const merged = { ...currentInputs, ...newInputs }
  const mergedTypes = { ...currentInputTypes, ...newTypes }
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const val = merged[port.name]
    if (val && gd.nodes.some((n: any) => n.id === val)) {
      const stillConnected = inEdges.some((e: any) => e.sourceNodeId === val)
      if (!stillConnected) { delete merged[port.name]; delete mergedTypes[port.name] }
    }
  }
  return { inputs: merged, inputTypes: mergedTypes }
}

function syncSelectedNodeInputs() {
  if (!selectedNode.value || !diagram.value) return
  const nodeId = selectedNode.value.nodeId
  const { inputs: merged, inputTypes: mergedTypes } = computeNodeInputsFromEdges(nodeId, selectedNode.value.inputs || {}, selectedNode.value.inputTypes || {})
  if (JSON.stringify(merged) !== JSON.stringify(selectedNode.value.inputs) || JSON.stringify(mergedTypes) !== JSON.stringify(selectedNode.value.inputTypes || {})) {
    diagram.value.setProperties(nodeId, { inputs: merged, inputTypes: mergedTypes })
    selectedNode.value = { ...selectedNode.value, inputs: merged, inputTypes: mergedTypes }
    store.setInputValue(nodeId, null, null)
    const storeNode = store.getNodeById(nodeId)
    if (storeNode) { storeNode.inputs = merged; storeNode.inputTypes = mergedTypes }
  }
}

function autoSetBranchHandle(edgeData: any) {
  if (!edgeData?.sourceNodeId || !edgeData?.targetNodeId) return
  const gd = diagram.value?.getGraphData()
  if (!gd) return
  const sourceNode = gd.nodes.find((n: any) => n.id === edgeData.sourceNodeId)
  if (!sourceNode) return
  if (sourceNode.properties?.nodeType !== 'branch' && sourceNode.properties?.classCode !== 'branch') return
  if (edgeData.properties?.sourceHandle) return
  const existingEdges = (gd.edges || []).filter((e: any) => e.sourceNodeId === edgeData.sourceNodeId && e.id !== edgeData.id)
  const hasSuccess = existingEdges.some((e: any) => e.properties?.sourceHandle === 'success')
  const handle = hasSuccess ? 'failure' : 'success'
  const label = handle === 'success' ? '成功' : '失败'
  const color = handle === 'success' ? '#67C23A' : '#F56C6C'
  diagram.value.setProperties(edgeData.id, { sourceHandle: handle })
  diagram.value.updateText(edgeData.id, label)
  const edge = gd.edges.find((e: any) => e.id === edgeData.id)
  if (edge) { edge.properties = { ...edge.properties, sourceHandle: handle }; edge.text = label; edge.style = { stroke: color, strokeWidth: 2 } }
}

function onEdgeChange() { syncSelectedNodeInputs(); const gd = diagram.value?.getGraphData(); if (gd) store.state.edges = (gd.edges || []).map((e: any) => ({ id: e.id, source: e.sourceNodeId, target: e.targetNodeId, sourceHandle: e.properties?.sourceHandle || null, targetHandle: e.properties?.targetHandle || null })) }

// ==================== Drag & Drop ====================
function onCanvasDragOver(event: DragEvent) { event.preventDefault(); event.dataTransfer!.dropEffect = 'copy' }
function onCanvasDrop(event: DragEvent) {
  event.preventDefault()
  if (!event.dataTransfer) return
  try {
    const raw = event.dataTransfer.getData('nodeData')
    if (!raw) return
    const item = JSON.parse(raw)
    const container = canvasRef.value
    if (diagram.value && container) {
      const hasSvg = !!container.querySelector('svg')
      if (!hasSvg) { const { clientWidth, clientHeight } = container; if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight) }
    }
    if (!diagram.value) { ElMessage.error('画布尚未初始化完成'); return }
    const lfPoint = diagram.value.getPointByClient(event.clientX, event.clientY)
    const pos = lfPoint?.canvasOverlayPosition || lfPoint
    const node = store.addNode(item, pos?.x ?? 200, pos?.y ?? 150)
    if (!node) return
    const category = item.category || skills.value.find((s: any) => s.skillCode === item.skillCode)?.category || ''
    const props_ = { classCode: node.classCode, nodeType: node.nodeType, title: node.title, skillCode: node.skillCode, config: node.config, inputs: node.inputs, inputTypes: node.inputTypes, outputs: node.outputs, inputPorts: node.inputPorts, outputPorts: node.outputPorts }
    if (node.nodeType === 'branch') (props_ as any).points = [[0, -30], [50, 0], [0, 30]]
    diagram.value.addNode({ id: node.id, type: lfShapeType(node.nodeType), x: pos?.x ?? 200, y: pos?.y ?? 150, text: node.title, style: nodeStyle(node.nodeType, node.skillCode, category), properties: props_ })
  } catch {}
}
function lfShapeType(nodeType: string) {
  if (nodeType === 'start' || nodeType === 'end') return 'circle'
  if (nodeType === 'branch') return 'polygon'
  return 'rect'
}

// ==================== Node Operations ====================
function handleAddNode(item: any) {
  const maxX = store.state.nodes.reduce((m: number, n: any) => Math.max(m, n.x || 0), 100)
  const maxY = store.state.nodes.reduce((m: number, n: any) => Math.max(m, n.y || 0), 80)
  const node = store.addNode(item, 120 + (maxX % 600), 80 + (maxY % 400))
  if (!node) return
  const category = item.category || skills.value.find((s: any) => s.skillCode === item.skillCode)?.category || ''
  const addProps = { classCode: node.classCode, nodeType: node.nodeType, title: node.title, skillCode: node.skillCode, config: node.config, inputs: node.inputs, inputTypes: node.inputTypes, outputs: node.outputs, inputPorts: node.inputPorts, outputPorts: node.outputPorts }
  if (node.nodeType === 'branch') (addProps as any).points = [[0, -30], [50, 0], [0, 30]]
  diagram.value.addNode({ id: node.id, type: lfShapeType(node.nodeType), x: node.x, y: node.y, text: node.title, style: nodeStyle(node.nodeType, node.skillCode, category), properties: addProps })
}

function handleUpdateNode(data: any) {
  if (!data.nodeId) return
  const storeNode = store.getNodeById(data.nodeId)
  const latestTitle = storeNode?.title || data.title
  const success = store.updateNode(data.nodeId, { title: latestTitle, classCode: data.classCode || data.nodeType, nodeType: data.nodeType, skillCode: data.skillCode, config: data.config, inputs: data.inputs, inputTypes: data.inputTypes, inputPorts: data.inputPorts, outputPorts: data.outputPorts })
  if (!success) { ElMessage.warning(`节点名称「${latestTitle}」已存在，请使用其他名称`); return }
  diagram.value.updateText(data.nodeId, latestTitle || data.skillCode || '')
  diagram.value.setProperties(data.nodeId, { classCode: data.classCode || data.nodeType, nodeType: data.nodeType, title: latestTitle, skillCode: data.skillCode, config: data.config, inputs: data.inputs, inputTypes: data.inputTypes, inputPorts: data.inputPorts, outputPorts: data.outputPorts })
  nextTick(() => {
    const sn = store.getNodeById(data.nodeId)
    if (sn) {
      let branchEdges: any = undefined
      if (sn.nodeType === 'branch' || sn.classCode === 'branch') {
        const gd = diagram.value?.getGraphData()
        const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === data.nodeId)
        branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
      }
      selectedNode.value = { nodeId: sn.id, nodeType: sn.nodeType, classCode: sn.classCode, title: sn.title, skillCode: sn.skillCode, config: { ...sn.config }, inputs: { ...sn.inputs }, inputTypes: { ...(sn.inputTypes || {}) }, outputs: { ...sn.outputs }, inputPorts: sn.inputPorts || [], outputPorts: sn.outputPorts || [], branchEdges }
      forceRefreshTick.value++
    }
  })
}

function promptEditNodeName(nodeId: string, currentName: string) {
  _renamingNodeId.value = nodeId
  _renamingTimestamp.value = Date.now()
  ElMessageBox.prompt('请输入节点名称', '编辑节点名称', { inputValue: currentName, inputPattern: /.+/, inputErrorMessage: '名称不能为空', confirmButtonText: '确定', cancelButtonText: '取消' })
    .then(({ value }: any) => {
      const name = (value || '').trim()
      if (!name) { _renamingNodeId.value = null; _renamingTimestamp.value = 0; return }
      if (!store.renameNode(nodeId, name)) { ElMessage.warning(`节点名称「${name}」已存在，请使用其他名称`); _renamingNodeId.value = null; _renamingTimestamp.value = 0; return }
      diagram.value.updateText(nodeId, name)
      diagram.value.setProperties(nodeId, { title: name })
      setTimeout(() => {
        const storeNode = store.getNodeById(nodeId)
        if (storeNode) {
          let branchEdges: any = undefined
          if (storeNode.nodeType === 'branch' || storeNode.classCode === 'branch') {
            const gd = diagram.value?.getGraphData()
            const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === nodeId)
            branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
          }
          selectedNode.value = { nodeId: storeNode.id, nodeType: storeNode.nodeType, classCode: storeNode.classCode, title: storeNode.title, skillCode: storeNode.skillCode, config: { ...storeNode.config }, inputs: { ...storeNode.inputs }, inputTypes: { ...(storeNode.inputTypes || {}) }, outputs: { ...storeNode.outputs }, inputPorts: storeNode.inputPorts || [], outputPorts: storeNode.outputPorts || [], branchEdges }
          forceRefreshTick.value++
        }
        _renamingNodeId.value = null; _renamingTimestamp.value = 0
      }, 300)
      ElMessage.success('节点名称已更新')
    })
    .catch(() => { _renamingNodeId.value = null; _renamingTimestamp.value = 0 })
}

function handleDeleteNode(nodeId: string) {
  if (!store.removeNode(nodeId)) { ElMessage.warning('开始节点不可删除'); return }
  diagram.value.deleteNode(nodeId)
  selectedNode.value = null
}

// ==================== Edge Operations ====================
function handleLinkNode({ portName, sourceNodeId, targetNodeId, sourceHandle }: any) {
  if (!diagram.value) return
  const gd = diagram.value.getGraphData()
  if (sourceHandle && portName === sourceHandle) {
    const toDelete = (gd.edges || []).filter((e: any) => e.sourceNodeId === sourceNodeId && e.properties?.sourceHandle === sourceHandle)
    for (const existing of toDelete) diagram.value.deleteEdge(existing.id)
    if (selectedEdgeId.value && toDelete.some((e: any) => e.id === selectedEdgeId.value)) selectedEdgeId.value = null
    if (targetNodeId) {
      const color = sourceHandle === 'success' ? '#67C23A' : '#F56C6C'
      const label = sourceHandle === 'success' ? '成功' : '失败'
      diagram.value.addEdge({ id: `e-${sourceNodeId}-${targetNodeId}-${Date.now()}`, type: 'polyline', sourceNodeId, targetNodeId, text: label, style: { stroke: color, strokeWidth: 2 }, properties: { sourceHandle, targetHandle: null } })
    }
    return
  }
  if (!targetNodeId) {
    const toDelete = (gd.edges || []).filter((e: any) => e.targetNodeId === targetNodeId && (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName)))
    for (const existing of toDelete) diagram.value.deleteEdge(existing.id)
    const storeNode = store.getNodeById(targetNodeId)
    if (storeNode && storeNode.inputs) delete storeNode.inputs[portName]
    if (storeNode && storeNode.inputTypes) delete storeNode.inputTypes[portName]
    onEdgeChange()
    return
  }
  const edge = store.connect(sourceNodeId, targetNodeId, null, portName)
  if (!edge) return
  const existingEdges = (gd.edges || []).filter((e: any) => e.targetNodeId === targetNodeId && (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName)))
  for (const existing of existingEdges) diagram.value.deleteEdge(existing.id)
  const sourceNode = gd.nodes.find((n: any) => n.id === sourceNodeId)
  const sourceType = sourceNode?.properties?.nodeType || sourceNode?.properties?.classCode
  let autoHandle: string | null = null
  if (sourceType === 'branch') {
    const branchEdges = (gd.edges || []).filter((e: any) => e.sourceNodeId === sourceNodeId)
    const hasSuccess = branchEdges.some((e: any) => e.properties?.sourceHandle === 'success')
    autoHandle = hasSuccess ? 'failure' : 'success'
  }
  diagram.value.addEdge({ id: edge.id, type: 'polyline', sourceNodeId, targetNodeId, properties: { sourceHandle: autoHandle, targetHandle: portName || null } })
}

// ==================== Canvas Operations ====================
function clearCanvas() {
  if (!diagram.value) return
  try { const gm = diagram.value.graphModel; if (gm) { gm.edges = []; gm.nodes = [] } else { diagram.value.render({ nodes: [], edges: [] }) } } catch {}
  store.clearAll()
}
function handleClearCanvas() {
  if (!store.state.nodes.length) { ElMessage.info('画布已为空'); return }
  ElMessageBox.confirm('确认清空画布上的所有节点和连线？', '清空确认', { type: 'warning' })
    .then(() => { clearCanvas(); selectedNode.value = null; ensureStartNode(); ElMessage.success('画布已清空') })
    .catch(() => {})
}
function ensureStartNode() {
  if (!diagram.value) return
  const hasStart = store.state.nodes.some((n: any) => n.classCode === 'start' || n.nodeType === 'start')
  if (!hasStart) {
    const startItem = { classCode: 'start', className: '开始' }
    const node = store.addNode(startItem, 100, 150)
    if (node) diagram.value.addNode({ id: node.id, type: 'circle', x: 100, y: 150, text: node.title, style: nodeStyle('start'), properties: { classCode: 'start', nodeType: 'start', title: node.title, skillCode: '', config: {}, inputs: {}, outputs: {}, inputPorts: [], outputPorts: [] } })
  }
}
function renderWorkflow(workflowConfig: any, layoutJson: any) {
  // 未配置工作流：清空 + 放一个起始节点，并标记为「已保存」
  // （ensureStartNode → store.addNode 会置脏，此处必须复位，否则选中空白叶子就出现未保存态）
  if (!workflowConfig) { clearCanvas(); ensureStartNode(); store.markClean(); return }
  try {
    const config = typeof workflowConfig === 'string' ? JSON.parse(workflowConfig) : workflowConfig
    const layout = layoutJson ? (typeof layoutJson === 'string' ? JSON.parse(layoutJson) : layoutJson) : null
    const { nodes, edges, idGenerator, migrated } = deserialize(config, layout)
    store.loadFromData(nodes, edges)
    // @ts-ignore - idGenerator needs to be set on state
    store.state.idGenerator = idGenerator
    store.markClean()
    const lfNodes = nodes.map((n: any) => {
      const nodeProps = { classCode: n.classCode, nodeType: n.nodeType, title: n.title, skillCode: n.skillCode, config: n.config, inputs: n.inputs, inputTypes: n.inputTypes || {}, outputs: n.outputs, inputPorts: n.inputPorts, outputPorts: n.outputPorts }
      if (n.nodeType === 'branch') (nodeProps as any).points = [[0, -30], [50, 0], [0, 30]]
      return { id: n.id, type: lfShapeType(n.nodeType), x: n.x, y: n.y, text: n.title || n.skillCode || n.id, style: nodeStyle(n.nodeType, n.skillCode), properties: nodeProps }
    })
    const lfEdges = edges.map((e: any) => {
      const isBranchAnchor = e.sourceHandle === 'success' || e.sourceHandle === 'failure'
      return { id: e.id, type: 'polyline', sourceNodeId: e.source, targetNodeId: e.target, text: isBranchAnchor ? (e.sourceHandle === 'success' ? '成功' : '失败') : '', style: isBranchAnchor ? { stroke: e.sourceHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 } : { stroke: '#5B8FF9', strokeWidth: 2 }, properties: { sourceHandle: e.sourceHandle || null, targetHandle: e.targetHandle || null } }
    })
    diagram.value.render({ nodes: lfNodes, edges: lfEdges })
    if (migrated && !sessionStorage.getItem('_wf_migration_tip_shown')) { ElMessage.info('旧格式节点 ID 已自动迁移重编号'); sessionStorage.setItem('_wf_migration_tip_shown', '1') }
  } catch (e) { ElMessage.error('工作流配置解析失败') }
}

// ==================== Layout / Validate / Save ====================
/**
 * 开发期一致性断言：比对 store 中的节点坐标与画布实际坐标。
 *
 * 背景：本项目历史上出现过「画布看起来动了、store 没动」类缺陷 —— 落库的
 * `LayoutJson` 与所见不一致，切走再切回坐标回滚、刷新后也保持不住。
 * 根因是 LogicFlow 的部分 API（如 `nodeModel.setProperties`）只写 `properties`
 * 而**不移动坐标**，而 `store.state.nodes` 才是序列化落库的唯一来源。
 *
 * 因此凡是「画布 → 落库」的临界点（保存前、卸载前）都要比对一次。
 * 仅开发构建生效（`import.meta.env.DEV`），生产构建整段被 tree-shake 掉。
 *
 * 坐标口径说明：`graphModel` 的 `node.x / node.y` 是**节点中心**，与
 * `store.state.nodes[].x / y` 同一口径（`syncPositionsFromDiagram` 依赖该等价关系）。
 */
function assertCanvasStoreConsistency(stage: string) {
  if (!import.meta.env.DEV) return
  const gd = diagram.value?.getGraphData?.()
  if (!gd?.nodes?.length) return

  const canvasMap = new Map<string, { x: number; y: number }>(
    gd.nodes.map((n: any) => [n.id, { x: Number(n.x) || 0, y: Number(n.y) || 0 }])
  )
  const drifts: string[] = []
  // 容差 0.5px：LogicFlow 内部存在取整，避免把浮点误差报成缺陷
  const TOLERANCE = 0.5

  for (const n of store.state.nodes as any[]) {
    const c = canvasMap.get(n.id)
    if (!c) { drifts.push(`${n.id} —— store 有 / 画布无`); continue }
    const sx = Number(n.x) || 0
    const sy = Number(n.y) || 0
    if (Math.abs(sx - c.x) > TOLERANCE || Math.abs(sy - c.y) > TOLERANCE) {
      drifts.push(`${n.id} —— store(${sx}, ${sy}) ≠ 画布(${c.x}, ${c.y})`)
    }
  }
  for (const gn of gd.nodes) {
    if (!store.getNodeById?.(gn.id)) drifts.push(`${gn.id} —— 画布有 / store 无`)
  }

  if (drifts.length) {
    console.error(
      `[WorkflowDesigner] 画布与 store 坐标脱节（${stage}）：落库的 LayoutJson 将与所见不符。\n` +
      drifts.map((d) => `  • ${d}`).join('\n') +
      '\n  排查方向：该交互是否绕过了 node:drop / node:drag 监听，或改用了只写 properties 的 API。'
    )
  }
}

/**
 * 以画布为准回读节点坐标写入 store（保存 / 切换前的兜底）。
 * 即使 node:drop 因 LogicFlow 版本差异未触发，也不会保存出与所见不符的坐标。
 */
function syncPositionsFromDiagram() {
  const gd = diagram.value?.getGraphData?.()
  if (!gd?.nodes?.length) return
  for (const gn of gd.nodes) {
    const storeNode = store.getNodeById(gn.id)
    if (storeNode && (storeNode.x !== gn.x || storeNode.y !== gn.y)) {
      storeNode.x = gn.x
      storeNode.y = gn.y
      store.markDirty()
    }
  }
}

function autoLayout() {
  const nodes = store.state.nodes
  if (!nodes.length) return
  const inDeg: any = {}, adj: any = {}
  nodes.forEach((n: any) => { inDeg[n.id] = 0; adj[n.id] = [] })
  store.state.edges.forEach((e: any) => { if (inDeg[e.target] !== undefined) { inDeg[e.target]++; adj[e.source].push(e.target) } })
  const queue = nodes.filter((n: any) => inDeg[n.id] === 0).map((n: any) => n.id)
  const ordered: string[] = []
  while (queue.length) { const c = queue.shift(); ordered.push(c!); for (const n of adj[c] || []) { inDeg[n]--; if (inDeg[n] === 0) queue.push(n) } }
  nodes.forEach((n: any) => { if (!ordered.includes(n.id)) ordered.push(n.id) })
  const posMap: any = {}
  ordered.forEach((id: string, idx: number) => { posMap[id] = { x: 120 + (idx % 4) * 240, y: 80 + Math.floor(idx / 4) * 140 } })
  // ⚠️ 必须先「移动画布」再「改 store」。
  //    setProperties 只把键值写进 node.properties，不会移动节点坐标（画布看起来纹丝不动，
  //    但 store 已改 → 保存下来的坐标与所见不一致）。必须走 graphModel.moveNode2Coordinate。
  //    而 moveNode 的兜底分支要靠「画布当前坐标」算增量 —— 若先把 store 改成目标值，
  //    增量会恒为 0，画布依旧不动，正是「脱节」缺陷本身。
  const gm = diagram.value?.graphModel
  for (const n of nodes) {
    const p = posMap[n.id]
    if (!p || !gm) continue
    if (typeof gm.moveNode2Coordinate === 'function') {
      gm.moveNode2Coordinate(n.id, p.x, p.y)
    } else if (typeof gm.moveNode === 'function') {
      const gn = typeof gm.getNodeModelById === 'function' ? gm.getNodeModelById(n.id) : null
      const fromX = Number(gn?.x ?? n.x) || 0
      const fromY = Number(gn?.y ?? n.y) || 0
      gm.moveNode(n.id, p.x - fromX, p.y - fromY)
    }
  }
  for (const n of nodes) { if (posMap[n.id]) { n.x = posMap[n.id].x; n.y = posMap[n.id].y } }
  store.markDirty()
  assertCanvasStoreConsistency('自动布局后')
  ElMessage.success('自动布局完成')
}

function validateGraph() {
  const nodes = store.state.nodes
  const edges = store.state.edges
  if (!nodes.length) { ElMessage.warning('画布为空'); return }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(nodes, edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const analysis = analyzeWorkflowTopology(config)
  const { validation, paths, summary } = analysis
  const nodeTitleMap: any = {}
  for (const n of nodes) nodeTitleMap[n.id] = n.title || n.id
  const pathDetails = paths.map((p: any, idx: number) => {
    const nodeNames = p.nodes.map((id: string) => nodeTitleMap[id] || id)
    const branchInfo = p.branchDecisions.length > 0 ? ` (${p.branchDecisions.map((d: any) => `${nodeTitleMap[d.at] || d.at}:${d.choice === 'success' ? '✓' : '✗'}`).join(', ')})` : ''
    return `  路径${idx + 1}: ${nodeNames.join(' → ')}${branchInfo}`
  }).join('\n')
  const pathInfo = paths.length > 0 ? `\n\n📋 拓扑路径 (${summary.totalPaths}): ${pathDetails}` : '\n\n📋 无有效路径'
  if (!validation.valid) {
    const errorMessages = validation.errors.map((e: any) => `  ✗ ${e.message}`).join('\n')
    const warnMessages = validation.warnings.length > 0 ? '\n\n' + validation.warnings.map((w: any) => `  ⚠ ${w.message}`).join('\n') : ''
    ElMessageBox.alert(`校验失败！发现 ${validation.errors.length} 个错误：\n\n${errorMessages}${warnMessages}${pathInfo}`, '拓扑校验结果', { type: 'error', confirmButtonText: '确定' })
    return false
  }
  if (validation.warnings.length > 0) {
    const warnMessages = validation.warnings.map((w: any) => `  ⚠ ${w.message}`).join('\n')
    ElMessageBox.alert(`校验通过（有警告）：\n\n${warnMessages}${pathInfo}`, '拓扑校验结果', { type: 'warning', confirmButtonText: '确定' })
    return true
  }
  ElMessageBox.alert(`✓ 校验通过！\n\n节点: ${summary.totalNodes} | 边: ${summary.totalEdges} | 路径: ${summary.totalPaths}${pathInfo}`, '拓扑校验结果', { type: 'success', confirmButtonText: '确定' })
  return true
}

async function handleSave() {
  if (!currentLeaf.value) { ElMessage.warning('请先选择项目'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }
  // 开发期守卫：保存前先比对一次，若已脱节则报出具体节点（仅在同步之前才检测得到）
  assertCanvasStoreConsistency('保存前')
  // 兜底：以画布为准同步一次坐标，确保落库的 LayoutJson 与所见一致
  syncPositionsFromDiagram()
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const layout = extractLayout(store.state.nodes)
  const leafName = currentLeaf.value[props.treeConfig.textField] || currentLeaf.value[props.treeConfig.codeField] || ''
  try {
    await ElMessageBox.confirm(`保存工作流到「${leafName}」？`, '保存确认', { type: 'info' })
    const ctx = { leaf: currentLeaf.value, filter: currentFilter, config, layout }
    const url = props.saveConfig.getUrl ? props.saveConfig.getUrl(ctx) : props.saveConfig.api
    const payload = props.saveConfig.buildPayload ? props.saveConfig.buildPayload(ctx) : {
      Code: currentLeaf.value.Code,
      WorkflowConfig: JSON.stringify(config),
      LayoutJson: JSON.stringify(layout)
    }
    const res = await yzhApi.post(url, payload)
    // 兼容两种返回体：ApiResponse（success 布尔）/ 自定义 { code, message }
    const failed = res?.success === false || (typeof res?.code === 'number' && res.code >= 400)
    if (!failed) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
      // ★ 回写「树里的那个对象」—— currentLeaf 是引用而非拷贝，因此这次回写
      //   会同时更新 phase.children 的缓存，切走再切回不会再回滚。
      //   两套实体字段名同时覆盖：ValidationRule.RuleJson / ReportSection.WorkflowConfig
      const configStr = JSON.stringify(config)
      const layoutStr = JSON.stringify(layout)
      Object.assign(currentLeaf.value, {
        RuleJson: configStr,
        LayoutJson: layoutStr,
        WorkflowConfig: configStr
      })
      store.markClean()
      emit('save-success', res)
      ElMessage.success('工作流保存成功')
    } else ElMessage.error(res?.err || res?.message || '保存失败')
  } catch (e: any) { if (e?.message !== 'cancel') ElMessage.error('保存失败') }
}

// ==================== Execution ====================
async function handleExecuteTest() {
  if (!currentLeaf.value) { ElMessage.warning('请先选择项目'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const analysis = analyzeWorkflowTopology(config)
  if (!analysis.validation.valid) {
    const errorMessages = analysis.validation.errors.map((e: any) => `  ✗ ${e.message}`).join('\n')
    ElMessageBox.alert(`拓扑校验失败，请先修复以下问题后再执行验证：\n\n${errorMessages}`, '执行验证 - 拓扑校验失败', { type: 'error', confirmButtonText: '确定' })
    return
  }
  executing.value = true
  executionResult.value = null
  try {
    const runApi = props.executeConfig?.runApi || '/api/Workflow/test/run'
    // 请求体字段名与后端 TaskExecutionRequest 的 C# 属性名逐字一致（PascalCase，YZH 命名铁律）
    const res = await yzhApi.post(runApi, {
      TaskType: 'TEST',
      RuleCode: currentLeaf.value.Code || currentLeaf.value.RuleCode,
      EnterpriseCode: currentLeaf.value.EnterpriseCode || 'YZH-STD-ENT',
      StandardCode: currentLeaf.value.StandardCode || currentFilter.StandardCode,
      PhaseCode: currentLeaf.value.PhaseCode || currentFilter.PhaseCode,
      ConfigJson: JSON.stringify(config)
    })
    if (res?.success !== false && res?.data) {
      // TaskExecutionResponse 同为 PascalCase（§16.9 铁律）
      executionResult.value = res.data
      if (res.data.Status === 'completed' && res.data.IsSuccess) {
        ElMessage.success(`执行完成：${res.data.Status} (${res.data.DurationMs}ms)`)
        emit('execute-success', res.data)
      } else {
        const errorMsg = res.data.NcResult?.error || res.data.Status || '执行失败'
        ElMessageBox.alert(`工作流执行失败：\n\n${errorMsg}`, '执行验证 - 执行失败', { type: 'error', confirmButtonText: '确定' })
      }
    } else {
      const errorMsg = res?.error || res?.err || res?.message || '执行失败'
      ElMessageBox.alert(`工作流执行失败：\n\n${errorMsg}`, '执行验证 - 执行失败', { type: 'error', confirmButtonText: '确定' })
    }
  } catch (e: any) { ElMessage.error('执行验证失败: ' + (e?.message || e)) }
  finally { executing.value = false }
}

// ==================== Node Test ====================
/**
 * 单节点 / AI 节点测试
 *
 * ⚠️ 2026-09-22 阶段二：后端两个入口已统一走 WfExecutionTaskService 并**落库**
 *   （wf_execution_task.TestScope = NODE / AI_NODE，wf_node_execution 有对应行），
 *   因此这里必须补发测试上下文元数据，否则落库的任务无法归属到规则/企业/阶段，也就无法回溯。
 *   响应新增 TaskCode（本次测试的任务编码），可凭它去日志/DB 里定位这次测试。
 */
async function handleTestNode(nodeData: any) {
  // 测试上下文元数据（可选字段，后端缺省时按 FULL / YZH-STD-ENT 兜底）
  const meta = {
    RuleCode: currentLeaf.value?.Code || currentLeaf.value?.RuleCode || '',
    EnterpriseCode: currentLeaf.value?.EnterpriseCode || 'YZH-STD-ENT',
    StandardCode: currentLeaf.value?.StandardCode || currentFilter.StandardCode || '',
    PhaseCode: currentLeaf.value?.PhaseCode || currentFilter.PhaseCode || ''
  }
  try {
    if (nodeData.nodeType === 'ai_node') {
      const mockOutputs: any = {}
      const nodes = store.state.nodes
      const customParams = nodeData.config?.customParams || []
      let parsedCustomParams = customParams
      if (typeof customParams === 'string') { try { parsedCustomParams = JSON.parse(customParams) } catch { parsedCustomParams = [] } }
      for (const param of parsedCustomParams) {
        if (param?.sourceType === 'link' && param?.sourceConfig?.nodeId) {
          const sourceNode = nodes.find((n: any) => n.id === param.sourceConfig.nodeId)
          if (sourceNode) mockOutputs[param.sourceConfig.nodeId] = { result: `[${sourceNode.title || param.sourceConfig.nodeId}]`, success: true }
        }
      }
      let ruleJson = ''
      try { ruleJson = JSON.stringify(serialize(nodes, store.state.edges, { version: 1, workflowType: props.workflowType })) } catch { ruleJson = '' }
      // 请求体字段名与后端 AiNodeTestRequest / WorkflowContext 的 C# 属性名逐字一致（PascalCase）
      const testBody = {
        ...meta,
        NodeId: nodeData.nodeId, NodeType: 'ai_node', Title: nodeData.title,
        Config: { ...nodeData.config, customParams: JSON.stringify(parsedCustomParams) },
        Inputs: nodeData.inputs || {}, InputTypes: nodeData.inputTypes || {},
        InputPorts: nodeData.inputPorts || [], OutputPorts: nodeData.outputPorts || [],
        WorkflowContext: {
          RuleJson: ruleJson,
          ContextParams: currentLeaf.value ? { EnterpriseCode: meta.EnterpriseCode, StandardCode: meta.StandardCode, PhaseCode: meta.PhaseCode } : {},
          MockOutputs: mockOutputs
        }
      }
      const res = await yzhApi.post('/api/Workflow/test/ai-node', testBody)
      if (res?.success && res.data) nodeData.onSuccess(res.data)
      else nodeData.onError(res?.error || res?.err || res?.message || 'AI 节点测试失败', null)
      return
    }
    const res = await yzhApi.post('/api/Workflow/test/node', {
      ...meta,
      NodeId: nodeData.nodeId, NodeType: nodeData.nodeType, Title: nodeData.title,
      SkillCode: nodeData.skillCode, Config: nodeData.config, Inputs: nodeData.inputs,
      InputTypes: nodeData.inputTypes, InputPorts: nodeData.inputPorts, OutputPorts: nodeData.outputPorts
    })
    if (res?.success && res.data) nodeData.onSuccess(res.data)
    else nodeData.onError(res?.error || res?.err || res?.message || '测试失败', null)
  } catch (e: any) { nodeData.onError('请求失败: ' + (e?.message || e), null) }
}

async function handleTestWorkflow(nodeData: any) {
  if (!currentLeaf.value) { nodeData.onError('请先选择项目', null); return }
  if (!store.state.nodes.length) { nodeData.onError('画布为空', null); return }
  try {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
    const res = await yzhApi.post('/api/Workflow/test/run', {
      TaskType: 'TEST',
      RuleCode: currentLeaf.value.Code || currentLeaf.value.RuleCode,
      EnterpriseCode: currentLeaf.value.EnterpriseCode || 'YZH-STD-ENT',
      StandardCode: currentLeaf.value.StandardCode || currentFilter.StandardCode,
      PhaseCode: currentLeaf.value.PhaseCode || currentFilter.PhaseCode,
      ConfigJson: JSON.stringify(config)
    })
    if (res?.success && res.data) nodeData.onSuccess(res.data)
    else nodeData.onError(res?.error || res?.err || res?.message || '测试失败', null)
  } catch (e: any) { nodeData.onError('请求失败: ' + (e?.message || e), null) }
}

/**
 * 文档提取节点配置期试运行（docField / docTable）
 *
 * ★ 2026-09-25 P2 起：原例外 E7（`Ok(new { code, data, message })` 无 success）已消灭，
 *    DocExtractionRuleController 全系列已改标准 ApiResponse —— `yzhApi` 原样透传，
 *    判定用 `res.success`，失败详情读 `res.err`，载荷读 `res.data`。
 *
 * ⚠️ 后端 TestFieldAsync / TestTableAsync 永不抛异常、信封 success 恒为 true，
 *    失败语义藏在 payload 内（value / rows 为空，message 说明原因），
 *    故这里按 payload 实际内容判定成功与否，而非只看信封。
 */
async function handleTestDocExtract({ nodeType, body, onSuccess, onError }: any) {
  try {
    const url = nodeType === 'docField' ? '/api/Workflow/DocExtractionRule/test-field' : '/api/Workflow/DocExtractionRule/test-table'
    const res: any = await yzhApi.post(url, body)
    const data = res?.data ?? res?.Data
    if (res?.success === false) { onError(res?.err || res?.message || '测试失败', data); return }
    if (!data) { onError(res?.err || res?.message || '测试失败：后端未返回数据'); return }
    // 成功判据：字段有实际值 / 表格有数据行
    const ok = nodeType === 'docField'
      ? data.value !== undefined && data.value !== null && data.value !== ''
      : Array.isArray(data.rows) && data.rows.length > 0
    if (ok) onSuccess(data)
    else onError(data.message || '提取失败：未取到值', data)
  } catch (e: any) { onError('请求失败: ' + (e?.message || e)) }
}

function onNodeDocChange(ruleCode: string) { if (ruleCode) loadFieldsAndTables(ruleCode) }
</script>

<style scoped lang="less">
.studio-layout { display: flex; flex-direction: column; height: 100%; background: #f1f5f9; }
.page-header { display: flex; align-items: center; padding: 12px 20px; background: #fff; border-bottom: 1px solid #e2e8f0; flex-shrink: 0; }
.header-left { display: flex; align-items: center; gap: 8px; }
.header-title { font-size: 16px; font-weight: 700; color: #1e293b; }
.page-body { display: flex; flex: 1; min-height: 0; min-width: 0; overflow: hidden; }
.left-panel { width: 280px; min-width: 200px; flex-shrink: 1; flex-basis: 280px; background: #fff; border-right: 1px solid #e2e8f0; display: flex; flex-direction: column; overflow: hidden; }
.right-panel { width: 360px; min-width: 260px; flex-shrink: 1; flex-basis: 360px; background: #fff; border-left: 1px solid #e2e8f0; display: flex; flex-direction: column; overflow: hidden; }
.panel-toggle { width: 14px; flex-shrink: 0; display: flex; align-items: center; justify-content: center; cursor: pointer; background: #fff; color: #94a3b8; transition: all 0.2s; }
.panel-toggle:hover { background: #f1f5f9; color: #2563eb; }
.panel-toggle .el-icon { transform: rotate(180deg); }
.panel-toggle.left-toggle { border-right: 1px solid #e2e8f0; }
.panel-toggle.left-toggle:hover .el-icon { transform: rotate(0deg); }
.panel-toggle.right-toggle { border-left: 1px solid #e2e8f0; }
.panel-toggle.right-toggle .el-icon { transform: rotate(0deg); }
.panel-toggle.right-toggle:hover .el-icon { transform: rotate(180deg); }
.panel-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; font-size: 13px; font-weight: 700; color: #334155; border-bottom: 1px solid #f1f5f9; }
.tree-search { padding: 8px 12px; border-bottom: 1px solid #f1f5f9; }
.tree-container { flex: 1; overflow-y: auto; padding: 8px; }
.main-content { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.canvas-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 16px; background: #fff; border-bottom: 1px solid #e2e8f0; overflow: hidden; }
.canvas-title { font-size: 13px; font-weight: 600; color: #475569; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; flex-shrink: 1; }
.toolbar-actions { display: flex; gap: 4px; flex-shrink: 0; }
.canvas-container { flex: 1; min-height: 0; background: #f8fafc; min-width: 240px; }
.canvas-footer { padding: 6px 16px; font-size: 12px; color: #94a3b8; background: #fff; border-top: 1px solid #e2e8f0; }
.studio-tabs { height: 100%; display: flex; flex-direction: column; }
:deep(.el-tabs__header) { margin: 0; padding: 0 16px; background: #fafbfc; border-bottom: 1px solid #e2e8f0; }
:deep(.el-tabs__content) { flex: 1; overflow: hidden; }
:deep(.el-tab-pane) { height: 100%; overflow-y: auto; }
.nodes-panel, .prop-panel-wrapper { height: 100%; }
.tree-node { display: flex; align-items: center; padding: 6px 8px; cursor: pointer; border-radius: 4px; transition: background 0.15s; }
.tree-node:hover { background: #f1f5f9; }
.tree-node.active { background: #eff6ff; color: #2563eb; }
.tree-node .toggle-icon { margin-right: 4px; transition: transform 0.2s; }
.tree-node.expanded > .toggle-icon { transform: rotate(90deg); }
.tree-node .type-icon { margin-right: 6px; font-size: 16px; }
.tree-node .type-icon.org { color: #3b82f6; }
.tree-node .type-icon.standard { color: #8b5cf6; }
.tree-node .type-icon.phase { color: #f59e0b; }
.tree-node .type-icon.rule { color: #94a3b8; }
.tree-node .type-icon.rule.configured { color: #10b981; }
.tree-node .tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.level-1 { padding-left: 20px; }
.level-2 { padding-left: 40px; }
.level-3 { padding-left: 60px; }
.rule-code-text { font-family: 'JetBrains Mono', monospace; background: #f1f5f9; padding: 2px 6px; border-radius: 2px; color: #64748b; }
</style>
