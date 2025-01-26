using KarlsonMapEditor.Automata.Backbone;
using KarlsonMapEditor.Automata.DefaultFunctions;
using KarlsonMapEditor.Automata.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KarlsonMapEditor.Scripting_API
{
    public class ScriptRunner
    {
        public Scope scope;

        public ScriptRunner(FunctionRunner mainFn)
        {
            scope = new Scope();
            scope.LogFunction = a => Loadson.Console.Log("[amta] " + a);
            DefaultFunctions.RegisterFunctions(scope);
            UnityAPI.RegisterFunctions(scope);
            mainFn.Call(scope);
        }

        public BaseValue InvokeFunction(string functionName, params object[] param)
        {
            var f = scope.GetVariable(functionName);
            if (f.Type != BaseValue.ValueType.Function)
                return NilValue.Nil;
            var fnr = new FunctionCall(f, param.WrapToParams());
            return fnr.Evaluate(scope);
        }
    }
}
