# 后端手写SQL修复 - 自动测试报告

**执行时间**: 2026-09-20 18:15  
**状态**: ⚠️ 依赖环境问题（dotnet SDK 未找到）

---

## 一、环境检查结果

### 1.1 编译环境
```bash
$ which dotnet
not found

$ find /usr/local /opt/homebrew -name "dotnet" 2>/dev/null
(no output)
```

**问题**: `dotnet` 命令未在系统 PATH 中找到，无法执行编译和运行。

### 1.2 依赖文件状态
| 检查项 | 状态 | 说明 |
|--------|------|------|
| 解决方案文件 | ✅ CertPlatform.sln 存在 | |
| 已编译DLL | ✅ bin/Debug/net8.0 目录存在 | 历史编译产物 |
| dotnet CLI | ❌ 未找到 | 需要安装或配置PATH |

---

## 二、已完成的工作

### 2.1 代码修改 ✅
- [x] PhaseDefinition.cs 补全 ISoftDelete/IIsValid 接口
- [x] DocExtractionRuleService.cs GetConfiguredRulesAsync 改用视图
- [x] StandardDirectoryService.cs 消除全部手写SQL
- [x] IDbOrm.cs 新增 PhysicalDeleteByConditionAsync/BulkUpdateByConditionAsync
- [x] SqlSugarDbOrm.cs 实现两个新接口方法

### 2.2 数据库视图 ✅
| 视图名 | 状态 | 验证 |
|--------|------|------|
| v_cert_configured_rules | ✅ 已创建 | 数据查询正常 |
| v_upload_task_detail | ✅ 已创建 | 数据查询正常 |
| v_standard_directory_root_files | ✅ 已创建 | 数据查询正常 |

### 2.3 实体类 ✅
| 实体类 | 路径 |
|--------|------|
| ConfiguredRuleView.cs | CertPlatform.Shared/Entities/Doc/ |
| UploadTaskDetailView.cs | CertPlatform.Shared/Entities/Dir/ |
| StandardDirectoryRootFileView.cs | CertPlatform.Shared/Entities/Dir/ |

---

## 三、测试执行受阻原因

| 原因 | 详情 |
|------|------|
| dotnet SDK 未安装 | 系统 PATH 中找不到 dotnet 命令 |
| 缺少编译环境 | 无法执行 `dotnet build` 和 `dotnet run` |

---

## 四、手动测试步骤（请用户执行）

### 4.1 确认 dotnet 环境
```bash
# 检查 dotnet 是否安装
which dotnet
dotnet --version

# 如果未安装，参考：
# https://learn.microsoft.com/dotnet/core/install macOS
```

### 4.2 编译项目
```bash
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台
dotnet build CertPlatform.sln --configuration Debug
```

### 4.3 启动后端服务
```bash
./scripts/backend/run-backend.sh all
```

预期输出:
```
[INFO] 开始编译后端项目...
[INFO] 编译成功!
[INFO] 后台启动后端服务...
[INFO] 服务地址: http://localhost:9992
[INFO] 服务已就绪 (耗时 Xs): http://localhost:9992
```

### 4.4 执行 smoke test
```bash
curl -s http://127.0.0.1:9992/api/User/getVierificationCode
# 预期返回验证码 JSON
```

### 4.5 执行端到端测试
```bash
./scripts/backend/test-doc-extraction-api.sh
```

---

## 五、关键验证点

### 5.1 视图查询验证
```sql
-- 在 MySQL 客户端执行
SELECT * FROM v_cert_configured_rules LIMIT 5;
SELECT * FROM v_upload_task_detail LIMIT 5;
SELECT * FROM v_standard_directory_root_files LIMIT 5;
```

### 5.2 API 接口验证
```bash
# 获取规则列表（应使用视图）
curl -s http://127.0.0.1:9992/api/Workflow/DocExtractionRule/configured-rules \
  -H "Authorization: Bearer <token>" | jq '.data'

# 上传流程测试（需登录态）
curl -s http://127.0.0.1:9992/api/StandardDirectory/upload/init \
  -X POST -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{"directoryCode":"D001"}'
```

---

## 六、下一步行动

### 6.1 立即可做
1. **安装 .NET SDK**（如未安装）:
   ```bash
   brew install dotnet
   ```

2. **验证编译**:
   ```bash
   cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台
   dotnet build CertPlatform.sln
   ```

3. **启动并测试**:
   ```bash
   ./scripts/backend/run-backend.sh
   ./scripts/backend/smoke_test.sh
   ```

### 6.2 性能监控建议
```sql
-- 检查视图执行计划
EXPLAIN SELECT * FROM v_cert_configured_rules WHERE RuleCode = 'xxx';

-- 监控慢查询
SHOW VARIABLES LIKE 'slow_query_log%';
```

---

## 七、总结

| 阶段 | 状态 | 阻塞原因 |
|------|------|----------|
| 代码修复 | ✅ 完成 | - |
| 数据库视图 | ✅ 完成 | - |
| 实体类创建 | ✅ 完成 | - |
| 编译验证 | ⏸️ 暂停 | dotnet SDK 未找到 |
| 接口测试 | ⏸️ 暂停 | 依赖编译 |
| 性能测试 | ⏸️ 暂停 | 依赖编译 |

**建议**: 请先安装 dotnet SDK 后重新执行测试流程。

---

*报告生成: AI Assistant*  
*日期: 2026-09-20*