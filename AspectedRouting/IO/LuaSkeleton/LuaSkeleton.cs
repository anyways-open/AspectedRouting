using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AspectedRouting.Language;

namespace AspectedRouting.IO.LuaSkeleton
{
    /// <summary>
    ///     The 'LuaSkeleton' is a class which is used in Lua generation of profiles.
    ///     The lua skeleton basically keeps track of dependencies, and added functions.
    ///     Once done, all these can be retrieved as code.
    ///     E.g. if an expression is turned into lua with 'ToExpression', then the dependencies will be automatically added.
    /// </summary>
    public partial class LuaSkeleton
    {
        private readonly HashSet<string> _alreadyAddedFunctions = new HashSet<string>();

        private readonly List<string> _constants = new List<string>();
        private Context _context;
        private readonly bool _useSnippets;

        private readonly HashSet<string> _dependencies = new HashSet<string>();
        private readonly List<string> _functionImplementations = new List<string>();

        /// <summary>
        ///     It turns out that creating lua tables is a huge performance overhead.
        ///     Lots of functions however need a constant table to be invoked. Creating this table over and over is performance
        ///     issue.
        ///     If this flag is set, those constant tables are exported so they are created only once
        /// </summary>
        private readonly bool _staticTables;

        public Context Context => _context;

        public LuaSkeleton(Context context, bool useSnippets, bool staticTables = false)
        {
            _context = context;
            _useSnippets = useSnippets;
            _staticTables = staticTables;
        }

        internal void AddDep(string name)
        {
            if (dependenciesAreGenerated && !_dependencies.Contains(name)) {
                throw new InvalidOperationException("You are attempting to add a dependency ('"+name+"') to the lua skeleton, but all the dependencies have been generated already. Make sure you 'addDep' call happens before this");
            }
            if (name.StartsWith("mapping"))
            {
                Console.Error.WriteLine(">>>");
                throw new Exception("A mapping was added as dependency - this is a bug");
            }

            if (name.Contains("stringToTags"))
            {
                AddDep("table_to_list");
            }
            _dependencies.Add(name);
        }

        public List<string> GenerateFunctions()
        {
            return _functionImplementations;
        }

        public bool ContainsFunction(string name)
        {
            return _alreadyAddedFunctions.Contains(name);
        }

        public void AddDependenciesFor(IExpression e)
        {
            var (_, functionNames) = e.InList().DirectlyAndInderectlyCalled(_context);
            foreach (var functionName in functionNames)
            {
                if (_context.DefinedFunctions.TryGetValue(functionName, out var aspectMeta))
                {
                    AddFunction(aspectMeta);
                }
                else
                {
                    AddDep(functionName);
                }
            }
        }

        private bool dependenciesAreGenerated = false;
        public List<string> GenerateDependencies()
        {
            dependenciesAreGenerated = true;
            var imps = new List<string>();

            foreach (var name in _dependencies)
            {
                var path = $"IO/lua/{name}.lua";
                if (File.Exists(path))
                {
                    imps.Add(File.ReadAllText(path));
                }
                else
                {
                    throw new FileNotFoundException(path);
                }
            }

            return imps;
        }

        public string AddConstant(string luaExpression)
        {
            _constants.Add(luaExpression);
            return "c" + (_constants.Count - 1);
        }

        public IEnumerable<string> GenerateConstants()
        {
            return _constants.Select((c, i) => $"c{i} = {c}");
        }

        private readonly Dictionary<string, uint> counters = new Dictionary<string, uint>();
        public string FreeVar(string key)
        {
            if (!counters.ContainsKey(key))
            {
                counters[key] = 0;
                return key;
            }

            var i = counters[key];
            counters[key]++;
            return key + i;

        }
    }
}