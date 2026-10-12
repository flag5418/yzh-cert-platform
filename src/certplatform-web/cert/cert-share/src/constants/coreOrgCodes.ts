/**
 * ★ 标准域机构 Code —— 「这行数据属于**标准域**（平台方建立的标准 / 规则 / 模板），
 * 不属于任何企业」的唯一前端常量。
 *
 * 后端权威定义：`CertPlatform.Shared/Domain/Scope/CoreOrgCodes.cs`（`StdEnv`）。
 * ⛔ 前端禁止再出现 `'yzh-std-env'` 字面量，一律引用本常量（守卫 **A-R10** 会拦）。
 *
 * ★ 2026-10-12 用户裁定：旧常量 `YZH_VIRTUAL_ENTERPRISE_CODE = 'YZH-STD-ENT'`
 * （原文件 `virtualEnterprise.ts`）**已删除** —— 它与本常量是**同一件事的两种写法**，
 * 用户明确「只能保留一个字段」。后端 `YzhVirtualEnterprise` 常量同批删除。
 */
export const CORE_ORG_STD_ENV = 'yzh-std-env'
