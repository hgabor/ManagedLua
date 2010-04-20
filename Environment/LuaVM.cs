
using System;
using System.Reflection;

namespace ManagedLua.Environment {
	/// <summary>
	/// Description of LuaVM.
	/// </summary>
	public interface LuaVM {
		Types.Closure WrapFunction(MethodInfo method, object obj);
		object GetGlobalVar(object key);
		void SetGlobalVar(object key, object value);
	}
}
