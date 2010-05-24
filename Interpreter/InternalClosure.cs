
using System;
using System.Collections.Generic;
using System.Reflection;

using ManagedLua.Environment.Types;
using Function = ManagedLua.Interpreter.VirtualMachine.Function;

namespace ManagedLua.Interpreter {
	/// <summary>
	/// Adapter class for an internal (managed) function.
	/// </summary>
	class InternalClosure: ClosureBase {
		object host;
		MethodInfo method;

		/// <summary>
		/// Creates an InternalClosure instance.
		/// </summary>
		/// <param name="method">An StdLib method</param>
		/// <param name="lib">An instance of the standard library</param>
		public InternalClosure(MethodInfo method, object host) {
			this.host = host;
			this.method = method;
		}

		private InternalClosure(InternalClosure source) : base(source) {
			this.host = source.host;
			this.method = source.method;
		}

		public override ClosureBase CreateCallableInstance() {
			return new InternalClosure(this);
		}

		public void Run() {
			var methodParams = method.GetParameters();

			var callParams = new List<object>();

			int i;
			for (i = 0; i < methodParams.Length; ++i) {
				Type t = methodParams[i].ParameterType;
				if (methodParams[i].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0) {
					//This is a vararg function
					Type et = t.GetElementType();
					var paramArray = new List<object>();
					for (; i < stack.Count; ++i) {
						if (!et.IsInstanceOfType(stack[i])) throw new ArgumentException();
						paramArray.Add(stack[i]);
					}
					callParams.Add(paramArray.ToArray());
					break;
				}
				object param;
				if (i >= stack.Count) {
					var attrs = methodParams[i].GetCustomAttributes(typeof(Environment.OptionalAttribute), false);
					if (attrs.Length > 0) {
						//This param is optional
						param = ((Environment.OptionalAttribute)attrs[0]).DefaultValue;
					}
					else {
						param = Nil.Value;
					}
				}
				else {
					param = stack[i];
				}

				if (!t.IsInstanceOfType(param)) throw new ArgumentException();
				callParams.Add(param);
			}

			object ret;

			try {
				ret = method.Invoke(host, callParams.ToArray());
			}
			catch (TargetInvocationException ex) {
				stack.Clear();
				stack.Add(VMCommand.ERROR);
				stack.Add(ex.ToString());
				return;
			}

			stack.Clear();

			if (method.GetCustomAttributes(typeof(Environment.MultiRetAttribute), false).Length != 0) {
				//MultiRet method
				stack.AddRange((object[])ret);
			}
			else {
				//SingleRet method
				stack.Add(ret);
			}
		}
	}

}
