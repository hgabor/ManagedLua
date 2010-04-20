
using System;
using System.Collections.Generic;
using System.Reflection;
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

	[AttributeUsage(AttributeTargets.Method)]
	public class MultiRetAttribute: Attribute {}


	public partial class StdLib {
		LuaVM vm;
		
		public StdLib(LuaVM vm) { this.vm = vm; }
		
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
		private ulong ConvertFromBase(string from, uint numberBase) {
			ulong acc = 0;
			for (int i = 0; i < from.Length; ++i) {
				int k = digits.IndexOf(char.ToLower(from[i]));
				if (k >= numberBase || k == -1) throw new InvalidCastException("Cannot convert string to number, it contains invalid characters");
				acc = acc * numberBase + (ulong)k;
			}
			return acc;
		}

		[Lib("tonumber")]
		public object tonumber(params object[] args) {
			try {
				if (args.Length > 2 || args.Length == 0) throw new ArgumentException("Function takes 1 or 2 arguments");
				else if (args.Length == 2 && !(args[1] is double)) throw new ArgumentException("Second argument must be a number");
				else if (args[0] is double) {
					return (double)args[0];
				}
				else if (args.Length == 1 || args[1].Equals(10d)) {
					return Convert.ToDouble(args[0]);
				}
				else {
					return (double)ConvertFromBase((string)args[0], (uint)(double)args[1]);
				}
			}
			catch (InvalidCastException) {
				return Nil.Value;
			}
		}
		
		[Lib("unpack")]
		[MultiRet]
		public object[] unpack(Table table, params object[] l) {
			double i = l.Length >= 1 ? (double)l[0] : 1;
			double j = l.Length >= 1 ? (double)l[0] : table.Length;
			
			var ret = new List<object>();
			while (i <= j) {
				ret.Add(l[(int)i]);
			}
			return ret.ToArray();
		}
		
		
		[Lib("next")]
		[MultiRet]
		public object[] next(Table table, params object[] p) {
			object index = (p.Length > 0 || p[0] == Nil.Value)  ? p[0] : Nil.Value;
			index = table.NextKey(index);
			object o = table[index];
			if (o == Nil.Value) {
				return new object[] { Nil.Value };
			}
			else if (o == null) throw new Exception("Key does not exist in the table");
			else {
				return new object[] {
					index, o
				};
			}
		}
		
		[Lib("pairs")]
		[MultiRet]
		public object[] pairs(Table t) {
			return new object[] {
				vm.GetGlobalVar("next"),
				t,
				Nil.Value
			};
		}
		
		
		[MultiRet]
		public object[] ipairs_next(Table t, params object[] p) {
			double index = (double)((p.Length > 0 || p[0] == Nil.Value)  ? p[0] : -1);
			++index;
			object o = t[index];
			if (o == Nil.Value) {
				return new object[] { Nil.Value };
			}
			else {
				return new object[] {
					index, o
				};
			}
		}
		
		[Lib("ipairs")]
		[MultiRet]
		public object[] ipairs(Table t) {
			return new object[] {
				vm.WrapFunction(typeof(StdLib).GetMethod("ipairs_next"), this),
				t,
				0.0
			};
		}
	}
}
