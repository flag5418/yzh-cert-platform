# `scripts/db/views/` —— 数据库视图 DDL 目录

> 规范正文：`docs/10-YZH架构/26-数据库视图设计规范-V1.md`
> 一句话：**库里有几个视图，这里就必须有几个同名 `.sql`**；视图定义只以本目录为准。

---

## 1. 目录约定

| 约定 | 说明 |
|------|------|
| 文件名 | `v_<业务名>.sql`，**必须与库中视图名逐字一致**（`information_schema.VIEWS.TABLE_NAME`） |
| 文件内容 | 头部注释块（用途 / 引用表 / 算法 / 日期 / **有效性口径** / 消费方 / 回滚）+ `CREATE OR REPLACE VIEW` |
| 幂等 | 一律 `CREATE OR REPLACE VIEW`，可反复执行 |
| 大小写 | 表名可写小写（容器 `lower_case_table_names = 2` ⇒ 大小写不敏感） |
| 排序规则 | 20260923 统一后全库均 `utf8mb4_general_ci` ⇒ DDL 内**不再写** `COLLATE` 提示 |

## 2. 有效性口径（规范 §三）

- ✅ **固化 `IsDeleted = 0`** —— 软删行 = 逻辑不存在（主表与 JOIN 维表都加）
- ⛔ **不固化 `IsValid`** —— 一律**以列暴露**，由消费侧判（后台 `/filter` `/list` 由 ORM 入口统一施加）
- ⚠️ 三值陷阱：`LEFT JOIN` 未命中侧为 `NULL` ⇒ 消费方判 **`== 1`**，⛔ 不可判 `!= 0`；类型一律 `int?`

## 3. 应用与校验

```bash
# 应用单个视图（幂等）
docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform \
  < scripts/db/views/v_xxx.sql

# 一致性校验（推荐改完视图就跑）
bash scripts/db/views/verify-views.sh
```

`verify-views.sh` 三项检查，任一不过即退出码 1：

1. 库中每个视图都有同名 DDL 文件
2. 无孤儿 DDL 文件（目录有、库中没有）
3. DDL 内无「固化 `IsValid`」反模式

## 4. 新增视图的接线清单（漏任一步都**静默失效**）

1. 写 DDL 到本目录 → 应用到库
2. 建 **T+V 视图实体**三件套：`[ViewName]` + `[SugarTable]` + `[NotMapped]`
   - ⛔ 视图专属列**禁用** `[SugarColumn(IsIgnore = true)]`（会把列从 SELECT 一并剔除）
   - ✅ 用 `[SugarColumn(IsOnlyIgnoreInsert = true, IsOnlyIgnoreUpdate = true)]`
3. 若实体落在 `Shared/Entities/`，跑前端守卫 `R17`（必要时登记 `R17_SHARED_ALLOW`）
4. `bash scripts/db/views/verify-views.sh` 必须绿

## 5. 已知偏差

见 `docs/10-YZH架构/26-数据库视图设计规范-V1.md` §一「偏差登记」（D-1 缺档 / D-4 `v_sys_user` 视图列被实体 `IsIgnore` 剔除）。

## 6. 回滚

每个 DDL 文件头部都写了回滚语句，形如：

```sql
DROP VIEW IF EXISTS `v_xxx`;
```
