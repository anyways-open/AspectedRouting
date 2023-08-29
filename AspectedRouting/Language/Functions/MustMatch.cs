using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.Language.Functions
{
    public class MustMatch : Function
    {
        public MustMatch() : base("must_match", true,
            new[] {
                // [String] -> (Tags -> [string]) -> Tags -> bool
                Curry.ConstructFrom(Typs.Bool, // Result type on top!
                    new ListType(Typs.String), // List of keys to check for
                    new Curry(Typs.Tags, new ListType(Typs.String)), // The function to execute on every key
                    Typs.Tags // The tags to apply this on
                )
            })
        {
            Funcs.AddBuiltin(this, "mustMatch");
        }

        private MustMatch(IEnumerable<Type> types) : base("must_match", types) { }

        public override string Description { get; } = Utils.Lines(
            "Checks that every specified key is present and gives a non-false value.\n",
            "If, on top, a value is present with a mapping, every key/value will be executed and must return a value that is not 'no' or 'false'. Note that 'null' is considered as true here too!",
            "Note that this is a privileged builtin function, as the parser will automatically inject the keys used in the called function.\n",
            "",
            "Usage example",
            "-------------",
            "",
            "`\'mustMatch\': { \"highway\": { \"proposed\": \"no\", \"undefined\":null }}`",
            "which will return 'yes' for {highway=residential}, {highway=living_street}, ..., but return 'no' for {highway=proposed}, but also for {some_other_key=xxx}",
            "Also note that {highway=undefined} will return (somewhat surprisingly) 'yes' too - as null-values are considered as true here too!");

        public override List<string> ArgNames { get; } = new List<string> {
            "neededKeys (do not specify in source code -added automatically by the parser)",
            "f"
        };

        public override IExpression Specialize(IEnumerable<Type> allowedTypes)
        {
            var unified = Types.SpecializeTo(allowedTypes);
            if (unified == null)
            {
                return null;
            }

            return new MustMatch(unified);
        }

        public override object Evaluate(Context c, params IExpression[] arguments)
        {
            var neededKeys = (IEnumerable<object>)arguments[0].Evaluate(c);
            var function = arguments[1];

            var tags = (Dictionary<string, string>)arguments[2].Evaluate(c);

            foreach (var oo in neededKeys)
            {
                var o = oo;
                while (o is IExpression e)
                {
                    o = e.Evaluate(c);
                }

                if (!(o is string tagKey))
                {
                    continue;
                }

                if (!tags.ContainsKey(tagKey))
                {
                    // A required key is missing
                    // Normally, we return no; but there is a second chance
                    // IF the mapping returns 'yes' on null, we make an exception and ignore it
                    var applied = function.Evaluate(c, new Constant(new Dictionary<string, string> {
                        {tagKey, ""}
                    }));
                    if (applied == null)
                    {
                        return "no";
                    }
                    if (applied.Equals("yes") || (applied is IEnumerable<object> l && l.Count() > 0 && l.ToList()[0].Equals("yes")))
                    {
                        continue; // We ignore the absence of the key
                    }
                    return "no";
                }
            }

            var result = (IEnumerable<object>)function.Evaluate(c, new Constant(tags));

            if (result.Any(o =>
                o == null ||
                o is string s && (s.Equals("no") || s.Equals("false"))))
            {
                // The mapped function is executed. If the mapped function gives 'no', null or 'false' for any value, "no" is returned
                return "no";
            }

            return "yes";

        }
    }
}