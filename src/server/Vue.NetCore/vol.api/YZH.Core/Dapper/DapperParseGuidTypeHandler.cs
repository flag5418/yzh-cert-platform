using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using YZH.Core.Const;
using YZH.Core.Enums;

namespace YZH.Core.Dapper
{
    public class DapperParseGuidTypeHandler
    {
        public static void InitParseGuid()
        {
            if (DBType.Name == DbCurrentType.MySql.ToString())
            {
                SqlMapper.AddTypeHandler(new DapperParseGuidTypeHandlerMySql());
                SqlMapper.RemoveTypeMap(typeof(Guid?));
            }
            else if (DBType.Name == DbCurrentType.Oracle.ToString())
            {
                SqlMapper.AddTypeHandler(new DapperParseGuidTypeHandlerOracle());
                SqlMapper.RemoveTypeMap(typeof(Guid));
                SqlMapper.AddTypeHandler(new DapperParseGuidNullTypeHandlerOracle());
                SqlMapper.RemoveTypeMap(typeof(Guid?));
            }
        }
    }
}
