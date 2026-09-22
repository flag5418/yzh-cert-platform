// ──── 内核 ────
export { SingleTableCore, type ActionHandler } from './SingleTableCore'
export { TreeTableCore } from './TreeTableCore'
export { TreeSide } from './TreeSide'
export { AssociationTreeCore, type AssociationApi, type AssociationSelection } from './AssociationTreeCore'
export { CheckTreeCore } from './CheckTreeCore'
export { LinkTableCore, type LinkTableApi } from './LinkTableCore'

// ──── 过渡别名（@deprecated，待剩余消费者迁移后删除） ────
export { TreeTableLogic } from './TreeTableLogic'

// ──── 大小写工具 ────
export { toCamelCase, toPascalCase, pascalCaseFormData, rowToFormData } from './SingleTableCore'
