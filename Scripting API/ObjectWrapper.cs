using KarlsonMapEditor.Automata.Backbone;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.Scripting_API
{
    public class NativeValue : ObjectValue
    {
        public object Object;

        public NativeValue(object obj)
        {
            Object = obj;
        }

        public override BaseValue GetChild(string name)
        {
            return Object.GetMember(name).Wrap();
        }

        public override void SetChild(string name, BaseValue value)
        {
            Object.SetMember(name, value.Unwrap(Object.GetMemberType(name)));
        }

        public override bool IsArrayConvention() => false;
        public override StringValue Stringify(int indent) => new StringValue("<NativeValue>" + Object.ToString());
        public override bool Equals(BaseValue other)
        {
            if (Object == null && other.Type == ValueType.Nil) return true;
            if (!(other is NativeValue nv)) return false;
            return nv.Object == Object;
        }
    }
    public class ReadonlyValue : ObjectValue
    {
        Dictionary<string, object> Reference;
        bool isArrayRef = false;

        public ReadonlyValue(Dictionary<string, object> values)
        {
            Reference = values;
        }
        public ReadonlyValue(object[] array) : this(new Dictionary<string, object>())
        {
            isArrayRef = true;
            Reference["length"] = array.Length;
            for (int i = 0; i < array.Length; i++)
                Reference[i.ToString()] = array[i];
        }

        public override BaseValue GetChild(string name)
        {
            return Reference[name].Wrap();
        }
        public override void SetChild(string name, BaseValue value) { } // readonly value
        public override bool IsArrayConvention() => isArrayRef;
        public override StringValue Stringify(int indent)
        {
            var ret = "<ReadonlyValue>" + Reference.ToString();
            foreach (var kp in Reference)
                ret += "\n" + kp.Key + " -> " + kp.Value.ToString();
            return new StringValue(ret);
        }
    }
    public class ReadonlyObject : BaseValue
    {
        public delegate object objectGetter();
        objectGetter Getter;
        public ReadonlyObject(objectGetter getter)
        {
            Getter = getter;
            Type = ValueType.Object;
        }
        public override BaseValue Evaluate(Scope currentScope)
        {
            return new NativeValue(Getter());
        }

        public override StringValue Stringify(int indent)
        {
            return new StringValue("<ReadonlyObject>" + Getter());
        }

        public override bool Equals(BaseValue other)
        {
            if(other is NativeValue nv) return nv == Getter();
            if(other is ReadonlyObject ro) return ro.Getter() == Getter();
            return false;
        }
    }

    public static class ReflectionExtensions
    {
        static MemberInfo[] GetMemberInfo(this Type type, string fieldName) => type.GetMember(fieldName, (BindingFlags)(-1));
        static MemberInfo GetAddressableInfo(this Type _type, string fieldName)
        {
            var type = _type;
            while (type != null)
            {
                var mi = type.GetMemberInfo(fieldName);
                if (mi.Length > 0)
                {
                    var candidate = mi.FirstOrDefault(x => x.MemberType == MemberTypes.Field || x.MemberType == MemberTypes.Property);
                    if (candidate != null)
                        return candidate;
                }
                type = type.BaseType;
            }
            return null;
        }
        public static object GetValue(this MemberInfo mi, object obj)
        {
            if (mi is FieldInfo fi) return fi.GetValue(obj);
            if (mi is PropertyInfo pi) return pi.GetValue(obj);
            return null;
        }
        public static void SetValue(this MemberInfo mi, object obj, object value)
        {
            if (mi is FieldInfo fi) fi.SetValue(obj, value);
            else if (mi is PropertyInfo pi) pi.SetValue(obj, value);
        }
        public static Type GetNativeType(this MemberInfo mi)
        {
            if (mi is FieldInfo fi) return fi.FieldType;
            if (mi is PropertyInfo pi) return pi.PropertyType;
            return null;
        }
        public static object GetMember(this object obj, string fieldName) => obj.GetType().GetAddressableInfo(fieldName)?.GetValue(obj) ?? null;
        public static void SetMember(this object obj, string fieldName, object value) => obj.GetType().GetAddressableInfo(fieldName)?.SetValue(obj, value);
        public static Type GetMemberType(this object obj, string fieldName) => obj.GetType().GetAddressableInfo(fieldName)?.GetNativeType() ?? null;

        //public static void Invoke(this Type type, string methodName, params object[] args) => type.GetMethodInfoStaticR(methodName).Invoke(null, args);
        //public static T Invoke<T>(this Type type, string methodName, params object[] args) => (T)type.GetMethodInfoStaticR(methodName).Invoke(null, args);

        //public static void Invoke(this object obj, string methodName, params object[] args) => obj.GetType().GetMethodInfoInstanceR(methodName).Invoke(obj, args);
        //public static T Invoke<T>(this object obj, string methodName, params object[] args) => (T)obj.GetType().GetMethodInfoInstanceR(methodName).Invoke(obj, args);
    }

    public static class ObjectValueExtensions
    {
        public static bool HasChild(this ObjectValue obj, string name, BaseValue.ValueType type)
        {
            var v = obj.GetChild(name);
            if (type == BaseValue.ValueType.AnyType) return v.Type != BaseValue.ValueType.Nil;
            return v.Type == type;
        }
    }

    public static class ObjectWrapper
    {
        // simple types
        /*public static BaseValue Wrap(this Vector2 v) => new ReadonlyValue(v);
        public static BaseValue Wrap(this Vector3 v) => new ReadonlyValue(v);
        public static BaseValue Wrap(this Quaternion v) => new ReadonlyValue(v);*/
        public static BaseValue Wrap<T>(this T[] v) => new ReadonlyValue(v.Cast<object>().ToArray());

        // function call wrapper
        public static List<IEvaluable> WrapToParams(this object[] param)
        {
            var ret = new List<IEvaluable>();
            foreach (var v in param)
                ret.Add(v.Wrap());
            return ret;
        }

        // base types
        public static BaseValue Wrap(this string str) => new StringValue(str);
        public static BaseValue Wrap(this double d) => new NumberValue(d);
        public static BaseValue Wrap(this float d) => new NumberValue(d);
        public static BaseValue Wrap(this int d) => new NumberValue(d);
        public static BaseValue Wrap(this bool d) => new NumberValue(d ? 1 : 0);

        public static BaseValue Wrap(this object obj)
        {
            if (obj is BaseValue bv) return bv; // value already wrapped
            if (obj == null) return NilValue.Nil;
            if (obj is int i) return i.Wrap();
            if (obj is double d) return d.Wrap();
            if (obj is float f) return f.Wrap();
            if (obj is string s) return s.Wrap();
            if (obj is bool b) return b.Wrap();
            /*if (obj is Vector2 v2) return v2.Wrap();
            if (obj is Vector3 v3) return v3.Wrap();
            if (obj is Quaternion q) return q.Wrap();*/
            if (obj.GetType().IsArray) return Wrap((object[])obj);
            return NativeWrap(obj);
        }

        public static BaseValue NativeWrap(object obj)
        {
            return new NativeValue(obj);
        }

        public static T Unwrap<T>(this BaseValue value) => (T)Unwrap(value, typeof(T));
        public static object Unwrap(this BaseValue value, Type type)
        {
            if (value.Type == BaseValue.ValueType.Nil)
                return null;
            if (value.Type == BaseValue.ValueType.Number)
            {
                double val = (double)value.Value;
                if (type == typeof(int))
                    return (int)val;
                if (type == typeof(double))
                    return val;
                if (type == typeof(float))
                    return (float)val;
                if (type == typeof(bool))
                    return value.HoldsTrue();
                Loadson.Console.Log("Tried unwrapping " + value.Stringify().Value + " to " + type + " but failed.");
            }
            if(value.Type == BaseValue.ValueType.String)
            {
                if(type == typeof(string))
                    return (string)value.Value;
            }

            // complex types
            if(value.Type == BaseValue.ValueType.Object)
            {
                ObjectValue obj = (ObjectValue)value;
                if (type == typeof(Vector2) && obj.HasChild("x", BaseValue.ValueType.Number) && obj.HasChild("y", BaseValue.ValueType.Number))
                    return new Vector2(obj.GetChild("x").Unwrap<float>(), obj.GetChild("y").Unwrap<float>());
                if (type == typeof(Vector3) && obj.HasChild("x", BaseValue.ValueType.Number) && obj.HasChild("y", BaseValue.ValueType.Number) && obj.HasChild("z", BaseValue.ValueType.Number))
                    return new Vector3(obj.GetChild("x").Unwrap<float>(), obj.GetChild("y").Unwrap<float>(), obj.GetChild("z").Unwrap<float>());
                if (type == typeof(Quaternion) && obj.HasChild("x", BaseValue.ValueType.Number) && obj.HasChild("y", BaseValue.ValueType.Number) && obj.HasChild("z", BaseValue.ValueType.Number) && obj.HasChild("w", BaseValue.ValueType.Number))
                    return new Quaternion(obj.GetChild("x").Unwrap<float>(), obj.GetChild("y").Unwrap<float>(), obj.GetChild("z").Unwrap<float>(), obj.GetChild("w").Unwrap<float>());
                if(type.IsArray)
                { // unwrap array
                    List<object> temp = new List<object>();
                    if (!obj.HasChild("length", BaseValue.ValueType.Number)) return null; // no 'length'
                    double dlen = obj.GetChild("length").Unwrap<double>();
                    if (dlen != Math.Floor(dlen)) return null;
                    for(int i = 0; i < dlen; i++)
                    {
                        if (!obj.HasChild(i.ToString(), BaseValue.ValueType.AnyType)) return null; // missing child
                        temp.Add(obj.GetChild(i.ToString()).Unwrap(type.GetElementType()));
                    }
                    return temp.ToArray();
                }
                if (NativeUnwrap(value, type, out object nativeObj))
                    return nativeObj;
            }

            Loadson.Console.Log("Tried unwrapping " + value.Stringify().Value + " to " + type + " but failed.");
            return null; // couldn't unwrap
        }

        public static bool NativeUnwrap(BaseValue value, Type type, out object obj)
        {
            if (!(value is NativeValue nv)) 
            {
                obj = null;
                return false;
            }
            if(!(nv.Object.GetType() == type))
            {
                obj = null;
                return false;
            }
            obj = nv.Object;
            return true;
        }
    }
}
