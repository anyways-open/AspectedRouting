using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.IO.itinero1;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;
using AspectedRouting.Tests;

namespace AspectedRouting.IO.itinero2
{
    /// <summary>
    ///     Lua printer for itinero2-lua format
    ///     The itinero 2.0 lua profile is a whole lot simpler then the 1.0 format,
    ///     as a single profile there only describes a single behaviour of a vehicle:
    ///     It has:
    ///     - name: string, e.g. 'bicycle.fastest'
    ///     - factor(attributes, result): void, where 'attributes' are all the tags of the way,
    ///     and result must contain (after calling):
    ///     - 'forward_speed', a double describing the forward speed (in km/h)
    ///     - 'backward_speed', the speed when travelling in the opposite direction (0 if not possible)
    ///     - 'forward', a double describing the forwardfactor
    ///     - 'backward', the backward factor
    ///     - 'canstop', a boolean indicating if stopping along the road is possible
    /// </summary>
    public partial class LuaPrinter2
    {
        private readonly List<AspectTestSuite> _aspectTests;
        private readonly string _behaviourName;
        private readonly IEnumerable<BehaviourTestSuite> _behaviourTestSuite;
        private readonly Context _context;
        private readonly bool _includeTests;
        private readonly LuaParameterPrinter _parameterPrinter;
        private readonly ProfileMetaData _profile;

        private readonly LuaSkeleton.LuaSkeleton _skeleton;


        public LuaPrinter2(ProfileMetaData profile, string behaviourName,
            Context context,
            List<AspectTestSuite> aspectTests, IEnumerable<BehaviourTestSuite> behaviourTestSuite,
            bool includeTests = true)
        {
            _skeleton = new LuaSkeleton.LuaSkeleton(context, true);
            _profile = profile;
            _behaviourName = behaviourName;
            _context = context;
            _aspectTests = aspectTests;
            _behaviourTestSuite = behaviourTestSuite;
            _includeTests = includeTests;
            _parameterPrinter = new LuaParameterPrinter(_profile, _skeleton);
        }

        private string TestRunner()
        {
            return new List<string>
            {
                "if (itinero == nil) then",
                "    itinero = {}",
                "    itinero.log = print",
                "",
                "    -- Itinero is not defined -> we are running from a lua interpreter -> the tests are intended",
                "    runTests = true",
                "",
                "",
                "else",
                "    print = itinero.log",
                "end",
                "",
                "test_all()",
                "if (not failed_tests and not failed_profile_tests and print ~= nil) then",
                $"    print(\"Tests OK ({_profile.Name}.{_behaviourName})\")",
                "else",
                "    error(\"Some tests failed\")",
                "end"
            }.Lined();
        }

        public string ToLua()
        {
            var profileDescr = _profile.Behaviours[_behaviourName]["description"].Evaluate(_context).ToString();
            var header =
                new List<string>
                {
                    $"name = \"{_profile.Name}.{_behaviourName}\"",
                    $"description = \"{profileDescr} ({_profile.Description})\"",
                    "",
                    "-- The hierarchy of types that this vehicle is; mostly used to check access restrictions",
                    "vehicle_types = " +
                    _skeleton.ToLua(new Constant(_profile.VehicleTyps.Select(v => new Constant(v)).ToArray()))
                };

            LuaTestPrinter testPrinter = null;
            if (_includeTests) {
                testPrinter = new LuaTestPrinter(_skeleton,
                    new List<string> {"unitTest","unitTestProfile2"});
                
            }

            var all = new List<string>
            {
                header.Lined(),
                "",
                GenerateMainFunction(),
                "",
                GenerateFactorFunction(),
                "",
                GenerateTurnCostFunction(),
                "",
                _parameterPrinter.GenerateDefaultParameters(),
                "",
                "",
                string.Join("\n\n", _skeleton.GenerateFunctions()),
                "",
                string.Join("\n\n", _skeleton.GenerateDependencies()), // Should be AFTER generating the main function so that all dependencies are known!
                "",
                string.Join("\n\n", _skeleton.GenerateConstants()),
                ""
            };

            if (_includeTests) {
                
                var usedFunctions = _profile.AllExpressionsFor(_behaviourName, _context)
                    .SelectMany(e => e.DirectlyCalled().calledFunctionNames).ToHashSet();
                var aspectTests = (_aspectTests ?? new List<AspectTestSuite>())
                    ?.Where(t => usedFunctions.Contains(t?.FunctionToApply?.Name))
                    ?.Select(ats => ats.WithoutRelationTests())
                    ?.ToList();
                Console.WriteLine("Behaviour " + _profile.Name + "." + _behaviourName + " uses functions " +
                                  string.Join(", ", usedFunctions) + ", found tests for " + aspectTests.Count +
                                  " of them");
               
                var tests = testPrinter?.GenerateFullTestSuite(
                    _behaviourTestSuite.ToList(),
                    aspectTests,
                    true) + "\n\n" + TestRunner();
                all.Add(tests);
            }

            return all.Lined();
        }
    }
}