using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace VOL.Core.BaseProvider
{
    public static class RepositoryBaseSqlExtensions
    {
        private static IDbConnection DbConnection(this IRepositoryDbContext repository)
        {
            return repository.DbContext.Database.GetDbConnection();
        }

        private static IDbTransaction DbContextTransaction(this IRepositoryDbContext repository)
        {
            return repository.DbContext.Database.CurrentTransaction?.GetDbTransaction();
        }

        public static List<TResult> QueryList<TResult>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return repository.DbConnection().Query<TResult>(sql, param,
                    transaction: repository.DbContextTransaction(),
                    commandType: commandType,
                    commandTimeout: timeout)
                .ToList();
        }

        public static async Task<IEnumerable<T>> QueryListAsync<T>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return await repository.DbConnection().QueryAsync<T>(sql, param,
                    transaction: repository.DbContextTransaction(),
                    commandType: commandType,
                    commandTimeout: timeout);
        }

        public static async Task<T> QueryFirstAsync<T>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null) where T : class
        {
            return await repository.DbConnection().QueryFirstOrDefaultAsync<T>(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static T QueryFirst<T>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null) where T : class
        {
            return repository.DbConnection().QueryFirstOrDefault<T>(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static async Task<dynamic> QueryDynamicFirstAsync(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return await repository.DbConnection().QueryFirstOrDefaultAsync(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static dynamic QueryDynamicFirst(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return repository.DbConnection().QueryFirstOrDefault(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static async Task<dynamic> QueryDynamicListAsync(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return await repository.DbConnection().QueryAsync(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static List<dynamic> QueryDynamicList(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return repository.DbConnection().Query(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout).ToList();
        }

        public static async Task<object> ExecuteScalarAsync(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return await repository.DbConnection().ExecuteScalarAsync(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static object ExecuteScalar(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return repository.DbConnection().ExecuteScalar(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static async Task<int> ExcuteNonQueryAsync(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return await repository.DbConnection().ExecuteAsync(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static int ExcuteNonQuery(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            return repository.DbConnection().Execute(sql, param,
                transaction: repository.DbContextTransaction(),
                commandType: commandType,
                commandTimeout: timeout);
        }

        public static async Task<(IEnumerable<T1>, IEnumerable<T2>)> QueryMultipleAsync<T1, T2>(this IRepositoryDbContext repository,
            string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (await reader.ReadAsync<T1>(), await reader.ReadAsync<T2>());
        }

        public static (List<T1>, List<T2>) QueryMultiple<T1, T2>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (reader.Read<T1>().ToList(), reader.Read<T2>().ToList());
        }

        public static async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>)> QueryDynamicMultipleAsync(this IRepositoryDbContext repository,
            string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using (SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout))
            {
                return (await reader.ReadAsync(), await reader.ReadAsync());
            }
        }

        public static (List<dynamic>, List<dynamic>) QueryDynamicMultiple(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (reader.Read().ToList(), reader.Read().ToList());
        }

        public static async Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>)> QueryMultipleAsync<T1, T2, T3>(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (await reader.ReadAsync<T1>(), await reader.ReadAsync<T2>(), await reader.ReadAsync<T3>());
        }

        public static (List<T1>, List<T2>, List<T3>) QueryMultiple<T1, T2, T3>(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (reader.Read<T1>().ToList(), reader.Read<T2>().ToList(), reader.Read<T3>().ToList());
        }

        public static async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>)> QueryDynamicMultipleAsync2(this IRepositoryDbContext repository,
            string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (await reader.ReadAsync<dynamic>(), await reader.ReadAsync<dynamic>());
        }

        public static (List<dynamic>, List<dynamic>) QueryDynamicMultiple2(this IRepositoryDbContext repository, string sql, object param = null,
            CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (reader.Read<dynamic>().ToList(), reader.Read<dynamic>().ToList());
        }

        public static async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>, IEnumerable<dynamic>)> QueryDynamicMultipleAsync3(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (await reader.ReadAsync<dynamic>(), await reader.ReadAsync<dynamic>(), await reader.ReadAsync<dynamic>());
        }

        public static (List<dynamic>, List<dynamic>, List<dynamic>) QueryDynamicMultiple3(this IRepositoryDbContext repository, string sql,
            object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (reader.Read<dynamic>().ToList(), reader.Read<dynamic>().ToList(), reader.Read<dynamic>().ToList());
        }

        public static async Task<(IEnumerable<dynamic>, IEnumerable<dynamic>, IEnumerable<dynamic>, IEnumerable<dynamic>, IEnumerable<dynamic>)> QueryDynamicMultipleAsync5(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                await reader.ReadAsync<dynamic>(),
                await reader.ReadAsync<dynamic>(),
                await reader.ReadAsync<dynamic>(),
                await reader.ReadAsync<dynamic>(),
                await reader.ReadAsync<dynamic>()
            );
        }

        public static (List<dynamic>, List<dynamic>, List<dynamic>, List<dynamic>, List<dynamic>) QueryDynamicMultiple5(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                reader.Read<dynamic>().ToList(),
                reader.Read<dynamic>().ToList(),
                reader.Read<dynamic>().ToList(),
                reader.Read<dynamic>().ToList(),
                reader.Read<dynamic>().ToList()
            );
        }

        public static async Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>)> QueryMultipleAsync<T1, T2, T3, T4>(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                await reader.ReadAsync<T1>(),
                await reader.ReadAsync<T2>(),
                await reader.ReadAsync<T3>(),
                await reader.ReadAsync<T4>()
            );
        }

        public static (List<T1>, List<T2>, List<T3>, List<T4>) QueryMultiple<T1, T2, T3, T4>(this IRepositoryDbContext repository, string sql,
            object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                reader.Read<T1>().ToList(),
                reader.Read<T2>().ToList(),
                reader.Read<T3>().ToList(),
                reader.Read<T4>().ToList()
            );
        }

        public static async Task<(IEnumerable<T1>, IEnumerable<T2>, IEnumerable<T3>, IEnumerable<T4>, IEnumerable<T5>)> QueryMultipleAsync<T1, T2, T3, T4, T5>(
            this IRepositoryDbContext repository, string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = await repository.DbConnection().QueryMultipleAsync(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                await reader.ReadAsync<T1>(),
                await reader.ReadAsync<T2>(),
                await reader.ReadAsync<T3>(),
                await reader.ReadAsync<T4>(),
                await reader.ReadAsync<T5>()
            );
        }

        public static (List<T1>, List<T2>, List<T3>, List<T4>, List<T5>) QueryMultiple<T1, T2, T3, T4, T5>(this IRepositoryDbContext repository,
            string sql, object param = null, CommandType? commandType = null, int? timeout = null)
        {
            using SqlMapper.GridReader reader = repository.DbConnection().QueryMultiple(sql, param,
                       transaction: repository.DbContextTransaction(),
                       commandType: commandType,
                       commandTimeout: timeout);
            return (
                reader.Read<T1>().ToList(),
                reader.Read<T2>().ToList(),
                reader.Read<T3>().ToList(),
                reader.Read<T4>().ToList(),
                reader.Read<T5>().ToList()
            );
        }
    }
}
