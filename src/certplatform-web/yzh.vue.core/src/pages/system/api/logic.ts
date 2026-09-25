/**
 * ApiPageLogic - 接口管理 Logic（YzhTreeTable 分组树 + SingleTableCore）
 *
 * 架构：
 * - 列/搜索/工具栏/行按钮由 /api/System/ApiSync/config（SysApi.json）配置驱动
 * - 列表走 GET /api/ApiSync/list（全量）→ dataLoader 客户端过滤 + GroupPath 组树（无分页）
 * - 同步 / Swagger / 展开折叠 = Toolbar CustomButtons → registerHandler 拦截（不打 /action/{method}）
 *
 * 分组说明：
 * sys_api.GroupPath 形如 System/Config、Foundation/ISOClause（模块/控制器），
 * 按 “/” 拆成层级节点，接口挂在最末级分组下，避免一屏几百行平铺。
 */

import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { SingleTableCore, type Page, type PageParams, type YzhAction } from '@yzh-core'
import {
  getApiList,
  getSwaggerOperationUrl,
  syncApis,
  SWAGGER_UI_URL,
  type ApiItem,
  type SyncResult,
} from '../../../api/system/api'

/** 树形表格行（分组节点 + 接口节点） */
export interface ApiTreeRow {
  Code: string
  NodeType: 'group' | 'api'
  Name: string
  GroupPath?: string
  Method?: string
  Path?: string
  /** 有效标志（1=有效 0=无效）—— 铁律九（原 Enable 列已迁移） */
  IsValid?: number
  ApiCount?: number
  Author?: string
  CreateTime?: string
  children?: ApiTreeRow[]
}

export class ApiPageLogic extends SingleTableCore<ApiTreeRow> {
  /** 与 [Route("api/System/[controller]")] 对齐 → GET /api/System/ApiSync/config */
  controllerName = 'System/ApiSync'

  /** 同步中 */
  syncing = ref(false)

  /** 同步结果（顶部 el-alert） */
  syncResult = ref<SyncResult | null>(null)

  /** 展开/折叠状态（按钮文案） */
  expandAll = ref(true)

  /** 全量接口缓存（组树数据源） */
  apiList = ref<ApiItem[]>([])

  /** 最近一次过滤后的匹配数（底部统计） */
  filteredCount = ref(0)

  constructor() {
    super()
    this.registerHandler('custom:Sync', () => this.handleSync())
    // 同一 key 服务工具栏（无 target → 首页）与行按钮（有 row → 深链）
    this.registerHandler('custom:Swagger', (row) => this.openSwagger(row as ApiTreeRow | undefined))
    this.registerHandler('custom:Expand', () => this.toggleExpandAll())
  }

  // ========================================================
  // 配置派生（slot 覆写：Name/Method 页面渲染；IsValid 由 EnableField 自动 slot）
  // ⚠️ EntityConfig.EnableField 的「值」= "IsValid"（名字保留，值必须是 IsValid —— 铁律九）
  // ========================================================

  /** 列：Name / Method 用 #column-* 插槽（分组 vs 接口、方法标签） */
  override get columns() {
    return super.columns.map((c) =>
      c.prop === 'Name' || c.prop === 'Method' ? { ...c, slot: true as const } : c,
    )
  }

  /** 行按钮：仅接口节点显示「测试」；分组节点无操作列 */
  override get rowActions(): YzhAction[] | ((row: ApiTreeRow) => YzhAction[]) {
    return (row: ApiTreeRow) => {
      if (row?.NodeType !== 'api') return []
      const base = super.rowActions
      return typeof base === 'function' ? base(row) : base
    }
  }

  // ========================================================
  // 数据加载（组树，客户端过滤，无 /filter）
  // ========================================================

  /**
   * YzhTreeTable dataLoader：
   * - 不打 /filter；调 /list 全量 → 按 search 参数客户端过滤 → GroupPath 组树
   * - 返回 { rows: tree, total: 匹配数 }（树表无分页）
   */
  override async dataLoader(params: PageParams): Promise<Page<ApiTreeRow>> {
    const { page = 1, rows = 20, sort, order, ...searchValues } = params
    void page
    void rows
    void sort
    void order

    this.loading.value = true
    try {
      // 同步后需强刷：每次 dataLoader 重取（列表量小，可接受）
      this.apiList.value = await getApiList()

      const filtered = this.filterApis(this.apiList.value, searchValues as Record<string, any>)
      this.filteredCount.value = filtered.length
      const tree = this.buildTree(filtered)
      this.rows.value = tree
      this.pagination.total = filtered.length
      this.onDataLoaded(tree)
      return { rows: tree, total: filtered.length }
    } catch (e: any) {
      ElMessage.error(e?.message || '加载接口列表失败')
      this.rows.value = []
      this.filteredCount.value = 0
      this.pagination.total = 0
      return { rows: [], total: 0 }
    } finally {
      this.loading.value = false
    }
  }

  /** 按搜索参数过滤（Name/Path/GroupPath like、Method eq —— 与 SearchFields Operator 一致） */
  private filterApis(list: ApiItem[], search: Record<string, any>): ApiItem[] {
    let result = list
    const { Name, Path, GroupPath, Method } = search
    if (Name) {
      const kw = String(Name).toLowerCase()
      result = result.filter(
        (a) =>
          (a.Name || '').toLowerCase().includes(kw) ||
          (a.Path || '').toLowerCase().includes(kw) ||
          (a.GroupPath || '').toLowerCase().includes(kw),
      )
    }
    if (Path) {
      const kw = String(Path).toLowerCase()
      result = result.filter((a) => (a.Path || '').toLowerCase().includes(kw))
    }
    if (GroupPath) {
      const kw = String(GroupPath).toLowerCase()
      result = result.filter((a) => (a.GroupPath || '').toLowerCase().includes(kw))
    }
    if (Method) {
      result = result.filter((a) => a.Method === Method)
    }
    return result
  }

  /** GroupPath → 模块/控制器分组树（逻辑自原手写 el-table 版迁移） */
  private buildTree(apis: ApiItem[]): ApiTreeRow[] {
    const modules = new Map<string, Map<string, ApiItem[]>>()

    for (const api of apis) {
      const groupPath = api.GroupPath || '未分组'
      const segments = groupPath.split('/').filter(Boolean)
      const moduleName = segments.length > 1 ? segments.slice(0, -1).join('/') : groupPath
      const controllerName = segments.length > 1 ? segments[segments.length - 1] : groupPath

      if (!modules.has(moduleName)) modules.set(moduleName, new Map())
      const controllers = modules.get(moduleName)!
      if (!controllers.has(controllerName)) controllers.set(controllerName, [])
      controllers.get(controllerName)!.push(api)
    }

    const rows: ApiTreeRow[] = []

    for (const [moduleName, controllers] of [...modules.entries()].sort((a, b) =>
      a[0].localeCompare(b[0]),
    )) {
      const controllerRows: ApiTreeRow[] = []

      for (const [controllerName, groupApis] of [...controllers.entries()].sort((a, b) =>
        a[0].localeCompare(b[0]),
      )) {
        const base = moduleName === controllerName ? '' : `${moduleName}/`
        controllerRows.push({
          Code: `group:${base}${controllerName}`,
          NodeType: 'group',
          Name: controllerName,
          GroupPath: `${base}${controllerName}`,
          ApiCount: groupApis.length,
          children: [...groupApis]
            .sort((a, b) => (a.Path || '').localeCompare(b.Path || ''))
            .map((api) => ({
              Code: api.Code,
              NodeType: 'api' as const,
              Name: api.Name,
              GroupPath: api.GroupPath,
              Method: api.Method,
              Path: api.Path,
              IsValid: api.IsValid,
              Author: api.Author,
              CreateTime: api.CreateTime,
            })),
        })
      }

      // 只有一级（如 Auth）时直接用控制器节点，避免多出一层同名分组
      if (controllerRows.length === 1 && moduleName === controllerRows[0].Name) {
        rows.push(controllerRows[0])
      } else {
        rows.push({
          Code: `group:${moduleName}`,
          NodeType: 'group',
          Name: moduleName,
          GroupPath: moduleName,
          ApiCount: controllerRows.reduce((sum, row) => sum + (row.ApiCount ?? 0), 0),
          children: controllerRows,
        })
      }
    }

    return rows
  }

  // ========================================================
  // 自定义动作（registerHandler 优先于 dispatch 内置分支）
  // ========================================================

  /** 同步接口（扫描 + 增量更新到 sys_api） */
  async handleSync(): Promise<void> {
    try {
      await ElMessageBox.confirm(
        '将扫描所有 Controller 接口并同步到数据库。已有接口的权限关联不会被清除。确认继续？',
        '同步接口',
        {
          confirmButtonText: '确认同步',
          cancelButtonText: '取消',
          type: 'warning',
        },
      )
    } catch {
      return
    }

    this.syncing.value = true
    this.syncResult.value = null
    try {
      this.syncResult.value = await syncApis()
      ElMessage.success(
        `同步完成：新增 ${this.syncResult.value.Added} 个，更新 ${this.syncResult.value.Updated} 个，删除 ${this.syncResult.value.Deleted} 个`,
      )
      await this.refresh()
    } catch (e: any) {
      ElMessage.error(e?.message || '同步失败')
    } finally {
      this.syncing.value = false
    }
  }

  /** 展开/折叠全部（YzhTreeTable expose expandAll/collapseAll；用状态跟踪按钮文案） */
  toggleExpandAll(): void {
    this.expandAll.value = !this.expandAll.value
    if (this.expandAll.value) {
      this._tableRef?.expandAll?.()
    } else {
      this._tableRef?.collapseAll?.()
    }
  }

  /**
   * Swagger 测试跳转。
   * 有 row → 深链到对应操作；无 → 首页。
   * 地址来源：yzhApi.baseURL 派生（缺省相对路径 /swagger，dev 走 vite proxy）
   */
  async openSwagger(row?: ApiTreeRow): Promise<void> {
    // 先同步打开标签页：await 之后再 open 会被浏览器当成弹窗拦截
    const win = window.open('', '_blank')

    const url = row
      ? await getSwaggerOperationUrl({ Method: row.Method ?? '', Path: row.Path ?? '' })
      : null
    const target = url ?? SWAGGER_UI_URL

    if (win) {
      win.location.href = target
    } else {
      window.location.href = target
    }

    if (row && !url) {
      ElMessage.warning('未在 Swagger 文档中定位到该接口，已打开 Swagger 首页')
    }
  }
}

export default ApiPageLogic
