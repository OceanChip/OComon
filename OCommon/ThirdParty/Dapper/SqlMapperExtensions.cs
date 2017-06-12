using Dapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Dapper
{
    public static class SqlMapperExtensions
    {
        private static readonly ConcurrentDictionary<Type, List<PropertyInfo>> _paramCache = new ConcurrentDictionary<Type, List<PropertyInfo>>();
        /// <summary>
        /// 插入数据到表
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="data"></param>
        /// <param name="table"></param>
        /// <param name="transaction"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        public static long Insert(this IDbConnection connection,dynamic data,string table,IDbTransaction transaction=null,int? commandTimeout = null)
        {
            var obj = data as object;
            IList<string> properties = GetProperties(obj);
            var columns = string.Join(",", properties);
            var values=string.Join(",",properties.Select(p=>"@"+p));
            var sql = $"insert into {table}({columns})values({values}) select cast(scope_identity() as bigint)";
            return connection.ExecuteScalar<long>(sql, obj, transaction, commandTimeout);
        }
        public static Task<long> InsertAsync(this IDbConnection connection,dynamic data,string table,IDbTransaction transaction=null,int? commandTimeout = null)
        {
            var obj = data as object;
            IList<string> properties = GetProperties(obj);
            var columns = string.Join(",", properties);
            var values = string.Join(",", properties.Select(p => "@" + p));
            var sql = $"insert into {table}({columns})values({values}) select cast(scope_identity() as bigint)";
            return connection.ExecuteScalarAsync<long>(sql, obj, transaction, commandTimeout);
        }
        /// <summary>
        /// 同步更新
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="data"></param>
        /// <param name="condition"></param>
        /// <param name="table"></param>
        /// <param name="transaction"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        public static int Update(this IDbConnection connection,dynamic data,dynamic condition,string table,IDbTransaction transaction=null,int? commandTimeout = null)
        {
            var obj = data as object;
            var conditionObj = condition as object;

            var updatePropertyInfos = GetPropertyInfos(obj);
            var wherePropertyInfos = GetPropertyInfos(conditionObj);

            var updateProperties = updatePropertyInfos.Select(p => p.Name);
            var whereProperties = wherePropertyInfos.Select(p => p.Name);

            var updateFields = string.Join(",", updateProperties.Select(p => p + "=@" + p));
            var whereFields = string.Empty;
            if (whereProperties.Any())
            {
                whereFields = $" where {string.Join(" and ", whereProperties.Select(p => p + "=@w_" + p))}";
            }
            var sql = $"update {table} set {updateFields} {whereFields}";

            var parameters = new DynamicParameters(data);
            var expandObject = new ExpandoObject() as IDictionary<string, object>;
            wherePropertyInfos.ForEach(p => expandObject.Add("w_" + p.Name, p.GetValue(conditionObj, null)));
            parameters.AddDynamicParams(expandObject);

            return connection.Execute(sql, parameters, transaction, commandTimeout);
        }
        /// <summary>
        /// 异步更新
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="data"></param>
        /// <param name="condition"></param>
        /// <param name="table"></param>
        /// <param name="transaction"></param>
        /// <param name="commandTimeout"></param>
        /// <returns></returns>
        public static Task<int> UpdateAsync(this IDbConnection connection, dynamic data, dynamic condition, string table, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var obj = data as object;
            var conditionObj = condition as object;

            var updatePropertyInfos = GetPropertyInfos(obj);
            var wherePropertyInfos = GetPropertyInfos(conditionObj);

            var updateProperties = updatePropertyInfos.Select(p => p.Name);
            var whereProperties = wherePropertyInfos.Select(p => p.Name);

            var updateFields = string.Join(",", updateProperties.Select(p => p + "=@" + p));
            var whereFields = string.Empty;
            if (whereProperties.Any())
            {
                whereFields = $" where {string.Join(" and ", whereProperties.Select(p => p + "=@w_" + p))}";
            }
            var sql = $"update {table} set {updateFields} {whereFields}";

            var parameters = new DynamicParameters(data);
            var expandObject = new ExpandoObject() as IDictionary<string, object>;
            wherePropertyInfos.ForEach(p => expandObject.Add("w_" + p.Name, p.GetValue(conditionObj, null)));
            parameters.AddDynamicParams(expandObject);

            return connection.ExecuteAsync(sql, parameters, transaction, commandTimeout);
        }
        public static int Delete(this IDbConnection connection, dynamic condition, string table, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var conditionObj = condition as object;
            var whereFields = string.Empty;
            var whereProperties = GetProperties(conditionObj);
            if (whereProperties.Count > 0)
            {
                whereFields = " where " + string.Join(" and ", whereProperties.Select(p => p + " = @" + p));
            }

            var sql = string.Format("delete from {0}{1}", table, whereFields);

            return connection.Execute(sql, conditionObj, transaction, commandTimeout);
        }
        public static Task<int> DeleteAsync(this IDbConnection connection, dynamic condition, string table, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            var conditionObj = condition as object;
            var whereFields = string.Empty;
            var whereProperties = GetProperties(conditionObj);
            if (whereProperties.Count > 0)
            {
                whereFields = " where " + string.Join(" and ", whereProperties.Select(p => p + " = @" + p));
            }

            var sql =$"delete from {table}{whereFields}";

            return connection.ExecuteAsync(sql, conditionObj, transaction, commandTimeout);
        }
        public static int GetCount(this IDbConnection connection,object condition,string table,bool isOr=false,IDbTransaction transaction=null,int? commandTimeout = null)
        {
            return QueryList<int>(connection, condition, table, "count(*)", isOr, transaction, commandTimeout).Single();
        }
        public static Task<int> GetCountAsync(this IDbConnection connection, object condition, string table, bool isOr = false, IDbTransaction transaction = null, int? commandTimeout = null)
        {

            return QueryListAsync<int>(connection, condition, table, "count(*)", isOr, transaction, commandTimeout).ContinueWith<int>(t=>t.Result.Single());
        }
        public static IEnumerable<dynamic> QueryList(this IDbConnection connection,dynamic condition,string table,string columns="*",bool isOr=false,IDbTransaction transaction=null,int? commandTimeout = null)
        {
            return QueryList<dynamic>(connection, condition, table, columns, isOr, transaction, commandTimeout);
        }
        public static Task<IEnumerable<dynamic>> QueryListAsync(this IDbConnection connection, dynamic condition, string table, string columns = "*", bool isOr = false, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return QueryListAsync<dynamic>(connection, condition, table, columns, isOr, transaction, commandTimeout);
        }
        private static IEnumerable<T> QueryList<T>(IDbConnection connection, object condition, string table, string columns, bool isOr, IDbTransaction transaction=null, int? commandTimeout=null)
        {
            return connection.Query<T>(BuilderQuerySQL(condition, table, columns, isOr), condition, transaction, true, commandTimeout);
        }
        private static Task<IEnumerable<T>> QueryListAsync<T>(IDbConnection connection, object condition, string table, string columns, bool isOr, IDbTransaction transaction = null, int? commandTimeout=null)
        {
            return connection.QueryAsync<T>(BuilderQuerySQL(condition, table, columns, isOr), condition, transaction, commandTimeout);
        }
        public static IEnumerable<dynamic> QueryPaged(this IDbConnection connection, dynamic condition, string table, string orderBy, int pageIndex, int pageSize, string columns = "*", bool isOr = false, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return QueryPaged<dynamic>(connection, condition, table, orderBy, pageIndex, pageSize, columns, isOr, transaction, commandTimeout);
        }
        public static Task<IEnumerable<dynamic>> QueryPagedAsync(this IDbConnection connection, dynamic condition, string table, string orderBy, int pageIndex, int pageSize, string columns = "*", bool isOr = false, IDbTransaction transaction = null, int? commandTimeout = null)
        {
            return QueryPagedAsync<dynamic>(connection, condition, table, orderBy, pageIndex, pageSize, columns, isOr, transaction, commandTimeout);
        }
        private static IEnumerable<T> QueryPaged<T>(IDbConnection connection, T condition, string table, string orderBy, int pageIndex, int pageSize, string columns, bool isOr, IDbTransaction transaction, int? commandTimeout)
        {
            var conditionObj = condition as object;
            var whereFields = string.Empty;
            var properties = GetProperties(conditionObj);
            if (properties.Count > 0)
            {
                var separator = isOr ? " OR " : " AND ";
                whereFields = $" WHERE {string.Join(separator, properties.Select(p => p + "=@" + p))}";
            }
            var sql = $"SELECT {columns} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {orderBy}) AS RowNumber，{columns} FROM {table} {whereFields}) AS Total where RowNumber>{(pageIndex - 1) * pageSize} and RowNumber<={pageIndex * pageSize}";

            return connection.Query<T>(sql, conditionObj, transaction, true, commandTimeout);
        }
        private static Task<IEnumerable<T>> QueryPagedAsync<T>(IDbConnection connection, T condition, string table, string orderBy, int pageIndex, int pageSize, string columns, bool isOr, IDbTransaction transaction, int? commandTimeout)
        {
            var conditionObj = condition as object;
            var whereFields = string.Empty;
            var properties = GetProperties(conditionObj);
            if (properties.Count > 0)
            {
                var separator = isOr ? " OR " : " AND ";
                whereFields = $" WHERE {string.Join(separator, properties.Select(p => p + "=@" + p))}";
            }
            var sql = $"SELECT {columns} FROM (SELECT ROW_NUMBER() OVER (ORDER BY {orderBy}) AS RowNumber，{columns} FROM {table} {whereFields}) AS Total where RowNumber>{(pageIndex - 1) * pageSize} and RowNumber<={pageIndex * pageSize}";

            return connection.QueryAsync<T>(sql, conditionObj, transaction, commandTimeout);
        }
        private static string BuilderQuerySQL(dynamic condition, string table, string columns, bool isOr)
        {
            var conditionObj = condition as object;
            var properties = GetProperties(conditionObj);
            if (properties.Count==0)
            {
                return $"select {columns} from {table}";
            }
            var separator = isOr ? " or " : " and ";
            var wherePart = string.Join(separator, properties.Select(p => p + "=@" + p));

            return $"select {columns} from {table} where {wherePart}";
        }

        /// <summary>
        /// 获取属性名称
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static IList<string> GetProperties(object obj)
        {
            if (obj == null)
                return new List<string>();
            if((obj is DynamicParameters))
                return (obj as DynamicParameters).ParameterNames.ToList();
            return GetPropertyInfos(obj).Select(p => p.Name).ToList();
        }
        /// <summary>
        /// 获取属性信息
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static List<PropertyInfo> GetPropertyInfos(object obj)
        {
            if (obj == null)
                return new List<PropertyInfo>();

            List<PropertyInfo> properties;
            if (_paramCache.TryGetValue(obj.GetType(), out properties)) return properties;
            properties = obj.GetType().GetProperties(BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public).ToList();
            _paramCache[obj.GetType()] = properties;
            return properties;
        }
        
    }
}
