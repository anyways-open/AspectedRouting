using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.IO.itinero1;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Tests;

namespace AspectedRouting.IO.itinero2
{
    /// <summary>
    /// Lua printer for itinero2-lua format
    ///
    /// The itinero 2.0 lua profile is a whole lot simpler then the 1.0 format,
    /// as a single profile there only describes a single behaviour of a vehicle:
    ///
    /// It has:
    /// - name: string, e.g. 'bicycle.fastest'
    /// - factor(attributes, result): void, where 'attributes' are all the tags of the way,
    /// and result must contain (after calling):
    /// - 'forward_speed', a double describing the forward speed (in km/h)
    /// - 'backward_speed', the speed when travelling in the opposite direction (0 if not possible)
    /// - 'forward', a double describing the forwardfactor
    /// - 'backward', the backward factor
    /// - 'canstop', a boolean indicating if stopping along the road is possible
    /// 
    /// </summary>
    public partial class LuaPrinter2
    {
        private readonly ProfileMetaData _profile;
        private readonly string _behaviourName;
        private readonly Context _context;
        private readonly List<AspectTestSuite> _aspectTests;
        private readonly IEnumerable<BehaviourTestSuite> _behaviourTestSuite;

        private readonly LuaSkeleton.LuaSkeleton _skeleton;
        private readonly LuaParameterPrinter _parameterPrinter;


        public LuaPrinter2(ProfileMetaData profile, string behaviourName,
            Context context,
            List<AspectTestSuite> aspectTests, IEnumerable<BehaviourTestSuite> behaviourTestSuite)
        {
            _skeleton = new LuaSkeleton.LuaSkeleton(context);
            _profile = profile;
            _behaviourName = behaviourName;
            _context = context;
            _aspectTests = aspectTests;
            _behaviourTestSuite = behaviourTestSuite;
            _parameterPrinter = new LuaParameterPrinter(_profile, _skeleton);
        }

        public string ToLua()
        {
            var header =
                new List<string>
                {
                    $"name = \"{_profile.Name}.{_behaviourName}\"",
                    $"generationDate = \"{DateTime.Now:s}\"",
                    $"description = \"{_profile.Description}\""
                };

            var tests = new LuaTestPrinter(_skeleton, new List<string>() {"unitTestProfile2"}).GenerateFullTestSuite(
                _behaviourTestSuite.ToList(), new List<AspectTestSuite>());
            var all = new List<string>
            {
                header.Lined(),
                "",
                GenerateMainFunction(),
                "",
                GenerateFactorFunction(),
                "",
                _parameterPrinter.GenerateDefaultParameters(),
                "",
                "",
                string.Join("\n\n", _skeleton.GenerateFunctions()),
                "",
                string.Join("\n\n", _skeleton.GenerateDependencies()), // Should be AFTER generating the main function!
                "",
                tests,
                "",
                
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
                "    print(\"Tests OK\")",
                "end"
            };

            return all.Lined();
        }
    }
}