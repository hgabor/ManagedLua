
using System;

namespace ManagedLua.Interpreter {
	class ThreadResult {
		public enum ResultType {
			Dead, FunctionCall, Error
		}
		public ResultType Type {get; set;}
		//public InternalClosure func {get;set;}
		public object Data;
	}
}
