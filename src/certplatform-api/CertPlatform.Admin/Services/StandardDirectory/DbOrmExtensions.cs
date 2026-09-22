using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Dir;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.DataBase.Models;
using YZH.Core.Stand.Models.Result;

namespace CertPlatform.Admin.Services.StandardDirectory
{
    /// <summary>
    /// IDbOrm 批量操作扩展方法，用于上传流程中的中间状态处理
    /// </summary>
    public static class DbOrmExtensions
    {
        /// <summary>按条件批量更新指定字段（绕过软删除/有效过滤，仅用于清理临时数据）</summary>
        public static async Task<Result<int>> BulkUpdateByConditionAsync(
            this IDbOrm db,
            string tableName,
            Expression<Func<StandardDirectoryFile, bool>> filter,
            Expression<Func<StandardDirectoryFile, StandardDirectoryFile>> updater,
            params string[] fields)
        {
            try
            {
                var param = Expression.Parameter(typeof(StandardDirectoryFile), "x");
                var body = updater.Body;
                var conditions = filter.Body.ToString()
                    .Replace("x.", "")
                    .Replace(" == ", " = ")
                    .Replace(" && ", " AND ")
                    .Replace(" || ", " OR ");

                var sql = $"UPDATE `{tableName}` SET {BuildSetClause(updater, fields)} WHERE {conditions}";
                var result = await db.Client.Ado.ExecuteCommandAsync(sql);
                return Result<int>.Ok(result);
            }
            catch (System.Exception ex)
            {
                return Result<int>.Fail($"批量更新失败：{ex.Message}");
            }
        }

        private static string BuildSetClause(Expression<Func<StandardDirectoryFile, StandardDirectoryFile>> updater, string[] fields)
        {
            // 提取 updater lambda 中设置的字段
            if (updater.Body is MemberInitExpression initExpr)
            {
                return string.Join(", ", initExpr.NewExpression.Arguments.Zip(
                    initExpr.Bindings, (arg, binding) => $"`{binding.Member.Name}`=@{binding.Member.Name}"));
            }
            return string.Join(", ", fields.Select(f => $"`{f}`=@{f}"));
        }

        /// <summary>按条件物理删除（绕过软删除，仅用于清理临时数据）</summary>
        public static async Task<Result<int>> PhysicalDeleteByConditionAsync<T>(
            this IDbOrm db,
            string tableName,
            Expression<Func<T, bool>> filter) where T : class, new()
        {
            try
            {
                var whereClause = BuildWhereClause(filter);
                var sql = $"DELETE FROM `{tableName}` WHERE {whereClause}";
                var result = await db.Client.Ado.ExecuteCommandAsync(sql);
                return Result<int>.Ok(result);
            }
            catch (System.Exception ex)
            {
                return Result<int>.Fail($"物理删除失败：{ex.Message}");
            }
        }

        private static string BuildWhereClause(Expression expression)
        {
            // 简化处理：将 LINQ 表达式转换为 SQL WHERE 子句
            // 注意：这只是一个简化实现，复杂查询仍需使用 SqlQueryAsync
            return expression.ToString()
                .Replace(".IsDeleted", ".IsDeleted")
                .Replace(" && ", " AND ")
                .Replace(" || ", " OR ")
                .Replace(" == ", " = ")
                .Replace(" != ", " <> ");
        }

        /// <summary>执行带动态 IN 条件的查询（用于关联表查询）</summary>
        public static async Task<Result<List<TResult>>> QueryWithInAsync<T, TResult>(
            this IDbOrm db,
            string table,
            string keyField,
            IEnumerable<string> values,
            string selectFields = "*",
            string extraWhere = null) where TResult : class, new()
        {
            if (values == null || !values.Any())
                return Result<List<TResult>>.Ok(new List<TResult>());

            var placeholders = string.Join(",", values.Select((_, i) => $"@p{i}"));
            var paramNameList = string.Join(",", values.Select((v, i) => $"@p{i}"));
            var sql = $"SELECT {selectFields} FROM `{table}` WHERE `{keyField}` IN ({paramNameList})";
            if (!string.IsNullOrEmpty(extraWhere))
                sql += $" AND {extraWhere}";

            var parameters = values.Select((v, i) => new { p = $"p{i}", v }).ToDictionary(
                x => x.p, x => (object)x.v);

            return await db.SqlQueryAsync<TResult>(sql, parameters);
        }
    }
}
