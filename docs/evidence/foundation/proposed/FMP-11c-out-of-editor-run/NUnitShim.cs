using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Minimal NUnit-compatible surface covering exactly what the FMP-11c test file uses.
// THIS IS NOT NUnit. No categories, no NUnit3 XML, no [OneTimeSetUp], no Unity editor environment.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TearDownAttribute : Attribute { }

    public delegate void TestDelegate();

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public class Constraint
    {
        public Func<object, string> Rule;
        public virtual string Failure(object actual) => Rule(actual);
        public virtual Constraint Within(double tolerance) => this;
        public virtual Constraint Within(float tolerance) => this;
    }

    public sealed class EqualConstraint : Constraint
    {
        public object Expected;
        public double? Tolerance;
        public override Constraint Within(double t) { Tolerance = t; return this; }
        public override string Failure(object actual) =>
            Utils.LooseEquals(actual, Expected, Tolerance) ? null : "Expected: " + Utils.Show(Expected) + "  But was: " + Utils.Show(actual);
    }

    public static class Utils
    {
        internal static bool LooseEquals(object a, object b) => LooseEquals(a, b, null);

        internal static bool LooseEquals(object a, object b, double? tolerance)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is bool || b is bool) return Equals(a, b);
            bool an = IsNumeric(a), bn = IsNumeric(b);
            if (an && bn)
            {
                if (tolerance.HasValue) return Math.Abs(Convert.ToDouble(a) - Convert.ToDouble(b)) <= tolerance.Value;
                if (IsIntegral(a) && IsIntegral(b)) return Convert.ToInt64(a) == Convert.ToInt64(b);
                return Convert.ToDouble(a) == Convert.ToDouble(b);
            }
            if (a is IEnumerable ea && b is IEnumerable eb && !(a is string) && !(b is string))
                return ea.Cast<object>().SequenceEqual(eb.Cast<object>());
            return Equals(a, b);
        }

        internal static bool IsNumeric(object o) => IsIntegral(o) || o is float || o is double || o is decimal;
        internal static bool IsIntegral(object o) => o is sbyte || o is byte || o is short || o is ushort
            || o is int || o is uint || o is long || o is ulong;

        internal static int Compare(object a, object b)
        {
            if (a == null || b == null) throw new AssertionException("Compare on null");
            return Convert.ToDouble(a).CompareTo(Convert.ToDouble(b));
        }

        internal static int LengthOf(object c)
        {
            if (c == null) return -1;
            if (c is string s) return s.Length;
            if (c is ICollection col) return col.Count;
            if (c is IEnumerable e) return e.Cast<object>().Count();
            throw new AssertionException("LengthOf on non-collection " + c.GetType());
        }

        internal static bool Contains(object container, object expected)
        {
            if (container == null) return false;
            if (container is string s) return expected is string sub && s.IndexOf(sub, StringComparison.Ordinal) >= 0;
            if (container is IEnumerable e) return e.Cast<object>().Any(x => LooseEquals(x, expected, null));
            throw new AssertionException("Contains on non-collection " + container.GetType());
        }

        internal static string Show(object v)
        {
            if (v == null) return "null";
            if (v is string s) return "\"" + s + "\"";
            if (v is IEnumerable e) return "[" + string.Join(", ", e.Cast<object>().Select(Show)) + "]";
            return v.ToString();
        }
    }

    internal static class Mk
    {
        public static Constraint C(Func<object, string> rule) => new Constraint { Rule = rule };
    }

    public static class Is
    {
        public static EqualConstraint EqualTo(object expected) => new EqualConstraint { Expected = expected };

        public static Constraint GreaterThan(object bound) => Mk.C(c => Utils.Compare(c, bound) > 0 ? null : "Expected: > " + Utils.Show(bound) + "  But was: " + Utils.Show(c));

        public static Constraint LessThan(object bound) => Mk.C(c => Utils.Compare(c, bound) < 0 ? null : "Expected: < " + Utils.Show(bound) + "  But was: " + Utils.Show(c));

        public static Constraint True => Mk.C(c => c is bool b && b ? null : "Expected: True  But was: " + Utils.Show(c));

        public static Constraint False => Mk.C(c => c is bool b && !b ? null : "Expected: False  But was: " + Utils.Show(c));

        public static Constraint Empty => Mk.C(c => Utils.LengthOf(c) == 0 ? null : "Expected: empty  But was: " + Utils.Show(c));

        public sealed class NotOperator
        {
            public Constraint Null => Mk.C(c => c != null ? null : "Expected: not null");
            public Constraint Empty => Mk.C(c => Utils.LengthOf(c) != 0 ? null : "Expected: not empty");
            public Constraint EqualTo(object expected) => Mk.C(c => !Utils.LooseEquals(c, expected) ? null : "Expected: not equal to " + Utils.Show(expected));
        }

        public static NotOperator Not => new NotOperator();

        public sealed class LengthOperator
        {
            public Constraint EqualTo(int n) => Mk.C(c => Utils.LengthOf(c) == n ? null : "Expected length: " + n + "  But was: " + Utils.LengthOf(c));
            public Constraint GreaterThan(int n) => Mk.C(c => Utils.LengthOf(c) > n ? null : "Expected length > " + n + "  But was: " + Utils.LengthOf(c));
        }

        public static LengthOperator Length => new LengthOperator();

        // Order-insensitive multiset equality, matching NUnit's Is.EquivalentTo for the flat
        // string/bool collections this test file compares.
        public static Constraint EquivalentTo(IEnumerable expected)
        {
            var want = expected == null ? new List<object>() : expected.Cast<object>().ToList();
            return Mk.C(c =>
            {
                var have = c == null ? new List<object>() : ((IEnumerable)c).Cast<object>().ToList();
                if (have.Count != want.Count) return "Expected: equivalent to " + Utils.Show(want) + "  But was: " + Utils.Show(have);
                var pool = new List<object>(have);
                foreach (var w in want)
                {
                    var hit = pool.FirstOrDefault(x => Utils.LooseEquals(x, w, null));
                    if (hit == null && !(w == null && pool.Contains(null))) return "Expected: equivalent to " + Utils.Show(want) + "  But was: " + Utils.Show(have);
                    pool.Remove(hit);
                }
                return null;
            });
        }
    }

    public static class Has
    {
        public static Is.LengthOperator Length => new Is.LengthOperator();
    }

    public static class Does
    {
        public static Constraint Contain(object expected) => Mk.C(c => Utils.Contains(c, expected) ? null : "Expected: contains " + Utils.Show(expected));

        public sealed class NotOperator
        {
            public Constraint Contain(object expected) => Mk.C(c => !Utils.Contains(c, expected) ? null : "Expected: does not contain " + Utils.Show(expected));
        }

        public static NotOperator Not => new NotOperator();
    }

    public static class Assert
    {
        public static void That(object actual, Constraint constraint) => That(actual, constraint, null);

        public static void That(object actual, Constraint constraint, string message, params object[] args)
        {
            var failure = constraint.Failure(actual);
            if (failure == null) return;
            var suffix = string.IsNullOrEmpty(message) ? "" : " -- " + (args.Length == 0 ? message : string.Format(message, args));
            throw new AssertionException(failure + suffix);
        }

        public static T Throws<T>(TestDelegate code) where T : Exception => Throws<T>(code, null);

        public static T Throws<T>(TestDelegate code, string message, params object[] args) where T : Exception
        {
            try { code(); }
            catch (T expected) { return expected; }
            catch (Exception other)
            {
                throw new AssertionException("Expected " + typeof(T).Name + " but got " + other.GetType().Name + ": " + other.Message);
            }
            throw new AssertionException("Expected " + typeof(T).Name + " but no exception was thrown");
        }
    }
}
