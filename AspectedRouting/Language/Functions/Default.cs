using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Typ;
using Type = AspectedRouting.Language.Typ.Type;

namespace AspectedRouting.Language.Functions
{
    public class Default : Function
    {

        public override string Description { get; } = "Calculates function `f` for the given argument. If the result is `null`, the default value is returned instead";
        public override List<string> ArgNames { get; } = new List<string> { "defaultValue", "f" };

        private IExpression overwriteDefault = null;
        private bool overwriteDefaultValue = false;

        private static Var a = new Var("a");
        private static Var b = new Var("b");
        
        public Default() : base("default", true,
            new[]
            {
                Curry.ConstructFrom(a, a, new Curry(b, a), b)
            })
        {
        }

        private Default(IEnumerable<Type> types) : base("default", types)
        {
        }

        public override IExpression Specialize(IEnumerable<Type> allowedTypes)
        {
            var unified = Types.SpecializeTo(allowedTypes);
            if (unified == null)
            {
                return null;
            }

            return new Default(unified);
        }

        public void OverwriteAllDefaultValues(IExpression defaultValue)
        {

            Console.Error.WriteLine("THE DEFAULT FUNCTION HAS BEEN OVERWRITTEN! Be careful with this");
            overwriteDefaultValue = true;
            overwriteDefault = defaultValue;
        }

        public void ResetDefaultValue()
        {
            overwriteDefaultValue = false;
            overwriteDefault = null;
        }
        
        public override object Evaluate(Context c, params IExpression[] arguments)
        {
            var defaultValue = arguments[0];
            var func = arguments[1];
            var args = arguments.ToList().GetRange(2, arguments.Length - 2).ToArray();

            var calculated = func.Evaluate(c, args);
            if (calculated == null)
            {
                if (overwriteDefaultValue) {
                    return defaultValue.Evaluate(c);
                }
                return defaultValue.Evaluate(c);
            }

            return calculated;
        }
    }
}