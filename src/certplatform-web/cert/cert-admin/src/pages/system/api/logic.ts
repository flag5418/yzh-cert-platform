/**
 * ApiLogic - 接口管理 Logic
 *
 * 功能：
 * - 展示所有接口（按 模块/控制器 分层的树形表格，与权限树同源）
 * - 触发接口同步（扫描 + 增量更新）
 * - 跳转到 Swagger 测试（可定位到具体接口）
 *
 * 分组说明：
 * sys_api.GroupPath 形如 System/Config、Foundation/ISOClause（模块/控制器），
 * 这里按 “/” 拆成层级节点，接口挂在最末级分组下，避免一屏几百行平铺。
 */

import { ref, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  getApiList,
  getSwaggerOperationUrl,
  syncApis,
  SWAGGER_UI_URL,
  type ApiItem,
  type SyncResult,
} from '@/api/system/api'

/** 树形表格行（分组节点 + 接口节点） */
export interface ApiTreeRow {
  Code: string
  NodeType: 'group' | 'api'
  Name: string
  GroupPath?: string
  Method?: string
  Path?: string
  Enable?: boolean
  ApiCount?: number
  children?: ApiTreeRow[]
}

export class ApiLogic {
  // ──── 接口列表 ────
  apiList = ref<ApiItem[]>([])

  // ──── 加载状态 ────
  loading = ref(false)
  syncing = ref(false)

  // ──── 搜索过滤 ────
  searchKeyword = ref('')
  filterMethod = ref('')

  // ──── 展开状态（用 key 重建表格以折叠全部） ────
  tableKey = ref(0)
  expandAll = ref(true)

  // ──── 过滤后的接口列表（叶子层） ────
  filteredApis = computed(() => {
    let result = this.apiList.value

    if (this.searchKeyword.value) {
      const keyword = this.searchKeyword.value.toLowerCase()
      result = result.filter(
        (a) =>
          a.Name.toLowerCase().includes(keyword) ||
          a.Path.toLowerCase().includes(keyword) ||
          a.GroupPath.toLowerCase().includes(keyword),
      )
    }

    if (this.filterMethod.value) {
      result = result.filter((a) => a.Method === this.filterMethod.value)
    }

    return result
  })

  // ──── 树形数据（模块 → 控制器 → 接口） ────
  treeRows = computed<ApiTreeRow[]>(() => {
    const modules = new Map<string, Map<string, ApiItem[]>>()

    for (const api of this.filteredApis.value) {
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

    for (const [moduleName, controllers] of [...modules.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
      const controllerRows: ApiTreeRow[] = []

      for (const [controllerName, apis] of [...controllers.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
        const base = moduleName === controllerName ? '' : `${moduleName}/`
        controllerRows.push({
          Code: `group:${base}${controllerName}`,
          NodeType: 'group',
          Name: controllerName,
          GroupPath: `${base}${controllerName}`,
          ApiCount: apis.length,
          children: [...apis]
            .sort((a, b) => a.Path.localeCompare(b.Path))
            .map((api) => ({
              Code: api.Code,
              NodeType: 'api' as const,
              Name: api.Name,
              GroupPath: api.GroupPath,
              Method: api.Method,
              Path: api.Path,
              Enable: api.Enable,
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
  })

  groupCount = computed(() => this.apiList.value.length)

  // ──── 同步结果 ────
  syncResult = ref<SyncResult | null>(null)

  // ========================================================
  // 加载接口列表
  // ========================================================

  async loadApiList(): Promise<void> {
    this.loading.value = true
    try {
      this.apiList.value = await getApiList()
    } catch (e: any) {
      ElMessage.error(e.message || '加载接口列表失败')
    } finally {
      this.loading.value = false
    }
  }

  // ========================================================
  // 触发同步
  // ========================================================

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
      await this.loadApiList()
    } catch (e: any) {
      ElMessage.error(e.message || '同步失败')
    } finally {
      this.syncing.value = false
    }
  }

  // ========================================================
  // 展开 / 折叠全部（el-table 无对应 API，用 key 重建 + default-expand-all）
  // ========================================================

  toggleExpandAll(): void {
    this.expandAll.value = !this.expandAll.value
    this.tableKey.value++
  }

  // ========================================================
  // Swagger 测试跳转（后端地址：9992 端口）
  // 传入 api 时直接定位到 Swagger 中的对应接口
  // ========================================================

  async openSwagger(api?: ApiItem | ApiTreeRow): Promise<void> {
    // 先同步打开标签页：await 之后再 open 会被浏览器当成弹窗拦截
    const win = window.open('', '_blank')

    const url = api ? await getSwaggerOperationUrl(api as ApiItem) : null
    const target = url ?? SWAGGER_UI_URL

    if (win) {
      win.location.href = target
    } else {
      window.location.href = target
    }

    if (api && !url) {
      ElMessage.warning('未在 Swagger 文档中定位到该接口，已打开 Swagger 首页')
    }
  }

  // ========================================================
  // 重置过滤
  // ========================================================

  resetFilter(): void {
    this.searchKeyword.value = ''
    this.filterMethod.value = ''
  }
}
