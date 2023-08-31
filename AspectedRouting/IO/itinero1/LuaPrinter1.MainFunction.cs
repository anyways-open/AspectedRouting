using System.Collections.Generic;
using System.Linq;
using AspectedRouting.IO.LuaSkeleton;
using AspectedRouting.IO.LuaSnippets;
using AspectedRouting.Language;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.IO.itinero1
{
    public partial class LuaPrinter1
    {


        private string GenerateMainProfileFunction()
        {

            var access = _skeleton.ToLuaWithTags(_profile.Access);
            var oneway = _skeleton.ToLuaWithTags(_profile.Oneway);
            var speed = _skeleton.ToLuaWithTags(_profile.Speed);

            var impl = string.Join("\n",
                "",
                "",
                "--[[",
                _profile.Name,
                "This is the main function called to calculate the access, oneway and speed.",
                "Comfort is calculated as well, based on the parameters which are padded in",
                "",
                "Created by " + _profile.Author,
                "]]",
                "function " + _profile.Name + "(parameters, tags, result)",
                "",
                "    -- initialize the result table on the default values",
                "    result.access = 0",
                "    result.speed = 0",
                "    result.factor = 1",
                "    result.direction = 0",
                "    result.canstop = true",
                "    result.attributes_to_keep = {}",
                "    tags._access = tags.access",
                "    tags._oneway = tags.oneway",
                "",
                "    local access = " + access,
                "    if (access == nil or access == \"no\" or access == false) then",
                "         return",
                "    end",
                "    tags.access = access",
                "    local oneway = " + oneway,
                "    tags.oneway = oneway",
                "    local speed = " + speed,
                "    tags.speed = speed",
                "    local distance = 1 -- the weight per meter for distance travelled is, well, 1m/m",
                "");

            impl +=
                "\n    local priority = 0\n        ";

            var tags = new LuaLiteral(Typs.Tags, "tags");
            foreach (var (parameterName, expression) in _profile.Priority)
            {
                var paramInLua = _skeleton.ToLua(new Parameter(parameterName));


                var expr = Funcs.Either(Funcs.Id, Funcs.Const, expression).Apply(tags)
                    .SpecializeToSmallestType()
                    .PruneTypes(t => !(t is Curry))
                    .Optimize(out _);

                if (expr.Types.Any(t => t.Name.Equals(Typs.Bool.Name)))
                {
                    expr = Funcs.Parse.Apply(expr).SpecializeToSmallestType();
                }
                var exprInLua = _skeleton.ToLua(expr);

                impl += "\n    " + string.Join("\n    ",
                            $"if({paramInLua} ~= 0) then",
                            $"    priority = priority + {paramInLua} * {exprInLua}",
                            "end"
                        );
            }

            var scalingFactor = Funcs.Default.Apply(new Constant(Typs.Double, 1.0), _profile.ScalingFactor, tags);

            impl += string.Join("\n",
                "  -- Calculate the scaling factor",
                "  local scalingfactor",
                Snippets.Convert(_skeleton, "scalingfactor", scalingFactor.SpecializeToSmallestType()),
                "",
                "priority = priority * scalingfactor",
                "",
                "",
                "    if (priority <= 0) then",
                "        result.access = 0",
                "        return",
                "    end",
                "",
                "    result.access = 1",
                "    result.speed = speed",
                "    result.factor = 1 / priority",
                "",
                "    result.direction = 0",
                "    if (oneway == \"with\" or oneway == \"yes\") then",
                "        result.direction = 1",
                "    elseif (oneway == \"against\" or oneway == \"-1\") then",
                "         result.direction = 2",
                "    end",
                "",
                "end"
            );

            return impl;
        }


        private (string functionName, string implementation) GenerateBehaviourFunction(
            string behaviourName,
            Dictionary<string, IExpression> behaviourParameters)
        {
            var referenceName = _profile.Name + "_" + behaviourName;
            var functionName = referenceName.AsLuaIdentifier();
            behaviourParameters.TryGetValue("description", out var description);

            _skeleton.AddDep("copy_tags");
            var usedkeys = _profile.AllExpressionsFor(behaviourName, _context)
                .PossibleTagsRecursive(_context)
                .Select(t => "\"" + t.Key + "\"")
                .ToHashSet();

            _skeleton.AddDep("remove_relation_prefix");
            var impl = string.Join("\n",
                "behaviour_" + functionName + "_used_keys = create_set({" + string.Join(", ", usedkeys) + "})",
                "--[[",
                description,
                "]]",
                "function behaviour_" + functionName + "(tags, result)",
                $"    tags = remove_relation_prefix(tags, \"{behaviourName.AsLuaIdentifier()}\")",
                "    local parameters = default_parameters()",
                "    parameters.name = \"" + referenceName + "\"",
                ""
            );

            impl += _parameterPrinter.DeclareParametersFor(behaviourParameters);

            impl += "    " + _profile.Name + "(parameters, tags, result)\n";
            impl += "    copy_tags(tags, result.attributes_to_keep, behaviour_" + functionName + "_used_keys)\n";
            impl += "end\n";
            return (functionName, impl);
        }
    }
}