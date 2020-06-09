using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Language.Functions;
using AspectedRouting.Language.Typ;

namespace AspectedRouting.Tests
{
    public class BehaviourTestSuite
    {
        public readonly ProfileMetaData Profile;
        public readonly string BehaviourName;
        public readonly IEnumerable<(ProfileResult, Dictionary<string, string> tags)> Tests;

        public static BehaviourTestSuite FromString(Context c, ProfileMetaData function, string behaviourName,
            string csvContents)
        {
            try
            {
                var all = csvContents.Split("\n").ToList();
                var keys = all[0].Split(",").ToList();
                keys = keys.GetRange(4, keys.Count - 4).Select(k => k.Trim()).ToList();

                foreach (var k in keys)
                {
                    if (k.StartsWith("_relations:"))
                    {
                        throw new ArgumentException(
                            "To inject relation memberships, use '_relation:<aspect_name>', without S after relation");
                    }

                    if (k.StartsWith("_relation:"))
                    {
                        var aspectName = k.Substring("_relation:".Length);
                        if (aspectName.Contains(":"))
                        {
                            throw new ArgumentException(
                                "To inject relation memberships, use '_relation:<aspect_name>', don't add the behaviour name");
                        }

                        if (!c.DefinedFunctions.ContainsKey(aspectName))
                        {
                            throw new ArgumentException(
                                $"'_relation:<aspect_name>' detected, but the aspect {aspectName} wasn't found. Try one of: " +
                                string.Join(",", c.DefinedFunctions.Keys));
                        }
                    }
                }


                var tests = new List<(ProfileResult, Dictionary<string, string>)>();

                var line = 1;
                foreach (var test in all.GetRange(1, all.Count - 1))
                {
                    line++;
                    if (string.IsNullOrEmpty(test.Trim()))
                    {
                        continue;
                    }

                    try
                    {
                        var testData = test.Split(",").ToList();

                        var speed = 0.0;
                        if (!string.IsNullOrEmpty(testData[2]))
                        {
                            speed = double.Parse(testData[2]);
                        }

                        var weight = 0.0;
                        if (!string.IsNullOrEmpty(testData[3]))
                        {
                            weight = double.Parse(testData[3]);
                        }

                        
                        
                        var expected = new ProfileResult(
                            testData[0],
                            testData[1],
                            speed,
                            weight
                        );

                        if (expected.Priority == 0 && expected.Access != "no")
                        {
                            throw new ArgumentException("A priority of zero is interpreted as 'no access' - don't use it");
                        }
                        
                        var vals = testData.GetRange(4, testData.Count - 4);
                        var tags = new Dictionary<string, string>();
                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (i < vals.Count && !string.IsNullOrEmpty(vals[i]))
                            {
                                tags[keys[i]] = vals[i];
                            }
                        }

                        tests.Add((expected, tags));
                    }
                    catch (Exception e)
                    {
                        throw new Exception("On line " + line, e);
                    }
                }

                return new BehaviourTestSuite(function, behaviourName, tests);
            }
            catch (Exception e)
            {
                throw new Exception("In the profile test file for " + behaviourName, e);
            }
        }

        public BehaviourTestSuite(
            ProfileMetaData profile,
            string behaviourName,
            IEnumerable<(ProfileResult, Dictionary<string, string> tags)> tests)
        {
            Profile = profile;
            BehaviourName = behaviourName;
            Tests = tests;
        }


        private static bool Eq(Context c, string value, object result)
        {
            var v = Funcs.Eq.Apply(new Constant(value), new Constant(Typs.String, result));

            var o = v.Evaluate(c);
            return o is string s && s.Equals("yes");
        }


        public bool RunTest(Context c, int i, ProfileResult expected, Dictionary<string, string> tags)
        {
            void Err(string message, object exp, object act)
            {
                Console.WriteLine(
                    $"[{Profile.Name}.{BehaviourName}]: Test on line {i + 1} failed: {message}; expected {exp} but got {act}\n    {{{tags.Pretty()}}}");
            }

            var actual = Profile.Run(c, BehaviourName, tags);

            var success = true;
            if (!expected.Access.Equals(actual.Access))
            {
                Err("access value incorrect", expected.Access, actual.Access);
                success = false;
            }


            if (expected.Access.Equals("no"))
            {
                return success;
            }


            if (!Eq(c, expected.Oneway, actual.Oneway))
            {
                Err("oneway value incorrect", expected.Oneway, actual.Oneway);
                success = false;
            }


            if (Math.Abs(actual.Speed - expected.Speed) > 0.0001)
            {
                Err("speed value incorrect", expected.Speed, actual.Speed);
                success = false;
            }


            if (Math.Abs(actual.Priority - expected.Priority) > 0.0001)
            {
                Err($"weight incorrect. Calculation is {string.Join(" + ", actual.PriorityExplanation)}",
                    expected.Priority,
                    actual.Priority);
                success = false;
            }


            if (!success)
            {
                Console.WriteLine();
            }

            return success;
        }

        public void Run(Context c)

        {
        

            var allOk = true;
            var i = 1;
            foreach (var (expected, tags) in Tests)
            {
                try
                {
                    allOk &= RunTest(c, i, expected, tags);
                }
                catch (Exception e)
                {
                    throw new Exception("In a test for " + BehaviourName, e);
                }

                i++;
            }

            if (!allOk)
            {
                throw new ArgumentException("Some tests failed for " + BehaviourName);
            }
            else
            {
                Console.WriteLine($"[{Profile.Name}] {Tests.Count()} tests successful for behaviour {BehaviourName}");
            }
        }
    }
}