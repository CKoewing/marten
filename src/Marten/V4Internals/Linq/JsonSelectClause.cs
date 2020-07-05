using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LamarCodeGeneration.Util;
using Marten.Linq;
using Marten.Linq.QueryHandlers;
using Marten.Util;

namespace Marten.V4Internals.Linq
{
    public class JsonSelectClause: ISelectClause
    {
        private readonly Type _sourceType;

        public JsonSelectClause(ISelectClause parent)
        {
            _sourceType = parent.SelectedType;
            FromObject = parent.FromObject;
        }

        public Type SelectedType => typeof(string);

        public string FromObject { get; }

        public string SelectionText { get; set; } = "select d.data from ";


        public void WriteSelectClause(CommandBuilder sql)
        {
            sql.Append(SelectionText);
            sql.Append(FromObject);
            sql.Append(" as d");
        }

        public string[] SelectFields()
        {
            return new string[] {"data"};
        }

        public ISelector BuildSelector(IMartenSession session)
        {
            return LinqConstants.StringValueSelector;
        }

        public IQueryHandler<T> BuildHandler<T>(IMartenSession session, Statement topStatement,
            Statement currentStatement)
        {
            if (currentStatement.Limit == 1)
            {
                return (IQueryHandler<T>)new QueryHandlers.OneResultHandler<string>(topStatement, LinqConstants.StringValueSelector, true, false);
            }

            return (IQueryHandler<T>) new JsonArrayHandler(topStatement, _sourceType);
        }

        public ISelectClause UseStatistics(QueryStatistics statistics)
        {
            throw new System.NotSupportedException("Marten does not yet support the usage of QueryStatistics combined with JSON queries");
        }
    }

    public class JsonArrayHandler: IQueryHandler<string>
    {
        private readonly Statement _statement;
        private string _arrayPrefix;
        private string _arraySuffix;

        public JsonArrayHandler(Statement statement, Type sourceType)
        {
            _statement = statement;
            if (sourceType.IsSimple())
            {
                _arrayPrefix = "[{";
                _arraySuffix = "}]";
            }
            else
            {
                _arrayPrefix = "[";
                _arraySuffix = "]";
            }
        }

        public void ConfigureCommand(CommandBuilder builder, IMartenSession session)
        {
            _statement.Configure(builder);
        }

        public string Handle(DbDataReader reader, IMartenSession session)
        {
            // TODO -- figure out better, more efficient ways to do this
            var builder = new StringWriter();

            builder.Write(_arrayPrefix);

            if (reader.Read())
            {
                using var text = reader.GetTextReader(0);
                builder.Write(text.ReadToEnd());
            }

            while (reader.Read())
            {
                using var text = reader.GetTextReader(0);
                builder.Write(',');
                builder.Write(text.ReadToEnd());
            }

            builder.Write(_arraySuffix);

            return builder.ToString();
        }

        public async Task<string> HandleAsync(DbDataReader reader, IMartenSession session, CancellationToken token)
        {
            // TODO -- figure out better, more efficient ways to do this
            var builder = new StringWriter();

            builder.Write(_arrayPrefix);

            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                using var text = reader.GetTextReader(0);
                builder.Write(await text.ReadToEndAsync().ConfigureAwait(false));
            }

            while (await reader.ReadAsync(token))
            {
                using var text = reader.GetTextReader(0);
                builder.Write(',');
                builder.Write(await text.ReadToEndAsync().ConfigureAwait(false));
            }

            builder.Write(_arraySuffix);

            return builder.ToString();
        }
    }
}
