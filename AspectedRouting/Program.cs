using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using AspectedRouting.IO;
using AspectedRouting.IO.jsonParser;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Tests;

namespace AspectedRouting
{
    internal class Repl
    {
        private readonly Context _c;
        private readonly string _profilesPath;

        private readonly Dictionary<string, ProfileMetaData> availableProfiles;


        public Repl(string profilesPath, Context c, Dictionary<string, ProfileMetaData> profiles)
        {
            _profilesPath = profilesPath;
            _c = c;
            availableProfiles = profiles ?? new Dictionary<string, ProfileMetaData>();
        }

        private ProfileMetaData LoadProfile(string vehicle)
        {
            var jsonFile = _profilesPath + "/" + vehicle + "/" + vehicle + ".json";
            var profile =
                JsonParser.ProfileFromJson(_c, File.ReadAllText(jsonFile), new FileInfo(jsonFile),
                    new DateTime());
            if (profile == null) {
                return null;
            }

            availableProfiles[profile.Name] = profile;
            return profile;
        }

        public void Run()
        {
            var profiles = availableProfiles;
            LoadProfile("bicycle");
            var profile = profiles["bicycle"];
            var behaviour = profile.Behaviours.Keys.First();
            do {
                Console.Write(profile.Name + "." + behaviour + " > ");
                var read = Console.ReadLine();
                if (read == null) {
                    return; // End of stream has been reached
                }

                if (read == "") {
                    Console.WriteLine("looƆ sᴉ dɐWʇǝǝɹʇSuǝdO");
                    continue;
                }

                if (read.Equals("quit")) {
                    return;
                }

                if (read.Equals("help")) {
                    Console.WriteLine(
                        Utils.Lines(
                            "select <behaviourName> to change behaviour or <vehicle.behaviourName> to change vehicle",
                            ""));
                    continue;
                }

                if (read.Equals("clear")) {
                    for (var i = 0; i < 80; i++) Console.WriteLine();

                    continue;
                }

                if (read.StartsWith("select")) {
                    var beh = read.Substring("select".Length + 1).Trim();

                    if (beh.Contains(".")) {
                        var profileName = beh.Split(".")[0];
                        if (!profiles.TryGetValue(profileName, out var newProfile)) {
                            var loaded = LoadProfile(beh.Split(".")[0]);
                            if (loaded != null) {
                                newProfile = loaded;
                            }
                            else {
                                Console.Error.WriteLine("Profile " + profileName + " not found, ignoring");
                                continue;
                            }
                        }

                        profile = newProfile;
                        beh = beh.Substring(beh.IndexOf(".", StringComparison.Ordinal) + 1);
                    }

                    if (profile.Behaviours.ContainsKey(beh)) {
                        behaviour = beh;
                        Console.WriteLine("Switched to " + beh);
                    }
                    else {
                        Console.WriteLine("Behaviour not found. Known behaviours are:\n   " +
                                          string.Join("\n   ", profile.Behaviours.Keys));
                    }


                    continue;
                }

                var tagsRaw = read.Split(";").Select(s => s.Trim());
                var tags = new Dictionary<string, string>();
                foreach (var str in tagsRaw) {
                    if (str == "") {
                        continue;
                    }

                    try {
                        var strSplit = str.Split("=");
                        var k = strSplit[0].Trim();
                        var v = strSplit[1].Trim();
                        tags[k] = v;
                    }
                    catch (Exception) {
                        Console.Error.WriteLine("Could not parse tag: " + str);
                    }
                }

                try {
                    var result = profile.Run(_c, behaviour, tags);
                    Console.WriteLine(result);
                }
                catch (Exception e) {
                    Console.WriteLine(e);
                    Console.WriteLine(e.Message);
                }
            } while (true);
        }
    }

    public static class Program
    {
        private static List<(AspectMetadata aspect, AspectTestSuite tests)> ParseAspects(
            this IEnumerable<string> jsonFileNames, List<string> testFileNames, Context context)
        {
            var aspects = new List<(AspectMetadata aspect, AspectTestSuite tests)>();
            var loadedAspects = context.LoadAllAspects(jsonFileNames.ToList());
            foreach (var aspect in loadedAspects) {
                var testName = aspect.Name + ".test.csv";
                var testPath = testFileNames.FindTest(testName);
                AspectTestSuite tests = null;
                if (!string.IsNullOrEmpty(testPath) && File.Exists(testPath)) {
                    tests = AspectTestSuite.FromString(context, aspect, File.ReadAllText(testPath));
                }

                aspects.Add((aspect, tests));
            }

            return aspects;
        }

        private static string FindTest(this IEnumerable<string> testFileNames, string testName)
        {
            var testPaths = testFileNames.Where(nm => nm.EndsWith(testName)).ToList();
            if (testPaths.Count > 1) {
                Console.WriteLine("[WARNING] Multiple tests found for " + testName + ", using only one arbitrarily");
            }

            if (testPaths.Count > 0) {
                return testPaths.First();
            }

            return null;
        }


        private static List<(ProfileMetaData profile, List<BehaviourTestSuite> profileTests)> ParseProfiles(
            IEnumerable<string> jsonFiles, IReadOnlyCollection<string> testFiles, Context context, DateTime lastChange)
        {
            var result = new List<(ProfileMetaData profile, List<BehaviourTestSuite> profileTests)>();
            foreach (var jsonFile in jsonFiles)
                try {
                    var profile =
                        JsonParser.ProfileFromJson(context, File.ReadAllText(jsonFile), new FileInfo(jsonFile),
                            lastChange);
                    if (profile == null) {
                        continue;
                    }

                    profile.SanityCheckProfile(context);

                    var profileTests = new List<BehaviourTestSuite>();
                    foreach (var behaviourName in profile.Behaviours.Keys) {
                        var path = testFiles.FindTest($"{profile.Name}.{behaviourName}.behaviour_test.csv");
                        if (path != null && File.Exists(path)) {
                            var test = BehaviourTestSuite.FromString(context, profile, behaviourName,
                                File.ReadAllText(path));
                            profileTests.Add(test);
                        }
                        else {
                            Console.WriteLine($"[{profile.Name}] WARNING: no test found for behaviour {behaviourName}");
                        }
                    }

                    result.Add((profile, profileTests));
                }
                catch (Exception e) {
                    // PrintError(jsonFile, e);
                    throw new Exception("In the file " + jsonFile, e);
                }

            return result;
        }


        private static void PrintError(string file, Exception exception)
        {
            var msg = exception.Message;
            while (exception.InnerException != null) {
                exception = exception.InnerException;
                msg += "\n    " + exception.Message;
            }

            Console.WriteLine($"Error in the file {file}:\n    {msg}");
        }


        private static int Main(string[] args)
        {
            var errMessage = MainWithError(args);
            if (errMessage == null) {
                return 0;
            }

            Console.WriteLine(errMessage);
            return 1;
        }


        private static void WriteOutputFiles(Context context,
            List<(AspectMetadata aspect, AspectTestSuite tests)> aspects, string outputDir,
            List<(ProfileMetaData profile, List<BehaviourTestSuite> profileTests)> profiles, bool includeTests)
        {
            foreach (var (profile, profileTests) in profiles) {
                var printer = new Printer(outputDir, profile, context, aspects, profileTests, includeTests);
                printer.PrintUsedTags();
                printer.WriteProfile1();
                printer.PrintMdInfo();
                printer.WriteAllProfile2();
            }

            File.WriteAllText($"{outputDir}/ProfileMetadata.json",
                Utils.GenerateExplanationJson(profiles.Select(p => p.profile), context)
            );
            File.WriteAllText($"{outputDir}/UsedTags.json",
                Utils.GenerateTagsOverview(profiles.Select(p => p.profile), context)
            );
        }

        private static (bool, List<ProfileMetaData> profiles) HandleProfiles(Context context, List<string> files,
            List<string> tests, DateTime lastChange,
            string outputDir, bool includeTests)
        {
            var aspects = ParseAspects(files, tests, context);

            var profiles = ParseProfiles(files, tests, context, lastChange);


            // With everything parsed and typechecked, time for tests
            var testsOk = true;
            foreach (var (aspect, t) in aspects)
                if (t == null) {
                    Console.WriteLine($"[{aspect.Name}] WARNING: no tests found: please add {aspect.Name}.test.csv");
                }
                else {
                    testsOk &= t.Run();
                }


            foreach (var (profile, profileTests) in profiles)
            foreach (var test in profileTests)
                testsOk &= test.Run(context);
            if (testsOk) {
                WriteOutputFiles(context, aspects, outputDir, profiles, includeTests);
            }

            return (testsOk, profiles.Select(p => p.profile).ToList());
        }

        public static string MainWithError(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US", false);
            if (args.Length < 2) {
                return
                    "Usage: <directory where all aspects and profiles can be found> <outputdirectory> [--include-tests]\n" +
                    "The flag '--include-tests' will append some self-tests in the lua files";
            }

            var inputDir = args[0];
            var outputDir = args[1];
            var replOnly = args.Contains("--repl");
            var includeTests = args.Contains("--include-tests") && !replOnly;
            var runRepl = !args.Contains("--no-repl") || replOnly;

            if (includeTests) {
                Console.WriteLine("Including tests in the lua files");
            }
            else {
                Console.WriteLine(
                    "Not including tests in the lua files; use --include-tests if you want to emit them too");
            }

            if (!Directory.Exists(outputDir)) {
                Directory.CreateDirectory(outputDir);
            }

            MdPrinter.GenerateHelpText(outputDir + "helpText.md");
            Console.WriteLine("Written helptext to " + outputDir);


            var files = Directory.EnumerateFiles(inputDir, "*.json", SearchOption.AllDirectories)
                .ToList();

            var tests = Directory.EnumerateFiles(inputDir, "*.csv", SearchOption.AllDirectories)
                .ToList();
            tests.Sort();


            foreach (var test in tests) {
                if (test.EndsWith(".test.csv") || test.EndsWith(".behaviour_test.csv")) {
                    continue;
                }

                throw new ArgumentException(
                    $"Invalid name for csv file ${test}, should end with either '.behaviour_test.csv' or '.test.csv'");
            }

            var context = new Context();
            context.LoadAspectsLazily(files.ToList());

            var lastChange = DateTime.UnixEpoch;
            var testsOk = true;
            List<ProfileMetaData> profiles = null;
            if (!replOnly) {
                (testsOk, profiles) = HandleProfiles(context, files, tests, lastChange, outputDir, includeTests);
            }

            foreach (var file in files) {
                var time = new FileInfo(file).LastWriteTimeUtc;
                if (lastChange < time) {
                    lastChange = time;
                }
            }


            if (runRepl) {
                Dictionary<string, ProfileMetaData> profileDict = null;
                if (profiles != null) {
                    profileDict = profiles.ToDictionary(p => p.Name, p => p);
                }

                new Repl(inputDir, context, profileDict).Run();
            }
            else {
                Console.WriteLine("Not starting REPL as --no-repl is specified");
            }

            return !testsOk ? "Some tests failed, quitting now without generating output" : null;
        }
    }
}