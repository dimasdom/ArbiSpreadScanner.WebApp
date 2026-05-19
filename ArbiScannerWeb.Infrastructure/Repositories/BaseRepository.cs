using ArbiScannerWeb.Abstractions.Interfaces.Repositories;
using ArbiScannerWeb.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ArbiScannerWeb.Infrastructure.Repositories
{
    public class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class, new()
    {
        private readonly AppDbContext _dbContext;

        public BaseRepository(AppDbContext context)
        {
            _dbContext = context;
        }
        
        public IQueryable<TEntity> GetAllQueryable()
        {
            return _dbContext.Set<TEntity>();
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllListAsync()
        {
            return await _dbContext.Set<TEntity>().ToListAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> GetAllListAsync(Expression<Func<TEntity, bool>> predicate)
        {

            return await _dbContext.Set<TEntity>()
               .Where(predicate)
               .ToListAsync();
        }

        public async Task<IEnumerable<TEntity>> GetAllListAsync(params Expression<Func<TEntity, object>>[] includeProperties)
        {
            return await Include(includeProperties).ToListAsync();
        }

        public async Task<IEnumerable<TEntity>> GetAllListAsync(Expression<Func<TEntity, bool>> predicate, params Expression<Func<TEntity, object>>[] includeProperties)
        {
            var query = Include(includeProperties);
            return await query.Where(predicate).AsQueryable().ToListAsync();
        }

        public virtual async Task<TEntity> AddAsync(TEntity entity)
        {
            var newEntity = await _dbContext.Set<TEntity>().AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            return newEntity.Entity;

        }

        public virtual async Task UpdateAsync(TEntity entity)
        {
            _dbContext.Update(entity);
            await _dbContext.SaveChangesAsync();

        }



        public virtual async Task DeleteAsync(TEntity entity)
        {
            _dbContext.Remove(entity);
            await _dbContext.SaveChangesAsync();

        }

        private IQueryable<TEntity> Include(params Expression<Func<TEntity, object>>[] includeProperties)
        {
            IQueryable<TEntity> query = _dbContext.Set<TEntity>().AsNoTracking();
            return includeProperties
                .Aggregate(query, (current, includeProperty) => current.Include(includeProperty));
        }

        public void NoTracking()
        {
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        }

        public async Task AddRange(IEnumerable<TEntity> entities)
        {
            await _dbContext.Set<TEntity>().AddRangeAsync(entities);
        }

        public virtual async Task<TEntity?> GetByIdAsync(object id)
        {
            return await _dbContext.FindAsync<TEntity>(id);
        }
    }
}
