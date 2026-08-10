# MinIO 数据清理与实施前准备清单

> **日期**：2026-08-10  
> **状态**：代码已备份，等待手动执行  
> **警告**：以下操作将清空所有 MinIO 文件，请确保已备份重要数据

---

## ✅ 已完成（自动）

- [x] 代码提交：`fa887fb` docs: Office文档转换实施文档 + 数据库字段 + 前端预览优化
- [x] 代码推送：`main -> origin/main`
- [x] 实施文档创建：`Office文档自动转换与MinIO路径重构实施文档-V2.md`

---

## 📋 手动执行清单（按顺序）

### 步骤 1：备份当前数据库（重要！）

```bash
# 进入项目目录
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台

# 备份数据库（请根据实际数据库配置修改）
mysqldump -h localhost -P 3307 -u root -p yzh_cert_platform > backup_$(date +%Y%m%d_%H%M%S).sql

# 验证备份文件
ls -lh backup_*.sql
```

---

### 步骤 2：清空 MinIO 文件（本地 OSS）

#### 方式 A：通过 MinIO Console（推荐）

1. 打开浏览器访问：`http://localhost:9001`
2. 登录（默认：admin / Yzh123456.）
3. 进入 `cert-platform` bucket
4. 全选所有文件/文件夹 → 删除

#### 方式 B：通过 mc 命令行

```bash
# 安装 mc 客户端（如未安装）
brew install minio/stable/mc

# 配置 MinIO 连接
mc alias set local http://localhost:9000 admin Yzh123456.

# 查看当前文件
mc ls local/cert-platform --recursive

# 清空所有文件（危险操作！请确认已备份）
mc rm local/cert-platform --recursive --force

# 验证已清空
mc ls local/cert-platform
```

#### 方式 C：通过 Docker 直接删除卷（最简单）

```bash
# 停止 MinIO 容器
docker stop minio

# 删除 MinIO 数据卷
docker volume rm $(docker volume ls -q | grep minio)

# 或者手动删除目录（根据实际挂载路径）
rm -rf /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docker/minio/data/*

# 重新启动 MinIO
docker start minio
```

---

### 步骤 3：清空本地 OSS 文件（如有）

```bash
# 检查本地 OSS 目录
ls -la src/server/Vue.NetCore/vol.web/wwwroot/oss/

# 清空本地 OSS 文件
rm -rf src/server/Vue.NetCore/vol.web/wwwroot/oss/*

# 验证
ls -la src/server/Vue.NetCore/vol.web/wwwroot/oss/
```

---

### 步骤 4：执行数据库变更

```bash
# 进入 MySQL（根据实际配置修改端口/用户名）
mysql -h localhost -P 3307 -u root -p

# 选择数据库
USE yzh_cert_platform;

# 执行迁移脚本
SOURCE src/server/Vue.NetCore/DB/mysql/add_file_convert_fields.sql;

# 验证字段添加成功
DESCRIBE cert_standard_directory_file;

# 退出
EXIT;
```

**预期输出**：
```
+------------------------+---------------+------+-----+---------+----------------+
| Field                  | Type          | Null | Key | Default | Extra          |
+------------------------+---------------+------+-----+---------+----------------+
| ...                    | ...           | ...  | ... | ...     | ...            |
| converted_storage_path | varchar(512)  | YES  |     | NULL    |                |
| convert_status         | varchar(20)   | YES  |     | NULL    |                |
| convert_message        | varchar(1024) | YES  |     | NULL    |                |
| convert_date           | datetime      | YES  |     | NULL    |                |
+------------------------+---------------+------+-----+---------+----------------+
```

---

### 步骤 5：清理数据库中的文件记录

```sql
-- 清空文件表（因为 MinIO 文件已删除，记录也需要清理）
-- 注意：这会删除所有文件记录，请确保已备份！

-- 查看当前文件数量
SELECT COUNT(*) FROM cert_standard_directory_file;

-- 清空文件表（可选：保留表结构，只清空数据）
TRUNCATE TABLE cert_standard_directory_file;

-- 或者软删除（推荐：保留历史记录）
-- UPDATE cert_standard_directory_file SET Enable = 0, Status = 'archived';
```

---

### 步骤 6：重启服务验证

```bash
# 重启后端 API
cd src/server/Vue.NetCore/vol.api/VOL.WebApi
dotnet run

# 重启前端（新终端）
cd src/server/Vue.NetCore/vol.web
npm run dev
```

---

### 步骤 7：验证清理结果

1. **MinIO 验证**：
   - 访问 `http://localhost:9001`
   - 确认 `cert-platform` bucket 为空

2. **前端验证**：
   - 访问 `http://localhost:9990`
   - 进入标准目录管理
   - 确认文件列表为空

3. **数据库验证**：
   ```sql
   SELECT COUNT(*) FROM cert_standard_directory_file WHERE Enable = 1;
   -- 预期结果：0
   ```

---

## 🚀 实施完成后通知我

当您完成以上所有步骤并验证通过后，请告诉我，我将开始实施：

1. **Phase 1**：路径重构 + 前端适配
2. **Phase 2**：xls→xlsx 转换
3. **Phase 3**：doc→docx 转换
4. **Phase 4**：前端状态展示

---

## 📁 相关文档

| 文档 | 路径 |
|------|------|
| 实施文档 V2 | `docs/20-架构决策/Office文档自动转换与MinIO路径重构实施文档-V2.md` |
| 数据库脚本 | `src/server/Vue.NetCore/DB/mysql/add_file_convert_fields.sql` |
| 方案评估 | `docs/20-架构决策/旧版 Office 文档后端自动转换方案评估-V1.md` |

---

## ⚠️ 注意事项

1. **备份优先**：执行任何删除操作前，请确保数据库已备份
2. **不可逆操作**：MinIO 文件删除后无法恢复（除非有备份）
3. **测试环境**：建议先在测试环境验证流程，再在生产环境执行
4. **并发影响**：清理期间请确保没有其他用户正在上传文件

---

**最后更新**：2026-08-10  
**代码版本**：`fa887fb`
