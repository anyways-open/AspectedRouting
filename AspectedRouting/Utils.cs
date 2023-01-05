using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;

namespace AspectedRouting
{
    public static class Utils
    {
        public static string Indent(this string s)
        {
            return s.Replace("\n", "\n    ");
        }

        public static List<T> InList<T>(this T t)
        {
            return new List<T> { t };
        }

        public static string Lined(this IEnumerable<string> lines)
        {
            return string.Join("\n", lines);
        }

        public static string Lines(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        public static int Multiply(this IEnumerable<int> ints)
        {
            var factor = 1;
            foreach (var i in ints) factor += i;

            return factor;
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            var result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static T[] SubArray<T>(this T[] data, int index)
        {
            return data.SubArray(index, data.Length - index);
        }

        public static string Quoted(this string s)
        {
            return "\"" + s + "\"";
        }

        public static string GenerateTagsOverview(IEnumerable<ProfileMetaData> profiles, Context context)
        {
            var allExpressions = new List<IExpression>();
            foreach (var profile in profiles)
                foreach (var behaviour in profile.Behaviours)
                    allExpressions.AddRange(profile.AllExpressions(context));

            var explanations = new List<string>();
            foreach (var tag in allExpressions.PossibleTags())
            {
                var values = new List<string>(tag.Value);
                values.Sort();
                explanations.Add(tag.Key.Quoted() + ": [" +
                                 string.Join(", ", values.Select(v => v.Quoted()))
                                 + "]");
            }

            explanations.Sort();


            return "{\n    " + string.Join(",\n    ", explanations) + "\n}";
        }

        /// <summary>
        ///     Generates a JSON file where all the profiles are listed, together with descriptions and other metadata.
        ///     Useful for other apps, e.g. the routing api to have
        /// </summary>
        /// <param name="profiles"></param>
        /// <param name="context"></param>
        /// <param name="select"></param>
        /// <returns></returns>
        public static string GenerateExplanationJson(IEnumerable<ProfileMetaData> profiles, Context context)
        {
            var metaItems = new List<string>();

            foreach (var profile in profiles)
            {
                var profileName = profile.Name;
                var author = profile.Author;
                var profileDescription = profile.Description;


                foreach (var behaviour in profile.Behaviours)
                {
                    var behaviourDescription = behaviour.Value["description"].Evaluate(new Context()) as string;
                    behaviourDescription ??= "";
                    var keys = new List<string>();
                    foreach (var tag in profile.AllExpressions(context).PossibleTags()) keys.Add(tag.Key.Quoted());

                    var meta = new Dictionary<string, string>
                    {
                        { "name", behaviour.Key },
                        { "type", profileName },
                        { "author", author },
                        { "description", behaviourDescription + " (" + profileDescription + ")" }
                    };
                    var json = string.Join(",", meta.Select(d =>
                        $"\"{d.Key}\": \"{d.Value}\""));

                    metaItems.Add($"{{{json}, " +
                                  $"\"usedKeys\": [{string.Join(", ", keys)}] }}\n");
                }
            }

            return "[" + string.Join(",\n", metaItems) + "]";
        }

        ///<summary>
        /// Parses an object, converts it to a double.
        /// throws an exception if not a double
        /// </summary>
        public static double AsDouble(object obj, string paramName)
        {
            if (obj == null)
            {
                throw new Exception($"Invalid value as result for {paramName}: got null");
            }
            switch (obj)
            {
                case bool b:
                    return b ? 1.0 : 0.0;
                case double d:
                    return d;
                case int j:
                    return j;
                case string s:
                    if (s.Equals("yes")) return 1.0;

                    if (s.Equals("no")) return 0.0;

                    throw new Exception($"Invalid value as result for {paramName}: got string {s}");
                default:
                    throw new Exception($"Invalid value as result for {paramName}: got object {obj}");
            }
        }
    }
}