/**
 * ConfigLogic — 系统参数配置 Logic（SingleTableCore 架构）
 *
 * 约定（YZH 架构铁律）：
 * - 后端行 / formData / YzhForm.fields[].prop 全部 PascalCase（JSON 字段名 = 实体属性名 = 数据库列名）
 * - 行操作（edit / delete / toggle-valid）由内核 dispatch 统一派发
 * - 分页 / 排序 / 搜索由内核 dataLoader 承担
 *
 * 本类只保留真正的配置差异（新增默认值），不再重复实现内核已有的 CRUD 能力。
 * 前端不生成 Code：实体约定 Code = Guid.NewGuid().ToString("N")，由后端新增时生成。
 */

import { SingleTableCore } from '@yzh-core'

export class ConfigLogic extends SingleTableCore<any> {
  controllerName = 'System/Config'

  /** 新增默认值（PascalCase，与 formFields[].prop 一致） */
  protected override get defaultValues(): Record<string, any> {
    return {
      ConfigType: 'string',
      IsValid: 1,
      IsReadonly: 0,
      Sort: 0,
    }
  }

  /** toggle-valid 确认弹窗显示参数键名 */
  protected override get entityNameField(): string {
    return 'ConfigKey'
  }
}

export default ConfigLogic
