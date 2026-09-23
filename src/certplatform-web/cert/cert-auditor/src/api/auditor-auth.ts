import { yzhApi } from '@yzh-core/api/client'

/**
 * 专家端认证 API
 *
 * 对应后端：`CertPlatform.Auditor/Controllers/AuditorAuthController.cs`
 * 路由前缀：`/api/AuditorAuth`（与历史项目 `src/old/.../Controllers/Auditor/Partial/AuthController.cs` 一致）
 *
 * ⚠️ 命名铁律（§16.9）：业务字段 PascalCase，与 C# 属性名 / DB 列名逐字一致。
 *    外层 `ApiResponse` 信封是 camelCase（`success` / `message` / `data`），已登记例外 E1。
 */

/** 可选体系认证机构（注册页下拉项） */
export interface CertBodyOption {
  /** 机构 Code（提交注册时回传） */
  Code: string
  /** 机构全称 */
  Name: string
  /** 机构简称 */
  ShortName?: string | null
  /** CNAS 机构编号 */
  CbCode?: string | null
}

/** 专家注册请求 */
export interface AuditorRegisterParams {
  /** 所选体系认证机构 Code */
  CertBodyCode: string
  /** 登录名（≥3 字符，全局唯一） */
  UserName: string
  /** 真实姓名（≤20 字符） */
  UserTrueName: string
  /** 登录密码（≥6 位，后端 AES 加密落库） */
  Password: string
  /** 手机号（可选） */
  PhoneNo?: string
  /** 邮箱（可选） */
  Email?: string
}

/** 专家注册结果 */
export interface AuditorRegisterResult {
  UserCode: string
  UserName: string
  /** 工作区机构 Code */
  OrgCode: string
  /** 工作区机构名称 */
  OrgName: string
  /** 绑定角色 Code（固定 ROLE_AUDIT_CLIENT_ADMIN） */
  RoleCode: string
}

/**
 * 获取可注册的体系认证机构列表（匿名）
 *
 * 后端只返回 `cert_certification_body` 中 `IsValid=1` 且 `Status='active'` 的机构，
 * 即「后台系统已经定义好的体系认证机构」—— 注册时只能从中选择。
 */
export async function getCertBodyOptions(): Promise<any> {
  return yzhApi.post('/api/AuditorAuth/GetOrgList', {}, { requireAuth: false })
}

/**
 * 专家注册（匿名）
 *
 * 一次注册 = 一个工作区，只能针对一个体系认证机构。
 * 成功后后端已建好：工作区机构节点 + 默认角色分组 + 用户 + 角色关联，
 * 直接跳登录页用该账号登录即可。
 */
export async function auditorRegister(params: AuditorRegisterParams): Promise<any> {
  return yzhApi.post('/api/AuditorAuth/Register', params, { requireAuth: false })
}
