/**
 * ApiResponse 判定原语（信封统一 · F 阶段）
 *
 * 权威语义：docs/10-YZH架构/22-接口返回规范-V1.md
 * 改造计划：docs/10-YZH架构/23-前后端信封统一改造计划-V1.md
 *
 * 四条铁律（23 §三）：
 *   F-1 判定唯一 —— 必须经 expectOk 通过才允许触发副作用
 *   F-2 失败必 throw —— 不返回 null/false 给流程层
 *   F-3 谁 catch 谁弹 —— 底层只 throw 不提示
 *   F-4 取消不是错误 —— 用 confirmOrFalse 吞掉 cancel/close
 */

import type { ApiResponse } from '../types/contracts'

/**
 * 业务失败错误（L3 结果层）。
 *
 * 与 client.ts 的 ApiError（L1 网络 / L2 HTTP）并列：
 *   ApiError  → 请求根本没成功（抛出点在 client.ts）
 *   BizError  → 请求成功、业务被拒绝（抛出点在 expectOk）
 * 两者都由**流程方法的同一个 catch** 统一提示（铁律 F-3）。
 */
export class BizError extends Error {
  readonly kind = 'business'
  /** 与 HTTP 解耦的业务码（前端禁止用于判断，仅排障） */
  readonly code: number
  /** 原始信封，供需要结构化错误元数据的调用方使用 */
  readonly raw: ApiResponse<any>
  /** 已被上层提示过（防 unhandledrejection 兜底双弹） */
  handled = false

  constructor(res: ApiResponse<any> | undefined | null, fallback = '操作失败') {
    super(res?.err || res?.message || fallback)
    this.name = 'BizError'
    this.code = res?.code ?? 400
    this.raw = (res ?? {}) as ApiResponse<any>
  }
}

/** 是否业务失败错误（catch 分支判断用） */
export function isBizError(e: unknown): e is BizError {
  return e instanceof BizError
}

/** 取信封错误文本：err 优先，message 兜底（P0 后失败时 message 为 ""） */
export function envelopeErrorText(
  res: ApiResponse<any> | undefined | null,
  fallback = '操作失败',
): string {
  return res?.err || res?.message || fallback
}

/**
 * F-1/F-2：写操作闸门。
 * success !== true 一律抛 BizError，绝静默、绝不返回给流程层。
 *
 * 泛型形参 `T` 必须保留：断言成 `ApiResponse<any>` 会让 `res.data` 变 `any`，
 * 下游 `.map(dto => ...)` 全部退化成隐式 any（noImplicitAny 报错）。
 *
 * @param res 信封
 * @param fallback 失败且信封无 err/message 时的兜底文案
 * @throws BizError
 */
export function expectOk<T = any>(
  res: ApiResponse<T> | undefined,
  fallback = '操作失败',
): asserts res is ApiResponse<T> & { success: true } {
  if (!res || res.success !== true) throw new BizError(res, fallback)
}

/**
 * expectOk + 取 data。写操作取返回实体时用。
 *
 * @throws BizError（success !== true 时）
 */
export function unwrapOk<T>(
  res: ApiResponse<T> | undefined,
  fallback = '操作失败',
): T {
  expectOk(res as ApiResponse<any>, fallback)
  return res!.data as T
}

/**
 * 兼容旧签名（20 处调用：role-api / role-menu / role-user / api）。
 *
 * 与 unwrapOk 的区别：第二个参数是 **data 的兜底值**，不是文案。
 * 语义已对齐铁律 F-2：success !== true → 抛 BizError（原文案走 err → message）。
 *
 * @param fallback data 为 null/undefined 时的兜底值
 * @throws BizError
 */
export function unwrap<T>(res: ApiResponse<T> | undefined, fallback: T): T {
  if (!res || res.success !== true) throw new BizError(res, '请求失败')
  return (res.data ?? fallback) as T
}

