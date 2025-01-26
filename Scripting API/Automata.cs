using KarlsonMapEditor.Automata.Backbone;
using KarlsonMapEditor.Automata.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Xsl;
using UnityEngine.SocialPlatforms;
using UnityEngine.UIElements;

namespace KarlsonMapEditor.Automata
{
    namespace Backbone
    {
        public abstract class BaseValue : IEvaluable
        {
            public enum ValueType
            {
                Number,
                String,
                Object,
                Function,
                Nil,
                AnyType,
            }
            public ValueType Type;
            public object Value;
            public virtual BaseValue Evaluate(Scope currentScope) => this;
            public StringValue Stringify() => Stringify(0);
            public abstract StringValue Stringify(int indent);
            public override bool Equals(object obj)
            {
                if (!(obj is BaseValue objBV)) return false;
                return Equals(objBV);
            }
            public abstract bool Equals(BaseValue other);
            public bool HoldsTrue()
            {
                if (Type == ValueType.Nil) return false;
                if (Type != ValueType.Number) return true;
                return (double)Value != 0;
            }

            public override int GetHashCode() => base.GetHashCode();
            public override string ToString() => (string)Stringify().Value;

            public static ValueType ValueTypeFromString(string str)
            {
                if (str == "number")
                    return ValueType.Number;
                if (str == "string")
                    return ValueType.String;
                if (str == "object")
                    return ValueType.Object;
                if (str == "function")
                    return ValueType.Function;
                throw new Exceptions.InvalidValueType("Invalid value type " + str);
            }
        }
        public class NumberValue : BaseValue
        {
            public NumberValue(double value)
            {
                Type = ValueType.Number;
                Value = value;
            }
            public override StringValue Stringify(int _) => new StringValue(((double)Value).ToString(CultureInfo.InvariantCulture));
            public override bool Equals(BaseValue other)
            {
                if (other.Type != ValueType.Number)
                    return false;
                return (double)Value == (double)other.Value;
            }
        }
        public class StringValue : BaseValue
        {
            public StringValue(string value)
            {
                Type = ValueType.String;
                Value = value;
            }
            public override StringValue Stringify(int _) => this;
            public override bool Equals(BaseValue other)
            {
                if (other.Type != ValueType.String)
                    return false;
                return (string)Value == (string)other.Value;
            }
            public static string EscapeString(string str)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < str.Length; i++)
                {
                    if (str[i] != '\\')
                    {
                        sb.Append(str[i]);
                        continue;
                    }
                    if (str[i + 1] == '\\' || str[i + 1] == '"')
                    {
                        sb.Append(str[i + 1]);
                        i++;
                        continue;
                    }
                    if (str[i + 1] == 'n')
                    {
                        sb.Append('\n');
                        i++;
                        continue;
                    }
                    if (str[i + 1] == 'x')
                    {
                        // check that we have enough characters
                        if (i + 3 >= str.Length)
                            throw new Exceptions.InvalidStringFormatException($"Tried escaping ASCII code but string was too short '{str}'");
                        int asciiCode = Convert.ToInt32(str.Substring(i + 2, 2), 16);
                        sb.Append(char.ConvertFromUtf32(asciiCode));
                        i += 3;
                        continue;
                    }
                    throw new Exceptions.InvalidStringFormatException($"Unknown string escape code {str[i + 1]} in string '{str}'");
                }
                return sb.ToString();
            }
        }
        public class ObjectValue : BaseValue
        {
            public ObjectValue()
            {
                Type = ValueType.Object;
                Value = new Dictionary<string, BaseValue>();
            }
            public override StringValue Stringify(int indent)
            {
                string ret = "{\n";
                foreach (var value in (Dictionary<string, BaseValue>)Value)
                    ret += (value.Key + ": " + value.Value.Stringify(indent + 2)).Indent(indent + 2) + "\n";
                ret += "}".Indent(indent);
                return new StringValue(ret);
            }
            public override bool Equals(BaseValue other)
            {
                if (other.Type != ValueType.Object)
                    return false;
                var d1 = (Dictionary<string, BaseValue>)Value;
                var d2 = (Dictionary<string, BaseValue>)other.Value;
                return d1 == d2 || d1.Count == d2.Count && !d1.Except(d2).Any();
            }
            public virtual BaseValue GetChild(string name)
            {
                var dict = (Dictionary<string, BaseValue>)Value;
                if (!dict.ContainsKey(name))
                    return NilValue.Nil;
                return dict[name];
            }
            public virtual void SetChild(string name, BaseValue value)
            {
                var dict = (Dictionary<string, BaseValue>)Value;
                if (value.Type == ValueType.Nil)
                    dict.Remove(name); // clear memory
                else
                    dict[name] = value;
            }
            public virtual bool IsArrayConvention()
            {
                var dict = (Dictionary<string, BaseValue>)Value;
                if (!dict.ContainsKey("length")) return false; // doesn't have length attribute
                var len_val = dict["length"];
                if (len_val.Type != ValueType.Number) return false; // length value is non-number
                var len = (double)len_val.Value;
                if (len < 0 || len != Math.Floor(len)) return false; // length value is negative / not whole
                if (dict.Keys.Count != (int)len + 1) return false; // There should be length + 1 keys
                var keys = Enumerable.Range(0, (int)len);
                return dict.Keys.Where(x => x != "length").All(x => keys.Contains(int.Parse(x)));
            }
        }
        public class FunctionValue : BaseValue
        {
            public FunctionValue(ICallable value)
            {
                Type = ValueType.Function;
                Value = value;
            }
            public override StringValue Stringify(int _)
            {
                string ret = "fun(";
                bool firstParam = true;
                foreach (var param in ((ICallable)Value).Head)
                {
                    if (!firstParam)
                        ret += ", ";
                    else
                        firstParam = false;
                    ret += param.Item1 + " " + param.Item2;
                }
                return new StringValue(ret + ")");
            }
            // functions can only be diferentiated by their head
            public override bool Equals(BaseValue other)
            {
                if (other.Type != ValueType.Function)
                    return false;
                var h1 = ((ICallable)Value).Head;
                var h2 = ((ICallable)other.Value).Head;
                return h1 == h2 || h1.Count == h2.Count && !h1.Except(h2).Any();
            }
        }
        public class NilValue : BaseValue
        {
            public static NilValue Nil => nil;
            static readonly NilValue nil = new NilValue()
            {
                Type = ValueType.Nil,
                Value = null
            };
            public override StringValue Stringify(int _) => new StringValue("<nil>");
            public override bool Equals(BaseValue other)
            {
                return other.Type == ValueType.Nil;
            }
        }

        public interface ICallable
        {
            BaseValue Call(Scope currentScope);
            List<(VarResolver, BaseValue.ValueType)> Head { get; }
        }

        public class FunctionRunner : ICallable
        {
            public class ReturnValue : Exception
            {
                public BaseValue retVal;
                public ReturnValue(BaseValue val) => retVal = val;
            }

            public List<(VarResolver, BaseValue.ValueType)> Head => head;
            List<(VarResolver, BaseValue.ValueType)> head;
            List<Instruction> body;
            public FunctionRunner(List<(VarResolver, BaseValue.ValueType)> head, List<Instruction> body)
            {
                this.head = head;
                this.body = body;
            }
            public BaseValue Call(Scope currentScope)
            {
                // use exceptions to handle function return value (it's easier)
                try
                {
                    foreach (var instr in body)
                        instr.Execute(currentScope);
                }
                catch (ReturnValue retVal)
                {
                    return retVal.retVal;
                }
                // default return value
                return NilValue.Nil;
            }
        }

        public class NativeFunction : ICallable
        {
            public delegate BaseValue nativeFnScope(BaseValue[] args, Scope scope);
            public delegate BaseValue nativeFn(BaseValue[] args);
            List<(VarResolver, BaseValue.ValueType)> head;
            public List<(VarResolver, BaseValue.ValueType)> Head => head;
            nativeFnScope fn;
            public NativeFunction(List<(VarResolver, BaseValue.ValueType)> head, nativeFn fn)
            {
                this.head = head;
                this.fn = (args, _) => fn(args);
            }
            public NativeFunction(List<(VarResolver, BaseValue.ValueType)> head, nativeFnScope fn)
            {
                this.head = head;
                this.fn = fn;
            }
            public BaseValue Call(Scope currentScope)
            {
                // extract variables
                List<BaseValue> args_eval = new List<BaseValue>();
                foreach (var arg in head)
                    args_eval.Add(arg.Item1.Evaluate(currentScope));
                return fn(args_eval.ToArray(), currentScope);
            }
        }

        public interface IEvaluable
        {
            BaseValue Evaluate(Scope currentScope);
        }

        public class Expression : IEvaluable
        {
            public IEvaluable lhs;
            public IEvaluable rhs;
            public ExpressionOperator op;

            public Expression(IEvaluable lhs, ExpressionOperator op, IEvaluable rhs)
            {
                this.lhs = lhs;
                this.rhs = rhs;
                this.op = op;
            }

            public enum ExpressionOperator
            {
                Plus,
                Minus,
                Times,
                Div,
                Modulo,
                Less,
                LessEqual,
                Greater,
                GreaterEqual,
                Equal,
                NotEqual,
                LogicalNot,
                LogicalAnd,
                LogicalOr,
            }

            public static ExpressionOperator OperatorFromString(string op)
            {
                switch (op)
                {
                    case "+":
                        return ExpressionOperator.Plus;
                    case "-":
                        return ExpressionOperator.Minus;
                    case "*":
                        return ExpressionOperator.Times;
                    case "/":
                        return ExpressionOperator.Div;
                    case "%":
                        return ExpressionOperator.Modulo;
                    case "<":
                        return ExpressionOperator.Less;
                    case "<=":
                        return ExpressionOperator.LessEqual;
                    case ">":
                        return ExpressionOperator.Greater;
                    case ">=":
                        return ExpressionOperator.GreaterEqual;
                    case "==":
                        return ExpressionOperator.Equal;
                    case "!=":
                        return ExpressionOperator.NotEqual;
                    case "!":
                        return ExpressionOperator.LogicalNot;
                    case "&&":
                        return ExpressionOperator.LogicalAnd;
                    case "||":
                        return ExpressionOperator.LogicalOr;
                    default:
                        throw new Exceptions.UnknownTokenException($"Couldn't convert '{op}' to an operator");
                }
            }

            public BaseValue Evaluate(Scope currentScope)
            {
                BaseValue lhs_value = lhs.Evaluate(currentScope);
                BaseValue rhs_value = rhs?.Evaluate(currentScope) ?? null;
                switch (op)
                {
                    case ExpressionOperator.Plus:
                        if (rhs == null)
                        { // unary operator +
                            if (lhs_value.Type == BaseValue.ValueType.String)
                                return new NumberValue(double.Parse((string)lhs_value.Value, CultureInfo.InvariantCulture));
                            throw new Exceptions.InvalidOperationException($"Tried converting non-string value {lhs_value.Stringify().Value} to Number");
                        }
                        if (lhs_value.Type == BaseValue.ValueType.Number && rhs_value.Type == BaseValue.ValueType.Number)
                            return new NumberValue((double)lhs_value.Value + (double)rhs_value.Value);
                        return new StringValue((string)lhs_value.Stringify().Value + (string)rhs_value.Stringify().Value);
                    case ExpressionOperator.Minus:
                        if (lhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"LHS value {lhs_value.Stringify().Value} of operator Minus is not Number");
                        if (rhs == null) // unary operator -
                            return new NumberValue(-(double)lhs_value.Value);
                        if (rhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"RHS value {rhs_value.Stringify().Value} of operator Minus is not Number");
                        return new NumberValue((double)lhs_value.Value - (double)rhs_value.Value);
                    case ExpressionOperator.Times:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Times");
                        if (lhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"LHS value {lhs_value.Stringify().Value} of operator Times is not Number");
                        if (rhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"RHS value {rhs_value.Stringify().Value} of operator Times is not Number");
                        return new NumberValue((double)lhs_value.Value * (double)rhs_value.Value);
                    case ExpressionOperator.Div:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Div");
                        if (lhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"LHS value {lhs_value.Stringify().Value} of operator Div is not Number");
                        if (rhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"RHS value {rhs_value.Stringify().Value} of operator Div is not Number");
                        return new NumberValue((double)lhs_value.Value / (double)rhs_value.Value);
                    case ExpressionOperator.Modulo:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Modulo");
                        if (lhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"LHS value {lhs_value.Stringify().Value} of operator Modulo is not Number");
                        if (rhs_value.Type != BaseValue.ValueType.Number)
                            throw new Exceptions.InvalidOperationException($"RHS value {rhs_value.Stringify().Value} of operator Modulo is not Number");
                        var val = (double)lhs_value.Value;
                        var mod = (double)rhs_value.Value;
                        if (mod <= 0) throw new Exceptions.InvalidOperationException($"RHS value {mod} is negative or zero");
                        while (val < 0) val += mod;
                        while (val >= mod) val -= mod;
                        return new NumberValue(val);
                    case ExpressionOperator.Less:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Less");
                        if (lhs_value.Type == BaseValue.ValueType.Number && rhs_value.Type == BaseValue.ValueType.Number)
                            return new NumberValue(((double)lhs_value.Value < (double)rhs_value.Value) ? 1 : 0);
                        if (lhs_value.Type == BaseValue.ValueType.String && rhs_value.Type == BaseValue.ValueType.String)
                            return new NumberValue(((string)lhs_value.Value).CompareTo((string)rhs_value.Value) < 0 ? 1 : 0);
                        throw new Exceptions.InvalidOperationException($"Tried comparing {lhs_value.Type} value {lhs_value.Stringify().Value} to {rhs_value.Type} value {rhs_value.Stringify().Value}");
                    case ExpressionOperator.LessEqual:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "LessEqual");
                        if (lhs_value.Type == BaseValue.ValueType.Number && rhs_value.Type == BaseValue.ValueType.Number)
                            return new NumberValue(((double)lhs_value.Value <= (double)rhs_value.Value) ? 1 : 0);
                        if (lhs_value.Type == BaseValue.ValueType.String && rhs_value.Type == BaseValue.ValueType.String)
                            return new NumberValue(((string)lhs_value.Value).CompareTo((string)rhs_value.Value) <= 0 ? 1 : 0);
                        throw new Exceptions.InvalidOperationException($"Tried comparting {lhs_value.Type} value {lhs_value.Stringify().Value} to {rhs_value.Type} value {rhs_value.Stringify().Value}");
                    case ExpressionOperator.Greater:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Greater");
                        if (lhs_value.Type == BaseValue.ValueType.Number && rhs_value.Type == BaseValue.ValueType.Number)
                            return new NumberValue(((double)lhs_value.Value > (double)rhs_value.Value) ? 1 : 0);
                        if (lhs_value.Type == BaseValue.ValueType.String && rhs_value.Type == BaseValue.ValueType.String)
                            return new NumberValue(((string)lhs_value.Value).CompareTo((string)rhs_value.Value) > 0 ? 1 : 0);
                        throw new Exceptions.InvalidOperationException($"Tried comparting {lhs_value.Type} value {lhs_value.Stringify().Value} to {rhs_value.Type} value {rhs_value.Stringify().Value}");
                    case ExpressionOperator.GreaterEqual:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "GreaterEqual");
                        if (lhs_value.Type == BaseValue.ValueType.Number && rhs_value.Type == BaseValue.ValueType.Number)
                            return new NumberValue(((double)lhs_value.Value >= (double)rhs_value.Value) ? 1 : 0);
                        if (lhs_value.Type == BaseValue.ValueType.String && rhs_value.Type == BaseValue.ValueType.String)
                            return new NumberValue(((string)lhs_value.Value).CompareTo((string)rhs_value.Value) >= 0 ? 1 : 0);
                        throw new Exceptions.InvalidOperationException($"Tried comparting {lhs_value.Type} value {lhs_value.Stringify().Value} to {rhs_value.Type} value {rhs_value.Stringify().Value}");
                    case ExpressionOperator.Equal:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "Equal");
                        return new NumberValue(lhs_value.Equals(rhs_value) ? 1 : 0);
                    case ExpressionOperator.NotEqual:
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "NotEqual");
                        return new NumberValue(lhs_value.Equals(rhs_value) ? 0 : 1);
                    case ExpressionOperator.LogicalNot:
                        return new NumberValue(lhs_value.HoldsTrue() ? 0 : 1);
                    case ExpressionOperator.LogicalAnd:
                    {
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "LogicalAnd");
                        var lhs_eval = lhs_value.HoldsTrue();
                        var rhs_eval = rhs_value.HoldsTrue();
                        return new NumberValue(lhs_eval && rhs_eval ? 1 : 0);
                    }
                    case ExpressionOperator.LogicalOr:
                    {
                        if (rhs == null)
                            throw new Exceptions.NullOperandException("RHS", "LogicalOr");
                        var lhs_eval = lhs_value.HoldsTrue();
                        var rhs_eval = rhs_value.HoldsTrue();
                        return new NumberValue(lhs_eval || rhs_eval ? 1 : 0);
                    }
                }
                return NilValue.Nil;
            }
        }

        public class FunctionCall : IEvaluable
        {
            IEvaluable function;
            List<IEvaluable> parameters;
            public FunctionCall(IEvaluable function, List<IEvaluable> parameters)
            {
                this.function = function;
                this.parameters = parameters;
            }

            public BaseValue Evaluate(Scope currentScope)
            {
                var res = function.Evaluate(currentScope);
                if (res.Type != BaseValue.ValueType.Function)
                    throw new Exceptions.VariableTypeException($"Tried calling non-function value {res.Stringify().Value}");
                var fn = (ICallable)res.Value;
                // ensure parameter count
                if (fn.Head.Count != parameters.Count)
                    throw new Exceptions.InvalidParametersException($"Parameter count of {parameters.Count} doesn't match Head of {res.Stringify().Value}");
                // evaluate parameters
                List<BaseValue> eval = parameters.Select(x => x.Evaluate(currentScope)).ToList();
                // ensure parameter type
                for (int i = 0; i < parameters.Count; i++)
                {
                    if (fn.Head[i].Item2 == BaseValue.ValueType.AnyType) continue;
                    if (fn.Head[i].Item2 != eval[i].Type)
                        throw new Exceptions.InvalidParametersException($"Parameter {i} {eval[i].Stringify().Value} type {eval[i].Type} doesn't match Head {fn.Head[i]}");
                }
                // scope the function
                Scope fnScope = new Scope(currentScope);
                // pass the arguments on the scope
                for (int i = 0; i < parameters.Count; i++)
                    fn.Head[i].Item1.Assign(fnScope, eval[i]);
                // call the function
                return fn.Call(fnScope);
            }
        }

        public class Scope
        {
            public delegate void logFn(string s);
            public logFn LogFunction;

            bool isGlobalScope = false;
            Scope parentScope, globalScope;
            Dictionary<string, BaseValue> variables = new Dictionary<string, BaseValue>();

            // global scope settings
            int maxWhileLoops;
            public int MaxWhileLoops => globalScope.maxWhileLoops;
            public Scope(int _maxWhileLoops = 10000)
            {
                isGlobalScope = true;
                parentScope = this;
                globalScope = this;
                maxWhileLoops = _maxWhileLoops;
                LogFunction = Console.Write;
            }
            public Scope(Scope outerScope)
            {
                parentScope = outerScope;
                globalScope = outerScope.globalScope;
                LogFunction = outerScope.LogFunction;
            }
            public Scope GetScopeOfVariable(string var_name)
            {
                if (var_name.StartsWith("!"))
                    return this;
                if (var_name.StartsWith(":"))
                    return globalScope;
                // try to find variable by traversing scopes
                Scope search = this;
                while (!search.variables.ContainsKey(var_name) && !search.isGlobalScope)
                    search = search.parentScope;
                if (!search.variables.ContainsKey(var_name))
                    return null; // couldn't find variable
                return search;
            }
            public BaseValue GetVariable(string var_name)
            {
                var var_scope = GetScopeOfVariable(var_name);
                if (var_scope == null)
                    return NilValue.Nil;
                if (var_name.StartsWith(":") || var_name.StartsWith("!"))
                    var_name = var_name.Substring(1);
                if (!var_scope.variables.ContainsKey(var_name))
                    return NilValue.Nil;
                return var_scope.variables[var_name];
            }
            public void SetVariable(string var_name, BaseValue value)
            {
                var var_scope = GetScopeOfVariable(var_name) ?? this;
                if (var_name.StartsWith(":") || var_name.StartsWith("!"))
                    var_name = var_name.Substring(1);
                if (value.Type == BaseValue.ValueType.Nil)
                    var_scope.variables.Remove(var_name); // remove variable to free up memory
                else
                    var_scope.variables[var_name] = value;
            }
        }

        public abstract class VarResolver : IEvaluable
        {
            public abstract BaseValue Resolve(Scope currentScope);
            public abstract void Assign(Scope currentScope, BaseValue value);
            public override abstract string ToString();
            public BaseValue Evaluate(Scope currentScope) => Resolve(currentScope);
        }
        public class VarNameResolver : VarResolver
        {
            string var_name;
            public VarNameResolver(string var_name)
            {
                this.var_name = var_name;
            }
            public override BaseValue Resolve(Scope currentScope) => currentScope.GetVariable(var_name);
            public override void Assign(Scope currentScope, BaseValue value) => currentScope.SetVariable(var_name, value);
            public override string ToString() => $"VarNameResolver({var_name})";
        }
        public class VarObjectResolver : VarResolver
        {
            VarResolver base_var;
            IEvaluable child_name;
            public VarObjectResolver(VarResolver base_var, IEvaluable child_name)
            {
                this.base_var = base_var;
                this.child_name = child_name;
            }
            public override BaseValue Resolve(Scope currentScope)
            {
                var res = base_var.Resolve(currentScope);
                if (res.Type != BaseValue.ValueType.Object)
                    throw new Exceptions.VariableTypeException($"Base value {res.Value} is not object");
                return ((ObjectValue)res).GetChild((string)child_name.Evaluate(currentScope).Stringify().Value);
            }
            public override void Assign(Scope currentScope, BaseValue value)
            {
                var res = base_var.Resolve(currentScope);
                if (res.Type != BaseValue.ValueType.Object)
                    throw new Exceptions.VariableTypeException($"Base value {res.Value} is not object");
                ((ObjectValue)res).SetChild((string)child_name.Evaluate(currentScope).Stringify().Value, value);
            }
            public override string ToString() => $"VarObjectResolver({base_var} -> {child_name})";
        }
        public class ObjectAccessor : VarResolver
        {
            public IEvaluable baseExpr;
            public IEvaluable accessor;
            public ObjectAccessor(IEvaluable baseExpr, IEvaluable accessor)
            {
                this.baseExpr = baseExpr;
                this.accessor = accessor;
            }

            public override BaseValue Resolve(Scope currentScope)
            {
                var baseVal = baseExpr.Evaluate(currentScope);
                if (baseVal.Type != BaseValue.ValueType.Object)
                    throw new Exceptions.InvalidOperationException("Tried walking non-object value " + baseVal.Stringify().Value);
                var accessVal = accessor.Evaluate(currentScope);
                return ((ObjectValue)baseVal).GetChild((string)accessVal.Stringify().Value);
            }
            public override void Assign(Scope currentScope, BaseValue value)
            {
                var baseVal = baseExpr.Evaluate(currentScope);
                if (baseVal.Type != BaseValue.ValueType.Object)
                    throw new Exceptions.InvalidOperationException("Tried walking non-object value " + baseVal.Stringify().Value);
                var accessVal = accessor.Evaluate(currentScope);
                ((ObjectValue)baseVal).SetChild((string)accessVal.Stringify().Value, value);
            }
            public override string ToString() => $"ObjectAccessor(({baseExpr}) -> ({accessor}))";
        }

        public abstract class Instruction
        {
            public enum InstructionType
            {
                VarAssign,
                FunCall,
                IfBlocks,
                WhileBlocks,
                ForBlocks,
                FunctionReturn,
            }
            public InstructionType Type;
            public abstract void Execute(Scope currentScope);
        }
        public class VarAssignInstruction : Instruction
        {
            VarResolver var;
            IEvaluable value;
            public VarAssignInstruction(VarResolver var, IEvaluable value)
            {
                Type = InstructionType.VarAssign;
                this.var = var;
                this.value = value;
            }
            public override void Execute(Scope currentScope) => var.Assign(currentScope, value.Evaluate(currentScope));
        }
        public class FunCallInstruction : Instruction
        { // this instruction is used when ignoring return type
            IEvaluable functionCall;
            public FunCallInstruction(IEvaluable functionCall)
            {
                Type = InstructionType.FunCall;
                this.functionCall = functionCall;
            }
            public override void Execute(Scope currentScope)
            {
                functionCall.Evaluate(currentScope); // invoke the function
            }
        }
        public class IfBlocksInstruction : Instruction
        {
            IEvaluable condition;
            List<Instruction> trueBlock;
            List<Instruction> falseBlock;
            public IfBlocksInstruction(IEvaluable condition, List<Instruction> trueBlock, List<Instruction> falseBlock)
            {
                Type = InstructionType.IfBlocks;
                this.condition = condition;
                this.trueBlock = trueBlock;
                this.falseBlock = falseBlock;
            }
            public override void Execute(Scope currentScope)
            {
                if (condition.Evaluate(currentScope).HoldsTrue())
                {
                    // scope block
                    Scope blockScope = new Scope(currentScope);
                    foreach (var instr in trueBlock)
                        instr.Execute(blockScope);
                }
                else if (falseBlock != null)
                {
                    // scope block
                    Scope blockScope = new Scope(currentScope);
                    foreach (var instr in falseBlock)
                        instr.Execute(blockScope);
                }
            }
        }
        public class WhileBlocksInstruction : Instruction
        {
            IEvaluable condition;
            List<Instruction> instructions;
            public WhileBlocksInstruction(IEvaluable condition, List<Instruction> instructions)
            {
                Type = InstructionType.WhileBlocks;
                this.condition = condition;
                this.instructions = instructions;
            }
            public override void Execute(Scope currentScope)
            {
                int loopCount = 0;
                while (condition.Evaluate(currentScope).HoldsTrue())
                {
                    // scope block
                    Scope blockScope = new Scope(currentScope);
                    foreach (var instr in instructions)
                        instr.Execute(blockScope);
                    if (++loopCount > currentScope.MaxWhileLoops)
                        throw new Exceptions.IterationLoopException(currentScope.MaxWhileLoops);
                }
            }
        }
        public class ForBlocksInstruction : Instruction
        {
            VarResolver iter_var;
            IEvaluable iter_array;
            List<Instruction> instructions;
            public ForBlocksInstruction(VarResolver iter_var, IEvaluable iter_array, List<Instruction> instructions)
            {
                Type = InstructionType.ForBlocks;
                this.iter_var = iter_var;
                this.iter_array = iter_array;
                this.instructions = instructions;
            }
            public override void Execute(Scope currentScope)
            {
                // evaluate array which needs to be iterrated
                var iter_val = iter_array.Evaluate(currentScope);
                // check that the value is object
                if (iter_val.Type != BaseValue.ValueType.Object)
                    throw new Exceptions.VariableTypeException($"Tried running for loop over non-object value {iter_val.Stringify().Value}");
                // check that the value is array-convention
                var array = (ObjectValue)iter_val;
                if (!array.IsArrayConvention())
                    throw new Exceptions.VariableTypeException($"Tried running for loop over non-array-covention value {iter_val.Stringify().Value}");
                var length = (int)(double)array.GetChild("length").Value;
                for (int i = 0; i < length; i++)
                {
                    // scope the block
                    Scope blockScope = new Scope(currentScope);
                    // inject iterator variable
                    iter_var.Assign(blockScope, array.GetChild(i.ToString()));
                    foreach (var instr in instructions)
                        instr.Execute(blockScope);
                }
            }
        }
        public class FunctionReturnInstruction : Instruction
        {
            public IEvaluable returnValue;
            public FunctionReturnInstruction(IEvaluable returnValue)
            {
                Type = InstructionType.FunctionReturn;
                this.returnValue = returnValue;
            }
            public override void Execute(Scope currentScope) => throw new FunctionRunner.ReturnValue(returnValue.Evaluate(currentScope));
        }
    }

    namespace Parser
    {
        public class Token
        {
            public Token(string val, TokenType type = TokenType.Unknown, uint originalLine = 0)
            {
                Value = val;
                Type = type;
                OriginalLine = originalLine;
            }

            public string Value;
            public TokenType Type;
            public uint OriginalLine;

            public enum TokenType
            {
                Unknown,
                Operator,
                Constant,
                Variable,
                RoundBracket,
                SquareBracket,
                Comma,
                EmptyObject,
                Assign,
                Keyword,
                VarType,
                NewLine,
                // type used at blocking
                UnaryOperator,
            }

            public static void PrintTokens(List<Token> tokens)
            {
                Console.WriteLine("--- TOKENS ----");
                var maxLen = tokens.OrderByDescending(x => x.Value.Length).First().Value.Length;
                foreach (var token in tokens)
                    Console.WriteLine(token.Value.PadRight(maxLen) + " " + (token.Type == TokenType.Unknown ? "!! UNKNOWN !!" : token.Type.ToString()));
                Console.WriteLine("---------------");
            }

            public override string ToString()
            {
                return $"Token({Type} {Value})";
            }
        }

        public static class ProgramCleaner
        {
            // prepare the program to be tokenized
            public static string CleanProgram(string program)
            {
                // collapse multi-line instructions
                program = program.Replace("\\\n", "");
                List<string> lines = new List<string>();
                foreach (var line in program.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#"))
                        continue; // remove comments
                    if (trimmed.Length == 0)
                        continue; // remove empty lines
                    lines.Add(line);
                }
                // remove in-line comments
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!lines[i].Contains('#'))
                        continue;
                    int last_comm = lines[i].LastIndexOf('#');
                    var last_str = lines[i].LastIndexOf('"');
                    if (last_comm > last_str)
                        lines[i] = lines[i].Substring(0, last_comm);
                }
                return string.Join("\n", lines);
            }
        }

        public static partial class Tokenizer
        {
            public static List<Token> Tokenize(string program)
            {
                List<Token> tokens = new List<Token> { new Token(program) };

                tokens = SplitByLines(tokens);
                tokens = ExtractStrings(tokens);
                tokens = ExtractVariables(tokens);
                tokens = ExtractKeywords(tokens);
                tokens = ExtractOperators(tokens);
                tokens = ExtractNumbers(tokens);

                if (tokens.Where(x => x.Type == Token.TokenType.Unknown).Any())
                    throw new Exceptions.UnknownTokenException("Found unknown token " + tokens.Where(x => x.Type == Token.TokenType.Unknown).First().Value);

                return tokens;
            }

            static List<Token> CleanTokens(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type == Token.TokenType.NewLine)
                    {
                        // treat new lines separately
                        tokens.Add(new Token("", Token.TokenType.NewLine));
                        continue;
                    }
                    if (token.Value.Trim().Length == 0) continue;
                    tokens.Add(new Token(token.Value.Trim(), token.Type));
                }
                return tokens;
            }

            static List<Token> SplitByLines(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    while (tk.Contains('\n'))
                    {
                        var idx = tk.IndexOf('\n');
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        tokens.Add(new Token("", Token.TokenType.NewLine));
                        tk = tk.Substring(idx + 1);
                    }
                    tokens.Add(new Token(tk));
                }
                // clean successive line breaks
                for (int i = tokens.Count - 1; i > 0; --i)
                    if (tokens[i].Type == Token.TokenType.NewLine && tokens[i - 1].Type == Token.TokenType.NewLine)
                        tokens.RemoveAt(i);
                return CleanTokens(tokens);
            }

            static List<Token> ExtractStrings(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    while (tk.Contains('"'))
                    {
                        var idx = tk.IndexOf('"');
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        ++idx;
                        while (idx < tk.Length && tk[idx] != '"')
                        {
                            if (tk[idx] == '\\')
                                ++idx;
                            ++idx;
                        }
                        tokens.Add(new Token(tk.Substring(tk.IndexOf('"'), idx - tk.IndexOf('"') + 1), Token.TokenType.Constant));
                        tk = tk.Substring(idx + 1);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }

            static List<Token> ExtractVariables(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    { // token is final
                        tokens.Add(token);
                        continue;
                    }
                    string tk = token.Value;
                    while (tk.Contains('$'))
                    {
                        var idx = tk.IndexOf('$');
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        while (idx < tk.Length && tk[idx].IsVarName())
                            ++idx;
                        tokens.Add(new Token(tk.Substring(tk.IndexOf('$'), idx - tk.IndexOf('$')), Token.TokenType.Variable));
                        tk = tk.Substring(idx);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }

            static readonly string[] VarTypes = { "number", "string", "function", "object", "nil" }; // nil is keyword for NilValue.Nil
            static readonly string[] Keywords = (new string[] { "fun", "nfu", "if", "el", "fi", "while", "ewhil", "for", "rfo", "return", "continue" }).Concat(VarTypes).ToArray();
            static List<Token> ExtractKeywords(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    string firstAp;
                    while ((firstAp = tk.ContainsAny(Keywords)) != "")
                    {
                        var idx = tk.IndexOf(firstAp);
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        if (VarTypes.Contains(firstAp))
                            tokens.Add(new Token(firstAp, Token.TokenType.VarType));
                        else
                            tokens.Add(new Token(firstAp, Token.TokenType.Keyword));
                        tk = tk.Substring(idx + firstAp.Length);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }

            static List<Token> ExtractOperators(List<Token> oldTokens) => ExtractOperatorsOneChar(ExtractOperatorsTwoChars(oldTokens));
            static readonly string[] OperatorsTwoChars = { "<=", ">=", "==", "!=", "{}" /*empty object*/, "&&", "||" };
            static List<Token> ExtractOperatorsTwoChars(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    string firstAp;
                    while ((firstAp = tk.ContainsAny(OperatorsTwoChars)) != "")
                    {
                        var idx = tk.IndexOf(firstAp);
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        tokens.Add(new Token(firstAp, firstAp == "{}" ? Token.TokenType.EmptyObject : Token.TokenType.Operator));
                        tk = tk.Substring(idx + 2);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }
            static readonly string[] OperatorsOneChar = { "+", "-", "*", "/", "%", "(", ")", ",", "!", "<", ">", "[", "]", "=" };
            static List<Token> ExtractOperatorsOneChar(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    string firstAp;
                    while ((firstAp = tk.ContainsAny(OperatorsOneChar)) != "")
                    {
                        var idx = tk.IndexOf(firstAp);
                        tokens.Add(new Token(tk.Substring(0, idx)));
                        if (firstAp == "(" || firstAp == ")")
                            tokens.Add(new Token(firstAp, Token.TokenType.RoundBracket));
                        else if (firstAp == "[" || firstAp == "]")
                            tokens.Add(new Token(firstAp, Token.TokenType.SquareBracket));
                        else if (firstAp == "=")
                            tokens.Add(new Token(firstAp, Token.TokenType.Assign));
                        else if (firstAp == ",")
                            tokens.Add(new Token(firstAp, Token.TokenType.Comma));
                        else
                            tokens.Add(new Token(firstAp, Token.TokenType.Operator));
                        tk = tk.Substring(idx + 1);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }

            const string NumberRegex = @"(\d+(\.\d+)?)|(\.\d+)";

            static List<Token> ExtractNumbers(List<Token> oldTokens)
            {
                List<Token> tokens = new List<Token>();
                foreach (var token in oldTokens)
                {
                    if (token.Type != Token.TokenType.Unknown)
                    {
                        tokens.Add(token);
                        continue;
                    }
                    var tk = token.Value;
                    while (Regex.Matches(tk, NumberRegex).Count > 0)
                    {
                        var match = Regex.Match(tk, NumberRegex);
                        tokens.Add(new Token(tk.Substring(0, match.Index)));
                        tokens.Add(new Token(match.Value, Token.TokenType.Constant));
                        tk = tk.Substring(match.Index + match.Length);
                    }
                    tokens.Add(new Token(tk));
                }
                return CleanTokens(tokens);
            }
        }

        public class ProgramParser
        {
            List<Token> tokens;
            int crnt = 0;
            public ProgramParser(List<Token> tokens)
            {
                // step over any blank newlines
                int i;
                for (i = 0; i < tokens.Count && tokens[i].Type == Token.TokenType.NewLine; i++) { }

                this.tokens = tokens.Skip(i).ToList();
            }
            List<Token> nextTokens => crnt >= tokens.Count ? new List<Token>() : tokens.Skip(crnt).ToList();

            public List<Instruction> ParseProgram()
            {
                List<Instruction> ret = new List<Instruction>();
                while (nextTokens.Count > 0)
                {
                    // step over any NL
                    while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.NewLine) ++crnt;
                    if (nextTokens[0].Type == Token.TokenType.Keyword)
                    {
                        if (nextTokens[0].Value == "if")
                        {
                            // find matching 'el' and 'fi' keywords
                            ++crnt;
                            var expr = ParseExpression();
                            int depth = 1;
                            int match_el = 0, match_fi = 0;
                            for (int i = 0; i < nextTokens.Count; i++)
                            {
                                if (nextTokens[i].Type != Token.TokenType.Keyword) continue;
                                if (nextTokens[i].Value == "if")
                                    ++depth;
                                else if (nextTokens[i].Value == "fi")
                                    --depth;
                                if (depth > 1) continue;
                                if (nextTokens[i].Value == "el" && depth == 1 && match_el == 0)
                                    match_el = i;
                                if (nextTokens[i].Value == "fi" && depth == 0 && match_fi == 0)
                                {
                                    match_fi = i;
                                    break;
                                }
                            }
                            if (match_fi == 0)
                                throw new Exceptions.MissingTokenException("Couldn't find matching 'fi' keyword");
                            if (match_el == 0)
                            {
                                // only parse true block
                                var true_block = new ProgramParser(nextTokens.Take(match_fi - 1).ToList()).ParseProgram();
                                crnt += match_fi + 1;
                                ret.Add(new IfBlocksInstruction(expr, true_block, null));
                            }
                            else
                            {
                                var true_block = new ProgramParser(nextTokens.Take(match_el - 1).ToList()).ParseProgram();
                                var false_block = new ProgramParser(nextTokens.Skip(match_el + 1).Take(match_fi - 1 - match_el - 1).ToList()).ParseProgram();
                                crnt += match_fi + 1;
                                ret.Add(new IfBlocksInstruction(expr, true_block, false_block));
                            }
                            continue;
                        }
                        if (nextTokens[0].Value == "while")
                        {
                            ++crnt;
                            var expr = ParseExpression();
                            int depth = 1;
                            int match_ewhil = 0;
                            for (int i = 0; i < nextTokens.Count; i++)
                            {
                                if (nextTokens[i].Type != Token.TokenType.Keyword)
                                    continue;
                                if (nextTokens[i].Value == "while")
                                    ++depth;
                                else if (nextTokens[i].Value == "ewhil")
                                    --depth;
                                if (depth == 0)
                                {
                                    match_ewhil = i;
                                    break;
                                }
                            }
                            if (match_ewhil == 0)
                                throw new Exceptions.MissingTokenException("Couldn't find matching 'ewhil' keyword");
                            var body = new ProgramParser(nextTokens.Take(match_ewhil - 1).ToList()).ParseProgram();
                            ret.Add(new WhileBlocksInstruction(expr, body));
                            crnt += match_ewhil + 1;
                            continue;
                        }
                        if (nextTokens[0].Value == "for")
                        {
                            ++crnt;
                            var variable = ParseVariable();
                            var expr = ParseExpression();
                            int depth = 1;
                            int match_rfo = 0;
                            for (int i = 0; i < nextTokens.Count; i++)
                            {
                                if (nextTokens[i].Type != Token.TokenType.Keyword)
                                    continue;
                                if (nextTokens[i].Value == "for")
                                    ++depth;
                                if (nextTokens[i].Value == "rfo")
                                    --depth;
                                if (depth == 0)
                                {
                                    match_rfo = i;
                                    break;
                                }
                            }
                            if (match_rfo == 0)
                                throw new Exceptions.MissingTokenException("Couldn't find matching 'rfo' keyword");
                            var body = new ProgramParser(nextTokens.Take(match_rfo - 1).ToList()).ParseProgram();
                            ret.Add(new ForBlocksInstruction(variable, expr, body));
                            crnt += match_rfo + 1;
                            continue;
                        }
                        if (nextTokens[0].Value == "return")
                        {
                            ++crnt;
                            // check if return is nil
                            IEvaluable retVal;
                            if (nextTokens.Count == 0 || nextTokens[0].Type == Token.TokenType.NewLine)
                            {
                                ++crnt;
                                retVal = NilValue.Nil;
                            }
                            else
                            {
                                retVal = ParseExpression();
                            }
                            ret.Add(new FunctionReturnInstruction(retVal));
                            continue;
                        }
                        throw new Exceptions.UnexpectedTokenException("Unexpected keyword " + nextTokens[0].Value);
                    }
                    // next instruction can be assignment, or fn_call
                    // check if line contains '='
                    int nextNL = nextTokens.FindIndex(x => x.Type == Token.TokenType.NewLine);
                    int nextEq = nextTokens.FindIndex(x => x.Type == Token.TokenType.Assign);
                    if (nextEq != -1 && (nextNL == -1 || nextEq < nextNL))
                    {
                        // var assign
                        var variable = ParseVariable();
                        ++crnt; // step over '='
                        var res = ParseExpression();
                        ret.Add(new VarAssignInstruction(variable, res));
                        continue;
                    }
                    // fn_call
                    var fn = ParseExpression();
                    ret.Add(new FunCallInstruction(fn));
                }
                return ret;
            }

            // infix operators only. prefix are handled separately
            static readonly string[][] OperatorOrder = {
                new string[] { "||" },
                new string[] { "&&" },
                new string[] { "<", "<=", ">", ">=", "==", "!="},
                new string[] { "+", "-" },
                new string[] { "*", "/", "%"}
            };

            public IEvaluable ParseExpression()
            {
                int skipWhenReturning = 0;
                // end of expression can be:
                // unbalanced closed ')' or ']'
                // comma in root
                // '\n'
                int end_of_expr = 0;
                int depth_r = 0, depth_s = 0;
                while (true)
                {
                    if (end_of_expr >= nextTokens.Count)
                        break; // reached end of input
                    if (nextTokens[end_of_expr].Type == Token.TokenType.NewLine)
                        break; // newline
                    if (nextTokens[end_of_expr].Type == Token.TokenType.RoundBracket)
                    {
                        if (nextTokens[end_of_expr].Value == "(")
                            ++depth_r;
                        else
                            --depth_r;
                    }
                    if (nextTokens[end_of_expr].Type == Token.TokenType.SquareBracket)
                    {
                        if (nextTokens[end_of_expr].Value == "[")
                            ++depth_s;
                        else
                            --depth_s;
                    }
                    if (depth_r > 0 || depth_s > 0)
                    {
                        ++end_of_expr;
                        continue;
                    }
                    if (depth_r == 0 && depth_s == 0 && nextTokens[end_of_expr].Type == Token.TokenType.Comma)
                        break; // comma in root
                    if (depth_r == -1 || depth_s == -1)
                        break; // unbalanced ')' or ']'
                    ++end_of_expr;
                }

                // check if expression is (expr)
                while (nextTokens[0].Type == Token.TokenType.RoundBracket && nextTokens[0].Value == "(" && nextTokens[end_of_expr - 1].Type == Token.TokenType.RoundBracket && nextTokens[end_of_expr - 1].Value == ")")
                {
                    ++crnt;
                    end_of_expr -= 2;
                    ++skipWhenReturning;
                }

                // split by last-eval operator
                foreach (var op_order in OperatorOrder)
                {
                    List<IEvaluable> innerExpr = new List<IEvaluable>();
                    List<Expression.ExpressionOperator> operators = new List<Expression.ExpressionOperator>();
                    int prev_start = 0;
                    depth_r = 0;
                    depth_s = 0;
                    for (int i = 0; i < end_of_expr; i++)
                    {
                        if (nextTokens[i].Type == Token.TokenType.RoundBracket)
                        {
                            if (nextTokens[i].Value == "(")
                                ++depth_s;
                            else
                                --depth_s;
                            continue;
                        }
                        if (nextTokens[i].Type == Token.TokenType.SquareBracket)
                        {
                            if (nextTokens[i].Value == "[")
                                ++depth_r;
                            else
                                --depth_r;
                            continue;
                        }
                        if (depth_r > 0 || depth_s > 0)
                            continue;
                        if (nextTokens[i].Type != Token.TokenType.Operator)
                            continue;
                        if (i > 0 && nextTokens[i - 1].Type == Token.TokenType.Operator || i == 0)
                            continue;
                        if (op_order.Contains(nextTokens[i].Value))
                        {
                            innerExpr.Add(new ProgramParser(nextTokens.Skip(prev_start).Take(i - prev_start).ToList()).ParseExpression());
                            operators.Add(Expression.OperatorFromString(nextTokens[i].Value));
                            prev_start = i + 1;
                        }
                    }
                    if (innerExpr.Count == 0) continue; // no more infix operators or prefix operators only
                    innerExpr.Add(new ProgramParser(nextTokens.Skip(prev_start).Take(end_of_expr - prev_start).ToList()).ParseExpression());
                    // construct tree
                    IEvaluable current = innerExpr[0];
                    for (int i = 1; i < innerExpr.Count; i++)
                        current = new Expression(current, operators[i - 1], innerExpr[i]);
                    crnt += end_of_expr;
                    crnt += skipWhenReturning;
                    return current;
                }

                IEvaluable lhs = null;

                // check for prefix operators
                while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.Operator)
                {
                    if (nextTokens[0].Value == "+")
                    {
                        ++crnt;
                        --end_of_expr;
                        var ret = new Expression(ParseExpression(), Expression.ExpressionOperator.Plus, null);
                        crnt += skipWhenReturning;
                        return ret;
                    }
                    if (nextTokens[0].Value == "-")
                    {
                        ++crnt;
                        --end_of_expr;
                        var ret = new Expression(ParseExpression(), Expression.ExpressionOperator.Minus, null);
                        crnt += skipWhenReturning;
                        return ret;
                    }
                    if (nextTokens[0].Value == "!")
                    {
                        ++crnt;
                        --end_of_expr;
                        var ret = new Expression(ParseExpression(), Expression.ExpressionOperator.LogicalNot, null);
                        crnt += skipWhenReturning;
                        return ret;
                    }
                    throw new Exceptions.UnexpectedTokenException($"Expected unary operator but found '{nextTokens[0].Value}'");
                }


                // check for function definition
                if (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.Keyword && nextTokens[0].Value == "fun")
                {
                    ++crnt;
                    --end_of_expr;
                    var old_crnt = crnt;
                    List<(VarResolver, BaseValue.ValueType)> arguments = ParseFunctionHead();
                    end_of_expr -= crnt - old_crnt;
                    // find matching nfu
                    int depth = 1;
                    int matching_nfu = 0;
                    for (int i = 0; i < nextTokens.Count; i++)
                    {
                        if (nextTokens[i].Type != Token.TokenType.Keyword)
                            continue;
                        if (nextTokens[i].Value == "fun")
                            ++depth;
                        if (nextTokens[i].Value == "nfu")
                            --depth;
                        if (depth == 0)
                        {
                            matching_nfu = i;
                            break;
                        }
                    }
                    if (matching_nfu == 0)
                        throw new Exceptions.MissingTokenException("Couldn't find matching 'nfu' keyword");
                    var body = new ProgramParser(nextTokens.Take(matching_nfu - 1).ToList()).ParseProgram();
                    end_of_expr -= matching_nfu + 1;
                    crnt += matching_nfu + 1;
                    lhs = new FunctionValue(new FunctionRunner(arguments, body));
                    while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.NewLine) ++crnt;
                    // only operation that can be done on a defined function is a function call
                    if (nextTokens.Count == 0)
                    {
                        crnt += skipWhenReturning;
                        return lhs;
                    }
                    if (nextTokens[0].Type != Token.TokenType.RoundBracket)
                    {
                        crnt += skipWhenReturning;
                        return lhs;
                    }
                }

                if (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.Constant)
                {
                    var token = nextTokens[0];
                    ++crnt;
                    --end_of_expr;
                    if (token.Value[0] == '"')
                        lhs = new StringValue(StringValue.EscapeString(token.Value.Substring(1, token.Value.Length - 2)));
                    else
                        lhs = new NumberValue(double.Parse(token.Value, CultureInfo.InvariantCulture));
                }
                else if (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.EmptyObject)
                {
                    ++crnt;
                    --end_of_expr;
                    lhs = new ObjectValue();
                }
                else if (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.VarType && nextTokens[0].Value == "nil")
                {
                    ++crnt;
                    --end_of_expr;
                    lhs = NilValue.Nil;
                }
                else if (nextTokens.Count > 0)
                {
                    // parse variable
                    var old_crnt = crnt;
                    lhs = ParseVariable();
                    end_of_expr -= crnt - old_crnt;
                }
                // check for end of expr
                if (nextTokens.Count == 0 || end_of_expr <= 0)
                {
                    crnt += skipWhenReturning;
                    return lhs;
                }
                // check for function calling or object accessor
                while (nextTokens.Count > 0 && (nextTokens[0].Type == Token.TokenType.RoundBracket || nextTokens[0].Type == Token.TokenType.SquareBracket))
                {
                    // check for function calling
                    while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.RoundBracket)
                    {
                        if (lhs == null)
                            throw new Exceptions.ExpressionParsingErrorException("Tried to parse function call but lhs is null");
                        if (nextTokens[0].Value == ")")
                            throw new Exceptions.UnexpectedTokenException("Expected '(' but found ')'");
                        // parse function arguments
                        ++crnt; // step over '('
                        --end_of_expr;
                        // scan for comma or ')'
                        depth_r = 0;
                        depth_s = 0;
                        int lastArg = 0;
                        List<IEvaluable> arguments = new List<IEvaluable>();
                        for (int i = 0; i < end_of_expr; i++)
                        {
                            if (nextTokens[i].Type == Token.TokenType.RoundBracket)
                            {
                                if (nextTokens[i].Value == "(")
                                    ++depth_s;
                                else
                                    --depth_s;
                            }
                            if (nextTokens[i].Type == Token.TokenType.SquareBracket)
                            {
                                if (nextTokens[i].Value == "[")
                                    ++depth_r;
                                else
                                    --depth_r;
                            }
                            if (depth_r > 0 || depth_s > 0)
                                continue;
                            if (depth_s < 0)
                            {
                                // reached ')'
                                if (i == 0) break; // no arguments
                                arguments.Add(new ProgramParser(nextTokens.Skip(lastArg).Take(i - lastArg).ToList()).ParseExpression());
                                lastArg = i;
                                break;
                            }
                            if (depth_s == 0 && nextTokens[i].Type == Token.TokenType.Comma)
                            {
                                arguments.Add(new ProgramParser(nextTokens.Skip(lastArg).Take(i - lastArg).ToList()).ParseExpression());
                                lastArg = i + 1;
                                continue;
                            }
                        }
                        lhs = new FunctionCall(lhs, arguments);
                        // step crnt
                        crnt += lastArg + 1; // + 1 step over ')'
                        end_of_expr -= lastArg + 1;
                    }
                    // check for object accessor
                    while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.SquareBracket)
                    {
                        if (lhs == null)
                            throw new Exceptions.ExpressionParsingErrorException("Tried to parse object accessor but lhs is null");
                        if (nextTokens[0].Value == "]")
                            throw new Exceptions.UnexpectedTokenException("Expected '[' but found ']'");
                        // parse accessor
                        ++crnt; // step over '['
                        --end_of_expr;
                        // find matching ']'
                        int match = 0;
                        int depth = 1;
                        for (int i = 0; i < end_of_expr; i++)
                        {
                            if (nextTokens[i].Type != Token.TokenType.SquareBracket) continue;
                            if (nextTokens[i].Value == "[")
                                ++depth;
                            else
                                --depth;
                            if (depth == 0)
                            {
                                match = i;
                                break;
                            }
                        }
                        if (match == 0)
                            throw new Exceptions.MissingTokenException("Couldn't find matching ']'");
                        var acc = new ProgramParser(nextTokens.Take(match).ToList()).ParseExpression();
                        lhs = new ObjectAccessor(lhs, acc);
                        // step crnt
                        crnt += match + 1;
                        end_of_expr -= match + 1;
                    }
                }

                crnt += skipWhenReturning;
                if (lhs == null)
                    throw new Exceptions.ExpressionParsingErrorException("Reached end of parsing function but lhs is null");
                return lhs;
            }

            public List<(VarResolver, BaseValue.ValueType)> ParseFunctionHead()
            {
                ++crnt; // step over opening '('
                List<(VarResolver, BaseValue.ValueType)> ret = new List<(VarResolver, BaseValue.ValueType)>();
                while (nextTokens[0].Type != Token.TokenType.RoundBracket || nextTokens[0].Value != ")")
                {
                    // parse variable
                    VarResolver arg = ParseVariable();
                    BaseValue.ValueType type = BaseValue.ValueType.AnyType;
                    if (nextTokens[0].Type == Token.TokenType.VarType)
                    {
                        type = BaseValue.ValueTypeFromString(nextTokens[0].Value);
                        ++crnt;
                    }
                    ret.Add((arg, type));
                    if (nextTokens[0].Type == Token.TokenType.Comma)
                        ++crnt; // step over ','
                }
                ++crnt; // step over closing ')'
                return ret;
            }

            public VarResolver ParseVariable()
            {
                VarResolver current;
                if (nextTokens[0].Type == Token.TokenType.Variable)
                {
                    current = VariableExpander.FromString(nextTokens[0].Value);
                    ++crnt;
                    while (nextTokens.Count > 0 && nextTokens[0].Type == Token.TokenType.SquareBracket)
                    {
                        if (nextTokens[0].Value == "]")
                            throw new Exceptions.UnexpectedTokenException("Expected '[' but found ']' instead");
                        ++crnt; // step over '['
                        // find mathcing ']'
                        int match = 0;
                        int depth = 1;
                        for (int i = 0; i < nextTokens.Count; i++)
                        {
                            if (nextTokens[i].Type != Token.TokenType.SquareBracket) continue;
                            if (nextTokens[i].Value == "[")
                                ++depth;
                            else
                                --depth;
                            if (depth == 0)
                            {
                                match = i;
                                break;
                            }
                        }
                        if (match == 0)
                            throw new Exceptions.MissingTokenException("Expected ']' but reached end of input");
                        var expr = new ProgramParser(nextTokens.Take(match).ToList()).ParseExpression();
                        crnt += match + 1;
                        current = new VarObjectResolver(current, expr);
                    }
                    return current;
                }
                else
                    throw new Exceptions.UnexpectedTokenException("Tried to parse non-variable token " + nextTokens[0]);
            }
        }

        public static class VariableExpander
        {
            public static VarResolver FromString(string full_var_name)
            {
                full_var_name = full_var_name.Substring(1); // skip over '$'
                // check for simple name
                int obj_acc = full_var_name.LastIndexOf(':');
                if (obj_acc <= 0)
                    return new VarNameResolver(full_var_name); // not object accessor
                var split = full_var_name.Split(':');
                if (split[0].Length == 0)
                {
                    split = split.Skip(1).ToArray();
                    split[0] = ":" + split[0];
                }
                VarResolver baseVar = new VarNameResolver(split[0]);
                for (int i = 1; i < split.Length; i++)
                    baseVar = new VarObjectResolver(baseVar, new StringValue(split[i]));
                return baseVar;
            }
        }
    }

    namespace Exceptions
    {
        [Serializable]
        public class InvalidOperationException : Exception
        {
            public InvalidOperationException(string message) : base(message) { }
        }

        [Serializable]
        public class NullOperandException : Exception
        {
            public NullOperandException(string side, string op) : base(side + " value of operator " + op + " is null") { }
        }

        [Serializable]
        public class UnknownTokenException : Exception
        {
            public UnknownTokenException(string message) : base(message) { }
        }

        [Serializable]
        public class VariableTypeException : Exception
        {
            public VariableTypeException(string message) : base(message) { }
        }

        [Serializable]
        public class InvalidParametersException : Exception
        {
            public InvalidParametersException(string message) : base(message) { }
        }

        [Serializable]
        public class IterationLoopException : Exception
        {
            public IterationLoopException(int maxTimes) : base("Loop ran more than " + maxTimes + " times") { }
        }

        [Serializable]
        public class InvalidStringFormatException : Exception
        {
            public InvalidStringFormatException(string message) : base(message) { }
        }

        [Serializable]
        public class UnexpectedTokenException : Exception
        {
            public UnexpectedTokenException(string message) : base(message) { }
        }

        [Serializable]
        public class InvalidUnaryOperatorException : Exception
        {
            public InvalidUnaryOperatorException(string message) : base(message) { }
        }

        [Serializable]
        public class MissingTokenException : Exception
        {
            public MissingTokenException(string message) : base(message) { }
        }

        [Serializable]
        public class InvalidValueType : Exception
        {
            public InvalidValueType(string message) : base(message) { }
        }

        [Serializable]
        public class ExpressionParsingErrorException : Exception
        {
            public ExpressionParsingErrorException(string message) : base(message) { }
        }
    }

    namespace DefaultFunctions
    {
        public static class DefaultFunctions
        {
            public static ICallable print = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!str"), BaseValue.ValueType.AnyType) }, (args, scope) =>
            {
                scope.LogFunction((string)args[0].Stringify().Value);
                return NilValue.Nil;
            });
            public static ICallable pow = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!a"), BaseValue.ValueType.Number), (new VarNameResolver("!b"), BaseValue.ValueType.Number) }, args =>
            {
                return new NumberValue(Math.Pow((double)args[0].Value, (double)args[1].Value));
            });
            public static ICallable range = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!a"), BaseValue.ValueType.Number) }, args =>
            {
                // check if args[0] is whole number >= 0
                double n = (double)args[0].Value;
                if (Math.Floor(n) != n) return NilValue.Nil;
                if (n < 0) return NilValue.Nil;
                var obj = new ObjectValue();
                for (int i = 0; i < n; i++)
                    obj.SetChild(i.ToString(), new NumberValue(i));
                obj.SetChild("length", new NumberValue(n));
                return obj;
            });
            public static ICallable range2 = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!a"), BaseValue.ValueType.Number), (new VarNameResolver("!b"), BaseValue.ValueType.Number) }, args =>
            {
                double a = (double)args[0].Value;
                double b = (double)args[1].Value;
                var obj = new ObjectValue();
                if (a <= b)
                {
                    int j = 0;
                    for (double i = a; i < b; ++i, ++j)
                        obj.SetChild(j.ToString(), new NumberValue(i));
                    obj.SetChild("length", new NumberValue(j));
                    return obj;
                }
                else
                {
                    int j = 0;
                    for (double i = a; i > b; --i, ++j)
                        obj.SetChild(j.ToString(), new NumberValue(i));
                    obj.SetChild("length", new NumberValue(j));
                    return obj;
                }
            });
            public static ICallable range3 = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!a"), BaseValue.ValueType.Number), (new VarNameResolver("!b"), BaseValue.ValueType.Number), (new VarNameResolver("!c"), BaseValue.ValueType.Number) }, args =>
            {
                double a = (double)args[0].Value;
                double b = (double)args[1].Value;
                double c = (double)args[2].Value;
                var obj = new ObjectValue();
                if (a <= b)
                {
                    if (c <= 0) return NilValue.Nil; // loop will never end
                    int j = 0;
                    for (double i = a; i < b; i += c, ++j)
                        obj.SetChild(j.ToString(), new NumberValue(i));
                    obj.SetChild("length", new NumberValue(j));
                    return obj;
                }
                else
                {
                    if (c >= 0) return NilValue.Nil; // loop will never end
                    int j = 0;
                    for (double i = a; i > b; i += c, ++j)
                        obj.SetChild(j.ToString(), new NumberValue(i));
                    obj.SetChild("length", new NumberValue(j));
                    return obj;
                }
            });
            public static ICallable @typeof = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!obj"), BaseValue.ValueType.AnyType) }, args =>
            {
                switch (args[0].Type)
                {
                    case BaseValue.ValueType.Number:
                        return new StringValue("number");
                    case BaseValue.ValueType.String:
                        return new StringValue("string");
                    case BaseValue.ValueType.Object:
                        return new StringValue("object");
                    case BaseValue.ValueType.Function:
                        return new StringValue("function");
                    default:
                        return new StringValue("nil");
                }
            });
            public static ICallable asciiC = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!n"), BaseValue.ValueType.Number) }, args =>
            {
                double n = (double)args[0].Value;
                if (Math.Floor(n) != n) return NilValue.Nil;
                if (n < 0 || n > 255) return NilValue.Nil;
                return new StringValue(((char)(int)n).ToString());
            });
            public static ICallable asciiN = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!s"), BaseValue.ValueType.String) }, args =>
            {
                string s = (string)args[0].Value;
                if (s.Length != 1) return NilValue.Nil;
                return new NumberValue(s[0]);
            });
            public static ICallable isarray = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!obj"), BaseValue.ValueType.Object) }, args =>
            {
                return new NumberValue(((ObjectValue)args[0]).IsArrayConvention() ? 1 : 0);
            });
            public static ICallable stoa = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!s"), BaseValue.ValueType.String) }, args =>
            {
                string s = (string)args[0].Value;
                var obj = new ObjectValue();
                for (int i = 0; i < s.Length; i++)
                    obj.SetChild(i.ToString(), new StringValue(s[i].ToString()));
                obj.SetChild("length", new NumberValue(s.Length));
                return obj;
            });
            public static ICallable atos = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!v"), BaseValue.ValueType.Object) }, args =>
            {
                var obj = (ObjectValue)args[0];
                if (!obj.IsArrayConvention()) return NilValue.Nil;
                StringBuilder s = new StringBuilder();
                int arr_len = (int)(double)obj.GetChild("length").Value;
                for (int i = 0; i < arr_len; i++)
                {
                    var ch = obj.GetChild(i.ToString());
                    s.Append(ch.Stringify().Value);
                }
                return new StringValue(s.ToString());
            });
            public static ICallable floor = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!val"), BaseValue.ValueType.Number) }, args =>
            {
                double f = (double)args[0].Value;
                return new NumberValue(Math.Floor(f));
            });
            public static ICallable ceil = new NativeFunction(new List<(VarResolver, BaseValue.ValueType)> { (new VarNameResolver("!val"), BaseValue.ValueType.Number) }, args =>
            {
                double f = (double)args[0].Value;
                return new NumberValue(Math.Ceiling(f));
            });

            public static void RegisterFunctions(Scope scope)
            {
                scope.SetVariable(":print", new FunctionValue(print));
                scope.SetVariable(":pow", new FunctionValue(pow));
                scope.SetVariable(":range", new FunctionValue(range));
                scope.SetVariable(":range2", new FunctionValue(range2));
                scope.SetVariable(":range3", new FunctionValue(range3));
                scope.SetVariable(":typeof", new FunctionValue(@typeof));
                scope.SetVariable(":asciiC", new FunctionValue(asciiC));
                scope.SetVariable(":asciiN", new FunctionValue(asciiN));
                scope.SetVariable(":stoa", new FunctionValue(stoa));
                scope.SetVariable(":atos", new FunctionValue(atos));
                scope.SetVariable(":isarray", new FunctionValue(isarray));
                scope.SetVariable(":floor", new FunctionValue(floor));
                scope.SetVariable(":ceil", new FunctionValue(ceil));
            }
        }
    }

    namespace Utils
    {
        public static class StringExtensions
        {
            public static string Indent(this string s, int indent)
            {
                return new string(' ', indent) + s;
            }
            public static bool IsStringToken(this string s) => s[0] == '"' && s.Last() == '"';
            public static bool IsVarName(this char c)
            {
                if (c >= 'A' && c <= 'Z') return true;
                if (c >= 'a' && c <= 'z') return true;
                if (c >= '0' && c <= '9') return true;
                if (c == '$' || c == '_' || c == ':' || c == '!') return true;
                return false;
            }
            public static string ContainsAny(this string s, string[] values)
            {
                int minIdx = -1;
                string matchVal = "";
                foreach (var value in values)
                    if (s.Contains(value))
                    {
                        if (minIdx == -1 || s.IndexOf(value) < minIdx)
                        {
                            minIdx = s.IndexOf(value);
                            matchVal = value;
                        }
                    }
                return minIdx == -1 ? "" : matchVal;
            }
        }
    }
}