using System;
using System.Collections.Generic;
using System.Linq;
using AspectedRouting.Language;
using AspectedRouting.Language.Expression;
using AspectedRouting.Tests;

namespace AspectedRouting.IO.md
{
    internal class MarkDownSection
    {
        private readonly List<string> parts = new List<string>();

        public override string ToString()
        {
            return string.Join("\n\n", parts);
        }

        public void AddTitle(string title, int level)
        {
            var str = "";
            for (var i = 0; i < level; i++)
            {
                str += "#";
            }

            str += " " + title;
            parts.Add(str);
        }

        public void Add(params string[] paragraph)
        {
            parts.Add(string.Join("\n", paragraph));
        }

        public void AddList(List<string> items)
        {
            parts.Add(string.Join("\n", items.Select(i => " - " + i)));
        }
    }


    public class ProfileToMD
    {
        private readonly string _behaviour;
        private readonly Context _c;
        private readonly ProfileMetaData _profile;
        private readonly MarkDownSection md = new MarkDownSection();

        public ProfileToMD(ProfileMetaData profile, string behaviour, Context c)
        {
            _profile = profile;
            _behaviour = behaviour;
            _c = c.WithAspectName(behaviour);
            _c.DefinedFunctions["speed"] = new AspectMetadata(profile.Speed, "speed", "The speed this vehicle is going",
                "", "km/h", "", true);
            if (!profile.Behaviours.ContainsKey(behaviour))
            {
                throw new ArgumentException("Profile does not contain behaviour " + behaviour);
            }
        }

        private static decimal R(double d)
        {
            return Math.Round((decimal)d, 2);
        }

        /**
         * Calculates an entry with `speed`, `priority` for the profile
         */
        private string TableEntry(string msg, Dictionary<string, string> tags, ProfileResult? reference,
            bool nullOnSame = false)
        {
            var profile = _profile.Run(_c, _behaviour, tags);
            if (!reference.HasValue)
            {
                return "| " + msg + "    | " + profile.Speed + " | " + profile.Priority + " | ";
            }

            if (reference.Equals(profile) && nullOnSame)
            {
                return null;
            }

            return "| " + msg + "    | " + R(profile.Speed) + " | " +
                   R(profile.Speed / reference.Value.Speed) + " | " +
                   R(profile.Priority) + " | " + R(profile.Priority / reference.Value.Priority) + " | " +
                   profile.Access + " | " + profile.Oneway;
        }

        private void AddTagsTable(ProfileResult reference, Dictionary<string, HashSet<string>> usedTags, string referenceTags)
        {
            var p = _profile;
            var b = _profile.Behaviours[_behaviour];

            var tableEntries = new List<string>();
            foreach (var (key, vals) in usedTags)
            {
                var values = vals;
                if (values.Count == 0 && key == "maxspeed")
                {
                    tableEntries.Add($" | {key}=* (example values below)");
                    values = new HashSet<string> {
                        "20", "30", "50", "70", "90", "120", "150"
                    };
                }

                if (values.Count == 0)
                {
                    tableEntries.Add($" | {key}=*");
                }

                if (values.Count > 0)
                {
                    foreach (var value in values)
                    {
                        var tags = new Dictionary<string, string>
                        {
                            [key] = value
                        };
                        var entry = TableEntry($"{key}={value} ", tags, reference);
                        if (entry == null)
                        {
                            continue;
                        }

                        tableEntries.Add(entry);
                    }
                }
            }
            md.Add("In the following table, you'll find:");
            md.AddList(new List<string>()
            {
                "The single tag used to calculate the result as *Tags*",
                "The *speed* that the vehicle would go if only these tags were present",
                "The _speedfactor_, i.e. the ratio between the speed and the speed of a reference road (namely `"+referenceTags+"` which has a speed of *"+reference.Speed+"*)",
                "The *priority* of this tag",
                "The _priority-factor_, i.e. the ratio between the priority and the priority of a reference road (namely `"+referenceTags+"` which has a priority of *"+reference.Priority+"*)",
                "The *access* for this vehicle. Many tags will influence speed or priority, but most will not have a special effect",
                "The *oneway* status caused by the tags; the default is `both`"

            });
            md.Add("| Tags | Speed (km/h) | speed/referencespeed | Priority | Priority/referencepriority | access | oneway | ",
                "| ---- | ------------ | ----------- | -------- | --------------- | ----- | ------ |",
                string.Join("\n", tableEntries));
        }

        private Dictionary<string, IExpression> TagsWithPriorityInfluence()
        {

            var p = _profile;
            var parameters = _profile.ParametersFor(_behaviour);
            var withInfluence = new Dictionary<string, IExpression>();

            foreach (var kv in p.Priority)
            {
                if (parameters[kv.Key].Equals(0.0) || parameters[kv.Key].Equals(0))
                {
                    continue;
                }

                withInfluence[kv.Key] = kv.Value;
            }

            return withInfluence;
        }


        public string MainFormula()
        {
            var p = _profile;
            var b = _profile.Behaviours[_behaviour];

            var overridenParams = new HashSet<string>();
            var paramValues = new Dictionary<string, object>();
            foreach (var kv in p.DefaultParameters)
            {
                paramValues[kv.Key] = kv.Value.Evaluate(_c);
            }

            foreach (var kv in b)
            {
                paramValues[kv.Key] = kv.Value.Evaluate(_c);
                overridenParams.Add(kv.Key);
            }

            var mainFormulaParts = p.Priority.Select(delegate (KeyValuePair<string, IExpression> kv)
            {
                var key = kv.Key;
                var param = paramValues[key];
                if (param.Equals(0) || param.Equals(0.0))
                {
                    return "";
                }

                if (overridenParams.Contains(key))
                {
                    param = "**" + param + "**";
                }


                var called = kv.Value.DirectlyCalled();
                return param + " * `" + string.Join("", called.calledFunctionNames) + "`";
            });

            var mainFormula = string.Join(" + ", mainFormulaParts.Where(p => p != ""));
            return mainFormula;
        }

        public override string ToString()
        {
            var p = _profile;
            var b = _profile.Behaviours[_behaviour];
            md.AddTitle($"[{_profile.Name}](./{_profile.Name}.md).{_behaviour}", 1);

            md.Add(p.Description);

            if (b.ContainsKey("description"))
            {
                md.Add(b["description"].Evaluate(_c).ToString());
            }


            md.Add("This profile is calculated as following (non-default keys are bold):", MainFormula());

            var residentialTags = new Dictionary<string, string>
            {
                ["highway"] = "residential"
            };

            var residentialTagsStr = string.Join(";", residentialTags.Select((k, v) => $"{k}={v}"));
            md.Add("| Tags | Speed (km/h) | Priority",
                "| ---- | ----- | ---------- | ",
                TableEntry("Residential highway (reference)", residentialTags, null));
            var reference = _profile.Run(_c, _behaviour, residentialTags);
            md.AddTitle("Tags influencing priority", 2);
            md.Add(
                "Priority is what influences which road to take. The routeplanner will search a way where `distance/priority` is minimal.");
            AddTagsTable(reference, TagsWithPriorityInfluence().Values.PossibleTagsRecursive(_c), residentialTagsStr);

            md.AddTitle("Tags influencing speed", 2);
            md.Add(
                "Speed is used to calculate how long the trip will take, but does _not_ influence which route is taken. Some profiles do use speed as a factor in priority too - in this case, these tags will be mentioned above too.");
            AddTagsTable(reference, _profile.Speed.PossibleTagsRecursive(_c), residentialTagsStr);

            md.AddTitle("Tags influencing access", 2);
            md.Add("These tags influence whether or not this road can be taken with this vehicle or behaviour");
            AddTagsTable(reference, _profile.Access.PossibleTagsRecursive(_c), residentialTagsStr);
            md.AddTitle("Tags influencing oneway", 2);
            md.Add("These tags influence whether or not this road can be taken in all directions or not");

            AddTagsTable(reference, _profile.Oneway.PossibleTagsRecursive(_c), residentialTagsStr);


            return md.ToString();
        }
    }
}