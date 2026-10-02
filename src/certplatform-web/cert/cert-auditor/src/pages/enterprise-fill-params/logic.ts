/**
 * EnterpriseFillParamsLogic — 企业全局参数定义（专家端 · ★ 左树右表）
 *
 * 菜单 `MENU_AUD_10`｜路由 `/enterprise-fill-params`
 * 后端：`FillParamValueController`（企业树 / 合并清单 / 保存 / AI 提示词）
 *       + `DocumentFillController`（能力清单 / 填充预览）
 *
 * ★ 页面形态（26 号 §3.2）：
 *   左树 = **企业 → 标准 → 阶段**（数据源 = `POST /enterprise-tree`）
 *   右区 = 完成度条 + 分组卡片 + 「填充预览」页签
 *
 * ★ 左树选中层级决定作用域（三种点击都有明确语义，不存在「点了没反应」的死节点）：
 *   - 点**企业**节点 → 标准 / 阶段都留空 → 只看「通用参数」
 *   - 点**标准**节点 → 阶段留空 → 「通用 + 该标准专属」
 *   - 点**阶段**节点 → 「通用 + 该标准专属 + 该阶段专属 + 该标准×该阶段专属」
 *
 * ★ 本页要讲清楚的一件事（用户 2026-10-02 的需求原话）：
 *   「**先自动将企业的基本信息和后台设置定义的参数关联形成列表显示出来，让企业完善**」
 *
 *   所以「参数完善」页不是一张空白表单，而是一张**已经替你填好一部分的清单**：
 *   - `auto` 项（企业全称 / 信用代码 / 地址…）**实时**取自「企业管理」，此处只读，
 *     旁边给「去企业管理修改」的指引 —— 改一处，所有文档同步；
 *   - `both` 项自动带出企业档案里的值作为初值，**可以改**，改了就落库、不再跟随档案；
 *   - `manual` 项（文件编号前缀 / 资质 / 荣誉…）企业自己填；
 *   - `ai` 项（质量方针 / 目标 / 企业概况）给「生成提示词」按钮 ——
 *     本期产出可投喂模型的提示词，结果回填后与人工填写的值等价（都可被覆盖、都可被文档复用）。
 *
 * ★ 「填充预览」Tab 是同一份数据的能力演示：企业 + 模板 → 成文 + 证据报告。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { TreeNode } from '@yzh-core'
import {
  getAiPrompt,
  getCapabilities,
  getEnterpriseTree,
  getMergeList,
  getPreview,
  saveValues,
} from '@share/api/cert/fill-param-value'
import type {
  CapabilitiesResult,
  EnterpriseTreeNode,
  FillCapability,
  FillProgress,
  MergedParamItem,
  MergedGroup,
  PreviewResult,
} from '@share/api/cert/fill-param-value'

/** 节点类型 → 图标（Element Plus 图标已在宿主 main.ts 全局注册） */
const NODE_ICON: Record<string, string> = {
  enterprise: 'OfficeBuilding',
  standard: 'Document',
  stage: 'Calendar',
}

/**
 * 业务节点（后端 `EnterpriseTreeNode`）→ 内核 `TreeNode`。
 *
 * <p>后端只出业务字段（`EnterpriseCode` / `StandardCode` / `StageCode`），
 * `IsLeaf` / `Extra.Icon` 等渲染信息由前端补 —— 后端不耦合前端组件契约。</p>
 */
function toTreeNodes(nodes: EnterpriseTreeNode[]): TreeNode[] {
  return (nodes || []).map((n) => {
    const children = toTreeNodes(n.Children ?? [])
    return {
      Code: n.Code,
      Name: n.Name,
      NodeType: n.NodeType,
      IsLeaf: children.length === 0,
      Extra: {
        Icon: NODE_ICON[n.NodeType] || 'Document',
        EnterpriseCode: n.EnterpriseCode,
        StandardCode: n.StandardCode,
        StageCode: n.StageCode,
        Subtitle: n.Subtitle,
        StageCount: n.StageCount,
      },
      Children: children,
    }
  })
}

/** 深度优先找第一个阶段节点（用于「打开页面即可看到清单」的自动定位） */
function firstStage(nodes: EnterpriseTreeNode[]): EnterpriseTreeNode | null {
  for (const n of nodes) {
    if (n.NodeType === 'stage') return n
    const hit = firstStage(n.Children ?? [])
    if (hit) return hit
  }
  return null
}

/** 值来源 → 界面标签 */
export const VALUE_SOURCE_LABEL: Record<string, string> = {
  auto: '自动带出',
  manual: '企业填写',
  ai: 'AI 生成',
  default: '默认值',
  empty: '待完善',
}

/** 值来源 → 标签色 */
export const VALUE_SOURCE_TAG: Record<string, 'success' | 'primary' | 'warning' | 'info'> = {
  auto: 'success',
  manual: 'primary',
  ai: 'warning',
  default: 'info',
  empty: 'info',
}

/** 枚举选项 JSON → 下拉选项（后端存的是 `[{"Value":"A","Label":"甲"}]`） */
export function parseEnumOptions(json?: string | null): { label: string; value: string }[] {
  if (!json) return []
  try {
    const arr = JSON.parse(json)
    if (!Array.isArray(arr)) return []
    return arr
      .filter((o) => o && typeof o === 'object')
      .map((o: any) => ({ label: String(o.Label ?? o.label ?? o.Value), value: String(o.Value ?? o.value) }))
  } catch {
    // 枚举 JSON 写坏时不该让整页崩掉：退化为空下拉，并在控制台留痕
    console.warn('[FillParams] EnumOptions 不是合法 JSON：', json)
    return []
  }
}

export class EnterpriseFillParamsLogic {
  // ──── ★ 左树（企业 → 标准 → 阶段）────
  readonly treeNodes = ref<TreeNode[]>([])
  readonly treeLoading = ref(false)
  /** 空树 / 无关联时的兜底提示（后端下发） */
  readonly treeHint = ref('')

  // ──── 当前作用域（由左树选中驱动，⛔ 不再用顶部三个下拉）────
  readonly enterpriseCode = ref('')
  readonly standardCode = ref('')
  readonly stageCode = ref('')
  /** 当前选中节点的显示名（顶栏展示） */
  readonly scopeLabel = ref('')
  /** 当前选中节点的类型：enterprise | standard | stage */
  readonly scopeLevel = ref('')

  // ──── 合并清单 ────
  readonly groups = ref<MergedGroup[]>([])
  /** ★ 与后端 `FillProgress` 同构（PascalCase）—— 不做一层字段映射，少一个漂移点 */
  readonly progress = ref<FillProgress>({
    Total: 0, Filled: 0, Empty: 0, Completion: 0, Required: 0, RequiredFilled: 0,
  })
  readonly enterpriseName = ref('')

  readonly loading = ref(false)
  readonly saving = ref(false)
  readonly loaded = ref(false)

  /** 本地编辑值：ParamCode → 值（只放**改过**的项，保存时只提交这些） */
  readonly edited = ref<Record<string, string>>({})

  // ──── 填充预览 ────
  readonly capabilities = ref<FillCapability[]>([])
  readonly demoTemplate = ref('')
  readonly demoHeader = ref('')
  readonly demoFooter = ref('')
  readonly preview = ref<PreviewResult | null>(null)
  readonly previewLoading = ref(false)
  readonly aiEnabled = ref(true)

  /** AI 提示词弹窗 */
  readonly aiDialogVisible = ref(false)
  readonly aiPromptText = ref('')
  readonly aiPromptTarget = ref<MergedParamItem | null>(null)

  /** 已改动的项数 */
  readonly dirtyCount = computed(() => Object.keys(this.edited.value).length)

  /** 完成度百分比（含本地未保存改动） */
  readonly liveCompletion = computed(() => {
    const total = this.progress.value.Total
    if (!total) return 0
    let filled = this.progress.value.Filled
    for (const [code, val] of Object.entries(this.edited.value)) {
      const item = this.findItem(code)
      if (!item) continue
      const wasFilled = item.IsFilled
      const nowFilled = !!val.trim()
      if (!wasFilled && nowFilled) filled++
      else if (wasFilled && !nowFilled) filled--
    }
    return Math.round((filled / total) * 1000) / 10
  })

  // ════════════════════════════════════════════════════════════════
  // 初始化
  // ════════════════════════════════════════════════════════════════

  async init(): Promise<void> {
    // 填充能力清单（预览 Tab 的说明卡）—— 与树并行拉取，失败不阻断
    const caps = await getCapabilities().catch(() => null as CapabilitiesResult | null)
    if (caps) {
      this.capabilities.value = caps.capabilities
      this.demoTemplate.value = caps.demoTemplate
      this.demoHeader.value = caps.demoHeader
      this.demoFooter.value = caps.demoFooter
    }
    await this.loadTree()
  }

  /**
   * ★ 加载企业树，并自动定位到第一个阶段。
   *
   * <p>不自动定位的话，页面打开时右区是空的（未选节点 = 无作用域），
   * 用户得自己一路点开才知道哪里能看清单 —— 空白右区容易被误读成「功能没做完」。</p>
   */
  async loadTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const res = await getEnterpriseTree()
      this.treeHint.value = res.Hint ?? ''
      this.treeNodes.value = toTreeNodes(res.Nodes)

      // 优先定位阶段；若该企业还没关联标准/阶段，退化为「该企业的通用参数」
      const target = firstStage(res.Nodes) ?? res.Nodes[0] ?? null
      if (target) {
        await this.applyScope(
          target.EnterpriseCode, target.StandardCode, target.StageCode,
          target.Name, target.NodeType,
        )
      }
    } catch (e: any) {
      ElMessage.error(e?.message || '加载企业树失败')
      this.treeNodes.value = []
    } finally {
      this.treeLoading.value = false
    }
  }

  /** 树节点点击 → 切换作用域并重载清单 */
  async onNodeClick(node: TreeNode): Promise<void> {
    const ex = (node.Extra ?? {}) as Record<string, any>
    await this.applyScope(
      String(ex.EnterpriseCode ?? ''),
      String(ex.StandardCode ?? ''),
      String(ex.StageCode ?? ''),
      node.Name,
      node.NodeType ?? '',
    )
  }

  /**
   * 应用作用域。
   *
   * <p>⚠️ 三编码留空的语义与后端 `merge-list` 的查询条件一一对应：
   * `StandardCode` 空 ⇒ 只匹配 `StandardCode=''` 的通用定义（不是「全部标准」）。
   * 所以「点企业节点」= 只看通用参数，这是设计不是缺陷。</p>
   */
  private async applyScope(
    enterpriseCode: string,
    standardCode: string,
    stageCode: string,
    label: string,
    level: string,
  ): Promise<void> {
    this.enterpriseCode.value = enterpriseCode
    this.standardCode.value = standardCode
    this.stageCode.value = stageCode
    this.scopeLabel.value = label
    this.scopeLevel.value = level
    if (enterpriseCode) await this.loadMergeList()
  }

  // ════════════════════════════════════════════════════════════════
  // 合并清单
  // ════════════════════════════════════════════════════════════════

  async loadMergeList(): Promise<void> {
    if (!this.enterpriseCode.value) {
      ElMessage.warning('请先在左侧选择企业')
      return
    }
    this.loading.value = true
    try {
      const res = await getMergeList({
        EnterpriseCode: this.enterpriseCode.value,
        StandardCode: this.standardCode.value,
        StageCode: this.stageCode.value,
      })
      this.groups.value = res.Groups
      this.progress.value = res.Progress
      // ★ 字段是 `Enterprise.Name`（PascalCase）—— 后端曾用 camelCase，
      //   前端按 `Name` 读恒得 undefined ⇒ 企业名永远空白且不报错
      this.enterpriseName.value = String(res.Enterprise?.Name ?? '')
      this.edited.value = {}
      this.loaded.value = true
      this.preview.value = null
    } catch (e: any) {
      ElMessage.error(e?.message || '加载参数清单失败')
    } finally {
      this.loading.value = false
    }
  }

  /** 某参数当前应显示的值（本地改动优先） */
  valueOf(item: MergedParamItem): string {
    return this.edited.value[item.ParamCode] ?? item.ParamValue ?? ''
  }

  /** 记录本地改动（只读项不记录） */
  onValueChange(item: MergedParamItem, val: string): void {
    if (!item.Editable) return
    if (val === (item.ParamValue ?? '')) {
      delete this.edited.value[item.ParamCode]
    } else {
      this.edited.value[item.ParamCode] = val
    }
  }

  private findItem(paramCode: string): MergedParamItem | undefined {
    for (const g of this.groups.value) {
      const hit = g.Items.find((i) => i.ParamCode === paramCode)
      if (hit) return hit
    }
    return undefined
  }

  /** 只读项（auto）的「去企业管理修改」跳转 */
  goToEnterprise(): void {
    // 复用专家端既有的企业管理页；不在此处复制其逻辑
    window.location.href = '/enterprises'
  }

  // ════════════════════════════════════════════════════════════════
  // 保存
  // ════════════════════════════════════════════════════════════════

  async save(): Promise<void> {
    const items = Object.entries(this.edited.value).map(([ParamCode, ParamValue]) => ({
      ParamCode,
      ParamValue,
      ValueSource: 'manual',
    }))
    if (items.length === 0) {
      ElMessage.info('没有需要保存的改动')
      return
    }

    this.saving.value = true
    try {
      const res = await saveValues({
        EnterpriseCode: this.enterpriseCode.value,
        StandardCode: this.standardCode.value,
        StageCode: this.stageCode.value,
        Items: items,
      })
      ElMessage.success(res.message || `已保存 ${res.savedCount} 项`)
      await this.loadMergeList()
    } catch (e: any) {
      ElMessage.error(e?.message || '保存失败')
    } finally {
      this.saving.value = false
    }
  }

  resetEdits(): void {
    this.edited.value = {}
    ElMessage.info('已放弃未保存的改动')
  }

  // ════════════════════════════════════════════════════════════════
  // AI 生成（本期产出提示词）
  // ════════════════════════════════════════════════════════════════

  async showAiPrompt(item: MergedParamItem): Promise<void> {
    try {
      const res = await getAiPrompt(this.enterpriseCode.value, item.ParamCode)
      this.aiPromptTarget.value = item
      this.aiPromptText.value = res.prompt
      this.aiDialogVisible.value = true
    } catch (e: any) {
      ElMessage.error(e?.message || '生成提示词失败')
    }
  }

  async copyAiPrompt(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.aiPromptText.value)
      ElMessage.success('提示词已复制，可粘贴到任意大模型；把结果填回该参数即可')
    } catch {
      ElMessage.warning('浏览器拒绝了剪贴板访问，请手动选中复制')
    }
  }

  // ════════════════════════════════════════════════════════════════
  // 填充预览
  // ════════════════════════════════════════════════════════════════

  async runPreview(): Promise<void> {
    if (!this.enterpriseCode.value) {
      ElMessage.warning('请先选择企业')
      return
    }
    this.previewLoading.value = true
    try {
      this.preview.value = await getPreview({
        EnterpriseCode: this.enterpriseCode.value,
        StandardCode: this.standardCode.value,
        StageCode: this.stageCode.value,
        AiEnabled: this.aiEnabled.value,
      })
    } catch (e: any) {
      ElMessage.error(e?.message || '填充预览失败')
    } finally {
      this.previewLoading.value = false
    }
  }

  /** 能力 key → 进度条颜色（让「4 项能力各命中多少」一眼可辨） */
  capabilityColor(kind: string): 'success' | 'warning' | 'primary' | 'info' {
    switch (kind) {
      case 'global': return 'primary'
      case 'replace': return 'success'
      case 'headerFooter': return 'warning'
      case 'ai': return 'info'
      default: return 'info'
    }
  }
}

export default EnterpriseFillParamsLogic
