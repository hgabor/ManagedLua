
using System;
using System.Collections.Generic;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {
	public partial class VirtualMachine {
		internal bool LessThan(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 < (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.CompareOrdinal((string)op1, (string)op2) < 0;
			}
			else {
				Table top1 = op1 as Table;
				if (top1 != null) {
					if (top1.Metatable != null && top1.Metatable["__lt"] is Closure) {
						Closure c = (Closure)top1.Metatable["__lt"];
						return (bool)vminterface.Call(c, op1, op2)[0];
					}
				}
				Table top2 = op2 as Table;
				if (top2 != null) {
					if (top2.Metatable != null && top2.Metatable["__lt"] is Closure) {
						Closure c = (Closure)top2.Metatable["__lt"];
						return (bool)vminterface.Call(c, op1, op2)[0];
					}
				}
				throw new ArgumentException(string.Format("Cannot compare {0} and {1}", op1.GetType(), op2.GetType()));
			}
		}

		internal bool LessThanEquals(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 <= (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.CompareOrdinal((string)op1, (string)op2) <= 0;
			}
			else {
				Table top1 = op1 as Table;
				if (top1 != null) {
					if (top1.Metatable != null && top1.Metatable["__le"] is Closure) {
						Closure c = (Closure)top1.Metatable["__le"];
						return (bool)vminterface.Call(c, op1, op2)[0];
					}
				}
				Table top2 = op2 as Table;
				if (top2 != null) {
					if (top2.Metatable != null && top2.Metatable["__le"] is Closure) {
						Closure c = (Closure)top2.Metatable["__le"];
						return (bool)vminterface.Call(c, op1, op2)[0];
					}
				}
				//Try not op2 < op1
				try {
					return !LessThan(op2, op1);
				}
				catch (Exception ex) {
					throw new ArgumentException(string.Format("Cannot compare {0} and {1}", op1.GetType(), op2.GetType()), ex);
				}
			}
		}

		internal new bool Equals(object op1, object op2) {
			/*if (op1.GetType() != op2.GetType()) return false;
			if (op1 is double && op2 is double) {
				return (double)op1 == (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.Compare((string)op1, (string)op2) == 0;
			}
			if (op1 is bool && op2 is bool) {
				return (bool)op1 == (bool)op2;
			}*/
			//Metamethods are not supported yet
			if (op1 is double && op2 is double) {
				return (double)op1 == (double)op2;
			}
			else return object.Equals(op1, op2);
		}
		
		Dictionary<Type, Table> metatables = new Dictionary<Type, Table>();
		
		private Table GetMetatableOrNull(object obj) {
			if (obj is Table) return ((Table)obj).Metatable;
			//TODO: userdata support
			if (metatables.ContainsKey(obj.GetType())) {
				return metatables[obj.GetType()];
			}
			return null;
		}
		
		private Table GetMetatable(object obj) {
			return GetMetatableOrNull(obj) ?? new Table();
		}
		
		internal object GetElement(object obj, object key) {
			Table t = obj as Table;
			object h;
			if (t != null) {
				object v = t[key];
				if (v != Nil.Value) return v;
				h = GetMetatable(t)["__index"];
				if (h == Nil.Value) return Nil.Value;
			}
			else {
				h = GetMetatable(obj)["__index"];
				if (h == Nil.Value) throw new InvalidOperationException("Cannot index non-table object without __index metamethod!");
			}
			if (h is ClosureBase) {
				return vminterface.Call((ClosureBase)h, obj, key)[0];
			}
			else {
				return GetElement(h, key);
			}
		}
		
		internal void NewIndex(object obj, object key, object value) {
			object h;
			Table t = obj as Table;
			if (t != null) {
				object v = t[key];
				if (v != Nil.Value) {
					t[key] = value;
					return;
				}
				h = GetMetatable(t)["__newindex"];
				if (h == Nil.Value) {
					t[key] = value;
					return;
				}
			}
			else {
				h = GetMetatable(obj)["__newindex"];
				if (h == Nil.Value) {
					throw new InvalidOperationException("Cannot add element to non-table object without __newindex metamethod!");
				}
			}
			if (h is ClosureBase) {
				vminterface.Call((ClosureBase)h, obj, key, value);
			}
			else {
				NewIndex(h, key, value);
			}
		}
		
		private object _getmetatable(object o) {
			Table mt = GetMetatableOrNull(o);
			if (mt == null) return Nil.Value;
			if (mt["__metatable"] != Nil.Value) return mt["__metatable"];
			else return mt;
		}
		
		[Environment.MultiRet]
		private object[] _setfenv(object f, Table env) {
			FunctionClosure fc = f as FunctionClosure;
			if (fc != null) {
				fc.env = env;
				return new object[] { fc };
			}
			double? d = f as double?;
			if (d != null) {
				if (d == 0) {
					currentThread.func.env = env;
					return new object[] {};
				}
				else {
					if (d != 1d) throw new NotSupportedException("Cannot access lower stack frames");
					int id = currentThread.frames.Count - (int)d + 1;
					if (id == currentThread.frames.Count) {
						currentThread.func.env = env;
						return new object[] { currentThread.func };
					}
					else {
						currentThread.frames.Peek().env = env;
						return new object[] { currentThread.func };
					}
				}
			}
			throw new ArgumentException("First argument must be a function or a number");
		}
		
		private object _tostring(object o) {
			object cl = GetMetatable(o)["__tostring"];
			if (cl == Nil.Value) {
				return o.ToString();
			}
			else {
				return vminterface.Call((Closure)cl, o)[0];
			}
		}
	}
}