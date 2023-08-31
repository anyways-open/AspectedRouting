using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.Language.Functions
{
    public class Min : Function
    {
        public override string Description { get; } =
            "Out of a list of values, gets the smallest value. In case of a list of bools, this acts as `and`. Note that 'null'-values are ignored. Returns `null` in case of an empty list (or 'no' in case of boolean comparison)";

        public override List<string> ArgNames { get; } = new List<string> { "list" };

        public Min() : base("min", true,
            new[]
            {
                new Curry(new ListType(Typs.Nat), Typs.Nat),
                new Curry(new ListType(Typs.Int), Typs.Int),
                new Curry(new ListType(Typs.PDouble), Typs.PDouble),
                new Curry(new ListType(Typs.Double), Typs.Double),
                new Curry(new ListType(Typs.Bool), Typs.Bool),
            })
        {
            Funcs.AddBuiltin(this, "and");
        }

        private Min(IEnumerable<Type> specializedTypes) : base("min",
            specializedTypes)
        {
        }

        public override IExpression Specialize(IEnumerable<Type> allowedTypes)
        {
            var unified = Types.SpecializeTo(allowedTypes);
            if (unified == null)
            {
                return null;
            }

            return new Min(unified);
        }


        public override object Evaluate(Context c, params IExpression[] arguments)
        {
            var ls = ((IEnumerable<object>)arguments[0].Evaluate(c)).Where(o => o != null);
            var expectedType = ((Curry)Types.First()).ResultType;

            switch (expectedType)
            {
                case BoolType _:
                    if (ls.Select(o => o.Equals("yes") || o.Equals("true")).All(b => b))
                    {
                        return "yes";
                    }

                    return "no";
                case DoubleType _:
                case PDoubleType _:

                    if (!ls.Any()) {
                        return null;
                    }

                    return ls.Select(o =>
                    {
                        while (o is IExpression e)
                        {
                            o = e.Evaluate(c);
                        }

                        return (double)o;
                    }).Min();
                default:
                    if (!ls.Any()) {
                        return null;
                    }
                    return ls.Select(o =>
                    {
                        while (o is IExpression e)
                        {
                            o = e.Evaluate(c);
                        }

                        return (int)o;
                    }).Min();
            }
        }
    }
}