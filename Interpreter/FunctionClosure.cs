
using System;
using System.Collections.Generic;
using ManagedLua.Environment.Types;
using Function = ManagedLua.Interpreter.VirtualMachine.Function;

namespace ManagedLua.Interpreter {
	/// <summary>
	/// A user defined closure
	/// </summary>
	class FunctionClosure: ClosureBase {
		internal Function f;
		internal uint[] code;
		internal int cSize;
		internal int pc = 0;
		internal int cycle = 0;

		internal int returnRegister;
		internal int returnsSaved;

		internal bool errorHandler = false;

		internal Table env;

		internal List<object> vararg;

		public FunctionClosure(Table env, Function f) {
			this.env = env;
			this.env["_G"] = this.env;
			this.f = f;
			this.code = f.Code;
			cSize = code.Length;
			pc = 0;
			newUpValues = new List<UpValue>();
			if ((f.IsVarargFlag & VirtualMachine.VARARG_ISVARARG) != 0) {
				vararg = new List<object>();
			}
		}

		public FunctionClosure(Table env, FunctionClosure f) : this(env, f.f) {
			stack.AddRange(f.stack);
			upValues.AddRange(f.upValues);
			if ((f.f.IsVarargFlag & VirtualMachine.VARARG_ISVARARG) != 0) {
				vararg.AddRange(f.vararg);
			}
		}

		public override ClosureBase CreateCallableInstance() {
			return new FunctionClosure(this.env, this);
		}


		internal List<UpValue> upValues = new List<UpValue>();
		public void AddUpValue(UpValue uv) {
			upValues.Add(uv);
		}
		public bool NeedsMoreUpValues {
			get {
				return upValues.Count != f.UpValues;
			}
		}

		internal List<UpValue> newUpValues = new List<UpValue>();
		internal void AddNewUpValue(UpValue uv) {
			if (!newUpValues.Exists(o => o.Value.Equals(uv))) {
				newUpValues.Add(uv);
			}
		}

		public override void AddParam(object o) {
			if (Top < f.Parameters) {
				base.AddParam(o);
			}
			else if ((f.IsVarargFlag & VirtualMachine.VARARG_ISVARARG) != 0) {
				vararg.Add(o);
			}
		}
	}

}
