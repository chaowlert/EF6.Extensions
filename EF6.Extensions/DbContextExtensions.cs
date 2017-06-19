using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace EF6.Extensions
{
    public static class DbContextExtensions
    {
        public static void EnsureLoad<TEntity>(this DbContext context, TEntity entity, string propertyName)
            where TEntity : class
        {
            var member = context.Entry(entity).Member(propertyName);
            if (member is DbCollectionEntry collection)
            {
                if (!collection.IsLoaded)
                    collection.Load();
            }
            else if (member is DbReferenceEntry reference)
            {
                if (!reference.IsLoaded)
                    reference.Load();
            }
        }

        public static Task EnsureLoadAsync<TEntity>(this DbContext context, TEntity entity, string propertyName)
            where TEntity : class
        {
            return EnsureLoadAsync(context, entity, propertyName, CancellationToken.None);
        }

        public static Task EnsureLoadAsync<TEntity>(this DbContext context, TEntity entity, string propertyName, CancellationToken cancellationToken)
            where TEntity : class
        {
            var member = context.Entry(entity).Member(propertyName);
            if (member is DbCollectionEntry collection)
            {
                if (!collection.IsLoaded)
                    return collection.LoadAsync(cancellationToken);
            }
            else if (member is DbReferenceEntry reference)
            {
                if (!reference.IsLoaded)
                    return reference.LoadAsync(cancellationToken);
            }
            return Task.FromResult(0);
        }

        public static async Task<int> NextValueFor(this DbContext context, string key, Period period = Period.NoPeriod, DateTime? date = null)
        {
            if (date == null && period != Period.NoPeriod)
                date = DateTime.UtcNow;
            var dateKey = date?.ToString(GetFormat(period)) ?? string.Empty;

            try
            {
                return await context.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR [{key}{dateKey}]").FirstAsync();
            }
            catch (SqlException)
            {
                try
                {
                    await context.Database.ExecuteSqlCommandAsync($"CREATE SEQUENCE [{key}{dateKey}] AS int START WITH 1");
                    if (date != null && period != Period.NoPeriod)
                    {
                        var lastDate = GetLastDate(date.Value, period);
                        var lastDateKey = lastDate.ToString(GetFormat(period));
                        await context.Database.ExecuteSqlCommandAsync($"DROP SEQUENCE IF EXISTS [{key}{lastDateKey}]");
                    }
                }
                catch { }

                return await context.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR [{key}{dateKey}]").FirstAsync();
            }
        }

        private static string GetFormat(Period period)
        {
            switch (period)
            {
                case Period.NoPeriod:
                    return "";
                case Period.Year:
                    return "yy";
                case Period.Month:
                    return "yyMM";
                case Period.Day:
                    return "yyMMdd";
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
            }
        }

        private static DateTime GetLastDate(DateTime date, Period period)
        {
            switch (period)
            {
                case Period.Year:
                    return date.AddYears(-1);
                case Period.Month:
                    return date.AddMonths(-1);
                case Period.Day:
                    return date.AddDays(-1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
            }
        }
    }

    public enum Period
    {
        NoPeriod = 0,
        Year,
        Month,
        Day,
    }
}
