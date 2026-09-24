/**
 * 实体配置适配层（AD-1..AD-7）
 *
 * 职责：
 * - 将后端 EntityConfigDto / TreeBehaviorConfigDto / TreeItemDto 转换为
 *   原子组件所需的 YzhTableColumn / YzhFormField / SearchField / YzhAction / 树节点
 * - 全部为纯函数：无副作用、无 IO、可单测（AD-6）
 * - 业务字段名（StandardCode / DicName / OrgCode …）只允许出现在页面或本层，
 *   内核（SingleTableCore / TreeTableCore）与组件不得硬编码（AD-7）
 */

import type { YzhFormField } from '../components/form'
import type {
  SearchField,
  YzhAction,
  YzhTableColumn,
} from '../components/table/types'
import type {
  EntityConfigDto,
  TreeBehaviorConfig,
  TreeItemDto,
} from '../types/contracts'

// ========================================================
// 控件类型映射（纯查表）
// ========================================================

/** ColumnConfig.Type → YzhFormField.type */
export function mapControlType(type: string): YzhFormField['type'] {
  const map: Record<string, YzhFormField['type']> = {
    TextBox: 'text',
    TextArea: 'textarea',
    NumberBox: 'number',
    Decimal: 'number',
    DatePicker: 'date',
    DateTimePicker: 'datetime',
    ComboBox: 'select',
    DropDownList: 'select',
    RadioButtonList: 'radio',
    CheckBox: 'checkbox',
    Switch: 'switch',
    Upload: 'upload',
    TreeSelect: 'treeSelect',
    Cascader: 'cascader',
    PasswordBox: 'password',
    Memo: 'textarea',
  }
  return map[type] || 'text'
}

/** ColumnConfig.Type → SearchField.type */
export function mapSearchType(type: string): SearchField['type'] {
  const map: Record<string, SearchField['type']> = {
    NumberBox: 'number',
    DatePicker: 'date',
    DateTimePicker: 'dateRange',
    ComboBox: 'select',
    DropDownList: 'select',
    RadioButtonList: 'select',
  }
  return map[type] || 'text'
}

/** SearchFieldConfig.ControlType → SearchField.type */
export function mapSearchControlType(ct: string): SearchField['type'] {
  const map: Record<string, SearchField['type']> = {
    input: 'text',
    select: 'select',
    date: 'date',
    cascader: 'cascader' as SearchField['type'],
  }
  return map[ct] || 'text'
}

// ========================================================
// AD-1 EntityConfig → YzhTableColumn[]
// ========================================================

export function toTableColumns(config: EntityConfigDto | null): YzhTableColumn[] {
  const cols = config?.Columns
  if (!cols) return []
  const enableField = config?.EnableField
  return cols
    .filter((c) => c.XsFlag)
    .map((c) => {
      const col: any = {
        prop: c.FieldName,
        label: c.DesName,
        width: Number(c.Width) || undefined,
        sortable: c.Sortable || undefined,
        fixed: (c.Fixed as 'left' | 'right') || undefined,
        align: (c.Align as 'left' | 'center' | 'right') || undefined,
        dictCode: c.DictCode || undefined,
      }
      if (c.Type === 'CustomSlot') {
        col.slot = c.FieldName
      }
      // EnableField 列自动走自定义插槽（前端渲染状态标签）
      if (enableField && c.FieldName === enableField) {
        col.slot = c.FieldName
      }
      return col as YzhTableColumn
    })
}

// ========================================================
// AD-2 EntityConfig → YzhFormField[]
// ========================================================

/** 表单布局列数（FormCols，0=自动：BcFlag≤10 用 1 列） */
export function toFormLayoutCols(config: EntityConfigDto | null): number {
  const formCols = config?.FormCols
  if (formCols && formCols > 0) return formCols
  const bcCount = config?.Columns?.filter((c) => c.BcFlag).length ?? 0
  return bcCount <= 10 ? 1 : 2
}

export function toFormFields(
  config: EntityConfigDto | null,
  editMode = '0',
  opts?: { withDefaults?: boolean },
): YzhFormField[] {
  const cols = config?.Columns
  const schema = config?.Schema
  if (!cols) return []
  const layoutCols = toFormLayoutCols(config)
  const colSpan = Math.floor(24 / layoutCols)
  const withDefaults = opts?.withDefaults ?? false
  return cols
    .filter((c) => c.BcFlag && c.Type !== 'Other')
    .map((c) => {
      const prop = c.FieldName
      // Schema 字典 key 是 camelCase（反射 ToCamelCase）→ 转 PascalCase 查表
      const camelKey = toCamelKey(prop)
      const fieldSchema = schema?.[camelKey]
      const fieldGroupIndex = c.GroupIndex || '0'
      const isDisabledByGroupIndex = editMode !== '0' && fieldGroupIndex !== editMode
      return {
        prop,
        label: c.DesName,
        type: mapControlType(c.Type),
        required: !c.Yxk,
        disabled: c.Enable === false || isDisabledByGroupIndex,
        span: colSpan,
        dictCode: c.DictCode || undefined,
        options: undefined,
        placeholder: c.Type?.includes('Picker')
          ? `请选择${c.DesName}`
          : `请输入${c.DesName}`,
        defaultValue: withDefaults
          ? c.Mrz
            ? c.Type === 'Switch'
              ? Number(c.Mrz)
              : c.Mrz
            : (fieldSchema as any)?.Default
          : undefined,
        fieldSchema,
      }
    })
}

// ========================================================
// EntityConfig → SearchField[]
// ========================================================

export function toSearchFields(config: EntityConfigDto | null): SearchField[] {
  const sf = config?.SearchFields
  if (sf && sf.length > 0) {
    return sf.map((s) => ({
      prop: s.Field,
      label: s.Label,
      type: mapSearchControlType(s.ControlType),
      placeholder: s.ControlType === 'select' ? `请选择${s.Label}` : `请输入${s.Label}`,
      // SearchFieldDto.Options 为 PascalCase {Label,Value} → 组件层 SearchField.options 为 camelCase {label,value}
      options: s.Options?.map((o) => ({ label: o.Label, value: o.Value })) ?? undefined,
    }))
  }
  // fallback：从列配置推导（最多 4 个可搜索字段）
  const cols = config?.Columns
  if (!cols) return []
  const excludeTypes = ['Upload', 'TreeSelect', 'Cascader', 'CheckBox']
  return cols
    .filter((c) => c.XsFlag && c.Type !== 'Other' && !excludeTypes.includes(c.Type) && c.BcFlag)
    .slice(0, 4)
    .map((c) => ({
      prop: c.FieldName,
      label: c.DesName,
      type: mapSearchType(c.Type),
      placeholder: `请输入${c.DesName}`,
    }))
}

// ========================================================
// AD-3 EntityConfig → YzhAction[]（Toolbar / RowButtons）
// ========================================================

/** 工具栏按钮（声明式） */
export function toToolbarActions(config: EntityConfigDto | null): YzhAction[] {
  const tb = config?.Toolbar
  if (!tb) return []
  const btns: YzhAction[] = []
  if (tb.Add !== false) btns.push({ key: 'add', text: '新增', type: 'primary' })
  if (tb.Delete !== false) btns.push({ key: 'delete', text: '批量删除', type: 'danger' })
  if (tb.Export !== false) btns.push({ key: 'export', text: '导出', type: 'success' })
  if (tb.Import !== false) btns.push({ key: 'import', text: '导入', type: 'warning' })
  if (tb.CustomButtons) {
    for (const [label, method] of Object.entries(tb.CustomButtons)) {
      btns.push({ key: `custom:${method}`, text: label, type: 'info' })
    }
  }
  return btns
}

/** 行操作按钮（颜色语义随声明走，不内置 edit/delete→颜色 假设） */
export function toRowActions(
  config: EntityConfigDto | null,
  enableField?: string | null,
): YzhAction[] {
  const rb = config?.RowButtons ?? {}
  const btns: YzhAction[] = []
  if (rb.Edit !== false) btns.push({ key: 'edit', text: '编辑', type: 'primary' })
  if (rb.Delete !== false) btns.push({ key: 'delete', text: '删除', type: 'danger' })
  // 自动注入启用/禁用按钮（当 Enable=true 且存在 EnableField 时）
  if (rb.Enable === true && enableField) {
    btns.push({ key: 'toggle-valid', text: '禁用/启用', type: 'warning' })
  }
  if (rb.CustomButtons) {
    for (const [label, method] of Object.entries(rb.CustomButtons)) {
      btns.push({ key: `custom:${method}`, text: label, type: 'info' })
    }
  }
  return btns
}

/** RowButtons → 行按钮字典（兼容旧 Record<string,string> 消费方） */
export function toRowActionButtons(
  config: EntityConfigDto | null,
  enableField?: string | null,
): Record<string, string> {
  const dict: Record<string, string> = {}
  for (const b of toRowActions(config, enableField)) dict[b.key] = b.text
  return dict
}

// ========================================================
// AD-4 TreeBehaviorConfig → YzhAction[]（树节点动作）
// ========================================================

export function toTreeActions(
  treeConfig: TreeBehaviorConfig | null,
  enableField?: string | null,
  options?: { allowAddChild?: boolean },
): YzhAction[] {
  const actions: YzhAction[] = []
  const tc = treeConfig
  if (!tc) return actions
  if (tc.AllowEdit) {
    if (options?.allowAddChild !== false) {
      actions.push({ key: 'add-child', text: '新增下级' })
    }
    actions.push({ key: 'edit', text: '编辑' })
  }
  if (tc.AllowDelete) {
    actions.push({ key: 'delete', text: '删除', type: 'danger', danger: true })
  }
  if (enableField) {
    actions.push({ key: 'toggle-valid', text: '禁用/启用', type: 'warning' })
  }
  // 后端注入的自定义动作
  if (tc.CustomActions) {
    for (const [method, label] of Object.entries(tc.CustomActions)) {
      actions.push({ key: `custom:${method}`, text: label, type: 'info' })
    }
  }
  return actions
}

// ========================================================
// AD-5 TreeItemDto → 组件节点（字段参数化后返回纯数据）
// ========================================================

export function treeItemToNode(dto: TreeItemDto, parent?: { level?: number } | null): TreeItemDto & { level?: number } {
  const level = ((parent as any)?.Extra?.level as number) ?? ((parent as any)?.level as number) ?? -1
  return {
    ...dto,
    Extra: { ...dto.Extra, level: level + 1 },
    Children: [],
  }
}

// ========================================================
// 内部工具
// ========================================================

/** PascalCase → camelCase（仅查 Schema 用；正式转换走 utils/case） */
function toCamelKey(name: string): string {
  if (!name) return name
  if (name[0] >= 'a' && name[0] <= 'z') return name
  return name[0].toLowerCase() + name.slice(1)
}
