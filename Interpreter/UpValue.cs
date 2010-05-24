
using System;
using System.Collections.Generic;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {
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
