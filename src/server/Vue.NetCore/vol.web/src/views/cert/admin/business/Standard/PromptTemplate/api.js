/**
 * Prompt 模板管理 API
 */
import http from "@/api/http.js";

/**
 * 获取提示词列表（可按类型/技能筛选）
 * 注意：后端返回 PascalCase 字段名，需要转换为 camelCase
 */
export const getPromptList = (params) => {
  // 后端 GetList 接口使用 query params，直接拼接 URL
  const query = new URLSearchParams();
  if (params?.promptType) query.append('promptType', params.promptType);
  if (params?.skillTarget) query.append('skillTarget', params.skillTarget);
  const queryString = query.toString();
  const url = queryString ? `/api/prompt-template/list?${queryString}` : '/api/prompt-template/list';
  return http.get(url);
};

/**
 * 根据编码获取单条提示词
 */
export const getPromptByCode = (promptCode) => {
  return http.get(`/api/prompt-template/${promptCode}`);
};

/**
 * 获取指定类型当前生效的提示词
 */
export const getActivePrompt = (promptType, skillTarget) => {
  const query = new URLSearchParams();
  if (skillTarget) query.append('skillTarget', skillTarget);
  const queryString = query.toString();
  const url = queryString ? `/api/prompt-template/active/${promptType}?${queryString}` : `/api/prompt-template/active/${promptType}`;
  return http.get(url);
};

/**
 * 创建或更新提示词
 */
export const savePrompt = (data) => {
  return http.post("/api/prompt-template", data);
};

/**
 * 删除提示词
 */
export const deletePrompt = (promptCode) => {
  return http.post(`/api/prompt-template/${promptCode}/delete`);
};

/**
 * 激活提示词
 */
export const activatePrompt = (promptCode) => {
  return http.post(`/api/prompt-template/${promptCode}/activate`);
};
