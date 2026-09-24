/**
 * LogLogic — 操作日志 Logic（只读表格页面）
 *
 * 后端：`SysLogController : YzhControllerBase<SysLog>`，路由 `/api/System/Log`
 *       （`/config` 返回 EntityConfig；`/filter` 分页查询；`/export` 导出）
 *
 * 只读性由**后端**声明：`SysLogController` 覆写了 `GetToolbar()` / `GetRowButtons()`，
 * 关闭 Add / Delete / Edit、仅保留 Export。前端无需再做禁用，照常绑定
 * `toolbar-actions` / `row-action-buttons` 即可 —— 后端不开启的按钮不会下发。
 *
 * ⚠️ 迁移说明：本文件原继承 `CrudPageLogic`，该内核已从 `@yzh-core` 移除
 *    （`yzh.vue.core/src/logic/CrudPageLogic.ts` 已删除），现统一使用 `SingleTableCore`。
 *    旧代码额外覆写的 `init() { await this.loadConfig() }` 也不再需要 ——
 *    基类 `SingleTableCore.init()` 本身就是「`loadConfig()` → `onAfterInit()`」。
 */

import { SingleTableCore } from '@yzh-core'

export class LogLogic extends SingleTableCore<any> {
  controllerName = 'System/Log'
}

export default LogLogic
