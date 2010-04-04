
using System;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Nil.
	/// </summary>
	public class Nil {
		private Nil() {}

		public static Nil Value = new Nil();

		public override string ToString() {
			return "nil";
		}
	}
}
