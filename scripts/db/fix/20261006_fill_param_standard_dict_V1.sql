-- ============================================================
-- 企业资料参数 · 字典化迁移 V1（2026-10-06）
--
-- 背景裁决（docs/20-体系认证/03-详细设计/05-企业资料规范化/26-核心菜单功能设计（待审批）-V2.md §3.1）：
--   /business/fill-param-def 由「标准核心字段（机构×标准×阶段 作用域 + 自动带出配置）」
--   重定义为「企业资料参数」—— 一个按标准管理的简单字典：
--   ① 左树 = [通用] + 各ISO标准；每条参数唯一归属一节点（StandardCode='' 为通用）
--   ② 关键字段不分机构（OrgCode 恒空串）—— DB 实证认证机构 2 家、参数字典全局共享
--   ③ 彻底砍自动带出（SourceKind/SourceExpr/MaintainMode 不再产生行为，DB 列保留、置安全值）
--   ④ 删除 9 条档案镜像参数（企业基本资料应由「企业基本资料」关联带出，字典不需要）
--   ⑤ 菜单 MENU_00217 改名「企业资料参数」（Url 不变，路由/ApiCode 不变）
--
-- 影响面：
--   - cert_fill_param_value 存量 0 行（2026-10-06 实测），无值迁移负担
--   - cert_fill_param_def 20 行 → 11 行有效（9 行软删，可复活但已清自动带出配置）
--   - 代码侧同步：FillParamDefController（删 scopes/enterprise-attrs/effective 三端点）、
--     FillParamValueController / DocumentFillController 三处 def 查询 OrgCode→''、
--     前端页面重写 —— 与本脚本同批部署
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' yzh_cert_platform < 本文件
-- 执行前：停后端；执行后：跑文末「验证 SQL」，期望全部 0 行
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ═════════════════════════════════════════════════════════
-- 第 1 节：软删 9 条档案镜像参数
-- （company_name/company_short_name/credit_code/legal_person/
--   company_address/industry_type/cert_scope/employee_count/contact_name）
-- 注：这些行多为 IsBuiltin=1，页面 OnBeforeDelete 会拦截 —— 迁移走 DB 直改；
--     同步清自动带出配置，将来若被 OnBeforeAdd 复活也是干净行。
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_def
SET IsDeleted   = 1,
    IsValid     = 0,
    DeleteBy    = 'sys_migration',
    DeleteTime  = NOW(),
    SourceExpr  = NULL,
    MaintainMode = 'manual'
WHERE IsDeleted = 0
  AND ParamCode IN (
      'company_name', 'company_short_name', 'credit_code', 'legal_person',
      'company_address', 'industry_type', 'cert_scope', 'employee_count',
      'contact_name');

-- ═════════════════════════════════════════════════════════
-- 第 2 节：三键归一 —— OrgCode / StageCode 恒空串
-- （按标准管理：StandardCode 保留 —— 通用='' 、标准=ISOStandard.Code；
--   不分机构、不分阶段。food_safety_manager 的 StandardCode=食品标准 不动，
--   quality_objective 通用行 + 食品标准覆写行 并存 → PickMostSpecific 仍生效）
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_def SET OrgCode   = '' WHERE OrgCode   <> '';
UPDATE cert_fill_param_def SET StageCode = '' WHERE StageCode <> '';

-- ═════════════════════════════════════════════════════════
-- 第 3 节：砍自动带出（MaintainMode 全部 manual + SourceExpr 清空）
-- ParamValueResolver.Resolve 的 auto/both 分支依赖 SourceExpr；
-- 双清后决策恒走 manual 分支（企业手填），对既有 4 条 both 行行为中性
-- （其 SourceExpr 本就 NULL）。
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_def SET SourceExpr   = NULL WHERE SourceExpr IS NOT NULL;
UPDATE cert_fill_param_def SET MaintainMode = 'manual' WHERE MaintainMode <> 'manual';

-- ═════════════════════════════════════════════════════════
-- 第 4 节：SourceKind —— 保留 ai（AI 生成门槛 AiGenerateResolver /
--   FillParamValueController:615 与专家端 AI 徽章仍依赖），其余 global → manual
-- ═════════════════════════════════════════════════════════
UPDATE cert_fill_param_def SET SourceKind = 'manual' WHERE SourceKind = 'global';

-- ═════════════════════════════════════════════════════════
-- 第 5 节：菜单改名（Url 不变 → 路由 R12 判据不受影响）
-- ═════════════════════════════════════════════════════════
UPDATE sys_menu
SET MenuName    = '企业资料参数',
    UpdateTime  = NOW(),
    UpdateBy    = 'sys_migration'
WHERE Code = 'MENU_00217'
  AND MenuName <> '企业资料参数';

-- ═════════════════════════════════════════════════════════
-- 验证 SQL（执行后逐条跑，期望全部 0 行 / 1 行正确）
-- ⚠️ 验证 SQL 直接比较字符串即可（本脚本显式写入确切值，不依赖排序规则推断）
-- ═════════════════════════════════════════════════════════

-- ① 9 条档案参数不得残留为有效行
SELECT ParamCode FROM cert_fill_param_def
WHERE IsDeleted = 0
  AND ParamCode IN ('company_name','company_short_name','credit_code','legal_person',
                    'company_address','industry_type','cert_scope','employee_count','contact_name');

-- ② 三键归一 + 自动带出清零（任何一行非空 = 迁移不完整）
SELECT ParamCode, OrgCode, StageCode, SourceExpr, MaintainMode, SourceKind
FROM cert_fill_param_def
WHERE OrgCode <> '' OR StageCode <> '' OR SourceExpr IS NOT NULL
   OR MaintainMode <> 'manual' OR SourceKind NOT IN ('ai', 'manual');

-- ③ 有效参数应为 11 行
SELECT COUNT(*) AS expect_11 FROM cert_fill_param_def WHERE IsDeleted = 0 AND IsValid = 1;

-- ④ 按标准归属分布（11 行 = 通用 9 + 食品标准 2[food_safety_manager + quality_objective 覆写]）
SELECT StandardCode, COUNT(*) AS cnt FROM cert_fill_param_def
WHERE IsDeleted = 0 GROUP BY StandardCode;

-- ⑤ 菜单改名生效
SELECT Code, MenuName, Url FROM sys_menu WHERE Code = 'MENU_00217';
