using System;
using System.Collections.Generic;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Typ;
using Type = AspectedRouting.Language.Typ.Type;

namespace AspectedRouting.Language.Functions
{
    public class Inv : Function
    {
        public Inv() : base("inv", true, new[]
        {
            new Curry(Typs.PDouble, Typs.PDouble),
            new Curry(Typs.Double, Typs.Double)
        })
        {
        }

        public Inv(IEnumerable<Type> types) : base("inv", types)
        {
        }

        public override string Description { get; } = "Calculates `1/d`";
        public override List<string> ArgNames { get; } = new List<string> { "d" };

        public override object Evaluate(Context c, params IExpression[] arguments)
        {
            var arg = arguments[0].Evaluate(c);
            if (IsNumber(arg)) {
                return 1 / (double)arg;
            }

            throw new Exception("Invalid type: cannot divide by " + arg);
        }

        private static bool IsNumber(object value)
        {
            return value is sbyte
                   || value is byte
                   || value is short
                   || value is ushort
                   || value is int
                   || value is uint
                   || value is long
                   || value is ulong
                   || value is float
                   || value is double
                   || value is decimal;
        }

        public override IExpression Specialize(IEnumerable<Type> allowedTypes)
        {
            var unified = Types.SpecializeTo(allowedTypes);
            if (unified == null) {
                return null;
            }

            return new Inv(unified);
        }
    }
}