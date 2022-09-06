using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.IO.LuaSkeleton;
using AspectedRouting.Language;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.IO.itinero2
{
    public partial class LuaPrinter2
    {
        private string GenerateFactorFunction()
        {
            var parameters = new Dictionary<string, IExpression>();
            foreach (var (name, value) in _profile.DefaultParameters)
            {
                parameters[name] = value;
            }

            foreach (var (name, value) in _profile.Behaviours[_behaviourName])
            {
                parameters[name] = value;
            }


            var aspects = new List<string>();

            foreach (var (paramName, expr) in _profile.Priority)
            {
                var weightExpr = parameters[paramName].Evaluate(_context);
                if (!(weightExpr is double weight))
                {
                    continue;
                }

                if (weight == 0)
                {
                    continue;
                }

                // The expression might still have multiple typings,
                // which take inputs different from 'Tags', so we specialize the expr first
                var appliedExpr = Funcs.Either(Funcs.Id, Funcs.Const, expr)
                    .Apply(new LuaLiteral(Typs.Tags, "tags").SpecializeToSmallestType())
                    .PruneTypes(tp => !(tp is Curry));
                var exprSpecialized = appliedExpr.Optimize(out _);

                if (exprSpecialized.Types.First().Equals(Typs.Bool) || exprSpecialized.Types.First().Equals(Typs.String))
                {
                    _skeleton.AddDep("parse");
                    exprSpecialized = Funcs.Parse.Apply(exprSpecialized);
                }

                var exprInLua = _skeleton.ToLua(exprSpecialized);
                if (exprInLua.Contains("constRight") || exprInLua.Contains("firstArg"))
                {
                    throw new Exception("Not optimized properly:" + exprSpecialized.Repr());
                }
                aspects.Add(weight + " * " + exprInLua);
            }

            Console.WriteLine(aspects.Lined());
            var code = new List<string>()
            {
                "--[[",
                "Generates the factor according to the priorities and the parameters for this behaviour",
                "Note: 'result' is not actually used",
                "]]",
                "function calculate_priority(parameters, tags, result, access, oneway, speed)",
                "    local distance = 1",
                "    local priority = \n        " + string.Join(" +\n       ", aspects),
                "    return priority",
                "end"
            };
            return code.Lined();
        }

        private string GenerateTurnCostFunction()
        {
            var vehicleTypes = _profile.VehicleTyps;
            _skeleton.AddDep("containedIn");
            _skeleton.AddDep("str_split");
            _skeleton.AddDep("calculate_turn_cost_factor");
            
            var code = new List<string> {
                "--[[ Function called by itinero2 on every turn restriction relation"," ]]",
                "function turn_cost_factor(attributes, result)",
                "  result.factor = calculate_turn_cost_factor(attributes, vehicle_types)" ,
                "end",
                "",
            };
            return code.Lined();
        }
        
        private string GenerateMainFunction()
        {
            var parameters = _profile.Behaviours[_behaviourName];

            _skeleton.AddDependenciesFor(_profile.Access);
            _skeleton.AddDependenciesFor(_profile.Oneway);
            _skeleton.AddDependenciesFor(_profile.Speed);

            _skeleton.AddDep("eq");
            _skeleton.AddDep("remove_relation_prefix");
            _skeleton.AddDep("debug_table");
            var code = new List<string>
            {
                "--[[",
                "Calculate the actual factor.forward and factor.backward for a segment with the given properties",
                "]]",
                "function factor(tags, result)",
                "    ",
                "    -- Cleanup the relation tags to make them usable with this profile",
                $"    tags = remove_relation_prefix(tags, \"{_behaviourName}\")",
                "    ",
                "    -- initialize the result table on the default values",
                "    result.forward_speed = 0",
                "    result.backward_speed = 0",
                "    result.forward = 0",
                "    result.backward = 0",
                "    result.canstop = true",
                "    result.attributes_to_keep = {} -- not actually used anymore, but the code generation still uses this",

                "",
                "",
                "    local parameters = default_parameters()",
                _parameterPrinter.DeclareParametersFor(parameters),
                "",
                "    local oneway = " + _skeleton.ToLuaWithTags(_profile.Oneway).Indent(),
                "    tags.oneway = oneway",
                "    -- An aspect describing oneway should give either 'both', 'against' or 'width'",

                "",
                "",
                "    -- forward calculation. We set the meta tag '_direction' to 'width' to indicate that we are going forward. The other functions will pick this up",
                "    tags[\"_direction\"] = \"with\"",
                "    local access_forward = " + _skeleton.ToLuaWithTags(_profile.Access).Indent(),
                "    if(oneway == \"against\") then",
                "        -- no 'oneway=both' or 'oneway=with', so we can only go back over this segment",
                "        -- we overwrite the 'access_forward'-value with no; whatever it was...",
                "        access_forward = \"no\"",
                "    end",
                "    if(access_forward ~= nil and access_forward ~= \"no\" and access_forward ~= false) then",
                "        tags.access = access_forward -- might be relevant, e.g. for 'access=dismount' for bicycles",
                "        result.forward_speed = " + _skeleton.ToLuaWithTags(_profile.Speed).Indent(),
                "        tags.speed = result.forward_speed",
                "        local priority = calculate_priority(parameters, tags, result, access_forward, oneway, result.forward_speed)",
                "        if (priority <= 0) then",
                "            result.forward_speed = 0",
                "        else",
                "            result.forward = 1 / priority",
                "         end",
                "    end",
                "",
                "    -- backward calculation",
                "    tags[\"_direction\"] = \"against\" -- indicate the backward direction to priority calculation",
                "    local access_backward = " + _skeleton.ToLuaWithTags(_profile.Access).Indent(),
                "    if(oneway == \"with\") then",
                "        -- no 'oneway=both' or 'oneway=against', so we can only go forward over this segment",
                "        -- we overwrite the 'access_forward'-value with no; whatever it was...",
                "        access_backward = \"no\"",
                "    end",
                "    if(access_backward ~= nil and access_backward ~= \"no\" and access_backward ~= false) then",
                "        tags.access = access_backward",
                "        result.backward_speed = " + _skeleton.ToLuaWithTags(_profile.Speed).Indent(),
                "        tags.speed = result.backward_speed",
                "        local priority = calculate_priority(parameters, tags, result, access_backward, oneway, result.backward_speed)",
                "        if (priority <= 0) then",
                "            result.backward_speed = 0",
                "        else",
                "            result.backward = 1 / priority",
                "         end",
                "    end",

                "end"
            };

            return code.Lined();
        }
    }
}