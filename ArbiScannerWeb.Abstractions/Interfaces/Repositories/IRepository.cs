using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Abstractions.Interfaces.Repositories
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        Task DeleteAsync(TEntity entity);
        Task UpdateAsync(TEntity entity);
        Task<TEntity?> GetByIdAsync(object id);
        IQueryable<TEntity> GetAllQueryable();
        Task<TEntity> AddAsync(TEntity entity);

        Task<IEnumerable<TEntity>> GetAllListAsync();
        Task<IEnumerable<TEntity>> GetAllListAsync(Expression<Func<TEntity, bool>> predicate);
        Task<IEnumerable<TEntity>> GetAllListAsync(params Expression<Func<TEntity, object>>[] includeProperties);
        Task<IEnumerable<TEntity>> GetAllListAsync(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includeProperties);
        void NoTracking();
        Task AddRange(IEnumerable<TEntity> entities);
    }
}
