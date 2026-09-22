// ──── 内核（新命名：*Core） ────
export { SingleTableCore, type ActionHandler } from './SingleTableCore'
export { TreeTableCore } from './TreeTableCore'
export { TreeSide } from './TreeSide'
export { AssociationTreeCore, type AssociationApi, type AssociationSelection } from './AssociationTreeCore'
export { CheckTreeCore } from './CheckTreeCore'
export { LinkTableCore, type LinkTableApi } from './LinkTableCore'

// ──── 过渡别名（@deprecated，全站迁移完成后删除） ────
export { CrudPageLogic } from './CrudPageLogic'
export { TreeTableLogic } from './TreeTableLogic'

// ──── 大小写工具（保持原导出路径兼容） ────
export { toCamelCase, toPascalCase, pascalCaseFormData, rowToFormData } from './SingleTableCore'
