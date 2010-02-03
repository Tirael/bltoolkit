﻿using System;
using System.Text;

namespace BLToolkit.Data.Sql.SqlProvider
{
	using DataProvider;

	public class SQLiteSqlProvider : BasicSqlProvider
	{
		public SQLiteSqlProvider(DataProviderBase dataProvider) : base(dataProvider)
		{
		}

		protected override string LimitFormat  { get { return "LIMIT {0}";  } }
		protected override string OffsetFormat { get { return "OFFSET {0}"; } }

		public override bool IsSkipSupported       { get { return SqlQuery.Select.TakeValue != null; } }
		public override bool IsNestedJoinSupported { get { return false; } }

		public override ISqlExpression ConvertExpression(ISqlExpression expr)
		{
			expr = base.ConvertExpression(expr);

			if (expr is SqlBinaryExpression)
			{
				SqlBinaryExpression be = (SqlBinaryExpression)expr;

				switch (be.Operation)
				{
					case "+": return be.SystemType == typeof(string)? new SqlBinaryExpression(be.SystemType, be.Expr1, "||", be.Expr2, be.Precedence): expr;
					case "^": // (a + b) - (a & b) * 2
						return Sub(
							Add(be.Expr1, be.Expr2, be.SystemType),
							Mul(new SqlBinaryExpression(be.SystemType, be.Expr1, "&", be.Expr2), 2), be.SystemType);
				}
			}
			else if (expr is SqlFunction)
			{
				SqlFunction func = (SqlFunction) expr;

				switch (func.Name)
				{
					case "Space"   : return new SqlFunction  (func.SystemType, "PadR", new SqlValue(" "), func.Parameters[0]);
					case "Convert" :
						{
							if (func.SystemType == typeof(bool))
							{
								ISqlExpression ex = AlternativeConvertToBoolean(func, 1);
								if (ex != null)
									return ex;
							}

							if (func.SystemType == typeof(DateTime) || func.SystemType == typeof(DateTimeOffset))
							{
								if (IsDateDataType(func.Parameters[0], "Date"))
									return new SqlFunction(func.SystemType, "Date", func.Parameters[1]);
								return new SqlFunction(func.SystemType, "DateTime", func.Parameters[1]);
							}

							return new SqlExpression(func.SystemType, "Cast({0} as {1})", Precedence.Primary, func.Parameters[1], func.Parameters[0]);
						}
				}
			}
			else if (expr is SqlExpression)
			{
				SqlExpression e = (SqlExpression)expr;

				if (e.Expr.StartsWith("Cast(StrFTime(Quarter"))
					return Inc(Div(Dec(new SqlExpression(e.SystemType, e.Expr.Replace("Cast(StrFTime(Quarter", "Cast(StrFTime('%m'"), e.Parameters)), 3));

				if (e.Expr.StartsWith("Cast(StrFTime('%w'"))
					return Inc(new SqlExpression(e.SystemType, e.Expr.Replace("Cast(StrFTime('%w'", "Cast(strFTime('%w'"), e.Parameters));

				if (e.Expr.StartsWith("Cast(StrFTime('%f'"))
					return new SqlExpression(e.SystemType, "Cast(strFTime('%f', {0}) * 1000 as int) % 1000", Precedence.Multiplicative, e.Parameters);

				if (e.Expr.StartsWith("DateTime"))
				{
					if (e.Expr.EndsWith("Quarter')"))
						return new SqlExpression(e.SystemType, "DateTime({1}, '{0} Month')", Precedence.Primary, Mul(e.Parameters[0], 3), e.Parameters[1]);

					if (e.Expr.EndsWith("Week')"))
						return new SqlExpression(e.SystemType, "DateTime({1}, '{0} Day')",   Precedence.Primary, Mul(e.Parameters[0], 7), e.Parameters[1]);
				}
			}

			return expr;
		}

		public override SqlQuery Finalize(SqlQuery sqlQuery)
		{
			sqlQuery = base.Finalize(sqlQuery);

			if (sqlQuery.IsDelete)
			{
				sqlQuery = GetAlternativeDelete(base.Finalize(sqlQuery));
				sqlQuery.From.Tables[0].Alias = "$";
			}
			else if (sqlQuery.IsUpdate)
			{
				sqlQuery = GetAlternativeUpdate(sqlQuery);
			}

			return sqlQuery;
		}

		protected override void BuildFromClause(StringBuilder sb)
		{
			if (!SqlQuery.IsUpdate)
				base.BuildFromClause(sb);
		}
	}
}
