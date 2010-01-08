
using System;

namespace ManagedLua.Environment {
	[AttributeUsage(AttributeTargets.Method)]
	public class LibAttribute: Attribute {
		public String PublicName { get; private set; }
		public String Table { get; private set; }
		public LibAttribute(string PublicName) : this(PublicName, "") {}
		public LibAttribute(string PublicName, string Table) {
			this.PublicName = PublicName;
			this.Table = Table;
		}
	}


	public class StdLib {
		[Lib("print")]
		public void print(string s) {
			Console.WriteLine(s);
		}
	}
}
