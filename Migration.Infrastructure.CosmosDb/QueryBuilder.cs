namespace Migration.Infrastructure.CosmosDb
{
    public class QueryBuilder
    {
        public static string Build(string rawQuery)
        {
            if( rawQuery.Contains("*"))
                return rawQuery;

            if (rawQuery.Contains("c.id"))
                return rawQuery;
            
            var query = rawQuery.Replace("from c".ToLower(), ", c.id from c");

            return query;
        }
    }
}