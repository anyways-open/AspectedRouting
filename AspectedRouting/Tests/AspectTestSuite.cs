using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;

namespace AspectedRouting.Tests
{
    public class AspectTestSuite
    {
        private readonly Context _context;
        public readonly AspectMetadata FunctionToApply;
        public readonly IEnumerable<(string expected, Dictionary<string, string> tags)> Tests;

        public AspectTestSuite(
            AspectMetadata functionToApply,
            IEnumerable<(string expected, Dictionary<string, string> tags)> tests,
            Context context)
        {
            if (functionToApply == null) {
                throw new NullReferenceException("functionToApply is null");
            }

            FunctionToApply = functionToApply;
            Tests = tests;
            _context = context;
        }

        public static AspectTestSuite FromString(Context c, AspectMetadata function, string csvContents)
        {
            var all = csvContents.Split("\n").ToList();
            var keys = all[0].Split(",").ToList();
            keys = keys.GetRange(1, keys.Count - 1);

            var tests = new List<(string, Dictionary<string, string>)>();

            foreach (var test in all.GetRange(1, all.Count - 1)) {
                if (string.IsNullOrEmpty(test.Trim())) {
                    continue;
                }

                var testData = test.Split(",").ToList();
                var expected = testData[0];
                var vals = testData.GetRange(1, testData.Count - 1);
                var tags = new Dictionary<string, string>();
                for (var i = 0; i < keys.Count; i++)
                    if (i < vals.Count && !string.IsNullOrEmpty(vals[i])) {
                        tags[keys[i]] = vals[i].Trim(new[] { '"' }).Replace("\"", "\\\"");
                    }

                tests.Add((expected, tags));
            }

            return new AspectTestSuite(function, tests, c);
        }

        /// <summary>
        ///     Returns a test suite where no tests are kept which contain keys of the scheme '_relation:<name>:<key>'
        /// </summary>
        /// <returns></returns>
        public AspectTestSuite WithoutRelationTests()
        {
            var newTests = new List<(string expected, Dictionary<string, string> tags)>();
            foreach (var (expected, tags) in Tests) {
                if (tags.Keys.Any(key => key.StartsWith("_relation") && key.Split(":").Length == 3)) {
                    continue;
                }

                newTests.Add((expected, tags));
            }

            return new AspectTestSuite(FunctionToApply, newTests, _context);
        }


        public bool Run()
        {
            var failed = false;
            var testCase = 0;
            foreach (var test in Tests) {
                testCase++;
                var context = new Context(_context).WithAspectName("unittest");
                foreach (var (key, value) in test.tags)
                    if (key.StartsWith("#")) {
                        context.AddParameter(key, value);
                    }

                try {
                    var tags = test.tags;
                    if (tags.ContainsKey("access")) {
                        tags["_access"] = tags["access"];
                    }

                    if (tags.ContainsKey("oneway")) {
                        tags["_oneway"] = tags["oneway"];
                    }

                    var actual = FunctionToApply.Evaluate(context, new Constant(tags));

                    if (test.expected.Equals("x")) {
                        // Little utility to fill out the csv file
                        Console.WriteLine("Line "+ (testCase + 1)+": got "+ actual);
                        continue;
                    }
                    
                    if (string.IsNullOrWhiteSpace(test.expected)) {
                        failed = true;
                        Console.WriteLine(
                            $"[{FunctionToApply.Name}] Line {testCase + 1} failed:\n   The expected value is not defined or only whitespace. Do you want null? Write null in your test as expected value\nThe resulting value is: "+actual);
                        continue;
                    }

                    if (test.expected == "null" && actual == null) {
                        // Test ok
                        continue;
                    }

                    if (actual == null) {
                        Console.WriteLine(
                            $"[{FunctionToApply.Name}] Line {testCase + 1} failed:\n   Expected: {test.expected}\n   actual value is not defined (null)\n   tags: {test.tags.Pretty()}\n");
                        failed = true;
                        continue;
                    }


                    var doesMatch = (actual is double d &&
                                     Math.Abs(double.Parse(test.expected, NumberStyles.Any,
                                         CultureInfo.InvariantCulture) - d) < 0.0001)
                                    || actual.ToString().Equals(test.expected);

                    if (!doesMatch) {
                        failed = true;
                        Console.WriteLine(
                            $"[{FunctionToApply.Name}] Line {testCase + 1} failed:\n   Expected: {test.expected}\n   actual: {actual}\n   tags: {test.tags.Pretty()}\n");
                    }
                }
                catch (Exception e) {
                    Console.WriteLine(
                        $"[{FunctionToApply.Name}] Line {testCase + 1} ERROR:\n   Expected: {test.expected}\n   error message: {e.Message}\n   tags: {test.tags.Pretty()}\n");
                    Console.WriteLine(e);
                    failed = true;
                }
            }


            Console.WriteLine($"[{FunctionToApply.Name}] {testCase} tests " + (failed ? "failed" : "successful"));
            return !failed;
        }
    }
}