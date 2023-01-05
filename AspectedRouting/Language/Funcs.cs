using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;
using Type = AspectedRouting.Language.Typ.Type;

// ReSharper disable UnusedMember.Global

namespace AspectedRouting.Language
{
    public static class Funcs
    {
        public static readonly Dictionary<string, Function> Builtins = new Dictionary<string, Function>();
        public static IEnumerable<string> BuiltinNames = Builtins.Keys;


        public static readonly Function Eq = new Eq();
        public static readonly Function NotEq = new NotEq();
        public static readonly Function Inv = new Inv();
        public static readonly Function IsNull = new IsNull();

        public static readonly Default Default = new Default();

        public static readonly Function Parse = new Parse();
        public static readonly Function ToStringFunc = new ToString();
        public static readonly Function Concat = new Concat();

        public static readonly Function ContainedIn = new ContainedIn();
        public static readonly Function Min = new Min();
        public static readonly Function Max = new Max();
        public static readonly Function Sum = new Sum();
        public static readonly Function Multiply = new Multiply();


        public static readonly Function AtLeast = new AtLeast();
        public static readonly Function FirstOf = new FirstMatchOf();
        public static readonly Function MustMatch = new MustMatch();

        public static readonly Function MemberOf = new MemberOf();


        public static readonly Function If = new If();
        public static readonly Function IfDotted = new IfDotted();

        public static readonly Function Id = new Id();
        public static readonly Function Const = new Const();
        public static readonly Function ConstRight = new ConstRight();
        public static readonly Function Dot = new Dot();
        public static readonly Function ListDot = new ListDot();
        public static readonly Function EitherFunc = new EitherFunc();

        public static readonly Function StringStringToTags = new StringStringToTagsFunction();
        public static readonly Function Head = new HeadFunction();

        public static void AddBuiltin(Function f, string name = null)
        {
            Builtins.Add(name ?? f.Name, f);
        }

        public static IExpression Either(IExpression a, IExpression b, IExpression arg)
        {
            return EitherFunc.Apply(a, b, arg);
        }

        public static Function BuiltinByName(string funcName)
        {
            if (funcName.StartsWith("$"))
            {
                funcName = funcName.Substring(1);
            }

            if (Builtins.TryGetValue(funcName, out var f)) return f;
            return null;
        }

        public static IExpression Finalize(this IExpression e)
        {
            if (e == null)
            {
                return null;
            }

            if (e.Types.Count() == 0)
            {
                throw new Exception("Expression has no valid types:\n" + e);
            }

            var eSmallest = e.SpecializeToSmallestType();
            if (eSmallest == null || !eSmallest.Types.Any())
            {
                throw new Exception("Could not specialize " + e);
            }

            // An argument 'optimizes' it's types from 'string -> bool' to 'string -> string'
            var eOpt = eSmallest.Optimize(out _);
            if (eOpt == null || !eOpt.Types.Any())
            {
                throw new Exception("Could not optimize " + eSmallest);
            }

            eOpt.SanityCheck();
            return eOpt;
        }

        public static IExpression SpecializeToSmallestType(this IExpression e)
        {
            if (e == null)
            {
                throw new Exception("Cannot specialize null to a smallest type");
            }
            if (e.Types.Count() == 1)
            {
                return e;
            }

            Type smallest = null;
            foreach (var t in e.Types)
            {
                if (smallest == null)
                {
                    smallest = t;
                    continue;
                }

                var smallestIsSuperset = smallest.IsSuperSet(t);
                if (!t.IsSuperSet(smallest) && !smallestIsSuperset)
                {
                    // Neither one is smaller then the other, we can not compare them
                    return e;
                }

                if (smallestIsSuperset)
                {
                    smallest = t;
                }
            }

            return e.Specialize(new[] { smallest });
        }

        public static IExpression Apply(this IExpression function, IEnumerable<IExpression> args)
        {
            return function.Apply(args.ToList());
        }

        public static IExpression Apply(this IExpression function, params IExpression[] args)
        {
            return function.Apply(args.ToList());
        }

        public static IExpression Apply(this IExpression function, List<IExpression> args)
        {
            if (args.Count == 0)
            {
                return function;
            }

            if (args.Count == 1)
            {
                return new Apply(function, args[0]);
            }

            var lastArg = args[args.Count - 1];
            var firstArgs = args.GetRange(0, args.Count - 1);
            var applied = Apply(function, firstArgs);
            return new Apply(applied, lastArg);
        }
    }
}