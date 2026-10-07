/**
 * EnterpriseFillParamsLogic — 企业全局参数定义（专家端 · ★ 左树右表）
 *
 * 菜单 `MENU_AUD_10`｜路由 `/enterprise-fill-params`
 * 后端：`FillParamValueController`（企业树 / 合并清单 / 保存）
 *
 * ★ 这是一页**简单填写页**（2026-10-07 用户裁决）：
 *   后台「企业资料参数」按标准定义要填哪些字段 → 这里把它们列出来 → 企业逐项填 → 保存。
 *   ⛔ **不接 AI**：原「生成提示词」按钮、AI 提示词弹窗、后端 `ai-prompt` 端点已全部删除。
 *
 * ★ 页面形态（26 号 §3.2）：
 *   左树 = **企业 → 标准（两级）**（数据源 = `POST /enterprise-tree`）
 *   右区 = 完成度条 + 分组卡片
 *   ⛔ **没有阶段层** —— 后台定义的 `StageCode` 恒空串（不分阶段），
 *   挂一层阶段只会让同一份清单重复出现 N 次。
 *
 * ⛔ 本页**没有**「填充预览」（2026-10-06 用户裁决）：
 *   预览 UI 归企业资料规范化册承载；后端 `DocumentFillController` 端点与引擎保留，本页不接。
 *
 * ★ 左树选中层级决定作用域（两种点击都有明确语义，不存在「点了没反应」的死节点）：
 *   - 点**企业**节点 → 标准留空 → 只看「通用参数」
 *   - 点**标准**节点 → 「通用 + 该标准专属」
 *
 * ★ 清单不是空白表单：`merge-list` 已按「后台定义 × 企业已有值」合并好，
 *   每项带 `ValueSource`（manual / default / empty）与 `Editable`，
 *   前端只负责按 `ValueType` 渲染控件 + 记录改动。
 *   （只读项分支 `Editable=false` →「去企业管理修改」保留：`auto`/`both` 维护方式
 *   一旦重新启用即可生效，当前迁移后恒为 `manual` 故不会出现。）
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { TreeNode } from '@yzh-core'
import router from '@/router'
import {
  getEnterpriseTree,
  getMergeList,
  saveValues,
} from '@share/api/cert/fill-param-value'
import type {
  EnterpriseTreeNode,
  FillProgress,
  MergedParamItem,
  MergedGroup,
} from '@share/api/cert/fill-param-value'

/** 节点类型 → 图标（Element Plus 图标已在宿主 main.ts 全局注册） */
const NODE_ICON: Record<string, string> = {
  enterprise: 'OfficeBuilding',
  standard: 'Document',
}

/**
 * 业务节点（后端 `EnterpriseTreeNode`）→ 内核 `TreeNode`。
 *
 * <p>后端只出业务字段（`EnterpriseCode` / `StandardCode`），
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
        Subtitle: n.Subtitle,
      },
      Children: children,
    }
  })
}

/** 深度优先找第一个标准节点（用于「打开页面即可看到清单」的自动定位） */
function firstStandard(nodes: EnterpriseTreeNode[]): EnterpriseTreeNode | null {
  for (const n of nodes) {
    if (n.NodeType === 'standard') return n
    const hit = firstStandard(n.Children ?? [])
    if (hit) return hit
  }
  return null
}

/** 值来源 → 界面标签 */
export const VALUE_SOURCE_LABEL: Record<string, string> = {
  auto: '自动带出',
  manual: '企业填写',
  default: '默认值',
  empty: '待完善',
}

/** 值来源 → 标签色 */
export const VALUE_SOURCE_TAG: Record<string, 'success' | 'primary' | 'warning' | 'info'> = {
  auto: 'success',
  manual: 'primary',
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

  // ──── 当前作用域（由左树选中驱动，⛔ 不用顶部下拉）────
  readonly enterpriseCode = ref('')
  readonly standardCode = ref('')
  /** 当前选中节点的显示名（顶栏展示） */
  readonly scopeLabel = ref('')
  /** 当前选中节点的类型：enterprise | standard（⛔ 没有 stage） */
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
    await this.loadTree()
  }

  /**
   * ★ 加载企业树（企业 → 标准），并自动定位到第一个标准节点。
   *
   * <p>不自动定位的话，页面打开时右区是空的（未选节点 = 无作用域），
   * 用户得自己点开才知道哪里能看清单 —— 空白右区容易被误读成「功能没做完」。</p>
   */
  async loadTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const res = await getEnterpriseTree()
      this.treeHint.value = res.Hint ?? ''
      this.treeNodes.value = toTreeNodes(res.Nodes)

      // 优先定位标准；若该企业还没关联标准，退化为「该企业的通用参数」
      const target = firstStandard(res.Nodes) ?? res.Nodes[0] ?? null
      if (target) {
        await this.applyScope(
          target.EnterpriseCode, target.StandardCode,
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
      node.Name,
      node.NodeType ?? '',
    )
  }

  /**
   * 应用作用域。
   *
   * <p>⚠️ 两编码留空的语义与后端 `merge-list` 的查询条件一一对应：
   * `StandardCode` 空 ⇒ 只匹配 `StandardCode=''` 的通用定义（不是「全部标准」）。
   * 所以「点企业节点」= 只看通用参数，这是设计不是缺陷。</p>
   *
   * <p>★ **没有 `StageCode`**（2026-10-07 用户裁决）：后台定义的 `StageCode` 恒空串，
   * 本页树也只到标准一级 —— 请求不带阶段维度，后端默认按空串匹配（= 全部）。</p>
   */
  private async applyScope(
    enterpriseCode: string,
    standardCode: string,
    label: string,
    level: string,
  ): Promise<void> {
    this.enterpriseCode.value = enterpriseCode
    this.standardCode.value = standardCode
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
      })
      this.groups.value = res.Groups
      this.progress.value = res.Progress
      // ★ 字段是 `Enterprise.Name`（PascalCase）—— 后端曾用 camelCase，
      //   前端按 `Name` 读恒得 undefined ⇒ 企业名永远空白且不报错
      this.enterpriseName.value = String(res.Enterprise?.Name ?? '')
      this.edited.value = {}
      this.loaded.value = true
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
    // 复用专家端既有的企业管理页；不在此处复制其逻辑（YZH 写法 = 路由跳转，⛔ 不用 location.href）
    void router.push({ name: 'Enterprises' })
  }

  // ════════════════════════════════════════════════════════════════
  // 保存
  // ════════════════════════════════════════════════════════════════

  async save(): Promise<void> {
    const items = Object.entries(this.edited.value).map(([ParamCode, ParamValue]) => ({
      ParamCode,
      ParamValue,
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
}

export default EnterpriseFillParamsLogic
