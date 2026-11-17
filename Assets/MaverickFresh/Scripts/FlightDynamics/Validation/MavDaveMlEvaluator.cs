#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace MaverickFresh.FlightDynamics.Validation
{
    /// <summary>
    /// VALIDATION-ONLY, minimal, independent evaluator for ANSI/AIAA S-119 (DAVE-ML 2.0) function
    /// files, written to interrogate NASA's NESC F-16 aerodynamic model without porting it by hand.
    ///
    /// Supported subset (exactly what F16_aero.dml uses; anything else throws, never guesses):
    ///   variableDef: initialValue constants, inputs with minValue/maxValue limits, MathML calculations
    ///   MathML: ci, cn, apply with plus, minus (unary/binary), times, divide, power, abs, lt,
    ///           piecewise/piece/otherwise
    ///   breakpointDef, griddedTableDef (row-major, last breakpoint fastest),
    ///   function with independentVarRef (min/max, extrapolate="neither" = hold at the limits),
    ///           dependentVarRef, functionDefn holding an inline griddedTableDef or griddedTableRef;
    ///           n-linear interpolation
    ///   checkData/staticShot: checkInputs (by signal name), checkOutputs (by signal name, with tol)
    ///
    /// It shares no code with Maverick's aerodynamic models, so a check-shot that passes here
    /// verifies this evaluator against NASA's own numbers, not against Maverick.
    /// </summary>
    public sealed class MavDaveMlEvaluator
    {
        private sealed class VarDef
        {
            public string id, name;
            public double? initial, min, max;
            public XmlElement math;
            public bool isInput;
        }

        private sealed class Table
        {
            public string[] bpIds;
            public double[] data;
        }

        private sealed class Function
        {
            public string[] indepIds;
            public double[] mins, maxs;
            public bool[] hold;
            public string depId;
            public Table table;
        }

        public sealed class CheckSignal
        {
            public string name;
            public double value;
            public double tol;
        }

        public sealed class StaticShot
        {
            public string name;
            public List<CheckSignal> inputs = new List<CheckSignal>();
            public List<CheckSignal> outputs = new List<CheckSignal>();
        }

        private readonly Dictionary<string, VarDef> vars = new Dictionary<string, VarDef>();
        private readonly Dictionary<string, string> nameToId = new Dictionary<string, string>();
        private readonly Dictionary<string, double[]> breakpoints = new Dictionary<string, double[]>();
        private readonly Dictionary<string, Table> tables = new Dictionary<string, Table>();
        private readonly Dictionary<string, Function> functionsByOutput = new Dictionary<string, Function>();
        public readonly List<StaticShot> StaticShots = new List<StaticShot>();

        private Dictionary<string, double> inputs = new Dictionary<string, double>();
        private Dictionary<string, double> cache = new Dictionary<string, double>();
        private readonly HashSet<string> evaluating = new HashSet<string>();

        public static MavDaveMlEvaluator Load(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(new System.IO.StringReader(xml), settings))
                doc.Load(reader);
            MavDaveMlEvaluator e = new MavDaveMlEvaluator();
            e.Parse(doc.DocumentElement);
            return e;
        }

        private static IEnumerable<XmlElement> Children(XmlNode n)
        {
            foreach (XmlNode c in n.ChildNodes)
                if (c is XmlElement el)
                    yield return el;
        }

        private static XmlElement Child(XmlNode n, string localName)
        {
            foreach (XmlElement c in Children(n))
                if (c.LocalName == localName)
                    return c;
            return null;
        }

        private static double? Attr(XmlElement e, string name)
        {
            string s = e.GetAttribute(name);
            if (string.IsNullOrEmpty(s)) return null;
            return double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static double[] Numbers(string text)
        {
            string[] parts = text.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            double[] v = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                v[i] = double.Parse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture);
            return v;
        }

        private static string TextWithoutComments(XmlElement e)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (XmlNode c in e.ChildNodes)
                if (c.NodeType == XmlNodeType.Text || c.NodeType == XmlNodeType.CDATA)
                    sb.Append(c.Value).Append(' ');
            return sb.ToString();
        }

        private Table ParseTable(XmlElement gt)
        {
            List<string> bps = new List<string>();
            foreach (XmlElement r in Children(Child(gt, "breakpointRefs")))
                bps.Add(r.GetAttribute("bpID"));
            return new Table { bpIds = bps.ToArray(), data = Numbers(TextWithoutComments(Child(gt, "dataTable"))) };
        }

        private void Parse(XmlElement root)
        {
            foreach (XmlElement e in Children(root))
            {
                switch (e.LocalName)
                {
                    case "variableDef":
                    {
                        VarDef v = new VarDef
                        {
                            id = e.GetAttribute("varID"),
                            name = e.GetAttribute("name"),
                            initial = Attr(e, "initialValue"),
                            min = Attr(e, "minValue"),
                            max = Attr(e, "maxValue"),
                            isInput = Child(e, "isInput") != null
                        };
                        XmlElement calc = Child(e, "calculation");
                        if (calc != null)
                            v.math = Child(calc, "math");
                        vars[v.id] = v;
                        nameToId[v.name] = v.id;
                        break;
                    }
                    case "breakpointDef":
                        breakpoints[e.GetAttribute("bpID")] = Numbers(Child(e, "bpVals").InnerText);
                        break;
                    case "griddedTableDef":
                        tables[e.GetAttribute("gtID")] = ParseTable(e);
                        break;
                    case "function":
                    {
                        Function f = new Function();
                        List<string> ids = new List<string>();
                        List<double> mins = new List<double>(), maxs = new List<double>();
                        List<bool> hold = new List<bool>();
                        foreach (XmlElement c in Children(e))
                        {
                            if (c.LocalName == "independentVarRef")
                            {
                                ids.Add(c.GetAttribute("varID"));
                                mins.Add(Attr(c, "min") ?? double.NegativeInfinity);
                                maxs.Add(Attr(c, "max") ?? double.PositiveInfinity);
                                string x = c.GetAttribute("extrapolate");
                                if (x != "neither" && x != "")
                                    throw new NotSupportedException("extrapolate=" + x);
                                hold.Add(true);
                            }
                            else if (c.LocalName == "dependentVarRef")
                                f.depId = c.GetAttribute("varID");
                            else if (c.LocalName == "functionDefn")
                            {
                                XmlElement gt = Child(c, "griddedTableDef");
                                XmlElement gr = Child(c, "griddedTableRef");
                                if (gt != null)
                                {
                                    f.table = ParseTable(gt);
                                    string gtId = gt.GetAttribute("gtID");
                                    if (!string.IsNullOrEmpty(gtId)) tables[gtId] = f.table;
                                }
                                else if (gr != null)
                                    f.table = tables[gr.GetAttribute("gtID")];
                                else
                                    throw new NotSupportedException("functionDefn without a gridded table");
                            }
                        }
                        f.indepIds = ids.ToArray();
                        f.mins = mins.ToArray();
                        f.maxs = maxs.ToArray();
                        f.hold = hold.ToArray();
                        functionsByOutput[f.depId] = f;
                        break;
                    }
                    case "checkData":
                        foreach (XmlElement shotEl in Children(e))
                        {
                            if (shotEl.LocalName != "staticShot") continue;
                            StaticShot shot = new StaticShot { name = shotEl.GetAttribute("name") };
                            ReadSignals(Child(shotEl, "checkInputs"), shot.inputs);
                            ReadSignals(Child(shotEl, "checkOutputs"), shot.outputs);
                            StaticShots.Add(shot);
                        }
                        break;
                }
            }
        }

        private static void ReadSignals(XmlElement parent, List<CheckSignal> into)
        {
            if (parent == null) return;
            foreach (XmlElement s in Children(parent))
            {
                if (s.LocalName != "signal") continue;
                XmlElement n = Child(s, "signalName");
                XmlElement v = Child(s, "signalValue");
                XmlElement t = Child(s, "tol");
                into.Add(new CheckSignal
                {
                    name = n != null ? n.InnerText.Trim() : "",
                    value = double.Parse(v.InnerText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture),
                    tol = t != null ? double.Parse(t.InnerText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture) : 0.0
                });
            }
        }

        public string IdForName(string name)
        {
            string id;
            return nameToId.TryGetValue(name, out id) ? id : name;
        }

        /// <summary>Sets inputs by varID (or by variable name) and clears the evaluation cache.</summary>
        public void SetInputs(Dictionary<string, double> byIdOrName)
        {
            inputs = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in byIdOrName)
                inputs[vars.ContainsKey(kv.Key) ? kv.Key : IdForName(kv.Key)] = kv.Value;
            cache = new Dictionary<string, double>();
        }

        public double Get(string idOrName)
        {
            string id = vars.ContainsKey(idOrName) ? idOrName : IdForName(idOrName);
            double v;
            if (cache.TryGetValue(id, out v)) return v;
            if (!evaluating.Add(id)) throw new InvalidOperationException("cyclic definition at " + id);
            try
            {
                VarDef d;
                if (!vars.TryGetValue(id, out d)) throw new KeyNotFoundException("unknown variable " + id);
                if (inputs.TryGetValue(id, out v)) { }
                else if (functionsByOutput.ContainsKey(id)) v = EvaluateFunction(functionsByOutput[id]);
                else if (d.math != null) v = EvalMath(FirstElement(d.math));
                else if (d.initial.HasValue) v = d.initial.Value;
                else throw new InvalidOperationException("no value for input " + id);
                if (d.min.HasValue && v < d.min.Value) v = d.min.Value;
                if (d.max.HasValue && v > d.max.Value) v = d.max.Value;
                cache[id] = v;
                return v;
            }
            finally
            {
                evaluating.Remove(id);
            }
        }

        private static XmlElement FirstElement(XmlNode n)
        {
            foreach (XmlElement e in Children(n)) return e;
            throw new InvalidOperationException("empty MathML");
        }

        private double EvalMath(XmlElement e)
        {
            switch (e.LocalName)
            {
                case "ci": return Get(e.InnerText.Trim());
                case "cn": return double.Parse(e.InnerText.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
                case "piecewise": return EvalPiecewise(e);
                case "apply": break;
                default: throw new NotSupportedException("MathML element " + e.LocalName);
            }
            List<XmlElement> kids = new List<XmlElement>(Children(e));
            string op = kids[0].LocalName;
            if (op == "piecewise") return EvalPiecewise(kids[0]);
            List<double> a = new List<double>();
            for (int i = 1; i < kids.Count; i++) a.Add(EvalMath(kids[i]));
            switch (op)
            {
                case "plus": { double s = 0; foreach (double x in a) s += x; return s; }
                case "times": { double p = 1; foreach (double x in a) p *= x; return p; }
                case "minus": return a.Count == 1 ? -a[0] : a[0] - a[1];
                case "divide": return a[0] / a[1];
                case "power": return Math.Pow(a[0], a[1]);
                case "abs": return Math.Abs(a[0]);
                case "lt": return a[0] < a[1] ? 1.0 : 0.0;
                default: throw new NotSupportedException("MathML operator " + op);
            }
        }

        private double EvalPiecewise(XmlElement pw)
        {
            foreach (XmlElement c in Children(pw))
            {
                if (c.LocalName == "piece")
                {
                    List<XmlElement> k = new List<XmlElement>(Children(c));
                    if (EvalMath(k[1]) != 0.0) return EvalMath(k[0]);
                }
                else if (c.LocalName == "otherwise")
                    return EvalMath(FirstElement(c));
            }
            throw new InvalidOperationException("piecewise without a matching piece or otherwise");
        }

        private double EvaluateFunction(Function f)
        {
            int n = f.indepIds.Length;
            double[][] bp = new double[n][];
            double[] x = new double[n];
            for (int i = 0; i < n; i++)
            {
                bp[i] = breakpoints[f.table.bpIds[i]];
                double v = Get(f.indepIds[i]);
                v = Math.Max(f.mins[i], Math.Min(f.maxs[i], v));
                v = Math.Max(bp[i][0], Math.Min(bp[i][bp[i].Length - 1], v)); // "neither": hold at the table edge
                x[i] = v;
            }
            int[] lo = new int[n];
            double[] frac = new double[n];
            for (int i = 0; i < n; i++)
            {
                double[] b = bp[i];
                int j = 0;
                while (j < b.Length - 2 && x[i] > b[j + 1]) j++;
                lo[i] = j;
                frac[i] = b.Length == 1 ? 0.0 : (x[i] - b[j]) / (b[j + 1] - b[j]);
            }
            int[] stride = new int[n];
            stride[n - 1] = 1;
            for (int i = n - 2; i >= 0; i--) stride[i] = stride[i + 1] * bp[i + 1].Length;
            double sum = 0.0;
            for (int corner = 0; corner < (1 << n); corner++)
            {
                double w = 1.0;
                int idx = 0;
                for (int i = 0; i < n; i++)
                {
                    bool up = ((corner >> i) & 1) == 1;
                    w *= up ? frac[i] : 1.0 - frac[i];
                    idx += (lo[i] + (up ? 1 : 0)) * stride[i];
                }
                if (w != 0.0) sum += w * f.table.data[idx];
            }
            return sum;
        }
    }
}
#endif
