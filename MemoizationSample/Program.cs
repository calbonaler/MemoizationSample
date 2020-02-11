using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace MemoizationSample
{
	class Program
	{
		static void Main()
		{
			Func<int, int> fibonacci = null!;
			fibonacci = Memoizer.Memoize<Func<int, int>>(x => x == 0 ? 0 : x == 1 ? 1 : fibonacci(x - 2) + fibonacci(x - 1));
			Console.WriteLine(fibonacci(30));
			Console.WriteLine(Memoizer.Cache);
		}
	}

	public static class Memoizer
	{
		public static ConcurrentDictionary<Delegate, object> Cache { get; } = new ConcurrentDictionary<Delegate, object>();

		public static TDelegate Memoize<TDelegate>(TDelegate body) where TDelegate : Delegate
		{
			var invokeMethod = typeof(TDelegate).GetMethod("Invoke");
			var parameters = invokeMethod!.GetParameters().Select(x => Expression.Parameter(x.ParameterType)).ToArray();
			var arguments = CreateArgumentsInternal(parameters);
			var lambda = Expression.Lambda<TDelegate>(
				Expression.Call(typeof(Memoizer), nameof(UpdateMemo), new[] { arguments.Type, invokeMethod.ReturnType },
					Expression.Constant(body),
					arguments,
					Expression.Lambda(Expression.Invoke(Expression.Constant(body), parameters))),
				"MemoizedCall", parameters);
			return lambda.Compile();
		}

		static NewExpression CreateArgumentsInternal(IEnumerable<Expression> expressions)
		{
			var arguments = expressions.Take(7);
			if (!arguments.Any())
				return Expression.New(typeof(ValueTuple));
			var restResult = CreateArgumentsInternal(expressions.Skip(7));
			if (restResult.Type != typeof(ValueTuple))
				arguments = arguments.Concat(Enumerable.Repeat(restResult, 1));
			var typeArguments = arguments.Select(x => x.Type).ToArray();
			return Expression.New(Type.GetType("System.ValueTuple`" + typeArguments.Length)!.MakeGenericType(typeArguments).GetConstructor(typeArguments), arguments);
		}

		static TResult UpdateMemo<TArguments, TResult>(Delegate @delegate, TArguments arguments, Func<TResult> creator) where TArguments: notnull => ((ConcurrentDictionary<TArguments, TResult>)Cache.GetOrAdd(@delegate, _ => new ConcurrentDictionary<TArguments, TResult>())).GetOrAdd(arguments, _ => creator());
	}
}
