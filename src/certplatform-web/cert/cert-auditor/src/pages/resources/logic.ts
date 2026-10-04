/**
 * 企业资料管理 Logic（审核员端 /resources）
 *
 * 布局契约（01 分册）：
 * - 左侧：**一棵树**（企业为父节点、企业已关联的「阶段」为子节点）——
 *   专家会管多个企业，左侧只保留树本身（旧版「企业树 + 阶段单选组」两套控件互相打架）；
 *   选阶段才能上传（需求 1 硬约束：未选阶段所有上传入口不可用）
 * - 右侧顶部：阶段头（企业/阶段 + 上传入口）+ 全局汇总条（标准数 / 应上传 / 已就位 / 转换中 / 缺失）
 * - 右侧主体：按标准分 Tab，Tab 内 = 模板**文件夹树**（按 ParentCode 嵌套）+ 槽位行 + 操作列
 *
 * ★ 数据源（05 §二 目标端点）：
 *   `stage-tree`（左树）→ `stage-overview`（汇总）→ `standard-directory`（每标准主数据）
 * ★ 上传（04 §一 四段式）：`upload/plan` 预览（DispatchMatcher M0–M3）→ `upload/init` → `upload/file` → `upload/confirm`
 * ★ 就位状态以后端 `Status` 字段为唯一权威（01 §5.1），前端不自行推断组合条件
 */
import { confirmOrFalse } from '@yzh-core'
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { YzhTableColumn, YzhTableDataLoader } from '@yzh-core'
import {
  stageTree, stageOverview, standardDirectory, planDispatch, uploadFiles, activeQueue, cancelQueue,
  downloadBlob, saveBlob, previewBlob,
  replaceFile, deleteFile, restoreFile, getVersions, getHistory, triggerExtract, getExtractionResult,
  isSlotLive, isSlotRemoved, slotModifyTime
} from '@share/api'
import type {
  EnterpriseNode, StageItem, StageOverviewResult, StageStandardSummary,
  StandardDirectoryResult, FileSlot, DispatchRow, FileVersionRow, HistoryItem
} from '@share/api'

/** 右侧文件夹树节点（模板结构 + 该文件夹下的槽位行） */
export interface FolderNode {
  Code: string
  FolderName: string
  FullPath: string
  Depth: number
  Files: FileSlot[]
  Children: FolderNode[]
  Required: number
  Live: number
  Ready: number
  Converting: number
  Missing: number
}

/**
 * 左树节点（企业 → 阶段，**一棵树**）
 *
 * <para>为什么要把企业/阶段塞进同一棵树：企业节点与阶段节点原来是「树 + 单选组」两套控件，
 * 专家管理多个企业时无法一眼看清「哪个企业有哪些阶段」，且选中态分散在两处互相打架。</para>
 */
export interface ResourceTreeNode {
  /**
   * 树内唯一键。
   *
   * <para>⚠️ 阶段节点键必须**带企业 Code**（`S:{企业Code}:{阶段Code}`）：同一工作区里多个企业会关联
   * 同一个认证阶段（实测 `G4测试企业甲` 与 `测试企业b` 都关联了 `复审`），只用 `S:{阶段Code}` 会撞键 ⇒
   * `el-tree` 的 `node-key` 失去唯一性，`setCurrentKey` 会把高亮打到别的企业的节点上。</para>
   */
  Key: string
  Type: 'enterprise' | 'stage'
  /** 企业 Code 或阶段 StageCode */
  Code: string
  Name: string
  /** 仅阶段节点：所属企业 Code */
  EnterpriseCode?: string
  /** 仅企业节点：阶段数量 */
  StageCount?: number
  /** 仅阶段节点：关联标准数（0 = 未关联，置灰） */
  StandardCount?: number
  Disabled?: boolean
  Children?: ResourceTreeNode[]
}

/** 企业节点键 */
function enterpriseKey(code: string): string {
  return `E:${code}`
}

/** 阶段节点键（带企业前缀，避免跨企业同阶段撞键） */
function stageKey(enterpriseCode: string, stageCode: string): string {
  return `S:${enterpriseCode}:${stageCode}`
}

/**
 * 单个标准的「不可用」文案：`标准名（编号）：原因`
 *
 * <para>为什么不直接用后端 Message：后端那句自带处置建议（"补齐模板后会自动初始化"），
 * 整句很长，塞进逐标准的清单里会被读成四条重复的告警。这里只给**短原因**，
 * 「关联有效 / 补齐后自动生成」这类处置说明统一放在上方提示条的 hint 里，避免两处口径不一致。</para>
 */
function formatStandardIssue(std: StageStandardSummary): string {
  const no = std.StandardNo ? `（${std.StandardNo}）` : ''
  // ★ 2026-09-30 提示信息优化：原「管理端未配置该标准的目录模板」是写给管理员的诊断，
  //   专家看不懂也管不了。改为专家视角 —— 只说现状，处置建议收进提示条的一行短话。
  return `${std.StandardName}${no} 暂无资料目录`
}

/** 待上传项（plan 预览 → 四段式执行） */
export interface UploadPlanRow {
  /** 本地 File 引用（按 fileKey 建索引回来） */
  File: File
  /** ★ 计划行 ↔ 本地 File 的关联键：RelativePath || FileName（同名不同目录必须分开） */
  Key: string
  FileName: string
  StandardCode: string
  StandardName: string
  SlotCode: string
  SlotFileName: string
  FolderCode: string
  FolderPath: string
  Level: string
  NeedsConfirm: boolean
  BlockReason?: string | null
  /** 人工取消勾选后不执行 */
  Selected: boolean
}

/** 未归属文件的人工指派（后端 `upload/init` 指派模式：只传 FolderCode 不传 SlotCode） */
export interface UnmatchedAssign {
  File: File
  FileName: string
  StandardCode: string
  FolderCode: string
}

/** 未归属原因 → 中文（`ambiguous` = 有候选但分不清，提示人工指派而不是硬匹配） */
const UNMATCHED_REASON_TEXT: Record<string, string> = {
  no_standard: '该阶段无可用标准',
  no_match: '未命中任何标准槽位',
  ambiguous: '有相近槽位但分不清'
}

export class ResourcesLogic {
  // ─── 左侧：企业 / 阶段（stage-tree 一次取齐） ───
  readonly enterpriseNodes = ref<EnterpriseNode[]>([])
  readonly selectedEnterprise = ref<string>('')
  readonly selectedEnterpriseName = ref<string>('')
  readonly loadingEnterprises = ref(false)
  readonly stages = ref<StageItem[]>([])
  readonly selectedStage = ref<string>('')
  readonly selectedStageName = ref<string>('')
  /** 左树「企业 → 阶段」一棵树的数据源 */
  readonly treeNodes = ref<ResourceTreeNode[]>([])
  /** 当前高亮的树节点 Key（`E:{企业Code}` / `S:{企业Code}:{阶段Code}`） */
  readonly selectedKey = ref<string>('')

  // ─── 右侧：阶段汇总 + 各标准主数据 ───
  readonly overview = ref<StageOverviewResult | null>(null)
  readonly directories = ref<Record<string, StandardDirectoryResult>>({})
  readonly activeTab = ref<string>('')
  readonly loading = ref(false)
  /** 不可配置的标准（机构未配模板）沿用后端给的说明文案 */
  readonly loadingMessage = ref<string>('')

  // ─── 轮询 ───
  readonly polling = ref(false)
  readonly queueBusy = ref(false)
  private pollTimer: ReturnType<typeof setInterval> | null = null

  // ─── 进度（四段式上传中） ───
  readonly uploading = ref(false)
  readonly uploadProgress = ref('')

  // ─── 单标准上传（plan 预览 → 确认） ───
  readonly singleUploadVisible = ref(false)
  readonly currentUploadStandardCode = ref<string>('')
  readonly singleUploadFolderCode = ref<string>('')
  readonly singleUploadFiles = ref<File[]>([])
  readonly singlePlanLoading = ref(false)
  readonly singlePlanRows = ref<UploadPlanRow[]>([])
  readonly singlePlanUnmatched = ref<string[]>([])
  readonly singlePlanBlocked = ref<UploadPlanRow[]>([])
  /** 「未命中槽位的文件直接放入所选文件夹」（D8 模板外文件的人工落地出口，默认关） */
  readonly singleAssignUnmatched = ref(false)

  // ─── 多标准上传（顶部入口，需求 3 核心） ───
  readonly batchUploadVisible = ref(false)
  readonly batchFiles = ref<File[]>([])
  readonly batchPlanLoading = ref(false)
  readonly batchPlanRows = ref<UploadPlanRow[]>([])
  readonly batchPlanUnmatched = ref<Array<{ Key: string; FileName: string; RelativePath: string; Reason: string }>>([])
  readonly batchPlanConflicts = ref<Array<{ StandardName: string; SlotFileName: string; FileNames: string[] }>>([])
  readonly batchPlanCross = ref<Array<{ FileName: string; Standards: string[] }>>([])
  readonly batchPlanBlocked = ref<UploadPlanRow[]>([])
  readonly batchPlanMessage = ref('')
  /** 未归属 → 人工指派：key = fileKey，值 = 目标标准 + 目标文件夹（D4 的另一半） */
  readonly unmatchedAssign = ref<Record<string, { StandardCode: string; FolderCode: string }>>({})

  // ─── 替换 / 移除 ───
  readonly replaceDialogVisible = ref(false)
  readonly replacingFile = ref<FileSlot | null>(null)
  readonly replaceNewFile = ref<File | null>(null)
  readonly replaceReason = ref<string>('')

  readonly deleteDialogVisible = ref(false)
  readonly deletingFile = ref<FileSlot | null>(null)
  readonly deleteReason = ref<string>('')

  // ─── 版本面板 ───
  readonly versionsVisible = ref(false)
  readonly versionsFile = ref<FileSlot | null>(null)
  readonly versionRows = ref<FileVersionRow[]>([])
  readonly historyRows = ref<HistoryItem[]>([])
  readonly restoringVersion = ref(0)
  readonly versionsRev = ref(0)

  /** 预览抽屉（PDF Blob → ObjectURL） */
  readonly previewVisible = ref(false)
  readonly previewUrl = ref('')
  readonly previewName = ref('')

  /** 提取结果抽屉 */
  readonly resultDrawerVisible = ref(false)
  readonly resultData = ref<any>(null)

  /** 清单重挂载计数（YzhTable 只在挂载时跑 dataLoader） */
  readonly dataRevision = ref(0)

  // ═══════════════════ YzhTable 配置（守卫 R6：页面禁内联 el-table） ═══════════════════

  readonly slotColumns: YzhTableColumn[] = [
    { prop: 'FileName', label: '标准文件槽位', minWidth: 300, slot: true },
    { prop: 'Status', label: '就位状态', width: 110, slot: true },
    { prop: 'ExtractState', label: '提取状态', width: 90, slot: true },
    { prop: 'VersionNumber', label: '版本', width: 70, slot: true },
    { prop: 'Actions', label: '操作', width: 320, fixed: 'right', slot: true }
  ]

  readonly versionColumns: YzhTableColumn[] = [
    { prop: 'VersionNumber', label: '版本', width: 70, slot: true },
    { prop: 'FileName', label: '文件名', minWidth: 170, showOverflowTooltip: true },
    { prop: 'FileSize', label: '大小', width: 90, slot: true },
    { prop: 'Reason', label: '归档原因', minWidth: 150, showOverflowTooltip: true },
    { prop: 'Restore', label: '操作', width: 100, fixed: 'right', slot: true }
  ]

  /** 不可配置标准的原因清单（标准名（编号）：原因） */
  readonly unconfiguredStandards = ref<string[]>([])

  folderRowsLoader(folder: FolderNode): YzhTableDataLoader<FileSlot> {
    return () => Promise.resolve({ rows: folder.Files, total: folder.Files.length })
  }

  versionRowsLoader(): YzhTableDataLoader<FileVersionRow> {
    return () => Promise.resolve({ rows: this.versionRows.value, total: this.versionRows.value.length })
  }

  /** YzhTable 内层表格 height:100%，嵌在折叠面板里会塌成 0 ⇒ 必须给显式高度 */
  panelHeight(count: number): number {
    return Math.min(44 * Math.max(count, 1) + 56, 420)
  }

  // ═══════════════════ 数据加载 ═══════════════════

  /** 左树：本工作区企业 → 已关联阶段（后端 stage-tree，一次取齐名称与标准数） */
  async loadEnterprises() {
    this.loadingEnterprises.value = true
    try {
      const data = await stageTree()
      this.enterpriseNodes.value = data?.Enterprises ?? []
      // 企业 → 阶段 拍平成一棵树（企业为父、阶段为子）
      this.treeNodes.value = this.enterpriseNodes.value.map(e => ({
        Key: enterpriseKey(e.Code),
        Type: 'enterprise' as const,
        Code: e.Code,
        Name: e.Name,
        StageCount: e.Stages?.length ?? 0,
        Children: (e.Stages ?? []).map(s => ({
          Key: stageKey(e.Code, s.StageCode),
          Type: 'stage' as const,
          Code: s.StageCode,
          Name: s.StageName,
          EnterpriseCode: e.Code,
          StandardCount: s.StandardCount,
          Disabled: s.StandardCount === 0
        }))
      }))
      if (!data?.Configured && data?.Message) ElMessage.warning(data.Message)
      // 选中项若已不在树上（切工作区/被删）→ 清空右侧
      if (this.selectedEnterprise.value
          && !this.enterpriseNodes.value.some(e => e.Code === this.selectedEnterprise.value)) {
        this.resetRightPane()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '企业加载失败')
      this.enterpriseNodes.value = []
      this.treeNodes.value = []
    } finally {
      this.loadingEnterprises.value = false
    }
  }

  private resetRightPane() {
    this.stopPolling()
    this.selectedEnterprise.value = ''
    this.selectedEnterpriseName.value = ''
    this.selectedStage.value = ''
    this.selectedStageName.value = ''
    this.selectedKey.value = ''
    this.stages.value = []
    this.overview.value = null
    this.directories.value = {}
    this.activeTab.value = ''
    // 旧阶段的「不可配置」说明文案不能带到新企业/阶段，否则空态提示是别人的错
    this.loadingMessage.value = ''
    this.unconfiguredStandards.value = []
  }

  /**
   * 左树节点点击的唯一入口（企业节点 → 重置右侧；阶段节点 → 直接进入该阶段）。
   * ★ 未关联标准的阶段不可进入（需求 1：未选阶段不能上传），但仍给用户明确出口提示。
   */
  async selectTreeNode(node: ResourceTreeNode) {
    if (node.Type === 'enterprise') {
      await this.selectEnterpriseByCode(node.Code)
      return
    }
    if (node.Disabled) {
      ElMessage.warning('该阶段尚未关联任何标准，请先在「阶段标准关联」页配置')
      return
    }
    // 跨企业点阶段：先切企业（保持不自动进阶段，由下面显式进入）
    if (this.selectedEnterprise.value !== node.EnterpriseCode) {
      await this.selectEnterpriseByCode(node.EnterpriseCode ?? '', true)
    }
    const stage = this.stages.value.find(s => s.StageCode === node.Code)
    if (stage) await this.selectStage(stage)
  }

  private async selectEnterpriseByCode(code: string, keepStageSelection = false) {
    const node = this.enterpriseNodes.value.find(e => e.Code === code)
    if (!node) return
    this.selectedKey.value = enterpriseKey(code)
    if (this.selectedEnterprise.value === code) return
    this.stopPolling()
    this.selectedEnterprise.value = node.Code
    this.selectedEnterpriseName.value = node.Name
    this.selectedStage.value = ''
    this.selectedStageName.value = ''
    this.overview.value = null
    this.directories.value = {}
    this.activeTab.value = ''
    this.loadingMessage.value = ''
    this.unconfiguredStandards.value = []
    this.stages.value = node.Stages ?? []

    // 只有一个阶段时直接进入（少一次点击；多阶段必须用户显式选，需求 1）
    if (!keepStageSelection && this.stages.value.length === 1) {
      const only = this.stages.value[0]
      if (only.StandardCount > 0) await this.selectStage(only)
    }
  }

  async selectStage(stage: StageItem) {
    if (stage.StandardCount === 0) {
      ElMessage.warning('该阶段尚未关联任何标准，请先在「阶段标准关联」页配置')
      return
    }
    this.selectedStage.value = stage.StageCode
    this.selectedStageName.value = stage.StageName
    this.selectedKey.value = stageKey(this.selectedEnterprise.value, stage.StageCode)
    this.activeTab.value = ''
    this.overview.value = null
    this.directories.value = {}
    await this.loadOverview()
  }

  /** 阶段汇总 + 各标准主数据（= 页面「刷新」/「文件检查」的统一入口） */
  async loadOverview() {
    if (!this.selectedEnterprise.value || !this.selectedStage.value) return
    this.loading.value = true
    this.loadingMessage.value = ''
    try {
      const ov = await stageOverview(this.selectedEnterprise.value, this.selectedStage.value)
      this.overview.value = ov
      if (!ov?.Configured) {
        this.loadingMessage.value = ov?.Message ?? '该阶段暂无可用标准'
        return
      }
      await this.loadDirectories(ov.Standards)
      if (!this.activeTab.value && this.tabStandards.length > 0) {
        this.activeTab.value = this.tabStandards[0].StandardCode
      }
      this.syncPolling()
    } catch (e: any) {
      ElMessage.error(e?.message ?? '加载失败')
    } finally {
      this.loading.value = false
    }
  }

  private async loadDirectories(standards: StageStandardSummary[]) {
    const next: Record<string, StandardDirectoryResult> = {}
    const unconfigured: string[] = []
    for (const s of standards) {
      if (!s.Configured) {
        unconfigured.push(formatStandardIssue(s))
        continue
      }
      try {
        next[s.StandardCode] = await standardDirectory(
          this.selectedEnterprise.value, this.selectedStage.value, s.StandardCode)
      } catch (e: any) {
        // 请求真失败（网络/服务端异常）时不能用「未配置模板」搪塞 —— 原样带出真实原因
        unconfigured.push(`${s.StandardName}：加载失败（${e?.message ?? '未知错误'}）`)
      }
    }
    this.directories.value = next
    this.dataRevision.value++
    /** 不可用标准清单（右侧常驻提示条与 Tab 过滤共用同一份，避免两处口径不一致） */
    this.unconfiguredStandards.value = unconfigured
    // ⛔ 不再弹 Toast：常驻提示条已经说明原因与处置（补模板后自动生成、无需重建关联），
    //   而 loadDirectories 会在每次切阶段与 5s 轮询时重复跑 ⇒ 飘字提示等于反复骚扰。
  }

  /** 只刷新一个标准（上传/替换/恢复后局部刷新，避免整页闪烁） */
  async reloadStandard(standardCode: string) {
    if (!standardCode) return
    try {
      this.directories.value = {
        ...this.directories.value,
        [standardCode]: await standardDirectory(
          this.selectedEnterprise.value, this.selectedStage.value, standardCode)
      }
      this.dataRevision.value++
      await this.refreshOverviewCounts()
    } catch (e: any) {
      ElMessage.error(e?.message ?? '刷新失败')
    }
  }

  private async refreshOverviewCounts() {
    try {
      this.overview.value = await stageOverview(this.selectedEnterprise.value, this.selectedStage.value)
    } catch { /* 汇总失败不覆盖已有清单 */ }
  }

  // ═══════════════════ 派生数据 ═══════════════════

  get tabStandards(): StageStandardSummary[] {
    return (this.overview.value?.Standards ?? []).filter(s => s.Configured)
  }

  get overviewSummary() {
    const s = this.overview.value?.Summary
    if (!s) return null
    return {
      standardCount: s.StandardCount ?? 0,
      totalRequired: s.TotalRequired ?? 0,
      totalLive: s.TotalLive ?? 0,
      totalConverting: s.TotalConverting ?? 0,
      totalMissing: s.TotalMissing ?? 0
    }
  }

  get emptyHint(): string {
    if (this.loadingMessage.value) return this.loadingMessage.value
    if (!this.selectedEnterprise.value) return '请先在左侧选择企业'
    if (!this.selectedStage.value) return '请先在左侧选择认证阶段（未选阶段不能上传资料）'
    return '该企业在此阶段暂无可管理标准'
  }

  directoryOf(standardCode: string): StandardDirectoryResult | null {
    return this.directories.value[standardCode] ?? null
  }

  standardOf(standardCode: string): StageStandardSummary | null {
    return this.tabStandards.find(s => s.StandardCode === standardCode) ?? null
  }

  /** 模板文件夹树（按 ParentCode 嵌套）+ 每级计数（01 §3.2 卡体） */
  folderTreeOf(standardCode: string): FolderNode[] {
    const dir = this.directoryOf(standardCode)
    if (!dir) return []

    const byCode = new Map<string, FolderNode>()
    for (const f of dir.Folders) {
      byCode.set(f.Code, {
        Code: f.Code, FolderName: f.FolderName, FullPath: f.FullPath,
        Depth: f.Depth, Files: [], Children: [], Required: 0, Live: 0, Ready: 0, Converting: 0, Missing: 0
      })
    }
    for (const file of dir.Files) {
      const node = byCode.get(file.FolderCode)
      // 根文件夹行必然存在；缺失（历史脏数据）时挂到第一个根节点，避免文件凭空消失
      const host = node ?? byCode.get(dir.Folders[0]?.Code ?? '')
      if (!host) continue
      host.Files.push(file)
    }

    const roots: FolderNode[] = []
    for (const f of dir.Folders) {
      const node = byCode.get(f.Code)!
      const parent = f.ParentCode ? byCode.get(f.ParentCode) : undefined
      if (parent) parent.Children.push(node)
      else roots.push(node)
    }

    // 计数按**子树全量**汇总（父目录的「已就位/缺失」= 自身 + 全部后代）。
    // ⛔ 只取一层子节点会漏掉三级文件夹里的文件 —— 根目录会显示 49/167 这种明显不对的数。
    const collect = (n: FolderNode): FileSlot[] => {
      const all = [...n.Files]
      for (const c of n.Children) all.push(...collect(c))
      return all
    }
    const roll = (n: FolderNode): void => {
      for (const c of n.Children) roll(c)
      const required = collect(n).filter(f => f.IsRequired)
      n.Required = required.length
      n.Live = required.filter(isSlotLive).length
      n.Ready = required.filter(f => f.Status === 'ready').length
      n.Converting = required.filter(f => f.Status === 'converting').length
      n.Missing = required.filter(f => f.Status === 'missing' || f.Status === 'removed').length
    }
    roots.forEach(roll)
    return roots
  }

  /** 目标文件夹下拉（含层级缩进；⛔ 不再硬编码 1质量手册 等 4 项） */
  folderOptionsOf(standardCode: string): Array<{ FolderCode: string; Label: string; FullPath: string }> {
    const dir = this.directoryOf(standardCode)
    if (!dir) return []
    return [...dir.Folders]
      .sort((a, b) => a.Depth - b.Depth || a.SortOrder - b.SortOrder)
      .map(f => ({
        FolderCode: f.Code,
        FullPath: f.FullPath,
        Label: `${'　'.repeat(Math.max(0, f.Depth - 1))}${f.FolderName}${f.FullPath ? '' : '（根）'}`
      }))
  }

  folderFullPathOf(standardCode: string, folderCode: string): string {
    return this.directoryOf(standardCode)?.Folders.find(f => f.Code === folderCode)?.FullPath ?? ''
  }

  // ═══════════════════ 轮询（转换/提取自动流转可见） ═══════════════════

  private hasInFlight(): boolean {
    return Object.values(this.directories.value).some(dir =>
      dir.Files.some(f => f.Status === 'converting' || f.Status === 'uploading'))
  }

  private syncPolling() {
    if (this.hasInFlight()) this.startPolling()
    else this.stopPolling()
  }

  private startPolling() {
    if (this.pollTimer) return
    this.polling.value = true
    this.pollTimer = setInterval(async () => {
      if (!this.selectedEnterprise.value || !this.selectedStage.value) { this.stopPolling(); return }
      try {
        const stds = this.tabStandards
        const refreshed: Record<string, StandardDirectoryResult> = { ...this.directories.value }
        for (const s of stds) {
          refreshed[s.StandardCode] = await standardDirectory(
            this.selectedEnterprise.value, this.selectedStage.value, s.StandardCode)
        }
        this.directories.value = refreshed
        this.dataRevision.value++
        await this.refreshOverviewCounts()

        // 队列忙闲（取消入口 + 写入类操作禁用依据）
        const firstCfg = refreshed[stds[0]?.StandardCode ?? '']?.EnterpriseConfigCode
        if (firstCfg) {
          const q = await activeQueue(firstCfg, this.selectedEnterprise.value)
          this.queueBusy.value = !!q?.IsBusy
        }
        if (!this.hasInFlight() && !this.queueBusy.value) this.stopPolling()
      } catch {
        this.stopPolling()
      }
    }, 5000)
  }

  stopPolling() {
    if (this.pollTimer) { clearInterval(this.pollTimer); this.pollTimer = null }
    this.polling.value = false
    this.queueBusy.value = false
  }

  /** 顶部「取消队列」：取消该标准目录的运行中队列 */
  async cancelRunningQueue(standardCode: string) {
    const cfg = this.directoryOf(standardCode)?.EnterpriseConfigCode
    if (!cfg) return
    try {
      const q = await activeQueue(cfg, this.selectedEnterprise.value)
      if (!q?.QueueCode) { ElMessage.info('当前没有运行中的转换队列'); return }
      const ok = await confirmOrFalse(`取消转换队列 ${q.QueueCode}？未完成的文件会标记为转换失败。`, '取消队列', { type: 'warning' })
      if (!ok) return
      await cancelQueue(q.QueueCode, this.selectedEnterprise.value)
      ElMessage.success('队列已取消')
      await this.reloadStandard(standardCode)
    } catch (e: any) {
      if (e !== 'cancel') ElMessage.error(e?.message ?? '取消失败')
    }
  }

  // ═══════════════════ 单标准上传（入口 A/B） ═══════════════════

  openSingleUpload(standardCode: string, folderCode?: string) {
    this.currentUploadStandardCode.value = standardCode
    const opts = this.folderOptionsOf(standardCode)
    this.singleUploadFolderCode.value = folderCode ?? opts[0]?.FolderCode ?? ''
    this.singleUploadFiles.value = []
    this.resetSinglePlan()
    this.singleUploadVisible.value = true
  }

  closeSingleUploadDialog() {
    this.singleUploadVisible.value = false
    this.singleUploadFiles.value = []
    this.resetSinglePlan()
  }

  private resetSinglePlan() {
    this.singlePlanRows.value = []
    this.singlePlanUnmatched.value = []
    this.singlePlanBlocked.value = []
  }

  setSingleUploadFiles(files: File[]) {
    const added = this.appendFiles(this.singleUploadFiles.value, files)
    if (added < files.length) ElMessage.info(`已忽略 ${files.length - added} 个重复文件`)
    this.resetSinglePlan()
  }

  clearSingleUploadFiles() {
    this.singleUploadFiles.value = []
    this.resetSinglePlan()
  }

  clearBatchFiles() {
    this.batchFiles.value = []
    this.resetBatchPlan()
  }

  /** 上传文件夹（webkitdirectory）：保留相对路径，才能让 M0 文件夹路径强匹配生效 */
  setSingleUploadFolderFiles(files: File[]) {
    this.singleUploadFiles.value = files
    this.resetSinglePlan()
  }

  removeSingleUploadFile(index: number) {
    this.singleUploadFiles.value.splice(index, 1)
    this.resetSinglePlan()
  }

  // ═══════════════════ 分发预览：本地文件 ↔ 计划行的关联键 ═══════════════════

  /**
   * 文件的关联键 = `webkitRelativePath || name`（单文件上传时补目标文件夹前缀，与请求体同口径）。
   *
   * <para>★ 为什么不能用 `File.name`：一个文件夹里可以有多份同名文件
   * （`1质量手册/x.doc` 与 `4记录文件/x.doc`），而分发计划对它们各产出一行。
   * 用文件名做索引会把多行塌缩到<b>同一个 File 对象</b> ⇒ 同一份字节被写进多个槽位
   * （这正是需求 3「一个文件夹里可能有 3 个标准的文件」的主场景）。</para>
   */
  private fileKey(file: File, folderPath = ''): string {
    const rel = (file as any).webkitRelativePath as string | undefined
    if (rel) return rel
    return folderPath ? `${folderPath}/${file.name}` : file.name
  }

  /** 未归属原因 → 中文 */
  unmatchedReasonText(reason: string): string {
    return UNMATCHED_REASON_TEXT[reason] ?? reason
  }

  /** 干跑预览：走后端 DispatchMatcher（M0–M3，与批量入口同一套匹配） */
  async previewSingleUpload() {
    if (this.singleUploadFiles.value.length === 0) { ElMessage.warning('请先选择文件'); return }
    this.singlePlanLoading.value = true
    this.resetSinglePlan()
    try {
      const folderPath = this.folderFullPathOf(this.currentUploadStandardCode.value, this.singleUploadFolderCode.value)
      const plan = await planDispatch(
        this.selectedEnterprise.value, this.selectedStage.value,
        this.singleUploadFiles.value.map(f => ({
          FileName: f.name,
          RelativePath: this.fileKey(f, folderPath),
          FileSize: f.size
        }))
      )
      const mine = plan.Standards.find(s => s.StandardCode === this.currentUploadStandardCode.value)
      const byKey = new Map(this.singleUploadFiles.value.map(f => [this.fileKey(f, folderPath), f]))
      const rows = (mine?.Rows ?? [])
      this.singlePlanRows.value = rows
        .filter(r => byKey.has(this.fileKeyOf(r)))
        .map(r => this.toPlanRow(r, byKey.get(this.fileKeyOf(r))!, r.StandardCode, r.StandardName))
      this.singlePlanBlocked.value = this.singlePlanRows.value.filter(r => r.BlockReason).map(r => ({ ...r, Selected: false }))
      // 未命中名单 = 后端报的 Unmatched + 本地未被任何计划行认领的文件（去重）
      const hitKeys = new Set(rows.map(r => this.fileKeyOf(r)))
      const backendUnmatched = new Set(plan.Unmatched.map(u => this.fileKeyOf({ RelativePath: u.RelativePath, FileName: u.FileName } as DispatchRow)))
      const names: string[] = []
      for (const u of plan.Unmatched) if (!names.includes(u.FileName)) names.push(u.FileName)
      for (const f of this.singleUploadFiles.value) {
        if (hitKeys.has(this.fileKey(f, folderPath)) || backendUnmatched.has(this.fileKey(f, folderPath))) continue
        if (!names.includes(f.name)) names.push(f.name)
      }
      this.singlePlanUnmatched.value = names
      if (this.singlePlanRows.value.length === 0 && !this.singleAssignUnmatched.value) {
        ElMessage.warning('所选文件未命中该标准的任何槽位（不会上传）；可勾选「放入所选文件夹」直接落地')
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '匹配预览失败')
    } finally {
      this.singlePlanLoading.value = false
    }
  }

  /** 计划行的关联键（后端回显的 RelativePath，单文件上传时等于 FileName） */
  private fileKeyOf(r: DispatchRow): string {
    return r.RelativePath || r.FileName
  }

  private toPlanRow(
    r: DispatchRow, file: File, standardCode: string, standardName: string
  ): UploadPlanRow {
    return {
      File: file,
      Key: this.fileKeyOf(r),
      FileName: r.FileName,
      StandardCode: standardCode,
      StandardName: standardName,
      SlotCode: r.SlotCode,
      SlotFileName: r.SlotFileName,
      FolderCode: r.FolderCode,
      FolderPath: r.FolderPath,
      Level: r.Level,
      NeedsConfirm: r.NeedsConfirm,
      BlockReason: r.BlockReason,
      // ★ M3（NeedsConfirm）默认**不勾选**：后端已不再瞎猜，前端也不该替用户确认
      Selected: !r.BlockReason && !r.NeedsConfirm
    }
  }

  /** 确认上传：四段式（init → file... → confirm），未选/被拦的行不执行 */
  async confirmSingleUpload() {
    const rows = this.singlePlanRows.value.filter(r => r.Selected && !r.BlockReason)
    // 勾了「放入所选文件夹」时，未命中槽位的文件走指派模式落进所选文件夹（D8）
    const loose = this.singleAssignUnmatched.value ? this.looseSingleFiles(rows) : []
    if (rows.length === 0 && loose.length === 0) { ElMessage.warning('没有可执行的上传行'); return }
    if (rows.length > 0) await this.runUpload(rows, this.currentUploadStandardCode.value)
    if (loose.length > 0) {
      ElMessage.success(`另有 ${loose.length} 个文件将直接放入「${
        this.folderFullPathOf(this.currentUploadStandardCode.value, this.singleUploadFolderCode.value) || '根目录'
      }」`)
      await this.runUpload(
        loose.map(f => ({ ...({} as UploadPlanRow), File: f, Selected: true })),
        this.currentUploadStandardCode.value,
        this.singleUploadFolderCode.value
      )
    }
    this.closeSingleUploadDialog()
  }

  /** 单标准入口里「未命中槽位、且未被计划行认领」的文件（按关联键去重） */
  private looseSingleFiles(matched: UploadPlanRow[]): File[] {
    const folderPath = this.folderFullPathOf(this.currentUploadStandardCode.value, this.singleUploadFolderCode.value)
    const taken = new Set(matched.map(r => r.Key))
    const out: File[] = []
    const seen = new Set<string>()
    for (const f of this.singleUploadFiles.value) {
      const k = this.fileKey(f, folderPath)
      if (taken.has(k) || seen.has(k)) continue
      seen.add(k)
      out.push(f)
    }
    return out
  }

  // ═══════════════════ 多标准上传（入口 C，需求 3 核心） ═══════════════════

  openBatchUpload() {
    this.batchFiles.value = []
    this.resetBatchPlan()
    this.batchUploadVisible.value = true
  }

  closeBatchUpload() {
    this.batchUploadVisible.value = false
    this.batchFiles.value = []
    this.resetBatchPlan()
  }

  private resetBatchPlan() {
    this.batchPlanRows.value = []
    this.batchPlanUnmatched.value = []
    this.batchPlanConflicts.value = []
    this.batchPlanCross.value = []
    this.batchPlanBlocked.value = []
    this.batchPlanMessage.value = ''
    this.unmatchedAssign.value = {}
  }

  /**
   * 追加文件（按关联键去重）。
   *
   * <para>★ 为什么必须去重：同一批里出现两份同一文件时，后端会为它产出<b>两行同槽位</b>的计划，
   * 于是「冲突」与「跨标准」告警全是假的（实测出现过 <c>9001标准 + 9001标准</c> 自我跨标准），
   * 用户反而看不懂真正的问题。</para>
   */
  private appendFiles(target: File[], files: File[], folderPath = ''): number {
    const seen = new Set(target.map(f => this.fileKey(f, folderPath)))
    let added = 0
    for (const f of files) {
      const k = this.fileKey(f, folderPath)
      if (seen.has(k)) continue
      seen.add(k)
      target.push(f)
      added++
    }
    return added
  }

  addBatchFiles(files: File[]) {
    const added = this.appendFiles(this.batchFiles.value, files)
    if (added < files.length) ElMessage.info(`已忽略 ${files.length - added} 个重复文件`)
    this.resetBatchPlan()
  }

  removeBatchFile(index: number) {
    this.batchFiles.value.splice(index, 1)
    this.resetBatchPlan()
  }

  /** 选中即干跑：逐标准独立匹配（未命中不进入该标准 —— 需求 3 硬约束） */
  async previewBatchUpload() {
    if (this.batchFiles.value.length === 0) { ElMessage.warning('请先选择文件'); return }
    this.batchPlanLoading.value = true
    this.resetBatchPlan()
    try {
      const plan = await planDispatch(
        this.selectedEnterprise.value, this.selectedStage.value,
        this.batchFiles.value.map(f => ({
          FileName: f.name,
          RelativePath: this.fileKey(f),
          FileSize: f.size
        }))
      )
      this.batchPlanMessage.value = plan.Message ?? ''
      // ★ 按关联键（RelativePath || FileName）索引，不按文件名 —— 同名不同目录必须各拿各的字节
      const byKey = new Map(this.batchFiles.value.map(f => [this.fileKey(f), f]))
      const rows: UploadPlanRow[] = []
      for (const s of plan.Standards) {
        for (const r of s.Rows) {
          const file = byKey.get(this.fileKeyOf(r))
          if (!file) continue
          rows.push(this.toPlanRow(r, file, s.StandardCode, s.StandardName))
        }
      }
      this.batchPlanRows.value = rows
      this.batchPlanBlocked.value = rows.filter(r => r.BlockReason)
      this.batchPlanUnmatched.value = plan.Unmatched.map(u => ({
        Key: u.RelativePath || u.FileName,
        FileName: u.FileName,
        RelativePath: u.RelativePath,
        Reason: u.Reason
      }))
      this.batchPlanConflicts.value = plan.Conflicts.map(c => ({
        StandardName: c.StandardName, SlotFileName: c.SlotFileName, FileNames: c.FileNames
      }))
      this.batchPlanCross.value = plan.CrossStandard.map(c => ({ FileName: c.FileName, Standards: c.Standards }))
    } catch (e: any) {
      ElMessage.error(e?.message ?? '分发预览失败')
    } finally {
      this.batchPlanLoading.value = false
    }
  }

  get batchPlanRowGroups(): Array<{ StandardCode: string; StandardName: string; Rows: UploadPlanRow[] }> {
    const map = new Map<string, { StandardCode: string; StandardName: string; Rows: UploadPlanRow[] }>()
    for (const r of this.batchPlanRows.value) {
      if (!map.has(r.StandardCode)) map.set(r.StandardCode, { StandardCode: r.StandardCode, StandardName: r.StandardName, Rows: [] })
      map.get(r.StandardCode)!.Rows.push(r)
    }
    return Array.from(map.values())
  }

  get selectedBatchCount(): number {
    return this.batchPlanRows.value.filter(r => r.Selected && !r.BlockReason).length
  }

  // ═══════════════════ 未归属 → 人工指派（D4 的另一半 / H6-N2） ═══════════════════

  /**
   * 设置某个未归属文件的目标标准 / 目标文件夹（可分两步选）。
   *
   * <para>★ 半选状态必须保留：只选标准、还没选文件夹时，若把这条记录丢掉，
   * 绑定它的标准下拉会立刻回空，用户永远走不到第二步（文件夹下拉也因此没有数据源）。</para>
   */
  setUnmatchedAssign(key: string, patch: { StandardCode?: string; FolderCode?: string }) {
    const cur = this.unmatchedAssign.value[key] ?? { StandardCode: '', FolderCode: '' }
    const next = { ...cur, ...patch }
    // 换标准 ⇒ 文件夹选择失效（文件夹 Code 跨标准不通用）
    if (patch.StandardCode !== undefined && patch.StandardCode !== cur.StandardCode) next.FolderCode = ''
    this.unmatchedAssign.value = { ...this.unmatchedAssign.value, [key]: next }
  }

  unmatchedAssignOf(key: string): { StandardCode: string; FolderCode: string } {
    return this.unmatchedAssign.value[key] ?? { StandardCode: '', FolderCode: '' }
  }

  /** 已指派齐（标准 + 文件夹都选好）的未归属文件 */
  get assignedUnmatched(): UnmatchedAssign[] {
    const byKey = new Map(this.batchFiles.value.map(f => [this.fileKey(f), f]))
    const out: UnmatchedAssign[] = []
    for (const u of this.batchPlanUnmatched.value) {
      const a = this.unmatchedAssign.value[u.Key]
      const file = byKey.get(u.Key)
      if (!a?.StandardCode || !a.FolderCode || !file) continue
      out.push({ File: file, FileName: u.FileName, StandardCode: a.StandardCode, FolderCode: a.FolderCode })
    }
    return out
  }

  /** 执行人工指派：按标准分组走四段式指派模式（不传 SlotCode） */
  async confirmUnmatchedAssign(): Promise<boolean> {
    const items = this.assignedUnmatched
    if (items.length === 0) { ElMessage.warning('请先为未归属文件选择目标标准与文件夹'); return false }

    const groups = new Map<string, UnmatchedAssign[]>()
    for (const it of items) {
      if (!groups.has(it.StandardCode)) groups.set(it.StandardCode, [])
      groups.get(it.StandardCode)!.push(it)
    }

    this.uploading.value = true
    let okFiles = 0
    let failed = 0
    try {
      for (const [standardCode, list] of groups) {
        const name = this.standardOf(standardCode)?.StandardName ?? standardCode
        try {
          const res = await uploadFiles(
            this.selectedEnterprise.value, this.selectedStage.value, standardCode,
            list.map(i => ({ File: i.File, FolderCode: i.FolderCode })),
            (done, total, n) => { this.uploadProgress.value = `${name} 指派 ${done}/${total} ${n}` }
          )
          okFiles += res?.ActivatedCount ?? list.length
          if (res?.QueueError) ElMessage.warning(`${name}：${res.QueueError}`)
        } catch (e: any) {
          failed++
          ElMessage.error(`${name} 指派失败：${e?.message ?? '未知错误'}`)
        }
      }
    } finally {
      this.uploading.value = false
      this.uploadProgress.value = ''
    }

    if (okFiles > 0) ElMessage.success(`已指派上传 ${okFiles} 个文件到 ${groups.size} 个标准`)
    if (failed > 0) return false
    // 指派成功的行从未归属清单移除，只留失败项
    const assignedKeys = new Set(this.batchPlanUnmatched.value
      .filter(u => this.assignedUnmatched.some(i => this.fileKey(i.File) === u.Key))
      .map(u => u.Key))
    this.batchPlanUnmatched.value = this.batchPlanUnmatched.value.filter(u => !assignedKeys.has(u.Key))
    this.unmatchedAssign.value = {}
    await this.loadOverview()
    return true
  }

  /** 勾选/取消一行（冲突区人工取舍的落点） */
  toggleBatchRow(row: UploadPlanRow, selected: boolean) {
    row.Selected = selected
  }

  /** 确认：按标准分组逐组走四段式（4.4），未归属/被拦行不产生任何调用 */
  async confirmBatchUpload() {
    const groups = this.batchPlanRowGroups
      .map(g => ({ ...g, Rows: g.Rows.filter(r => r.Selected && !r.BlockReason) }))
      .filter(g => g.Rows.length > 0)
    if (groups.length === 0) { ElMessage.warning('没有可执行的上传行'); return }

    let okFiles = 0
    let failed = 0
    this.uploading.value = true
    try {
      for (const g of groups) {
        try {
          const res = await uploadFiles(
            this.selectedEnterprise.value, this.selectedStage.value, g.StandardCode,
            g.Rows.map(r => ({ File: r.File, SlotCode: r.SlotCode })),
            (done, total, name) => { this.uploadProgress.value = `${g.StandardName} ${done}/${total} ${name}` }
          )
          okFiles += res?.ActivatedCount ?? g.Rows.length
          if (res?.QueueError) ElMessage.warning(`${g.StandardName}：${res.QueueError}`)
        } catch (e: any) {
          failed++
          ElMessage.error(`${g.StandardName} 上传失败：${e?.message ?? '未知错误'}`)
        }
      }
    } finally {
      this.uploading.value = false
      this.uploadProgress.value = ''
    }

    if (okFiles > 0) ElMessage.success(`已上传 ${okFiles} 个文件到 ${groups.length} 个标准`)
    // 还有待指派的未归属文件时不关弹窗，否则用户的指派选择会被静默丢弃
    if (failed === 0 && this.assignedUnmatched.length === 0) this.closeBatchUpload()
    await this.loadOverview()
  }

  /** 单标准执行体：四段式 + 局部刷新。给了 `assignFolderCode` 则走指派模式（模板外文件） */
  private async runUpload(rows: UploadPlanRow[], standardCode: string, assignFolderCode?: string) {
    this.uploading.value = true
    try {
      const res = await uploadFiles(
        this.selectedEnterprise.value, this.selectedStage.value, standardCode,
        rows.map(r => assignFolderCode
          ? { File: r.File, FolderCode: assignFolderCode }
          : { File: r.File, SlotCode: r.SlotCode }),
        (done, total, name) => { this.uploadProgress.value = `${done}/${total} ${name}` }
      )
      ElMessage.success(`已上传 ${res?.ActivatedCount ?? rows.length} 个文件，转换队列已受理`)
      if (res?.QueueError) ElMessage.warning(res.QueueError)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '上传失败')
    } finally {
      this.uploading.value = false
      this.uploadProgress.value = ''
    }
    await this.reloadStandard(standardCode)
  }

  // ═══════════════════ 替换 / 移除 ═══════════════════

  openReplaceDialog(file: FileSlot) {
    this.replacingFile.value = file
    this.replaceNewFile.value = null
    this.replaceReason.value = ''
    this.replaceDialogVisible.value = true
  }

  closeReplaceDialog() {
    this.replaceDialogVisible.value = false
    this.replacingFile.value = null
    this.replaceNewFile.value = null
    this.replaceReason.value = ''
  }

  setReplaceNewFile(file: File | null) { this.replaceNewFile.value = file }

  async confirmReplace() {
    const target = this.replacingFile.value
    if (!target) return
    if (!this.replaceNewFile.value) { ElMessage.warning('请选择新文件'); return }
    if (!this.replaceReason.value.trim()) { ElMessage.warning('请填写替换原因'); return }
    try {
      const notice = await replaceFile(
        this.replaceNewFile.value, target.Code, this.selectedEnterprise.value,
        this.replaceReason.value.trim(), slotModifyTime(target)
      )
      ElMessage.success('文件已替换，旧版本已归档')
      this.closeReplaceDialog()
      await this.reloadStandard(target.StandardCode ?? this.activeTab.value)

      // ★ 2026-09-30 裁决 J4 的连带后果：按 RuleCode 归档会把人工补录值一起清掉。
      //   后端已写 change_log 留痕，这里弹窗问专家是否要重新补录（05 号 §5.3）。
      if (notice?.manualValuesArchived) {
        ElMessageBox.alert(notice.notice, '人工补录值已被覆盖', {
          type: 'warning',
          confirmButtonText: '知道了',
          dangerouslyUseHTMLString: false,
        }).catch(() => undefined)
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '替换失败')
    }
  }

  openDeleteDialog(file: FileSlot) {
    this.deletingFile.value = file
    this.deleteReason.value = ''
    this.deleteDialogVisible.value = true
  }

  closeDeleteDialog() {
    this.deleteDialogVisible.value = false
    this.deletingFile.value = null
    this.deleteReason.value = ''
  }

  async confirmDelete() {
    const target = this.deletingFile.value
    if (!target) return
    if (!this.deleteReason.value.trim()) { ElMessage.warning('请填写移除原因'); return }
    try {
      await deleteFile(
        target.Code, this.selectedEnterprise.value,
        this.deleteReason.value.trim(), slotModifyTime(target)
      )
      ElMessage.success('已移除（存储对象保留，可在版本面板恢复）')
      this.closeDeleteDialog()
      await this.reloadStandard(this.activeTab.value)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '移除失败')
    }
  }

  // ═══════════════════ 下载 / 预览 ═══════════════════

  async downloadRow(row: FileSlot) {
    if (!row.StoragePath) { ElMessage.warning('该文件尚未上传'); return }
    try {
      saveBlob(await downloadBlob(row.StoragePath), row.FileName)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '下载失败')
    }
  }

  async downloadVersion(row: FileVersionRow) {
    try {
      saveBlob(await downloadBlob(row.StoragePath), row.FileName)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '下载失败')
    }
  }

  /** 预览：一律取 Blob 再用 ObjectURL（Authorization 头无法随裸链接发送） */
  async previewRow(row: FileSlot) {
    try {
      const blob = await previewBlob(row.Code, this.selectedEnterprise.value)
      if (this.previewUrl.value) URL.revokeObjectURL(this.previewUrl.value)
      this.previewUrl.value = URL.createObjectURL(blob)
      this.previewName.value = row.FileName
      this.previewVisible.value = true
    } catch (e: any) {
      ElMessage.error(e?.message ?? '预览失败（可能尚未生成预览产物）')
    }
  }

  closePreview() {
    this.previewVisible.value = false
    if (this.previewUrl.value) { URL.revokeObjectURL(this.previewUrl.value); this.previewUrl.value = '' }
  }

  // ═══════════════════ 版本 / 时间线 / 恢复 ═══════════════════

  async openVersions(file: FileSlot) {
    this.versionsFile.value = file
    this.versionRows.value = []
    this.historyRows.value = []
    this.restoringVersion.value = 0
    this.versionsVisible.value = true
    try {
      const [versions, history] = await Promise.all([
        getVersions(file.Code, this.selectedEnterprise.value),
        getHistory(file.Code, this.selectedEnterprise.value)
      ])
      this.versionRows.value = versions ?? []
      this.historyRows.value = history?.Timeline ?? []
      this.versionsRev.value++
    } catch (e: any) {
      ElMessage.error(e?.message ?? '版本信息加载失败')
    }
  }

  closeVersions() {
    this.versionsVisible.value = false
    this.versionsFile.value = null
  }

  async handleRestore(row: FileVersionRow) {
    const file = this.versionsFile.value
    if (!file || this.restoringVersion.value > 0) return
    const ok = await confirmOrFalse(
      `用归档版本 v${row.VersionNumber}（${row.FileName}）恢复？当前文件会先归档，版本号继续递增。`,
      '恢复确认', { type: 'warning', confirmButtonText: '恢复' }
    )
    if (!ok) return

    this.restoringVersion.value = row.VersionNumber
    try {
      await restoreFile(file.Code, this.selectedEnterprise.value, row.VersionNumber, '从版本面板恢复')
      ElMessage.success(`已恢复 v${row.VersionNumber}，重新进入转换队列`)
      this.closeVersions()
      await this.reloadStandard(this.activeTab.value)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '恢复失败')
    } finally { this.restoringVersion.value = 0 }
  }

  // ═══════════════════ 提取（真链，偏差 D10） ═══════════════════

  async handleTriggerExtract(file: FileSlot) {
    try {
      const isRetry = file.ExtractState === 'failed'
      await triggerExtract(file.Code, this.selectedEnterprise.value)
      ElMessage.success(isRetry ? '已重新提交提取' : '提取已入队')
      await this.reloadStandard(this.activeTab.value)
    } catch (e: any) { ElMessage.error(e?.message ?? '触发提取失败') }
  }

  async handleViewResult(file: FileSlot) {
    this.resultDrawerVisible.value = true
    this.resultData.value = null
    try { this.resultData.value = await getExtractionResult(file.Code, this.selectedEnterprise.value) }
    catch { /* 无结果抽屉会显示空态 */ }
  }

  previewExtractedJson(json: string) {
    try { ElMessage.info(JSON.stringify(JSON.parse(json), null, 2).slice(0, 400) + '...') }
    catch { ElMessage.info(json?.slice(0, 400) + '...') }
  }

  opTypeText(item: HistoryItem): string {
    const map: Record<string, string> = {
      upload: '上传', replace: '替换', delete: '移除', restore: '恢复',
      extract_trigger: '触发提取', extract_done: '提取完成', archive: '归档'
    }
    return map[item.OpType] ?? item.OpType
  }

  /** 匹配层级 → 中文（分发预览用） */
  levelText(level: string): string {
    const map: Record<string, string> = {
      M0Path: 'M0 路径强匹配', M1Exact: 'M1 文件名精确', M2Contains: 'M2 主词包含', M3FolderExt: 'M3 分类+扩展名（需确认）'
    }
    return map[level] ?? level
  }

  /** 行是否展示「就位」相关操作 */
  isLive(row: FileSlot): boolean { return isSlotLive(row) }
  isRemoved(row: FileSlot): boolean { return isSlotRemoved(row) }
}

export default ResourcesLogic
