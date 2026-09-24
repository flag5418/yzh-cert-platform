#!/bin/bash
# 清库重建 V1 — 准则 A（2026-09-24）
# 顺序：停依赖 → DROP/CREATE → schema → views → seed → Code 回填/NOT NULL → 校验
set -euo pipefail

REBUILD_DIR="$(cd "$(dirname "$0")" && pwd)"
DB=yzh_cert_platform
MYSQL_PWD_ROOT=Yzh123456.
MYSQL=(docker exec yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4)

echo "== 1. DROP + CREATE ${DB} (utf8mb4 / utf8mb4_general_ci) =="
"${MYSQL[@]}" -e "DROP DATABASE IF EXISTS \`${DB}\`; CREATE DATABASE \`${DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;"

echo "== 2. Import schema =="
docker exec -i yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4 "${DB}" < "${REBUILD_DIR}/schema_before.sql"

echo "== 3. Import views (SET NAMES + views) =="
{
  echo "SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;"
  cat "${REBUILD_DIR}/views_before.sql"
} | docker exec -i yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4 "${DB}"

echo "== 4. Import core seed =="
docker exec -i yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4 "${DB}" < "${REBUILD_DIR}/seed_core.sql"

echo "== 5. Import dict seed =="
docker exec -i yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4 "${DB}" < "${REBUILD_DIR}/seed_dict.sql"

echo "== 6. Code 回填（先 UPDATE 后 NOT NULL；必须 docker exec -i 才能收 stdin） =="
docker exec -i yzh-mysql mysql -uroot -p"${MYSQL_PWD_ROOT}" --default-character-set=utf8mb4 "${DB}" <<'SQL'
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- cert_sys_config：业务键 = ConfigKey
UPDATE cert_sys_config SET Code = ConfigKey WHERE Code IS NULL OR Code = '';

-- 核心表兜底回填
UPDATE Sys_User SET Code = CONCAT('USER_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';
UPDATE Sys_Role SET Code = CONCAT('ROLE_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';
UPDATE Sys_Menu SET Code = CONCAT('MENU_', LPAD(Id, 6, '0')) WHERE Code IS NULL OR Code = '';

-- 收紧为 NOT NULL（准则 A）
ALTER TABLE cert_sys_config MODIFY COLUMN Code varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码=ConfigKey';
ALTER TABLE Sys_User MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
ALTER TABLE Sys_Role MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
ALTER TABLE Sys_Menu MODIFY COLUMN Code varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '业务唯一编码';
SQL

echo "== 7. 校验 =="
"${MYSQL[@]}" "${DB}" -N -e "
SELECT 'tables', COUNT(*) FROM information_schema.tables WHERE table_schema='${DB}' AND table_type='BASE TABLE'
UNION ALL SELECT 'views', COUNT(*) FROM information_schema.tables WHERE table_schema='${DB}' AND table_type='VIEW'
UNION ALL SELECT 'config_null_code', COUNT(*) FROM cert_sys_config WHERE Code IS NULL OR Code=''
UNION ALL SELECT 'user_null_code', COUNT(*) FROM Sys_User WHERE Code IS NULL OR Code=''
UNION ALL SELECT 'role_null_code', COUNT(*) FROM Sys_Role WHERE Code IS NULL OR Code=''
UNION ALL SELECT 'menu_null_code', COUNT(*) FROM Sys_Menu WHERE Code IS NULL OR Code=''
UNION ALL SELECT 'config_total', COUNT(*) FROM cert_sys_config
UNION ALL SELECT 'users', COUNT(*) FROM Sys_User
UNION ALL SELECT 'roles', COUNT(*) FROM Sys_Role
UNION ALL SELECT 'menus', COUNT(*) FROM Sys_Menu
UNION ALL SELECT 'apis', COUNT(*) FROM sys_api;
"

echo "== DONE =="
