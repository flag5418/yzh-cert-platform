using SqlSugar;
using System;
using VOL.Core.Const;

namespace VOL.Core.DBManage
{
    public static class SqlSugarDbType
    {
        /// <summary>
        /// 根据配置文件中指定的xxDbType获取对应的数据库类型
        /// </summary>
        /// <param name="dbContextName"></param>
        /// <returns></returns>
        public static DbType GetType(string dbContextName = null)
        {
            string _dbType = DBType.Name.ToLower();
  
            DbType dbType = DbType.SqlServer;
            //配置连接不同的数据库类型，比如同时使用mysql、sqlserver、pgsql数据库
            switch (_dbType)
            {
                //case "mssql":
                //    dbType = DbType.SqlServer;
                //    break;
                case "mysql":
                    dbType = DbType.MySql;
                    break;
                case "oracle":
                    dbType = DbType.Oracle;
                    break;
                case "pgsql":
                    dbType = DbType.PostgreSQL;
                    break;
                case "kdbndp":
                    dbType = DbType.Kdbndp;
                    break;
                case "gaussdb":
                    dbType = DbType.GaussDB;
                    break;
                case "oceanbase":
                    dbType = DbType.OceanBase;
                    break;
                case "dm":
                    dbType = DbType.Dm;
                    break;
                default:
                    break;
            }
            return dbType;
        }
    }
}
