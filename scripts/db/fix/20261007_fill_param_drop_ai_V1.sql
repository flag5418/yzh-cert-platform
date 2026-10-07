-- ============================================================
-- 企业资料参数 · 去 AI 化迁移 V1（2026-10-07）
--
-- 背景裁决（docs/20-体系认证/03-详细设计/05-企业资料规范化/26-核心菜单功能设计（待审批）-V2.md §3.2 / R8）：
--   `/enterprise-fill-params`（专家端「企业资料参数」填写页）定性为**简单填写页**：
--   后台按标准定义要填哪些字段 → 这里逐项列出来 → 企业手填 → 保存。
--   「这个信息是手动填写的，不是靠 ai 分析的」（用户 2026-10-07 原话）。
--
-- 于是本轮删掉三处 AI 触点：
--   ① 前端「生成提示词」按钮 + AI 提示词弹窗 + `efp-ai-note` 样式
--   ② 后端 `POST /api/Auditor/FillParamValue/ai-prompt` 端点（部署后 ApiSync 自动清 sys_api 与角色关联）
--   ③ DB 取值来源中的 ai 残留 —— cert_fill_param_def.SourceKind / cert_fill_param_value.ValueSource
--
-- 与 20261006_fill_param_standard_dict_V1.sql 的关系：
--   - 该脚本 §4 曾「保留 ai」（当时理由：AiGenerateResolver / AI 徽章仍依赖）；
--     现已核实 AiGenerateResolver **只由锚点前缀 {{ai:...}} 决定，与 SourceKind 无关**
--     （见 CertPlatform.Shared/Fill/Resolvers/AiGenerateResolver.cs 类注释），
--     专家端 AI 徽章所在的 ai-prompt 端点本轮已删除 ⇒ 保留 ai 的理由不再成立。
--   - cert_doc_fill_value.SourceKind 是**另一张表**（文档落值来源），ai 仍是合法值 —— 本脚本不碰。
--   - ValueSource 的实际取值由 ParamValueResolver 写死（manual/default/empty…），
--     前端不再提交该字段 ⇒ 迁移安全，存量值不会被回写。
--
-- 影响面（2026-10-07 实测）：
--   - cert_fill_param_def：4 行 SourceKind='ai' → 'manual'
--       quality_policy / quality_objective(通用) / quality_objective(食品标准) / company_profile
--   - cert_fill_param_value：0 行（UPDATE 是防御性的，实际影响 0 行）
--   - 字典 FILL_SOURCE_KIND 的 ai 选项**刻意保留**（无前端引用，且 cert_doc_fill_value 仍用 ai）
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'<root密码>' yzh_cert_platform < 本文件
-- 执行前：停后端；执行后：跑文末「验证 SQL」，期望全部 0 行
-- 同批部署：FillParamValueController（删 ai-prompt）、前端 enterprise-fill-params 页与 fill-param-value.ts
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ═════════════════════════════════════════════════════════
-- 第 1 节：定义侧 —— SourceKind 的 ai 全部落回 manual
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_def
SET SourceKind = 'manual',
    UpdateTime = NOW(),
    UpdateBy   = 'sys_migration'
WHERE SourceKind = 'ai';

-- ═════════════════════════════════════════════════════════
-- 第 2 节：值侧 —— ValueSource 的 ai 全部落回 manual
-- （当前 0 行；防御性更新，避免将来有人手工造出 ai 值来源）
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_value
SET ValueSource = 'manual'
WHERE ValueSource = 'ai';

-- ═════════════════════════════════════════════════════════
-- 第 3 节：列注释同步 —— 枚举说明里删掉 ai
-- （注释是下一个人判断「这个列还能填什么」的唯一线索，必须与实际行为一致）
-- ⚠️ MODIFY COLUMN 刻意**逐字复刻**原定义（varchar(20) NOT NULL + 原默认值），
--    只换 COMMENT —— 改类型/默认值属未授权的 schema 变更，会被评审挡下来。
-- ═════════════════════════════════════════════════════════
ALTER TABLE cert_fill_param_def MODIFY COLUMN SourceKind
  VARCHAR(20) NOT NULL DEFAULT 'global'
  COMMENT '★取值来源类别：global=全局参数 | replace=替换 | headerFooter=页眉页脚 | manual=人工填写 | compute=计算派生（⛔ai 已于 2026-10-07 作废，见 20261007_fill_param_drop_ai_V1.sql）';

ALTER TABLE cert_fill_param_value MODIFY COLUMN ValueSource
  VARCHAR(20) NOT NULL DEFAULT 'auto'
  COMMENT '★值来源：manual=企业手工填写 | auto=自动映射带出 | default=默认值 | empty=待完善 | import=批量导入（⛔ai 已于 2026-10-07 作废，见 20261007_fill_param_drop_ai_V1.sql）';

-- ═════════════════════════════════════════════════════════
-- 验证 SQL（执行后逐条跑，期望全部 0 行 / 分布合理）
-- ⚠️ 只用 = / <> 比较字面量，⛔ 不靠排序规则推断（见 AGENTS.md 铁律九）
-- ═════════════════════════════════════════════════════════

-- ① 定义侧不得残留 ai
SELECT ParamCode, StandardCode FROM cert_fill_param_def WHERE SourceKind = 'ai';

-- ② 值侧不得残留 ai
SELECT EnterpriseCode, ParamCode FROM cert_fill_param_value WHERE ValueSource = 'ai';

-- ③ 有效定义的 SourceKind 分布（本轮后应全为 manual）
SELECT SourceKind, COUNT(*) AS cnt FROM cert_fill_param_def WHERE IsDeleted = 0 GROUP BY SourceKind;

-- ④ 列注释已更新
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_COMMENT FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND ((TABLE_NAME = 'cert_fill_param_def'  AND COLUMN_NAME = 'SourceKind')
    OR (TABLE_NAME = 'cert_fill_param_value' AND COLUMN_NAME = 'ValueSource'));
