﻿
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	
	/// <summary>
	/// This class contains all lua's standard library functions, and some misc. helper methods.
	/// The documentation of the library functions can be found at http://www.lua.org/manual/5.1/
	/// </summary>
	public partial class StdLib {
		LuaVM vm;

		public StdLib(LuaVM vm) {
			this.vm = vm;
			this.Init_package();
		}
		
		/// <summary>
		/// Converts the specified value to boolean.
		/// Returns false if value is false or nil, otherwise true.
		/// </summary>
		/// <param name="value">The value</param>
		/// <returns>The value converted to boolean</returns>
		public static bool ToBool(object value) {
			if (value == Nil.Value) return false;
			else if (value is bool) return (bool)value;
			else return true;
		}

		[Lib("assert")]
		[MultiRet]
		public object[] assert(object v, [Optional("assertion failed!")] string msg) {
			if (v != Nil.Value && !false.Equals(v)) return new object[] { v, msg };
			else return new object[] { VMCommand.ERROR, msg };
		}
		
		[Lib("collectgarbage")]
		public void collectgarbage(params object[] args) {
			//GC.Collect();
		}

		//TODO: Level attribute
		[Lib("error")]
		[MultiRet]
		public object[] error(string msg, [Optional(1d)] double level) {
			return new object[] { VMCommand.ERROR, msg };
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

		[Lib("loadstring")]
		public Closure loadstring(string code, [OptionalAttribute] object chunkname) {
			string codename;
			if (chunkname == Nil.Value) {
				codename = code.Trim();
				codename = code.Substring(0, Math.Min(codename.Length, 20));
			}
			else {
				codename = chunkname.ToString();
			}
			return vm.CompileString(code, codename);
		}

		[Lib("next")]
		[MultiRet]
		public object[] next(Table table, params object[] p) {
			object index = p.Length > 0 ? p[0] : Nil.Value;
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

		[Lib("pairs")]
		[MultiRet]
		public object[] pairs(Table t) {
			return new object[] {
				vm.GetGlobalVar("next"),
				t,
				Nil.Value
			};
		}

		[Lib("print")]
		public void print(params object[] o) {
			string[] s = Array.ConvertAll(o, obj => obj.ToString());
			Console.WriteLine(string.Join("\t", s));
		}

		[Lib("rawequal")]
		public bool rawequal(object o1, object o2) {
			return (o1.Equals(o2));
		}

		[Lib("rawget")]
		public object rawget(Table table, object key) {
			return table[key];
		}

		[Lib("rawset")]
		public Table rawset(Table table, object key, object value) {
			table[key] = value;
			return table;
		}

		[Lib("select")]
		[MultiRet]
		public object[] select(object o, params object[] args) {
			if ("#".Equals(o)) {
				return new object[] {(double)args.Length };
			}
			else if (o is double) {
				int i = (int)(double)o;
				if (i == 0) throw new ArgumentOutOfRangeException("First argument cannot be zero");
				if (i > 0) {
					var ret = new object[args.Length - i + 1];
					Array.Copy(args, i - 1, ret, 0, args.Length - i + 1);
					return ret;
				}
				else { // i < 0
					var ret = new object[-i];
					Array.Copy(args, args.Length - i, ret, 0, -i);
					return ret;
				}
			}
			else {
				throw new ArgumentOutOfRangeException("A number or \"#\" expected as first argument", "o");
			}
		}
		
		[Lib("setmetatable")]
		public object setmetatable(object o, object mto) {
			Table t = o as Table;
			Table mt = mto as Table;
			if (t != null) {
				if (t.Metatable != null && t.Metatable["__metatable"] != Nil.Value) {
					throw new InvalidOperationException("Table has a protected metatable");
				}
				else if (mto == Nil.Value) {
					t.Metatable = null;
				}
				else if (mt != null) {
					t.Metatable = mt;
				}
				else {
					throw new ArgumentException("Metatable can only be a table");
				}
				return t;
			}
			else {
				throw new ArgumentException("setmetatable is only supported on tables");
			}
		}

		private const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
		private ulong ConvertFromBase(string from, uint numberBase) {
			from = from.Trim();
			if (string.IsNullOrEmpty(from)) throw new InvalidCastException("Cannot convert empty string");
			ulong acc = 0;
			for (int i = 0; i < from.Length; ++i) {
				int k = digits.IndexOf(char.ToLower(from[i]));
				if (k >= numberBase || k == -1) throw new InvalidCastException("Cannot convert string to number, it contains invalid characters");
				acc = acc * numberBase + (ulong)k;
			}
			return acc;
		}

		[Lib("tonumber")]
		public object tonumber(object v, [Optional(10d)] double b) {
			try {
				if (v is double) {
					return v;
				}
				else if (b == 10d) {
					return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
				}
				else {
					return (double)ConvertFromBase((string)v, (uint)b);
				}
			}
			catch (InvalidCastException) {
				return Nil.Value;
			}
			catch (FormatException) {
				return Nil.Value;
			}
		}
		
		[Lib("type")]
		public string type(object value) {
			if (value == Nil.Value) return "nil";
			if (value is double) return "number";
			if (value is string) return "string";
			if (value is bool) return "boolean";
			if (value is Table) return "table";
			if (value is Closure) return "function";
			if (value is Thread) return "thread";
			return "userdata";
		}

		[Lib("unpack")]
		[MultiRet]
		public object[] unpack(Table table, params object[] l) {
			double i = l.Length >= 1 ? (double)l[0] : 1;
			double j = l.Length >= 2 ? (double)l[1] : table.Length;

			var ret = new List<object>();
			while (i <= j) {
				ret.Add(table[i]);
				++i;
			}
			return ret.ToArray();
		}
	}
}
