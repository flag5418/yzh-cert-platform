// ============================================================
//  YZH 单表默认「操作列」预设
//  作用：在 Vol columns 末尾自动追加一列「修改 / 删除」行级按钮
//  注意：Vol 列渲染走 render 函数（formatter 只返回字符串给 v-html，不能写 VNode）
//        参考 Sys_User.vue render: (h, {row, column, index}) => h(...)
//        为确保在 Vite HMR / Vue 组件异步注册场景下 button 一定能渲染出来，
//        render 中改用 Vue 原生 <a> + 内联样式（语义为 link），onClick 走原生 DOM 事件，
//        不依赖 Element Plus 的 el-button 组件是否已完成 app.component 注册。
// ============================================================
import type { IYZHEntitySchema } from '@/types/yzh/YZHEntitySchema'

export interface IYZHActionHandlers<TEntity = any> {
  onEdit: (row: TEntity) => void
  onDelete: (row: TEntity) => void
  /** 是否禁用（每行单独判断） */
  disabledEdit?: (row: TEntity) => boolean
  disabledDelete?: (row: TEntity) => boolean
  /** 按钮文本，默认「修改/删除」 */
  editText?: string
  deleteText?: string
}

/** 在 columns 末尾追加「操作列」，返回新的 columns 数组 */
export function buildActionColumn<TKey, TEntity extends object>(
  schema: IYZHEntitySchema<TKey, TEntity>,
  handlers: IYZHActionHandlers<TEntity>,
  baseColumns: any[] = []
) {
  const {
    onEdit,
    onDelete,
    disabledEdit,
    disabledDelete,
    editText = '修改',
    deleteText = '删除'
  } = handlers
  // 关键：参考 Sys_Department.vue / Sys_User.vue / Sys_Dictionary.vue 中真正可工作的 render 列：
  //   ① 必须有 field: 非空字符串（'操作'/'__yzh_action'），Vol 内部 el-table-column :prop="column.field" 依赖它
  //   ② 不要配置 column.edit / column.children / column.readonly（否则 Vol 会走到多表头 / 文件编辑分支，跳过 <table-render>）
  //   ③ render 必须是 function 类型（不能是 getter/箭头函数赋值成其他类型），Vol L476 是 `v-else-if="column.render && typeof column.render == 'function'"`
  const col: any = {
    title: '操作',
    field: '__yzh_action',
    key: '__yzh_action',
    dataIndex: '__yzh_action',
    width: 160,
    minWidth: 160,
    align: 'center',
    fixed: 'right',
    hidden: false
  }
  // 显式赋值 render（函数声明式，非对象方法简写，保证 typeof column.render === 'function' 在各种 proxy/defineProperty 包装下都对）
  col.render = function render(h: any, params: any) {
    const row: TEntity = params?.row
    const disabledE = typeof disabledEdit === 'function' ? !!disabledEdit(row) : false
    const disabledD = typeof disabledDelete === 'function' ? !!disabledDelete(row) : false
    const linkBtn = (
      text: string,
      color: string,
      disabled: boolean,
      click: (e: MouseEvent) => void
    ) =>
      h(
        'a',
        {
          href: 'javascript:void(0)',
          style: {
            display: 'inline-block',
            padding: '0 8px',
            lineHeight: 1.5,
            fontSize: '13px',
            color: disabled ? '#C0C4CC' : color,
            textDecoration: 'none',
            cursor: disabled ? 'not-allowed' : 'pointer',
            opacity: disabled ? '0.6' : '1'
          },
          onClick: (e: any) => {
            try {
              e?.stopPropagation?.()
              e?.preventDefault?.()
            } catch (_) {}
            if (!disabled) click(e as MouseEvent)
          }
        },
        // Vue 3 h(type, props, children)：children 必须是数组/字符串/数字/VNode，
        // Vol TableRender 是 functional 组件，对 `() => text` 形式的函数 children 不做二次解包（Element Plus 的 el-table-column 会，这里普通 VNode 不会）
        [text]
      )
    return h(
      'div',
      {
        style: {
          display: 'inline-flex',
          gap: '4px',
          alignItems: 'center',
          justifyContent: 'center',
          width: '100%'
        }
      },
      [
        linkBtn(editText, '#409EFF', disabledE, () => onEdit(row)),
        linkBtn(deleteText, '#F56C6C', disabledD, () => onDelete(row))
      ]
    )
  }
  const filtered = baseColumns.filter(
    (c) =>
      c.key !== '__yzh_action' &&
      c.field !== '__yzh_action' &&
      !(typeof c.title === 'string' && c.title.indexOf('操作') === 0)
  )
  return [...filtered, col]
}
