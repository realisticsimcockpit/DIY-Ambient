using System;
using System.Collections.Generic;
using System.Reflection;

namespace DIYAmbient.Core
{
    // Read only documented/observed normalized member names selected by the adapter.
    // This does NOT invent support for a simulator or inspect raw game structures.
    public sealed class TelemetryFlagReader
    {
        private readonly object gate = new object();
        private readonly Dictionary<Type, Dictionary<string, MemberInfo>> members =
            new Dictionary<Type, Dictionary<string, MemberInfo>>();

        public bool Read(object data, string name, out bool available)
        {
            available = false;
            if (data == null || string.IsNullOrEmpty(name)) return false;
            try
            {
                MemberInfo member;
                Type type = data.GetType();
                lock (gate)
                {
                    Dictionary<string, MemberInfo> byName;
                    if (!members.TryGetValue(type, out byName))
                    { byName = new Dictionary<string, MemberInfo>(); members.Add(type, byName); }
                    if (!byName.TryGetValue(name, out member))
                    {
                        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                        member = property != null && property.CanRead && property.GetIndexParameters().Length == 0 ?
                            (MemberInfo)property : type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                        byName.Add(name, member);
                    }
                }
                if (member == null) return false;
                object value = member is PropertyInfo ? ((PropertyInfo)member).GetValue(data, null) : ((FieldInfo)member).GetValue(data);
                if (value == null) return false;
                if (value is bool) { available = true; return (bool)value; }
                // An unknown enum (including unnamed numeric values), string or multi-state
                // numeric value is unavailable, not a silently supported false/true flag.
                if (value.GetType().IsEnum) return false;
                if (!(value is byte || value is sbyte || value is short || value is ushort ||
                    value is int || value is uint || value is long || value is ulong ||
                    value is float || value is double || value is decimal)) return false;
                double number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                if (number != 0 && number != 1) return false;
                available = true; return number == 1;
            }
            catch
            {
                // A throwing getter must not escape onto SimHub's critical update path.
                available = false; return false;
            }
        }
    }
}
