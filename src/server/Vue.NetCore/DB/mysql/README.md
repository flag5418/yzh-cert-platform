# SQL 脚本管理规范

> **位置**：`src/server/Vue.NetCore/DB/mysql/`
>
> **最后更新**：2026-07-31

## 📋 数据库连接信息

| 环境 | 主机 | 端口 | 数据库 | 用户名 |
|------|------|------|--------|--------|
| 开发 | localhost | 3307 | yzh_cert_platform | root |
| 缓存 | localhost | 6380 | - | - |

**连接命令**：
```bash
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform
```

---

## 📁 脚本清单与执行顺序

### ⚠️ 重要：必须按顺序执行！

#### Phase 1 - 基础设施（用户体系 + 权限）

| 序号 | 脚本文件 | 说明 | 依赖 |
|------|----------|------|------|
| 1 | `cert_phase1_user_extension.sql` | Sys_User 表扩展字段 | 无 |
| 2 | `cert_platform_tables_v2.1.sql` | 所有业务表结构（Phase 1+2） | #1 |
| 3 | `cert_phase1_exec.sql` | Phase 1 主脚本（机构/注册表） | #2 |
| 4 | `cert_phase1_permissions.sql` | 角色权限配置 | #3 |
| 5 | `cert_platform_menu_init.sql` | 完整菜单初始化 | #4 |
| 6 | `cert_phase1_safe_exec.sql` | 安全版执行脚本（推荐） | #2 |

#### Phase 2 - 业务数据

| 序号 | 脚本文件 | 说明 | 依赖 |
|------|----------|------|------|
| 7 | `cert_phase2_data_dictionary.sql` | 数据字典初始化 | #2 |
| 8 | `cert_phase2_test_data.sql` | 测试数据（10个账号） | #7 |

#### 辅助脚本

| 脚本文件 | 说明 |
|----------|------|
| `cert_phase1_final.sql` | Phase 1 最终版（合并） |
| `cert_phase1_user_auth.sql` | 用户认证相关 |
| `cert_platform_menu_simple.sql` | 简化版菜单 |

---

## 🚀 快速执行指南

### 方式一：全新环境初始化（推荐）

```bash
# 1. 进入目录
cd src/server/Vue.NetCore/DB/mysql

# 2. 按顺序执行
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase1_user_extension.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_platform_tables_v2.1.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase1_exec.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase1_permissions.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_platform_menu_init.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase2_data_dictionary.sql
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase2_test_data.sql
```

### 方式二：使用安全版脚本（一键执行）

```bash
# 安全版脚本已包含幂等检查，可重复执行
mysql -h 127.0.0.1 -P 3307 -u root -p yzh_cert_platform < cert_phase1_safe_exec.sql
```

---

## 📊 测试账号

所有测试账号密码均为：**123456**

| 角色 | 用户名 | User_Type | 说明 |
|------|--------|-----------|------|
| 超级管理员 | admin | 1 | 平台最高权限 |
| 总管理员 | super_admin | 10 | 平台管理 |
| 运维人员 | devops | 13 | 运维操作 |
| 配置人员 | configer | 14 | 系统配置 |
| 质量专员 | qa | 15 | 质量审核 |
| 审核管理员 | cb001_admin | 20 | CB001 机构管理员 |
| 审核组长 | cb001_leader | 21 | CB001 审核组长 |
| 普通审核员 | cb001_auditor | 22 | CB001 审核员 |
| 企业账号 | ent001 | 30 | 示例企业 |

**示例机构**：CB001 - 河北雄安尚龙认证有限公司

---

## ⚠️ 注意事项

1. **幂等性**：所有脚本支持重复执行（使用 `IF NOT EXISTS`）
2. **备份优先**：执行前请备份数据库
3. **字符集**：确保数据库使用 `utf8mb4` 字符集
4. **端口确认**：开发环境使用 **3307** 端口（非默认 3306）

---

## 🔧 故障排查

### 问题：Table 'xxx' already exists
**解决**：脚本已包含 `CREATE TABLE IF NOT EXISTS`，可安全忽略

### 问题：Duplicate entry for key 'PRIMARY'
**解决**：数据已存在，使用 `INSERT IGNORE` 或先清空表

### 问题：Can't connect to MySQL server
**检查**：
```bash
# 确认 Docker MySQL 容器运行状态
docker ps | grep mysql

# 确认端口映射
docker port <container_id>
```

---

## 📝 脚本维护规范

1. **新增脚本**：必须复制到此目录，并在本文档中登记
2. **命名规则**：`cert_phase{N}_{功能描述}.sql`
3. **版本控制**：SQL 脚本纳入 Git 版本管理
4. **注释要求**：每个脚本头部包含：
   - 脚本用途
   - 作者/日期
   - 依赖关系
   - 执行顺序

---

**维护人**：AI 编程助手 / 开发团队  
**更新频率**：随 Phase 迭代更新
