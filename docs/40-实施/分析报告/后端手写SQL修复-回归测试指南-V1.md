# 后端手写SQL修复 - 回归测试指南

## 一、测试环境准备

### 1.1 数据库状态确认

```sql
-- 验证三个新视图已创建
SHOW FULL TABLES IN yzh_cert_platform WHERE TABLE_TYPE = 'VIEW';

-- 预期结果（共9个视图）：
v_cert_configured_rules
v_cert_phase_definition
v_cert_stage
v_certification_body
v_iso_standard
v_standard_directory_root_files
v_sys_user
v_upload_task_detail
v_workflow
```

### 1.2 后端服务启动

```bash
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台
./scripts/backend/start.sh
```

---

## 二、功能回归测试清单

### 2.1 标准目录管理接口

| 接口 | 方法 | 验证点 | 预期结果 |
|------|------|--------|----------|
| `GET /api/v1/standard-directory/org-tree` | GetOrganizationTreeAsync | 组织树正常返回 | ✅ 三级树结构正确 |
| `GET /api/v1/standard-directory/folders/{code}/root-files` | GetRootFilesAsync | 根级文件列表 | ✅ 使用 v_standard_directory_root_files |
| `GET /api/v1/standard-directory/config/{code}` | GetConfigAsync | 配置信息 | ✅ 正常返回 |
| `POST /api/v1/standard-directory/upload/init` | UploadInitAsync | 初始化上传任务 | ✅ 返回 taskId |
| `POST /api/v1/standard-directory/upload/file` | UploadFileAsync | 上传文件 | ✅ 更新状态为 uploaded |
| `POST /api/v1/standard-directory/upload/confirm` | UploadConfirmAsync | 确认上传 | ✅ 文件激活，创建转换队列 |
| `POST /api/v1/standard-directory/upload/cancel` | UploadCancelAsync | 取消上传 | ✅ 清理草稿数据 |
| `GET /api/v1/standard-directory/upload/status/{taskId}` | GetUploadStatusAsync | 查询上传状态 | ✅ 返回进度信息 |

### 2.2 文档提取规则接口

| 接口 | 方法 | 验证点 | 预期结果 |
|------|------|--------|----------|
| `GET /api/v1/doc-extraction/rules` | GetConfiguredRulesAsync | 规则列表 | ✅ 使用 v_cert_configured_rules |
| `POST /api/v1/doc-extraction/rules` | SaveRuleAsync | 保存规则 | ✅ 字段/表格同步保存 |
| `GET /api/v1/doc-extraction/rules/{code}` | GetRuleAsync | 规则详情 | ✅ 包含字段和表格 |
| `DELETE /api/v1/doc-extraction/rules/{code}` | DeleteRuleAsync | 删除规则 | ✅ 软删除 |
| `POST /api/v1/doc-extraction/rules/sync` | SyncExtractionResultAsync | 同步提取结果 | ✅ 同步到 B-08/B-09 |

### 2.3 阶段文件树接口

| 接口 | 方法 | 验证点 | 预期结果 |
|------|------|--------|----------|
| `GET /api/v1/standard-directory/tree/{dirCode}` | GetStageFileTreeAsync | 文件树 | ✅ 含规则状态 |
| `POST /api/v1/standard-directory/retry-conversions` | RetryFailedConversionsAsync | 失败重试 | ✅ 重新入队 |

---

## 三、API 测试命令

### 3.1 基础查询测试

```bash
# 获取组织树
curl -s http://localhost:9990/api/v1/standard-directory/org-tree | jq '.'

# 获取已配置规则列表
curl -s http://localhost:9990/api/v1/doc-extraction/rules | jq '.'

# 获取根级文件（测试新视图）
curl -s "http://localhost:9990/api/v1/standard-directory/folders/<folderCode>/root-files" | jq '.'
```

### 3.2 上传流程测试

```bash
# Step 1: 初始化上传
curl -X POST http://localhost:9990/api/v1/standard-directory/upload/init \
  -H "Content-Type: application/json" \
  -d '{
    "directoryCode": "<yourDirCode>",
    "standardCode": "<standardCode>",
    "phaseCode": "<phaseCode>",
    "folders": [{"path": "/根目录"}],
    "files": [{"path": "/根目录/test.docx", "name": "test.docx"}]
  }'

# 记录返回的 taskId

# Step 2: 模拟文件上传（实际测试需上传真实文件）
# curl -X POST ... 

# Step 3: 确认上传
curl -X POST "http://localhost:9990/api/v1/standard-directory/upload/confirm/<taskId>"

# Step 4: 查询上传状态
curl -s "http://localhost:9990/api/v1/standard-directory/upload/status/<taskId>" | jq '.'
```

### 3.3 错误场景测试

```bash
# 取消不存在的任务
curl -X POST "http://localhost:9990/api/v1/standard-directory/upload/cancel/non-existent-task-id"
# 预期：返回 404 或错误提示

# 查询不存在的任务状态
curl -s "http://localhost:9990/api/v1/standard-directory/upload/status/non-existent-task-id"
# 预期：返回 null
```

---

## 四、数据库直接验证

### 4.1 视图数据验证

```sql
-- 测试 v_cert_configured_rules
SELECT * FROM v_cert_configured_rules LIMIT 5;
-- 预期：返回已配置的规则列表

-- 测试 v_upload_task_detail（需有上传任务）
SELECT * FROM v_upload_task_detail LIMIT 5;
-- 预期：返回任务及关联文件

-- 测试 v_standard_directory_root_files
SELECT * FROM v_standard_directory_root_files LIMIT 5;
-- 预期：返回根级别文件
```

### 4.2 物理删除测试

```sql
-- 检查 IsValid=0 的草稿记录（上传流程中会产生）
SELECT Code, FileCode, FileName, IsValid, UploadStatus, TaskId 
FROM cert_standard_directory_file 
WHERE IsValid = 0 
LIMIT 10;
```

---

## 五、性能监控

### 5.1 视图执行计划

```sql
EXPLAIN SELECT * FROM v_cert_configured_rules WHERE RuleCode = 'xxx';
EXPLAIN SELECT * FROM v_upload_task_detail WHERE TaskId = 'xxx';
EXPLAIN SELECT * FROM v_standard_directory_root_files WHERE DirectoryCode = 'xxx';
```

### 5.2 慢查询日志

```bash
# 监控后端慢查询
tail -f logs/app.log | grep -i "slow\|elapsed"
```

---

## 六、回滚预案

如发现问题需要回滚：

```bash
# 恢复原始数据库视图（如果有备份）
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < scripts/db/views/rollback.sql

# 或手动删除视图
DROP VIEW IF EXISTS v_cert_configured_rules;
DROP VIEW IF EXISTS v_upload_task_detail;
DROP VIEW IF EXISTS v_standard_directory_root_files;
```

---

## 七、测试完成标准

- [ ] 所有 CRUD 接口响应正常
- [ ] 上传流程 4 步完整通过
- [ ] 视图查询返回数据正确
- [ ] 无 SQL 注入风险
- [ ] 响应时间符合预期（<500ms）
- [ ] 无新增异常日志

---

*生成时间：2026-09-20 18:10*
