using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AspectedRouting.IO.jsonParser;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;

namespace AspectedRouting.Language
{
    public class Context
    {
        public readonly string AspectName;

        private readonly Dictionary<string, string> AvailableFilenames = new Dictionary<string, string>();

        public readonly Dictionary<string, AspectMetadata> DefinedFunctions = new Dictionary<string, AspectMetadata>();
        public readonly Dictionary<string, IExpression> Parameters = new Dictionary<string, IExpression>();

        public Context()
        {
        }

        protected Context(string aspectName, Dictionary<string, IExpression> parameters,
            Dictionary<string, AspectMetadata> definedFunctions, Dictionary<string, string> availableFilenames)
        {
            AspectName = aspectName;
            Parameters = parameters;
            DefinedFunctions = definedFunctions;
            AvailableFilenames = availableFilenames;
        }

        public Context(Context c) : this(c.AspectName, c.Parameters, c.DefinedFunctions, c.AvailableFilenames)
        {
        }

        public void AddParameter(string name, string value)
        {
            Parameters.Add(name, new Constant(value));
        }

        public void AddParameter(string name, IExpression value)
        {
            Parameters.Add(name, value);
        }

        public void AddFunction(string name, AspectMetadata function)
        {
            if (Funcs.Builtins.ContainsKey(name)) {
                throw new ArgumentException("Function " + name + " already exists, it is a builtin function");
            }

            if (DefinedFunctions.ContainsKey(name) && !function.ProfileInternal) {
                throw new ArgumentException("Function " + name + " already exists");
            }

            DefinedFunctions[name] = function;
        }

        public AspectMetadata GetAspect(string name)
        {
            if (name.StartsWith("$")) {
                name = name.Substring(1);
            }

            if (DefinedFunctions.TryGetValue(name, out var aspect)) {
                return aspect;
            }
            
            if (AvailableFilenames.TryGetValue(name, out var filename)) {
                this.LoadAspect(filename);
                AvailableFilenames.Remove(name);
                return GetAspect(name);
            }

            throw new ArgumentException(
                $"The aspect {name} is not a defined function. Known functions are " +
                string.Join(", ", DefinedFunctions.Keys));
        }

        public IExpression GetFunction(string name)
        {
            if (name.StartsWith("$")) {
                name = name.Substring(1);
            }

            if (Funcs.Builtins.ContainsKey(name)) {
                return Funcs.Builtins[name];
            }

            if (DefinedFunctions.ContainsKey(name)) {
                return DefinedFunctions[name];
            }

            if (AvailableFilenames.TryGetValue(name, out var filename)) {
                return GetAspect(name);
            }

            throw new ArgumentException(
                $"The function {name} is not a defined nor builtin function. Known functions are " +
                string.Join(", ", DefinedFunctions.Keys));
        }

        public Context WithParameters(Dictionary<string, IExpression> parameters)
        {
            return new Context(AspectName, parameters, DefinedFunctions, AvailableFilenames);
        }

        public Context WithAspectName(string name)
        {
            return new Context(name, Parameters, DefinedFunctions, AvailableFilenames);
        }

        public void LoadAspectsLazily(List<string> filenames)
        {
            foreach (var filename in filenames) {
                var fi = new FileInfo(filename);
                var expectedName = fi.Name.Substring(0, fi.Name.Length - ".json".Length);
                AvailableFilenames[expectedName] = filename;
            }
        }

        /**
         * Eagerly loads the given aspects
         */
        public List<AspectMetadata> LoadAllAspects(List<string> filenames)
        {
            var loaded = new List<AspectMetadata>();
            LoadAspectsLazily(filenames);
            foreach (var filename in filenames) {
                var aspect = LoadAspect(filename);
                if (aspect != null) {
                    loaded.Add(aspect);
                }
            }

            return loaded;
        }

        private AspectMetadata LoadAspect(string filename)
        {
            var fi = new FileInfo(filename);
            var expectedName = fi.Name.Substring(0, fi.Name.Length - ".json".Length);
            if (DefinedFunctions.TryGetValue(expectedName, out var aspect)) {
                return aspect;
            }

            aspect = JsonParser.AspectFromJson(this, File.ReadAllText(filename), fi.Name);
            if (aspect == null) {
                return null;
            }

            AddFunction(aspect.Name, aspect);
            return aspect;
        }
    }
}