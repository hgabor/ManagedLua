
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
		Types.Closure CompileString(string str, string filename);
		Types.Closure Load(System.IO.Stream s, string filename);
		object[] Call(Types.Closure c, params object[] args);
		
		event EventHandler Shutdown;
	}
}
