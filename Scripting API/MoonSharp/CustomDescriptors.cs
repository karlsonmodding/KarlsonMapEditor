using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter;
using System;
using MoonSharp.Interpreter.Compatibility;

namespace KarlsonMapEditor
{
    public class CustomDescriptors
    {
        public class DestructableProxyUserDataDescriptor : IUserDataDescriptor
        {
            private IUserDataDescriptor m_ProxyDescriptor;

            private IProxyFactory m_ProxyFactory;

            // Summary:
            //     Gets the descriptor which describes the proxy object
            public IUserDataDescriptor InnerDescriptor => m_ProxyDescriptor;

            // Summary:
            //     Gets the name of the descriptor (usually, the name of the type described).
            public string Name { get; private set; }

            // Summary:
            //     Gets the type this descriptor refers to
            public Type Type => m_ProxyFactory.TargetType;

            public DestructableProxyUserDataDescriptor(IProxyFactory proxyFactory, IUserDataDescriptor proxyDescriptor, string friendlyName = null)
            {
                m_ProxyFactory = proxyFactory;
                Name = friendlyName ?? (proxyFactory.TargetType.Name + "::proxy");
                m_ProxyDescriptor = proxyDescriptor;
            }

            // Summary:
            //     Proxies the specified object.
            private object Proxy(object obj)
            {
                if (obj == null)
                {
                    return null;
                }

                return m_ProxyFactory.CreateProxyObject(obj);
            }

            // Summary:
            //     Performs an "index" "get" operation.
            //   isDirectIndexing:
            //     If set to true, it's indexed with a name, if false it's indexed through brackets.
            public DynValue Index(Script script, object obj, DynValue index, bool isDirectIndexing)
            {
                bool destroyed = obj != null && (UnityEngine.Object)obj == null;
                if (destroyed) { return DynValue.Nil; }
                return m_ProxyDescriptor.Index(script, Proxy(obj), index, isDirectIndexing);
            }

            // Summary:
            //     Performs an "index" "set" operation.
            //   isDirectIndexing:
            //     If set to true, it's indexed with a name, if false it's indexed through brackets.
            public bool SetIndex(Script script, object obj, DynValue index, DynValue value, bool isDirectIndexing)
            {
                return m_ProxyDescriptor.SetIndex(script, Proxy(obj), index, value, isDirectIndexing);
            }

            // Summary:
            //     Converts this userdata to string
            public string AsString(object obj)
            {
                return m_ProxyDescriptor.AsString(Proxy(obj));
            }

            // Summary:
            //     Gets a "meta" operation on this userdata. If a descriptor does not support this
            //     functionality, it should return "null" (not a nil). These standard metamethods
            //     can be supported (the return value should be a function accepting the classic
            //     parameters of the corresponding metamethod): __add, __sub, __mul, __div, __div,
            //     __pow, __unm, __eq, __lt, __le, __lt, __len, __concat, __pairs, __ipairs, __iterator,
            //     __call These standard metamethods are supported through other calls for efficiency:
            //     __index, __newindex, __tostring
            public DynValue MetaIndex(Script script, object obj, string metaname)
            {
                return m_ProxyDescriptor.MetaIndex(script, Proxy(obj), metaname);
            }

            // Summary:
            //     Determines whether the specified object is compatible with the specified type.
            //     Unless a very specific behaviour is needed, the correct implementation is a simple
            //     " return type.IsInstanceOfType(obj); "
            public bool IsTypeCompatible(Type type, object obj)
            {
                return Framework.Do.IsInstanceOfType(type, obj);
            }
        }
    }
}
