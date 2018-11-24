using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MemoizationSample
{
	class Program
	{
		static void Main(string[] args)
		{
			Func<int, int> fibonacci = null;
			fibonacci = Memoizer.Memoize<Func<int, int>>(x => x == 0 ? 0 : x == 1 ? 1 : fibonacci(x - 2) + fibonacci(x - 1));
			Console.WriteLine(fibonacci(30));
			Console.WriteLine(Memoizer.Cache);
		}
	}

	public static class Memoizer
	{
		static readonly ConcurrentDictionary<Delegate, object> m_Cache = new ConcurrentDictionary<Delegate, object>();

		public static ConcurrentDictionary<Delegate, object> Cache => m_Cache;

		public static TDelegate Memoize<TDelegate>(TDelegate body) where TDelegate : class
		{
			if (!typeof(Delegate).IsAssignableFrom(typeof(TDelegate)))
				throw new ArgumentException($"{nameof(TDelegate)} must be delegate.", nameof(TDelegate));
			var invokeMethod = typeof(TDelegate).GetMethod("Invoke");
			var parameters = invokeMethod.GetParameters().Select(x => Expression.Parameter(x.ParameterType)).ToArray();
			var arguments = CreateArguments(parameters);
			var lambda = Expression.Lambda<TDelegate>(
				Expression.Call(typeof(Memoizer), nameof(UpdateMemo), new[] { arguments.Type, invokeMethod.ReturnType },
					Expression.Constant(body),
					arguments,
					Expression.Lambda(Expression.Invoke(Expression.Constant(body), parameters))),
				"MemoizedCall", parameters);
			return lambda.Compile();
		}

		static NewExpression CreateArguments(IEnumerable<Expression> expressions)
		{
			var arguments = expressions.Take(7);
			if (!arguments.Any())
				return null;
			var restResult = CreateArguments(expressions.Skip(7));
			if (restResult != null)
				arguments = arguments.Concat(Enumerable.Repeat(restResult, 1));
			var typeArguments = arguments.Select(x => x.Type).ToArray();
			return Expression.New(Type.GetType("System.ValueTuple`" + typeArguments.Length).MakeGenericType(typeArguments).GetConstructor(typeArguments), arguments);
		}

		static TResult UpdateMemo<TArguments, TResult>(Delegate @delegate, TArguments arguments, Func<TResult> creator) => ((ConcurrentDictionary<TArguments, TResult>)m_Cache.GetOrAdd(@delegate, _ => new ConcurrentDictionary<TArguments, TResult>())).GetOrAdd(arguments, _ => creator());
	}
}
