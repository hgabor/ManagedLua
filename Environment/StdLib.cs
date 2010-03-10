
using System;
using System.Text;
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

		private const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
		private long ConvertFromBase(string from, int numberBase) {
			long acc = 0;
			for (int i = 0; i < from.Length; ++i) {
				int k = digits.IndexOf(from[i]);
				acc = acc * numberBase + k;
			}
			return acc;
		}

		[Lib("tonumber")]
		public object tonumber(params object[] args) {
			try {
				if (args.Length >= 2 || args.Length == 0) throw new ArgumentException("Function takes 1 or 2 arguments");
				else if (args.Length == 2 && !(args[1] is double)) throw new ArgumentException("Second argument must be a number");
				else if (args[0] is double) {
					return (double)args[0];
				}
				else if (args.Length == 1 || args[1].Equals(10d)) {
					return Convert.ToDouble(args[0]);
				}
				else {
					return (double)ConvertFromBase((string)args[0], (int)args[1]);
				}
			}
			catch (InvalidCastException) {
				return Nil.Value;
			}
		}
	}
}
