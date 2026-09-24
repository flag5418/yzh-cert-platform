/**
 * 原子 API 出口 —— 系统域（System）
 * 后端镜像：src/yzh-core/YZH.Core.Web/Controllers/System/
 */
export * from './menu'
// 注：role-user / role-menu / role-api 同名函数（checkAdd/getCheckTree/…）不可进本 barrel（TS2308）；
// 消费者经子路径导入：`@yzh-core/api/system/role-user`（package.json exports `./api/system/*`）。
