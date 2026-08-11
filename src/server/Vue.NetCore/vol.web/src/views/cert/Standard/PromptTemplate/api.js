/**
 * Prompt 模板管理 API
 */
import http from "@/api/http.js";

/**
 * 获取提示词列表（可按类型/技能筛选）
 */
export const getPromptList = (params) => {
  return http.get("/api/prompt-template", params);
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
  return http.get(`/api/prompt-template/active/${promptType}`, { skillTarget });
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
  return http.delete(`/api/prompt-template/${promptCode}`);
};

/**
 * 激活提示词
 */
export const activatePrompt = (promptCode) => {
  return http.post(`/api/prompt-template/${promptCode}/activate`);
};
