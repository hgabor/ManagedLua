
using System;
using System.Collections.Generic;

namespace ManagedLua.Environment {
	public partial class StdLib {
		[Lib("coroutine", "create")]
		[MultiRet]
		public object[] coroutine_create(Types.Closure c) {
			return new object[]{ Types.VMCommand.CO_CREATE, c };
		}
		
		[Lib("coroutine", "resume")]
		[MultiRet]
		public object[] coroutine_resume(Types.Thread t, params object[] args) {
			List<object> ret = new List<object>();
			ret.Add(Types.VMCommand.CO_RESUME);
			ret.Add(t);
			ret.AddRange(args);
			return ret.ToArray();
		}
		
		[Lib("coroutine", "running")]
		public Types.VMCommand coroutine_running() {
			return Types.VMCommand.CO_RUNNING;
		}
		
		[Lib("coroutine", "status")]
		public string coroutine_status(Types.Thread thread) {
			switch(thread.Status) {
				case Types.Thread.StatusType.Normal:
					return "normal";
				case Types.Thread.StatusType.Suspended:
					return "suspended";
				case Types.Thread.StatusType.Running:
					return "running";
				case Types.Thread.StatusType.Dead:
					return "dead";
				default:
					throw new ArgumentException("Invalid thread");
			}
		}
		
		[Lib("coroutine", "yield")]
		[MultiRet]
		public object[] coroutine_yield(params object[] args) {
			List<object> ret = new List<object>();
			ret.Add(Types.VMCommand.CO_YIELD);
			ret.Add(true);
			ret.AddRange(args);
			return ret.ToArray();
		}
	}
}
