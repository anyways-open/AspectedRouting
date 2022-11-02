using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using AspectedRouting.IO.itinero1;
using AspectedRouting.IO.itinero2;
using AspectedRouting.IO.md;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Tests;

namespace AspectedRouting
{
    /**
     * Prints to the specified location
     */
    public class Printer
    {
        private readonly List<(AspectMetadata aspect, AspectTestSuite tests)> _aspects;
        private readonly Context _context;
        private readonly string _outputDirectory;
        private readonly ProfileMetaData _profile;
        private readonly List<BehaviourTestSuite> _profileTests;
        private readonly bool _includeTests;

        public Printer(string outputDirectory, ProfileMetaData profile, Context context,
            List<(AspectMetadata aspect, AspectTestSuite tests)> aspects,
            List<BehaviourTestSuite> profileTests, bool includeTests)
        {
            _outputDirectory = outputDirectory;
            _profile = profile;
            _context = context;
            _aspects = aspects;
            _profileTests = profileTests;
            _includeTests = includeTests;


            if (!Directory.Exists($"{outputDirectory}/profile-documentation/")) {
                Directory.CreateDirectory($"{outputDirectory}/profile-documentation/");
            }

            if (!Directory.Exists($"{outputDirectory}/itinero1/")) {
                Directory.CreateDirectory($"{outputDirectory}/itinero1/");
            }

            if (!Directory.Exists($"{outputDirectory}/itinero2/")) {
                Directory.CreateDirectory($"{outputDirectory}/itinero2/");
            }
        }

        public void PrintUsedTags()
        {
            var profile = _profile;
            var context = _context;
            Console.WriteLine("\n\n\n---------- " + profile.Name +
                              " : used tags and corresponding values --------------");
            foreach (var (key, values) in profile.AllExpressions(context).PossibleTags()) {
                var vs = "*";
                if (values.Any()) {
                    vs = string.Join(", ", values);
                }

                Console.WriteLine(key + ": " + vs);
            }

            Console.WriteLine("\n\n\n------------------------");
        }

        public void WriteProfile1()
        {
            var aspectTests = _aspects.Select(a => a.tests).ToList();

            var luaProfile = new LuaPrinter1(_profile, _context,
                aspectTests,
                _profileTests,
                _includeTests
            ).ToLua();

            var itinero1ProfileFile = Path.Combine($"{_outputDirectory}/itinero1/" + _profile.Name + ".lua");
            File.WriteAllText(itinero1ProfileFile, luaProfile);
            Console.WriteLine($"Written {new FileInfo(itinero1ProfileFile).FullName}");
        }

        public void WriteAllProfile2()
        {
            foreach (var (behaviourName,_) in _profile.Behaviours) {
                WriteProfile2(behaviourName);
            }
        }

        public void WriteProfile2(string behaviourName)
        {
            var aspectTests = _aspects.Select(a => a.tests).ToList();

            var lua2behaviour = new LuaPrinter2(
                _profile,
                behaviourName,
                _context,
                aspectTests,
                _profileTests.Where(testsSuite => testsSuite.BehaviourName == behaviourName),
                _includeTests
            ).ToLua();

            var itinero2ProfileFile = Path.Combine($"{_outputDirectory}/itinero2/{_profile.Name}.{behaviourName}.lua");
            File.WriteAllText(
                itinero2ProfileFile,
                lua2behaviour);
            Console.WriteLine($"Written {new FileInfo(itinero2ProfileFile).FullName}");


        }

        public void PrintMdInfo()
        {
            var profileMd = new MarkDownSection();
            profileMd.AddTitle(_profile.Name, 1);

            profileMd.Add(_profile.Description);
            profileMd.AddTitle("Default parameters", 4);
            profileMd.Add("| name | value | ", "| ---- | ---- | ",
                string.Join("\n",
                    _profile.DefaultParameters.Select(delegate(KeyValuePair<string, IExpression> kv)
                    {
                        var v = kv.Value.Evaluate(_context);
                        if (!(v is string || v is int || v is double)) {
                            v = "_special value_";
                        }

                        return $" | {kv.Key} | {v} |";
                    }))
            );
            foreach (var (behaviourName, vars) in _profile.Behaviours) {
                var behaviourMd = new ProfileToMD(_profile, behaviourName, _context);

                File.WriteAllText(
                    $"{_outputDirectory}/profile-documentation/{_profile.Name}.{behaviourName}.md",
                    behaviourMd.ToString());
                profileMd.AddTitle($"[{behaviourName}](./{_profile.Name}.{behaviourName}.md)", 2);
                profileMd.Add(vars["description"].Evaluate(_context).ToString());
                profileMd.Add(behaviourMd.MainFormula());
            }

            File.WriteAllText(
                $"{_outputDirectory}/profile-documentation/{_profile.Name}.md",
                profileMd.ToString());
        }
    }
}