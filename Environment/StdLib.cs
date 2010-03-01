
using System;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	[AttributeUsage(AttributeTargets.Method)]
	public class LibAttribute: Attribute {
		public String PublicName { get; private set; }
		public String Table { get; private set; }
		public LibAttribute(string PublicName) : this("", PublicName) {}
		public LibAttribute(string Table, string PublicName) {
			this.PublicName = PublicName;
			this.Table = Table;
		}
	}


	public partial class StdLib {
		[Lib("print")]
		public void print(params object[] o) {
			string[] s = Array.ConvertAll(o, obj => obj.ToString());
			Console.WriteLine(string.Join("\t", s));
		}
		
		//TODO: Metatable for UserData
		[Lib("setmetatable")]
		public object setmetatable(object t, Table mt) {
			if (t is Table) {
				((Table)t).Metatable = mt;
				return t;
			}
			else {
				throw new ArgumentException("setmetatable is only supported on tables");
			}
		}
		
		[Lib("getmetatable")]
		public object getmetatable(object t) {
			if (t is Table) {
				return (object)((Table)t).Metatable ?? Nil.Value;
			}
			else {
				throw new ArgumentException("getmetatable is only supported on tables");
			}
		}
	}
}
