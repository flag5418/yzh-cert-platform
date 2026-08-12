/**
 * 文档提取规则管理 API
 */
import http from "@/api/http.js";

/**
 * 获取文件树（复用标准目录接口）
 * @param {Object} params - 查询参数
 * @param {number} params.standardId - 标准ID
 */
export const getFileTree = (params) => {
  return http.get("/api/StandardDirectory/tree", params);
};

/**
 * 获取文档详情
 * @param {string} fileCode - 文件编码
 */
export const getFileDetail = (fileCode) => {
  return http.get(`/api/StandardDirectory/file/${fileCode}`);
};

/**
 * AI 自动分析文档
 * @param {Object} data
 * @param {string} data.fileCode - 文件编码
 * @param {string} data.skill - 使用的技能（word/excel/pdf）
 */
export const aiAnalyzeDocument = (data) => {
  return http.post("/api/DocExtractionRule/analyze", data);
};

/**
 * 生成提取 Prompt
 * @param {Object} data
 * @param {string} data.fileCode - 文件编码
 * @param {Array} data.fields - 字段定义列表
 * @param {Array} data.tables - 表格定义列表
 */
export const generatePrompt = (data) => {
  return http.post("/api/DocExtractionRule/generate-prompt", data);
};

/**
 * 验证 Prompt
 * @param {Object} data
 * @param {string} data.fileCode - 文件编码
 * @param {string} data.prompt - Prompt 内容
 */
export const verifyPrompt = (data) => {
  return http.post("/api/DocExtractionRule/verify", data);
};

/**
 * 保存提取规则
 * @param {Object} data
 * @param {string} data.fileCode - 文件编码
 * @param {string} data.skill - 技能类型
 * @param {Array} data.fields - 字段定义
 * @param {Array} data.tables - 表格定义
 * @param {string} data.prompt - Prompt 内容
 * @param {boolean} data.isValid - 是否验证通过
 */
export const saveExtractionRule = (data) => {
  return http.post("/api/DocExtractionRule/save", data);
};

/**
 * 获取已保存的规则
 * @param {string} fileCode - 文件编码
 */
export const getExtractionRule = (fileCode) => {
  return http.get(`/api/DocExtractionRule/${fileCode}`);
};

/**
 * 删除提取规则
 * @param {string} fileCode - 文件编码
 */
export const deleteExtractionRule = (fileCode) => {
  return http.post(`/api/DocExtractionRule/${fileCode}/delete`);
};

/**
 * 获取 AI 配置信息
 */
export const getAIConfig = () => {
  return http.get("/api/DocExtractionRule/ai-config");
};

/**
 * 更新 AI 配置信息
 * @param {Object} data
 * @param {string} data.apiKey - 阿里云 API Key
 * @param {string} data.model - 模型名称
 */
export const updateAIConfig = (data) => {
  return http.post("/api/DocExtractionRule/ai-config", data);
};

/**
 * 获取可用技能列表
 */
export const getSkills = () => {
  return http.get("/api/DocExtractionRule/skills");
};
