
using System;
using System.Collections.Generic;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {
	
	/// <summary>
	/// Manages upvalues for closures
	/// </summary>
	/// <description>
	/// An upvalue is a value that can live outside the lua interpreter's stack frame.
	/// Initially, it points to an index of a lua stack. After it is closed (the value is discarded from the stack),
	/// it survives, and other functions that reference it (closures) can still access it.
	/// </description>
	class UpValue {
		private object value = Nil.Value;
		private List<object> stack;
		private int stackIndex;
		private bool closed = false;

		public UpValue(List<object> stack, int stackIndex) {
			this.stack = stack;
			this.stackIndex = stackIndex;
		}

		public void CloseIfIndexGreaterThanOrEquals(int i) {
			if (!closed && stackIndex >= i) Close();
		}

		public void Close() {
			if (closed) return;
			value = stack[stackIndex];
			stack = null;
			closed = true;
		}

		public object Value {
			get {
				if (closed) return this.value;
				else return stack[stackIndex];
			}
			set {
				if (closed) this.value = value;
				else stack[stackIndex] = value;
			}
		}
	}
}
