using SqlSugar;

namespace VOL.Core.DbContext
{
    public abstract class BaseDbContext : DbContext
    {

        public virtual ISqlSugarClient SqlSugarClient { get; set; }

        public bool QueryTracking
        {
            set { }
        }
        public BaseDbContext() : base() { }

        public virtual ISugarQueryable<TEntity> Set<TEntity>(bool filterDeleted = false) where TEntity : class, new()
        {
            return SqlSugarClient.Queryable<TEntity>();
        }

        public int SaveChanges()
        {
            return SqlSugarClient.SaveQueues();
        }

    }
}
