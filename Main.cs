using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Albion
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class SentenceAttribute : Attribute
    {
        public SentenceAttribute(string _sentence)
        {
            this.sentence = _sentence;
        }

        protected string sentence;
        public string Sentence { get { return this.sentence; } }

        protected bool converters = false;
        public bool Converters { get { return this.converters; } set { this.converters = value; } }

        protected string id = "";
        public string ID { get { return this.id; } set { this.id = value; } }
    }

    /// <summary>
    /// The ID corresponds to an identifier used to call results
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ExtensionAttribute : Attribute
    {
        public ExtensionAttribute() { }

        protected string id = "";
        public string ID { get { return this.id; } set { this.id = value; } }
    }

    /// <summary>
    /// Convert the string value using the specified converter.
    /// The converter must accept a string and should return the parameter type
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
    public class AConvertAttribute : Attribute
    {
        public AConvertAttribute(string methodname) 
        {
            MethodName = methodname;
        }

        public string MethodName { get; protected set; }

        protected Type converters = typeof(Convert);
        public Type Converter { get { return converters; } set { converters = value; } }

        public MethodInfo ConvertMethod
        {
            get
            {
                MethodInfo r = Converter.GetMethods().First(x => x.Name == MethodName && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType == typeof(string));
                if (r == null) throw new Exception("The specified method does not exist.");
                else return r;
            }
        }
    }

    public class Answer
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public bool Failed { get; set; }
        public Type Returns { get; set; }
        public Exception Error { get; set; }
        private MethodInfo Infos { get; set; }
        public string ExtensionID { get; set; }
        private Dictionary<string, string> Parameters { get; set; }

        public Answer(Exception err)
        {
            Failed = true;
            Error = err;
            Returns = null;
            Infos = null;
            Parameters = null;
            Name = null;
            ExtensionID = null;
            ID = null;
        }

        public Answer(MethodInfo info, Dictionary<string, string> pa, int i)
        {
            Returns = info.ReturnType;
            Infos = info;
            Parameters = pa;
            Name = info.Name;
            ExtensionID = (info.DeclaringType.GetCustomAttributes(typeof(ExtensionAttribute), false)[0] as ExtensionAttribute).ID;
            ID = (info.GetCustomAttributes(typeof(SentenceAttribute), false)[i] as SentenceAttribute).ID;
            Error = null;
            Failed = false;
        }

        /// <summary>
        /// Call the static method.
        /// </summary>
        /// <returns>What the method returns (this.Returns)</returns>
        public object Call()
        {
            List<dynamic> param = new List<dynamic>();
            foreach (var i in Infos.GetParameters())
            {
                bool isCustom = false;
                foreach (var o in i.GetCustomAttributes(typeof(AConvertAttribute), false))
                {
                    isCustom = true;
                    AConvertAttribute a = o as AConvertAttribute;
                    if (a.ConvertMethod != null && a.ConvertMethod.ReturnType == i.ParameterType)
                        param.Add(a.ConvertMethod.Invoke(null, new string[1] { Parameters[i.Name] }));
                }
                if (!isCustom) param.Add(Parameters[i.Name]);
            }
            return Infos.Invoke(null, param.ToArray());
        }
    }

    public class Engine
    {
        private List<Type> Extensions { get; set; }

        /// <summary>
        /// Initialize an Albion Engine, used to translate commands.
        /// </summary>
        public Engine()
        {
            Extensions = new List<Type>();
        }

        /// <summary>
        /// Add the specified extensions to this instance of Albion.
        /// </summary>
        /// <param name="ts">Types of classes (must have the Extension Attribute)</param>
        /// <returns>The number of added extensions</returns>
        public int AddExtensions(params Type[] ts)
        {
            int extensions = Extensions.Count;
            foreach (Type t in ts)
                if (t.GetCustomAttributes(typeof(ExtensionAttribute), false).Length == 1 && t.IsClass) Extensions.Add(t);
            return Extensions.Count - extensions;
        }

        /// <summary>
        /// Translates a sentence to a method.
        /// </summary>
        /// <param name="input">The sentence entered by the user.</param>
        /// <returns>Nothing yet</returns>
        public Answer Ex(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) return new Answer(new Exception("The input mustn't be empty."));
            List<Answer> Possibilities = new List<Answer>();
            foreach (Type t in Extensions)
                Possibilities.Add(_Ex(t, input));
            return Possibilities.Where(x => x != null).FirstOrDefault(x => !x.Failed);
        }

        private Answer _Ex(Type type, string input)
        {
            List<string> p = (from m in type.GetMethods()
                              from a in m.GetCustomAttributes(typeof(SentenceAttribute), false)
                              where a != null && !String.IsNullOrWhiteSpace((a as SentenceAttribute).Sentence)
                              select (a as SentenceAttribute).Sentence)
                              .ToList<string>();

            input = input.Trim();

            int _test = 0;
            foreach (string s in p)
            {
                MatchCollection one = Regex.Matches(s, @"{[\w\d:]+}|([\w\s\d'-]+)");

                string reg = "";
                foreach (Match m in one) if (!reg.EndsWith(m.Value) && !m.Value.EndsWith("}")) reg += m.Value + "|";
                reg = reg.Substring(0, reg.Length - 1);

                if (!reg.Contains("|"))
                {
                    if (reg.ToLower() != input.ToLower()) continue;
                    else 
                    {
                        Sentence esentence = new Sentence(type, input, s, new Dictionary<string, string>(0));
                        var emethod = esentence.Method;
                        if (emethod == null) continue;
                        else
                        {
                            int e__test = _test;
                            _test++;
                            Dictionary<string, string> vs = new Dictionary<string, string>(0);

                            return new Answer(emethod, vs, e__test);
                        }
                    };
                }
                if (Regex.Replace(input, ".*?" + reg.Replace("|", ".*?") + ".*", "").Trim() != "") 
                    continue;

                string r = Regex.Replace(input, reg, "{");
                string[] matches = r.Split('{');
                matches = matches.Where(x => !String.IsNullOrWhiteSpace(x)).Select(x => x).ToArray();

                Dictionary<string, string> dic = new Dictionary<string, string>(matches.Length);
                string[] _matches = Regex.Matches(s, @"{([\w\d:]+)}", RegexOptions.IgnoreCase).Cast<Match>().Select(x => x.Value.Substring(1, x.Value.Length - 2)).ToArray();
                if (_matches.Length != matches.Length || matches.Length != one.Cast<Match>().Count(x => x.Value.EndsWith("}"))) continue;

                for (int i = 0; i < matches.Length; i++)
                    dic.Add(_matches[i], matches[i]);

                Sentence sentence = new Sentence(type, input, s, dic);
                int __test = _test;
                _test++;

                var method = sentence.Method;
                if (method == null) continue;
                else
                {
                    if (method.GetParameters().Length != sentence.Variables.Count)
                        continue;
                    List<string> l = new List<string>();
                    Dictionary<string, string> vs = sentence.Variables.ToDictionary(x => Regex.Replace(x.Key, ":.+", ""), x => x.Value);

                    foreach (var pa in method.GetParameters())
                        if (vs.ContainsKey(pa.Name)) l.Add(vs[pa.Name]);
                        else continue;
                    if (l.Count != vs.Count) continue;
                    return new Answer(method, vs, __test);
                }
            }

            return null;
        }
    }

    //todo: Extension Attribute

    public class Sentence
    {
        public Sentence(Type type, string full, string template, Dictionary<string, string> variables)
        {
            Full = full;
            Template = template;
            Variables = variables;
            Ty = type;
            Method = FindMethod();
        }

        public string Full { get; private set; }
        public string Template { get; private set; }
        public Dictionary<string, string> Variables { get; private set; }
        private Type Ty { get; set; }
        public MethodInfo Method { get; private set; }

        private MethodInfo FindMethod()
        {
            foreach (var i in Ty.GetMethods())
                foreach (var o in i.GetCustomAttributes(typeof(SentenceAttribute), false))
                    if ((o as SentenceAttribute).Sentence == Template)
                        return i;

            return null;
        }
    }

    public static class Convert
    {
        /// <summary>
        /// Converts a string to an object.
        /// </summary>
        /// <param name="v">{Key}</param>
        /// <param name="r">Value</param>
        /// <returns></returns>
        public static dynamic All(string v, string r)
        {
            string t = Regex.Match(v, @".*?(?=:)").Value.ToLower().Trim();
            if (String.IsNullOrWhiteSpace(t)) throw new Exception("Invalid convertion format.");

            string tt = Regex.Replace(v, ".*?:", "").ToUpper().ToCharArray()[0] + Regex.Replace(v, ".*?:", "").ToLower().Substring(1);

            var m = typeof(Convert).GetMethod(tt);
            if (m == null) throw new Exception("The method '" + tt + "' doesn't exist.");
            else return m.Invoke(null, new string[] { r });
        }

        private static int Digit(string w)
        {
            w = w.Replace("fort", "fourt").Replace("nint", "ninet");
            List<string> s = w.Split(new string[1] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();

            List<string> i = new List<string>() 
                { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

            List<string> ii = new List<string>() 
                { "o", "ten", "twenty", "thirty", "fourty", "fifty", "sixty", "seventy", "eighty", "ninety" };

            List<string> exc = new List<string>() 
                { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };

            if (exc.IndexOf(w) >= 0) return exc.IndexOf(w) + 10;
            else if (s.Count > 2 || s.Count < 1) return -1;
            else if (s.Count == 1)
            {
                if (ii.IndexOf(s[0]) > -1) s.Add("zero");
                else if (i.IndexOf(s[0]) > -1) s.Insert(0, "o");
                else return -1;
            }

            int iii = Int32.Parse(ii.IndexOf(s[0]).ToString() + i.IndexOf(s[1]).ToString());

            return iii;
        }

        /// <summary>
        /// Converts a string to an integer (up to 999 999).
        /// </summary>
        /// <param name="s">A string that'll be filtered.</param>
        /// <returns>null if incorrect format, or the int corresponding</returns>
        public static int Number(string s)
        {
            // extract the number
            s = Regex.Replace(s, @"[^a-zA-Z\s]", " ").ToLower();
            s = Regex.Replace(s, " +", " ");
            List<string> keeps = new List<string>()
                { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
                  "ten", "twenty", "thirty", "fourty", "fifty", "sixty", "seventy", "eighty", "ninety",
                  "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
                  "hundred", "thousand" };
            string[] splitted = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string keep in splitted)
                if (keeps.IndexOf(keep) < 0) s = Regex.Replace(s, String.Format(@"[^\w]{0}[^\w]|^{0}|{0}$", keep), " ");

            splitted = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (splitted.Length == 0) return -1;
            if (splitted.Count(x => x == "zero") > 0)
                if (splitted.Count(x => x == "zero") > 1) return -1;
                else if (splitted.Length > 1) return -1;
            if (splitted.Count(x => x == "hundred") > 2 || splitted.Count(x => x == "thousand") > 1) return -1;

            MatchCollection m = Regex.Matches(s, @"(?<ht>.*?)hundred(?=.*thousand)|(?<th>.*?)thousand|(?<hu>.*?)hundred|(?<in>.+?)$");

            var ght = m.Cast<Match>().Select(x => x.Groups["ht"]).FirstOrDefault(y => y.Value != "");
            string _ht = (ght == null) ? "" : ght.Value.Trim();
            var gth = m.Cast<Match>().Select(x => x.Groups["th"]).FirstOrDefault(y => y.Value != "");
            string _th = (gth == null) ? "" : gth.Value.Trim();
            var ghu = m.Cast<Match>().Select(x => x.Groups["hu"]).FirstOrDefault(y => y.Value != "");
            string _hu = (ghu == null) ? "" : ghu.Value.Trim();
            var gin = m.Cast<Match>().Select(x => x.Groups["in"]).FirstOrDefault(y => y.Value != "");
            string _in = (gin == null) ? "" : gin.Value.Trim();

            int hundredthousands = Digit(_ht);
            hundredthousands = (hundredthousands == -1) ? (Regex.IsMatch(s, @"(?<ht>.*?)hundred(?=.*thousand)") ? 1 : 0) : hundredthousands;
            int thousands = Digit(_th);
            thousands = (thousands == -1) ? (Regex.IsMatch(s, "thousand") ? 1 : 0) : thousands;
            int hundreds = Digit(_hu);
            hundreds = (hundreds == -1) ? (Regex.IsMatch(s, @"hundred") ? 1 : 0) : hundreds;
            int numbers = Digit(_in);
            numbers = (numbers == -1) ? 0 : numbers;

            return (hundredthousands * 100000) + (thousands * 1000) + (hundreds * 100) + (numbers) * (s.Contains("minus") ? -1 : 1);
        }

        private static string Digit(int i)
        {
            List<string> ls1 = new List<string>() 
                { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

            List<string> ls2 = new List<string>() 
                { "zero", "ten", "twenty", "thirty", "fourty", "fifty", "sixty", "seventy", "eighty", "ninety" };

            List<string> exc = new List<string>() 
                { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };

            if (i > 99 || i < 0) return null;
            else if (i < 10) return ls1[i];
            else if (i % 10 == 0) return ls2[i / 10];
            else if (exc.Count > i - 10) return exc[i - 10];
            else
            {
                int i1 = Int32.Parse(i.ToString().Substring(0, 1));
                int i2 = Int32.Parse(i.ToString().Substring(1, 1));
                return ls2[i1] + "-" + ls1[i2];
            }
        }

        public static string Number(int i)
        {
            if (i > 999999) return null;
            string s = i.ToString("000000");
            string digits = Digit(Int32.Parse(s.Substring(4)));
            string hundred = (s.Substring(3, 1) == "0") ? " " : Digit(Int32.Parse(s.Substring(3, 1)));
            string thousand = (s.Substring(1, 2) == "00") ? " " : Digit(Int32.Parse(s.Substring(1, 2)));
            string hundredthousands = (s.Substring(0, 1) == "0") ? " " : Digit(Int32.Parse(s.Substring(0, 1)));
            string finale = ((hundredthousands == " ") ? "" : hundredthousands + " hundred ")
                          + ((thousand == " ") ? "" : thousand + " hundred ")
                          + ((hundred == " ") ? "" : hundred + " hundred ")
                          + ((digits == "zero") ? "" : digits);
            return Regex.Replace(finale, " +", " ");
        }

        public static TimeSpan In(string r)
        {
            // some regex should to the trick
            Match matches = Regex.Match(r, @"(\d{1,3}) minutes?|(\d{1,3}) hours?|(\d{1,3}) days?|(\d{1,3}) weeks?", RegexOptions.IgnoreCase);
            int minutes = (!String.IsNullOrEmpty(matches.Groups[1].Value)) ? Int32.Parse(matches.Groups[1].Value) : 0;
            int hours = (!String.IsNullOrEmpty(matches.Groups[2].Value)) ? Int32.Parse(matches.Groups[2].Value) : 0;
            int days = (!String.IsNullOrEmpty(matches.Groups[3].Value)) ? Int32.Parse(matches.Groups[3].Value) : 0;
            int weeks = (!String.IsNullOrEmpty(matches.Groups[4].Value)) ? Int32.Parse(matches.Groups[4].Value) : 0;
            days += weeks * 7;
            return new TimeSpan(days, hours, minutes, 0);
        }
    }
}
