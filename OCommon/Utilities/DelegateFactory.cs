using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Utilities
{
    /// <summary>
    /// 动态代理工厂类
    /// </summary>
    public class DelegateFactory
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="D"></typeparam>
        /// <param name="methodInfo"></param>
        /// <param name="parameterTypes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" ></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static D CreateDelegate<D>(MethodInfo methodInfo,Type[] parameterTypes)where D : class
        {
            Ensure.NotNull(methodInfo, nameof(methodInfo));
            Ensure.NotNull(parameterTypes, nameof(parameterTypes));

            var parameters = methodInfo.GetParameters();
            if(parameters?.Length > parameterTypes.Length)
            {
                throw new ArgumentOutOfRangeException($"函数参数个数{parameters?.Length},实际传入个数{parameterTypes.Length}");
            }
            var dynamicMethod = new DynamicMethod(
                methodInfo.Name,
                MethodAttributes.Static | MethodAttributes.Public,
                CallingConventions.Standard,
                methodInfo.ReturnType,
                parameterTypes,
                typeof(object),
                true)
            {
                InitLocals=false
            };
            var dynamicEmit = new DynamicEmit(dynamicMethod);
            if (!methodInfo.IsStatic)
            {
                dynamicEmit.LoadArgument(0);
                dynamicEmit.CastTo(typeof(object), methodInfo.DeclaringType);
            }
            for(int index = 0; index < parameters.Length; index++)
            {
                dynamicEmit.LoadArgument(index + 1);
                dynamicEmit.CastTo(parameterTypes[index + 1], parameters[index].ParameterType);
            }
            dynamicEmit.Call(methodInfo);
            dynamicEmit.Return();

            return dynamicMethod.CreateDelegate(typeof(D)) as D;

        }
        class DynamicEmit
        {
            private ILGenerator _ilGenerator;
            private static readonly Dictionary<Type, OpCode> _coverts = new Dictionary<Type, OpCode>();

            static DynamicEmit()
            {
                _coverts.Add(typeof(sbyte), OpCodes.Conv_I1);
                _coverts.Add(typeof(short),OpCodes.Conv_I2);
                _coverts.Add(typeof(long), OpCodes.Conv_I4);
                _coverts.Add(typeof(byte), OpCodes.Conv_U1);
                _coverts.Add(typeof(ushort), OpCodes.Conv_U2);
                _coverts.Add(typeof(uint), OpCodes.Conv_U4);
                _coverts.Add(typeof(ulong), OpCodes.Conv_U8);
                _coverts.Add(typeof(float), OpCodes.Conv_R4);
                _coverts.Add(typeof(double), OpCodes.Conv_R8);
                _coverts.Add(typeof(bool), OpCodes.Conv_I1);
                _coverts.Add(typeof(char), OpCodes.Conv_U2);
            }

            public DynamicEmit(DynamicMethod dmethod)
            {
                this._ilGenerator = dmethod.GetILGenerator();
            }
            public DynamicEmit(ILGenerator ilGen)
            {
                this._ilGenerator = ilGen;
            }
            public void LoadArgument(int argumentIndex)
            {
                switch (argumentIndex)
                {
                    case 0:
                        this._ilGenerator.Emit(OpCodes.Ldarg_0);
                        break;
                    case 1:
                        this._ilGenerator.Emit(OpCodes.Ldarg_1);
                        break;
                    case 2:
                        this._ilGenerator.Emit(OpCodes.Ldarg_2);
                        break;
                    case 3:
                        this._ilGenerator.Emit(OpCodes.Ldarg_3);
                        break;
                    default:
                        if (argumentIndex < 0x100)
                        {
                            this._ilGenerator.Emit(OpCodes.Ldarg_S, (byte)argumentIndex);
                        }
                        else
                        {
                            this._ilGenerator.Emit(OpCodes.Ldarg, argumentIndex);
                        }
                        break;
                }
            }

            public void CastTo(Type fromType,Type toType)
            {
                if(fromType != toType)
                {
                    if(toType == typeof(void))
                    {
                        if(!(fromType == typeof(void)))
                        {
                            this.Pop();
                        }
                    }
                    else
                    {
                        if (fromType.IsValueType)
                        {
                            if (toType.IsValueType)
                            {
                                this.Convert(toType);
                                return;
                            }
                            this._ilGenerator.Emit(OpCodes.Box, fromType);
                        }
                        this.CastTo(toType);
                    }
                }
            }

            public void CastTo(Type toType)
            {
                if (toType.IsValueType)
                {                    
                    this._ilGenerator.Emit(OpCodes.Unbox_Any, toType);
                }
                else
                {
                    this._ilGenerator.Emit(OpCodes.Castclass, toType);
                }
            }

            public void Pop()
            {
                this._ilGenerator.Emit(OpCodes.Pop);
            }

            public void Convert(Type toType)
            {
                this._ilGenerator.Emit(_coverts[toType]);
            }
            public void Return()
            {
                this._ilGenerator.Emit(OpCodes.Ret);
            }
            public void Call(MethodInfo method)
            {
                if(method.IsFinal || !method.IsVirtual)
                {
                    this._ilGenerator.EmitCall(OpCodes.Call, method, null);
                }
                else
                {
                    this._ilGenerator.EmitCall(OpCodes.Callvirt, method, null);
                }
            }
        }
    }
}
