using KarlsonMapEditor.Automata.Backbone;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KarlsonMapEditor.Scripting_API
{
    public static class UnityAPI
    {
        static List<(VarResolver, BaseValue.ValueType)> GenerateHead(params BaseValue.ValueType[] types)
        {
            var list = new List<(VarResolver, BaseValue.ValueType)>();
            int i = 0;
            foreach ( var type in types )
                list.Add((new VarNameResolver("$!" + i++), type));
            return list;
        }

        static ICallable _Vector2 = new NativeFunction(GenerateHead(BaseValue.ValueType.Number, BaseValue.ValueType.Number), (args) => new Vector2(args[0].Unwrap<float>(), args[1].Unwrap<float>()).Wrap());
        static ICallable _Vector3 = new NativeFunction(GenerateHead(BaseValue.ValueType.Number, BaseValue.ValueType.Number, BaseValue.ValueType.Number), (args) => new Vector3(args[0].Unwrap<float>(), args[1].Unwrap<float>(), args[2].Unwrap<float>()).Wrap());
        static ICallable _Quaternion = new NativeFunction(GenerateHead(BaseValue.ValueType.Number, BaseValue.ValueType.Number, BaseValue.ValueType.Number, BaseValue.ValueType.Number), (args) => new Quaternion(args[0].Unwrap<float>(), args[1].Unwrap<float>(), args[2].Unwrap<float>(), args[3].Unwrap<float>()).Wrap());
        static ICallable ToEuler = new NativeFunction(GenerateHead(BaseValue.ValueType.Object), args => args[0].Unwrap<Quaternion>().eulerAngles.Wrap());
        static ICallable FromEuler = new NativeFunction(GenerateHead(BaseValue.ValueType.Object), args => Quaternion.Euler(args[0].Unwrap<Vector3>()).Wrap());
        static ICallable GameObject_Find = new NativeFunction(GenerateHead(BaseValue.ValueType.String), args => GameObject.Find(args[0].Unwrap<string>()).Wrap());
        static ICallable GameObject_Instantiate = new NativeFunction(GenerateHead(BaseValue.ValueType.Object), args => UnityEngine.Object.Instantiate(args[0].Unwrap<GameObject>()).Wrap());
        static ICallable GameObject_Destroy = new NativeFunction(GenerateHead(BaseValue.ValueType.Object), args => { UnityEngine.Object.Destroy(args[0].Unwrap<GameObject>()); return NilValue.Nil; });

        public static void RegisterFunctions(Scope scope)
        {
            scope.SetVariable(":Vector2", new FunctionValue(_Vector2));
            scope.SetVariable(":Vector3", new FunctionValue(_Vector3));
            scope.SetVariable(":Quaternion", new FunctionValue(_Quaternion));
            scope.SetVariable(":ToEuler", new FunctionValue(ToEuler));
            scope.SetVariable(":FromEuler", new FunctionValue(FromEuler));
            scope.SetVariable(":GameObject", new ReadonlyValue(new Dictionary<string, object>
            {
                { "Find", new FunctionValue(GameObject_Find) },
                { "Instantiate", new FunctionValue(GameObject_Instantiate) },
                { "Destroy", new FunctionValue(GameObject_Destroy) },
            }));
            scope.SetVariable(":LocalPlayer", new FunctionValue(new NativeFunction(GenerateHead(), args => new NativeValue(PlayerMovement.Instance))));
        }
    }
}
