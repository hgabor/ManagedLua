using System;
using System.Collections.Generic;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {

	/// <summary>
	/// Provides common operations for function/closure calls.
	/// To call a function, you first have to create a callable
	/// closure with the CreateCallableInstance() function.
	/// </summary>
	abstract class ClosureBase: Closure {
		//TODO: IsPrototype check
		protected internal List<object> stack = new List<object>();
		private int top;
		protected internal int Top {
			get { return top; }
			set {
				top = value;
				while (stack.Count < top) { stack.Add(Nil.Value); }
			}
		}

		/// <summary>
		/// Creates a closure with default properties.
		/// </summary>
		public ClosureBase() {}

		/// <summary>
		/// Creates a closure based on another one, by copying stack contents.
		/// </summary>
		/// <param name="source">The source closure</param>
		protected ClosureBase(ClosureBase source) {
			this.stack.AddRange(source.stack);
			this.top = source.top;
		}

		/// <summary>
		/// Pushes a parameter onto the function's stack.
		/// </summary>
		/// <param name="o">The object</param>
		public virtual void AddParam(object o) {
			stack.Add(o);
			Top++;
		}

		/// <summary>
		/// Returns the return value(s) of the function.
		/// </summary>
		/// <returns>The list of return values</returns>
		public List<object> GetResults() {
			return stack;
		}

		/// <summary>
		/// Creates a callable closure of the prototype.
		/// </summary>
		/// <returns>The callable closure</returns>
		public abstract ClosureBase CreateCallableInstance();
	}
}
