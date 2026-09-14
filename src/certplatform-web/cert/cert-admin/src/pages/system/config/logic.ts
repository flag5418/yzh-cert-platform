/**
 * ConfigLogic — 系统参数配置 Logic
 *
 * 约定（YZH 架构铁律）：
 * - 后端行 / formData / YzhForm.fields[].prop 全部 PascalCase（JSON 字段名 = 实体属性名 = 数据库列名）
 * - 行操作（edit / delete）直接复用基类 CrudPageLogic.onRowClick（删除自带确认框）
 * - 分页 / 排序 / 搜索由基类 dataLoader 承担（搜索条件来自 YzhTable 传入的 params，
 *   Operator 取自 EntityConfig.SearchFields）
 *
 * 本类只保留真正的配置差异，不再重复实现基类已有的 CRUD 能力。
 */

import { CrudPageLogic } from '@yzh-core'

export class ConfigLogic extends CrudPageLogic<any> {
  controllerName = 'System/Config'

  /** 行操作按钮（从后端 RowButtons 配置派生） */
  get rowActionButtons(): Record<string, string> {
    const buttons: Record<string, string> = {}
    const rb = (this.config.value as any)?.RowButtons
    if (rb?.Edit !== false) buttons['edit'] = '编辑'
    if (rb?.Delete !== false) buttons['delete'] = '删除'
    return buttons
  }

  /**
   * 初始化：只加载页面配置
   *
   * 表格数据由 YzhTable 通过 dataLoader 首次加载，这里不再调用 loadPage()，
   * 否则首屏会重复请求 /filter，并使 logic.rows 与表格数据成为两套状态。
   */
  async init(): Promise<void> {
    await this.loadConfig()
  }

  /**
   * 新增弹窗：补默认值（PascalCase，与 formFields[].prop 一致）
   *
   * 前端不再生成 Code：实体约定 Code = Guid.NewGuid().ToString("N")（32 位），
   * 由后端在新增时生成，前端造 36 位带连字符 UUID 会与约定不一致。
   */
  openAddDialog(): void {
    super.openAddDialog()
    if (!this.formData.ConfigType) this.formData.ConfigType = 'string'
    if (this.formData.IsValid === undefined) this.formData.IsValid = 1
    if (this.formData.IsReadonly === undefined) this.formData.IsReadonly = 0
    if (this.formData.Sort === undefined) this.formData.Sort = 0
  }
}

export default ConfigLogic
