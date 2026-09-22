/**
 * @deprecated 改名过渡别名（ST-2）：请改用 `SingleTableCore`。
 * 本文件将在全站迁移完成后删除。
 */

import SingleTableCoreShim from './SingleTableCore'

export { SingleTableCore as CrudPageLogic, type ActionHandler } from './SingleTableCore'

// 大小写工具保持原导入路径兼容
export { toCamelCase, toPascalCase, pascalCaseFormData, rowToFormData } from '../utils/case'

export default SingleTableCoreShim
