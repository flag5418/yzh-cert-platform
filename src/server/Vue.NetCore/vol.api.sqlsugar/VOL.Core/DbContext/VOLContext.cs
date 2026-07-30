using VOL.Core.DbSqlSugar;
using VOL.Core.Extensions.AutofacManager;


namespace VOL.Core.DbContext
{
    public class VOLContext : BaseDbContext, IDependency
    {
        public VOLContext() : base()
        {
            base.SqlSugarClient = DbManger.Db;
        }
        public VOLContext(string configId) : base()
        {
            base.SqlSugarClient = DbManger.GetConnection(configId);
        }
    }
}
