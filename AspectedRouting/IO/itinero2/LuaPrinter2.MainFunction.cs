using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AspectedRouting.IO.LuaSkeleton;
using AspectedRouting.IO.LuaSnippets;
using AspectedRouting.Language;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.IO.itinero2
{
    public partial class LuaPrinter2
    {
        private string GenerateFactorFunction()
        {
            var parameters = new Dictionary<string, IExpression>();
            foreach (var (name, value) in _profile.DefaultParameters) parameters[name] = value;

            foreach (var (name, value) in _profile.Behaviours[_behaviourName]) parameters[name] = value;


            var aspects = new List<string>();
            var tags = new LuaLiteral(Typs.Tags, "tags");
            foreach (var (paramName, expr) in _profile.Priority)
            {
                var weightExpr = parameters[paramName].Evaluate(_context);
                if (!(weightExpr is double weight)) continue;

                if (weight == 0) continue;

                // The expression might still have multiple typings,
                // which take inputs different from 'Tags', so we specialize the expr first
                var appliedExpr = Funcs.Either(Funcs.Id, Funcs.Const, expr)
                    .Apply(tags)
                    .PruneTypes(tp => !(tp is Curry));
                var exprSpecialized = appliedExpr.Optimize(out _);

                if (exprSpecialized.Types.First().Equals(Typs.Bool) ||
                    exprSpecialized.Types.First().Equals(Typs.String))
                {
                    _skeleton.AddDep("parse");
                    exprSpecialized = Funcs.Parse.Apply(exprSpecialized);
                }

                var exprInLua = _skeleton.ToLua(exprSpecialized);
                if (exprInLua.Contains("constRight") || exprInLua.Contains("firstArg"))
                    throw new Exception("Not optimized properly:" + exprSpecialized.Repr());
                aspects.Add(weight.ToString(CultureInfo.InvariantCulture) + " * " + exprInLua.ToString(CultureInfo.InvariantCulture));
            }

            var scalingFactor = Funcs.Default.Apply(new Constant(Typs.Double, 1.0), _profile.ScalingFactor, tags)
                .SpecializeToSmallestType();
            var code = new List<string>
            {
                "--[[",
                "Generates the factor according to the priorities and the parameters for this behaviour",
                "Note: 'result' is not actually used",
                "]]",
                "function calculate_priority(parameters, tags, result, access, oneway, speed)",
                "    local distance = 1",
                "    local priority = \n        " + string.Join(" +\n       ", aspects),
                "",
                "local scalingfactor",
                Snippets.Convert(_skeleton, "scalingfactor", scalingFactor),
                "    return priority * scalingfactor",
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


            /**
             *  Calculates the turn cost factor for relation attributes or obstacles.
 Keep in mind that there are no true relations in the routerDB anymore, instead the attributes are copied onto a turn cost object.
 This turn cost object has a set of sequence of edges and is applied onto the vertex.0
 
 Obstacles such as bollards are converted into a turn cost as well.
 calculate_turn_cost_factor will be called for this bollard too to calculate the weight.

If result.factor = -1 if passing is not possible - this is more or less equal to an infinite cost
If result.factor = 0 if no weight/passing is possible
If result.factor is positive, that is the cost.

There is no forward or backward, so this should always be the same for the same attributes
             */
            var parameters = _profile.Behaviours[_behaviourName];
            var tags = new LuaLiteral(Typs.Tags, "attributes");
            var hasAccess = _profile.ObstacleAccess.Apply(tags).SpecializeToSmallestType().Optimize(out _);
            var code = new List<string>
            {
                "--[[ Function called by itinero2 on every turn restriction relation", " ]]",
                "function turn_cost_factor(attributes, result)",
                "    local parameters = default_parameters()",
                _parameterPrinter.DeclareParametersFor(parameters),
                "local has_access",
                Snippets.Convert(_skeleton, "has_access", hasAccess),
                "if ( has_access == \"no\" or has_access == \"false\") then",
                "    result.factor = -1",
                "else",
                Snippets.Convert(_skeleton, "result.factor",  _profile.ObstacleCost.Apply(tags).SpecializeToSmallestType().Optimize(out _)),
                "end",

                "  -- not known by the profile or invalid value - use the default implementation",
                "  if (result.factor == nil) then",
                "    result.factor = calculate_turn_cost_factor(attributes, vehicle_types)",
                "  end",
                "end",
                ""
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
                "    tags._oneway = tags.oneway",
                "    tags._access = tags.access",
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
                "        -- we overwrite the 'access_backward'-value with no; whatever it was...",
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