using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;
using Type = AspectedRouting.Language.Typ.Type;

namespace AspectedRouting.IO.jsonParser
{
    public static partial class JsonParser
    {
        public static AspectMetadata AspectFromJson(Context c, string json, string fileName)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("defaults", out _))
                {
                    // this is a profile
                    return null;
                }

                Console.Write("Parsing " + fileName + "... ");

                var aspect = doc.RootElement.ParseAspect(fileName, c);

                Console.WriteLine($"\rAspect {aspect.Name} has type {string.Join(",", aspect.ExpressionImplementation.Types)}");
                return aspect;
            }
            catch (Exception e)
            {
                throw new Exception("In the file " + fileName, e);
            }
        }

        public static ProfileMetaData ProfileFromJson(Context c, string json, FileInfo f, DateTime lastChange)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("defaults", out _))
                {
                    return null;
                    // this is an aspect
                }

                return ParseProfile(doc.RootElement, c, f, lastChange);
            }
            catch (Exception e)
            {
                throw new Exception("In the file " + f, e);
            }
        }

        /**
         * Mostly used in the unit tests
         */
        [Obsolete]
        public static IExpression ParseExpression(Context c, string json)
        {
            return JsonDocument.Parse(json).RootElement.ParseExpression(c);
        }


        private static ProfileMetaData ParseProfile(this JsonElement e, Context context, FileInfo filepath, DateTime lastChange)
        {
            if (!e.TryGetProperty("speed", out _))
            {
                // not a profile
                return null;
            }

            var name = e.Get("name");
            var author = e.TryGet("author");
            if (filepath != null && !(name + ".json").ToLower().Equals(filepath.Name.ToLower()))
            {
                throw new ArgumentException($"Filename does not match the defined name: " +
                                            $"filename is {filepath.Name}, declared name is {name}");
            }

            JsonElement GetTopProperty(string name)
            {
                if (e.TryGetProperty(name, out var p))
                {
                    return p;
                }

                throw new ArgumentException(
                    filepath + " is not a valid profile; it does not contain the obligated parameter " + name);
            }

            var defaultParameters = e.GetProperty("defaults").ParseParameters();

            var contextWithParameters = new Context(context);
            foreach (var (paramName, paramExpression) in defaultParameters)
            {
                if (!context.Parameters.TryGetValue(paramName, out var previousParam))
                {
                    contextWithParameters.AddParameter(paramName, paramExpression);
                }
            }

            var vehicleTypes = GetTopProperty("vehicletypes").EnumerateArray().Select(
                el => el.GetString()).ToList();
            var metadata = GetTopProperty("metadata").EnumerateArray().Select(
                el => el.GetString()).ToList();


            var access = ParseProfileProperty(e, contextWithParameters, "access").Finalize();
            var oneway = ParseProfileProperty(e, contextWithParameters, "oneway").Finalize();
            var speed = ParseProfileProperty(e, contextWithParameters, "speed").Finalize();
            var scalingFactor = ParseProfileProperty(e, contextWithParameters, "scalingfactor", Funcs.Const.Apply(new Constant(1))).Finalize();
            var obstacle_access = ParseProfileProperty(e, contextWithParameters, "obstacleaccess", Funcs.Const.Apply(new Constant(new Var("any"), null))).Finalize();
            var obstacle_cost = ParseProfileProperty(e, contextWithParameters, "obstaclecost", Funcs.Const.Apply(new Constant(new Var("any0"), null))).Finalize();

            IExpression TagsApplied(IExpression x)
            {
                return new Apply(x, new Constant(new Dictionary<string, string>()));
            }

            context.AddFunction("speed",
                new AspectMetadata(TagsApplied(speed), "speed", "The speed of this profile", author, "", filepath.Name,
                    true));
            context.AddFunction("access",
                new AspectMetadata(TagsApplied(access), "access", "The access of this profile", author, "",
                    filepath.Name,
                    true));
            context.AddFunction("oneway",
                new AspectMetadata(TagsApplied(oneway), "oneway", "The oneway of this profile", author, "",
                    filepath.Name,
                    true));
            context.AddFunction("distance",
                new AspectMetadata(new Constant(1), "distance", "The distance travelled of this profile", author, "",
                    filepath.Name,
                    true));


            var weights = new Dictionary<string, IExpression>();

            var weightProperty = GetTopProperty("priority");
            foreach (var prop in weightProperty.EnumerateObject())
            {
                var parameter = prop.Name.TrimStart('#');
                var factor = ParseExpression(prop.Value, context).Finalize();
                weights[parameter] = factor;
            }

            var profiles = new Dictionary<string, Dictionary<string, IExpression>>();

            var behaviours = GetTopProperty("behaviours");
            foreach (var profile in behaviours.EnumerateObject())
            {
                profiles[profile.Name] = ParseParameters(profile.Value);
            }

            if (lastChange < filepath.LastWriteTimeUtc)
            {
                lastChange = filepath.LastWriteTimeUtc;
            }

            return new ProfileMetaData(
                name,
                e.Get("description"),
                author,
                filepath?.DirectoryName ?? "unknown",
                vehicleTypes,
                defaultParameters,
                profiles,
                access,
                oneway,
                speed,
                obstacle_access, obstacle_cost,
                weights,
                scalingFactor,
                metadata,
                lastChange
            );
        }

        private static readonly IExpression _mconst = Funcs.EitherFunc.Apply(Funcs.Id, Funcs.Const);

        private static readonly IExpression _mappingWrapper = Funcs.EitherFunc.Apply(Funcs.Id, Funcs.StringStringToTags);

        private static IExpression ParseMapping(IEnumerable<JsonProperty> allArgs, Context context)
        {
            var keys = new List<string>();
            var exprs = new List<IExpression>();

            foreach (var prop in allArgs)
            {
                if (prop.Name.Equals("#"))
                {
                    continue;
                }

                keys.Add(prop.Name);
                var argExpr = ParseExpression(prop.Value, context);
                IExpression mappingWithOptArg;
                if (argExpr.Types.Count() == 1 && argExpr.Types.First().Equals(Typs.String))
                {
                    mappingWithOptArg =
                        Funcs.Either(Funcs.Id, Funcs.Eq, argExpr);
                }
                else
                {
                    mappingWithOptArg = new Apply(_mconst, argExpr);
                }

                exprs.Add(mappingWithOptArg);
            }

            try
            {
                var simpleMapping = new Mapping(keys, exprs);

                var wrapped = _mappingWrapper.Apply(simpleMapping);
                if (keys.Count == 1)
                {
                    // We can interpret this directly without going through a list

                    wrapped = Funcs.Either(Funcs.Id,
                        new Apply(Funcs.Dot, Funcs.Head),
                        wrapped);
                }

                return wrapped;
            }
            catch (Exception e)
            {
                throw new Exception("While constructing a mapping with members " + string.Join(", ", exprs) +
                                    ": " + e.Message, e);
            }
        }

        private static IExpression ParseExpression(this JsonElement e, Context context)
        {
            if (e.ValueKind == JsonValueKind.Object)
            {
                // Parse an actual function
                var funcCall = e.EnumerateObject().Where(v => v.Name.StartsWith("$")).ToList();
                var allArgs = e.EnumerateObject().Where(v => !v.Name.StartsWith("$")).ToList();

                if (funcCall.Count > 2)
                {
                    throw new ArgumentException("Multiple calls defined in object " + e);
                }

                if (funcCall.Count == 1)
                {
                    return ParseFunctionCall(context, funcCall, allArgs);
                }

                // Funccall has no elements: this is a mapping of strings or tags onto a value

                return ParseMapping(allArgs, context);
            }

            if (e.ValueKind == JsonValueKind.Array)
            {
                var exprs = e.EnumerateArray().Select(json =>
                        Funcs.Either(Funcs.Id, Funcs.Const, json.ParseExpression(context)))
                    .ToList();

                var list = new Constant(exprs);
                return Funcs.Either(Funcs.Id, Funcs.ListDot, list);
            }

            if (e.ValueKind == JsonValueKind.Number)
            {
                if (e.TryGetDouble(out var d))
                {
                    return new Constant(d);
                }

                if (e.TryGetInt32(out var i))
                {
                    return new Constant(i);
                }
            }

            if (e.ValueKind == JsonValueKind.True)
            {
                return new Constant(Typs.Bool, "yes");
            }

            if (e.ValueKind == JsonValueKind.False)
            {
                return new Constant(Typs.Bool, "no");
            }

            if (e.ValueKind == JsonValueKind.String)
            {
                var s = e.GetString();
                if (s.StartsWith("$"))
                {
                    var bi = Funcs.BuiltinByName(s);

                    if (bi != null)
                    {
                        return Funcs.Either(Funcs.Dot, Funcs.Id, bi);
                    }

                    var definedFunc = context.GetFunction(s);
                    return Funcs.Either(Funcs.Dot, Funcs.Id, new FunctionCall(s, definedFunc.Types));
                }

                if (s.StartsWith("#"))
                {
                    // This is a parameter, the type of it is free
                    if (context.Parameters.TryGetValue(s.Substring(1), out var param))
                    {
                        return new Parameter(s).Specialize(param.Types);

                    }

                    return new Parameter(s);
                }

                return new Constant(s);
            }

            if (e.ValueKind == JsonValueKind.Null)
            {
                return new Constant(new Var("a"), null);
            }

            throw new Exception("Could not parse " + e);
        }

        private static IExpression ParseFunctionCall(Context context, IReadOnlyCollection<JsonProperty> funcCall,
            IEnumerable<JsonProperty> allArgs)
        {
            var funcName = funcCall.First().Name;

            var func = Funcs.BuiltinByName(funcName);

            if (func == null)
            {
                throw new ArgumentException($"The function with name {funcName} is not found");
            }

            // The list where all the arguments are collected
            var args = new List<IExpression>();


            // First argument of the function is the value of this property, e.g.
            // { "$f": "xxx", "a2":"yyy", "a3":"zzz" }
            var firstArgument = ParseExpression(funcCall.First().Value, context);


            // Cheat for the very special case 'mustMatch'
            if (func.Equals(Funcs.MustMatch))
            {
                // It gets an extra argument injected
                var neededKeys = firstArgument.PossibleTags().Keys.ToList();
                var neededKeysArg = new Constant(new ListType(Typs.String), neededKeys);
                args.Add(neededKeysArg);
                args.Add(firstArgument);
                return func.Apply(args);
            }


            args.Add(firstArgument);

            var allExprs = allArgs
                .Where(kv => !kv.NameEquals("#")) // Leave out comments
                .ToDictionary(kv => kv.Name, kv => kv.Value.ParseExpression(context));


            if (allExprs.Count > 1)
            {
                if (func.ArgNames == null || func.ArgNames.Count < 2)
                    throw new ArgumentException("{funcName} does not specify argument names");


                foreach (var argName in func.ArgNames.GetRange(1, func.ArgNames.Count - 1))
                // We skip the first argument, that one is already added
                {
                    args.Add(allExprs[argName]);
                }
            }
            else if (allExprs.Count == 1)
            {
                args.Add(allExprs.Single().Value);
            }

      
            var either = Funcs.Either(Funcs.Id, Funcs.Dot, func).Apply(args);
            if (either != null && either.Types.Any()) {
                return either;
            }

            // Attempt a rescue: apply the dot differently
            var lastArg = args.Last();
            var firstArgs = args.GetRange(0, args.Count - 1);
            var eitherFixed = Funcs.Either(Funcs.Id, Funcs.Dot, func.Apply(firstArgs)).Apply(lastArg);

            if (eitherFixed == null || !eitherFixed.Types.Any()) {
                throw new ArgumentException(
                    $"Could not type function application of \n\t{func.TypeBreakdown()}\nand arguments {string.Join("\n\n\t", args.Select(a => a.TypeBreakdown()))}");
            }

            return eitherFixed;
        }


        private static IExpression GetTopLevelExpression(this JsonElement root, Context context)
        {
            IExpression mapping = null;
            if (root.TryGetProperty("value", out var j))
            {
                // The expression is placed in the default 'value' location
                mapping = j.ParseExpression(context);
            }


            // We search for the function call with '$'
            foreach (var prop in root.EnumerateObject())
            {
                if (!prop.Name.StartsWith("$")) continue;


                var f = (IExpression)Funcs.BuiltinByName(prop.Name);
                if (f == null || f.Types.Count() == 0)
                {
                    throw new KeyNotFoundException($"The builtin function {prop.Name} was not found");
                }

                var fArg = prop.Value.ParseExpression(context);

                if (fArg == null || fArg.Types.Count() == 0)
                {
                    throw new ArgumentException("Could not type expression " + prop);
                }


                if (mapping != null)
                {
                    // This is probably a firstOrderedVersion, a default, or some other function that should be applied
                    return
                        new Apply(
                            Funcs.Either(Funcs.Id, Funcs.Dot, new Apply(f, fArg)), mapping
                        );
                }

                // Cheat for the very special case 'mustMatch'
                if (f.Equals(Funcs.MustMatch))
                {
                    // It gets an extra argument injected
                    var neededKeys = fArg.PossibleTags().Keys.ToList();
                    var neededKeysArg = new Constant(new ListType(Typs.String), neededKeys);
                    f = f.Apply(new[] { neededKeysArg });
                }

                var appliedDot =Funcs.Dot.Apply( f).Apply( fArg);
                var appliedDirect = new Apply(f, fArg);

                if (!appliedDot.Types.Any())
                {
                    // Applied dot doesn't work out, so we return the other one
                    return appliedDirect;
                }

                if (!appliedDirect.Types.Any())
                {
                    return appliedDot;
                }

                var eithered = new Apply(new Apply(Funcs.EitherFunc, appliedDot), appliedDirect);


                // We apply the builtin function through a dot
                return eithered;
            }

            if (mapping != null)
            {
                return mapping;
            }


            throw new Exception(
                "No top level reducer found. Did you forget the '$' in the reducing function? Did your forget 'value' to add the mapping?");
        }

        private static AspectMetadata ParseAspect(this JsonElement e, string filepath, Context context)
        {
            var expr = GetTopLevelExpression(e, context);

            var targetTypes = new List<Type>();
            foreach (var t in expr.Types)
            {
                var a = Var.Fresh(t);
                var b = Var.Fresh(new Curry(a, t));

                if (t.Unify(new Curry(Typs.Tags, a)) != null &&
                    t.Unify(new Curry(Typs.Tags, new Curry(a, b))) == null
                ) // Second should not  match
                {
                    // The target type is 'Tags -> a', where a is NOT a curry
                    targetTypes.Add(t);
                }
            }

            if (targetTypes.Count == 0)
            {
                throw new ArgumentException("The top level expression has types:\n" +
                                            string.Join("\n    ", expr.Types) +
                                            "\nwhich can not be specialized into a form suiting `tags -> a`\n" + expr);
            }

            var exprSpec = expr.Specialize(targetTypes);
            if (expr == null)
            {
                throw new Exception("Could not specialize the expression " + expr + " to one of the target types " +
                                    string.Join(", ", targetTypes));
            }

            expr = exprSpec.Finalize();

            if (expr.Finalize() == null)
            {
                throw new NullReferenceException("The finalized form of expression `" + exprSpec + "` is null");
            }

            expr = expr.SpecializeToSmallestType();


            var name = e.Get("name");
            if (expr.Types.Count() > 1)
            {
                throw new ArgumentException("The aspect " + name + " is ambigous, it matches multiple types: " +
                                            string.Join(", ", expr.Types));
            }

            if (filepath != null && !(name + ".json").ToLower().Equals(filepath.ToLower()))
            {
                throw new ArgumentException($"Filename does not match the defined name: " +
                                            $"filename is {filepath}, declared name is {name}");
            }

            var keys = (IEnumerable<string>)expr.PossibleTags()?.Keys ?? new List<string>();
            foreach (var key in keys)
            {
                if (!key.Trim().Equals(key))
                {
                    Console.WriteLine($"Warning: a key can be trimmed: '{key}'");
                }
            }

            return new AspectMetadata(
                expr,
                name,
                e.Get("description"),
                e.TryGet("author"),
                e.TryGet("unit"),
                filepath ?? "unknown"
            );
        }
    }
}