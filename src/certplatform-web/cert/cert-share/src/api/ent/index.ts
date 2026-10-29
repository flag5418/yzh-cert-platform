// 企业域 API（专家端企业资料管理）
// - enterprise-file      企业资料库（已按标准备好的材料，落 enterprise-documents/）
// - enterprise-original  企业原始资料（散乱原始资料，落 enterprise-original-source/）
//   ⛔ 两者是**不同业务**（36 号 §2.1），切勿合并
// - enterprise-normalize 企业资料规范化（原始资料 → 规范化标准文档；产物落 enterprise-documents/）
//   ⚠️ 与 `enterprise-original` 是**上下游**关系：后者是输入素材，前者是输出产物，⛔ 也不是同一件事
// - enterprise-doc      ★ **上传模块重写版**（36 号 §6.3，2026-10-29）：只按「企业」一层，
//   ⛔ 无阶段、上传无标准；标准只从当前有效认证计划来。与 `enterprise-original` 是**两代实现**，
//   `enterprise-original` 保留到 P5 迁移完成后删除 —— ⛔ 不要在同一页面里混用两者。
export * from './enterprise-file'
export * from './enterprise-original'
export * from './enterprise-normalize'
export * from './enterprise-doc'
