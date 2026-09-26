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
      // ★ 声明式枚举标签（2026-09-26）：列上声明 `Options` → 自动映射为 tagMap，
      //   把原始值（如 `active`）渲染成中文标签（如「合作中」）。
      //   消费方：YzhTable.vue 的 `v-else-if="col.tagMap"` 分支（C-A6 通用标签渲染）。
      //   ⚠️ `dictCode` 分支在模板里**优先于** tagMap；两者同时声明时以 dictCode 为准。
      if (c.Options && c.Options.length > 0) {
        col.tagMap = Object.fromEntries(c.Options.map((o) => [o.Value, o.Label]))
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
  const unitSpan = Math.floor(24 / layoutCols)
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

      // ★ 消费栅格坐标（2026-09-26 补齐 —— 原实现是 G19：所有字段统一 span，
      //   导致「备注 / 企业地址」这类 Memo 被挤在半行、版式看起来"胡乱拼凑"）。
      //   · ColSpan = **列数**（相对 FormCols）→ 换算成 24 栅格
      //   · RowSpan = **行数** → 透传给 YzhForm（`grid-row: span N`）
      const colUnits = Math.min(layoutCols, Math.max(1, c.ColSpan ?? 1))
      const rowUnits = Math.max(1, c.RowSpan ?? 1)

      return {
        prop,
        label: c.DesName,
        type: mapControlType(c.Type),
        required: !c.Yxk,
        disabled: c.Enable === false || isDisabledByGroupIndex,
        span: Math.min(24, unitSpan * colUnits),
        rowSpan: rowUnits,
        dictCode: c.DictCode || undefined,
        // ★ 声明式选项（2026-09-26）：列上声明 `Options` → 直接作为 select/radio/checkbox 的选项。
        //   ⚠️ 字典路（DictCode）**仍未打通**：`YzhForm` 不读 dictCode、字典端点返回的
        //   `Value` 是字典项 Code（GUID）而非业务值 → 需要字典时仍由页面显式注入 options。
        options: c.Options?.length ? c.Options.map((o) => ({ label: o.Label, value: o.Value })) : undefined,
        // ★ 声明式占位提示（2026-09-26）：JSON 的 `Placeholder` 优先，否则按控件类型回落
        placeholder:
          c.Placeholder ||
          (c.Type?.includes('Picker') ? `请选择${c.DesName}` : `请输入${c.DesName}`),
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

/**
 * RowButtons.CustomButtons 统一形状：{ 后端方法名: 按钮文字 }（与后端
 * InjectRowActions / YzhControllerBase GetRowButtons 示例 / TreeConfig.CustomActions 一致）。
 * 注意：Toolbar.CustomButtons 仍为 { 按钮文字: 方法名 }（JSON 声明式，如 SysApi.json）。
 */
function isStateMethod(method: string): boolean {
  const m = method.toLowerCase()
  return m === 'enable' || m === 'disable'
}

/**
 * 行启用状态判据 —— IsValid(int 0/1) 与 IsActive(bool) 通吃。
 *
 * ★ 铁律：行按钮必须**按行状态二选一**（启用行只显「禁用」、停用行只显「启用」），
 *   禁止合并文案「禁用/启用」—— 那样用户看不出当前行是启用还是停用。
 *   配色约定：启用行→ warning 橙「禁用」，停用行→ success 绿「启用」。
 */
export function isRowEnabled(value: unknown): boolean {
  return value === 1 || value === true
}

/** 读行状态字段（PascalCase 优先、camelCase 兜底），缺省视为已启用 */
function readRowEnabledState(row: Record<string, any> | undefined, field: string): boolean {
  const camel = field.charAt(0).toLowerCase() + field.slice(1)
  const val = row?.[field] ?? row?.[camel] ?? 1
  return isRowEnabled(val)
}

/** 行操作按钮（颜色语义随声明走，不内置 edit/delete→颜色 假设） */
export function toRowActions(
  config: EntityConfigDto | null,
  enableField?: string | null,
): YzhAction[] | ((row: Record<string, any>) => YzhAction[]) {
  const rb = config?.RowButtons ?? {}
  const base: YzhAction[] = []
  if (rb.Edit !== false) base.push({ key: 'edit', text: '编辑', type: 'primary' })
  if (rb.Delete !== false) base.push({ key: 'delete', text: '删除', type: 'danger' })

  const cb = rb.CustomButtons ?? {}
  const stateEntries = Object.entries(cb).filter(([method]) => isStateMethod(method))
  const otherEntries = Object.entries(cb).filter(([method]) => !isStateMethod(method))
  const otherActions: YzhAction[] = otherEntries.map(([method, label]) => ({
    key: `custom:${method}`,
    text: label,
    type: 'info' as const,
  }))

  // ── 分支 1：CustomButtons 含 enable/disable（key = custom:方法名，走 /action/{method}）
  //    有 EnableField 时按行状态二选一（与 organization / user / enterprise 同构）
  if (stateEntries.length > 0 && enableField) {
    return (row: Record<string, any>) => {
      const on = readRowEnabledState(row, enableField)
      const actions = [...base]
      const disableEntry = stateEntries.find(([m]) => m.toLowerCase() === 'disable')
      const enableEntry = stateEntries.find(([m]) => m.toLowerCase() === 'enable')
      if (on && disableEntry) {
        actions.push({ key: `custom:${disableEntry[0]}`, text: disableEntry[1], type: 'warning' })
      } else if (!on && enableEntry) {
        actions.push({ key: `custom:${enableEntry[0]}`, text: enableEntry[1], type: 'success' })
      }
      actions.push(...otherActions)
      return actions
    }
  }

  // ── 分支 2：RowButtons.Enable=true 且无状态型 CustomButtons（去重）
  //    key 固定 toggle-valid → 复用内核 dispatch /toggle-valid 与确认弹窗
  if (rb.Enable === true && enableField) {
    const field = enableField
    return (row: Record<string, any>) => {
      const on = readRowEnabledState(row, field)
      return [
        ...base,
        on
          ? { key: 'toggle-valid', text: '禁用', type: 'warning' }
          : { key: 'toggle-valid', text: '启用', type: 'success' },
        ...otherActions,
      ]
    }
  }

  // ── 兜底：状态型 CustomButtons 但无 EnableField → 无法判态，按声明全量渲染（旧行为）
  for (const [method, label] of stateEntries) {
    base.push({ key: `custom:${method}`, text: label, type: 'warning' })
  }
  return [...base, ...otherActions]
}

/** RowButtons → 行按钮字典（兼容旧 Record<string,string> 消费方） */
export function toRowActionButtons(
  config: EntityConfigDto | null,
  enableField?: string | null,
): Record<string, string> {
  const dict: Record<string, string> = {}
  const actions = toRowActions(config, enableField)
  const list = typeof actions === 'function' ? actions({}) : actions
  for (const b of list) dict[b.key] = b.text
  return dict
}

// ========================================================
// AD-4 TreeBehaviorConfig → YzhAction[]（树节点动作）
// ========================================================

export function toTreeActions(
  treeConfig: TreeBehaviorConfig | null,
  enableField?: string | null,
  options?: { allowAddChild?: boolean; node?: Record<string, any> },
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

  // 状态判据：优先取节点 Extra（须传 options.node，否则按已启用处理）
  const field = tc.EnableField ?? enableField
  const extra =
    (options?.node?.Extra as Record<string, any> | undefined) ??
    (options?.node as Record<string, any> | undefined) ??
    {}
  const on = field ? readRowEnabledState(extra, field) : true

  // 与 TreeTableCore.resolveTreeActions 同构：按状态二选一，禁止合并文案
  if (tc.AllowToggle !== false && field) {
    actions.push(
      on
        ? { key: 'toggle-disable', text: '禁用', type: 'warning' }
        : { key: 'toggle-enable', text: '启用', type: 'success' },
    )
  }

  // 后端注入的自定义动作（enable/disable 同样按状态二选一）
  if (tc.CustomActions) {
    for (const [method, label] of Object.entries(tc.CustomActions)) {
      const m = method.toLowerCase()
      if (m === 'disable') {
        if (on) actions.push({ key: `custom:${method}`, text: label, type: 'warning' })
        continue
      }
      if (m === 'enable') {
        if (!on) actions.push({ key: `custom:${method}`, text: label, type: 'success' })
        continue
      }
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
