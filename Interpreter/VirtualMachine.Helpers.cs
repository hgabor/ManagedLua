
using System;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {
	public partial class VirtualMachine {
		
		private bool LessThan(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 < (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.Compare((string)op1, (string)op2) < 0;
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

		private bool LessThanEquals(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 <= (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.Compare((string)op1, (string)op2) <= 0;
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

		private new bool Equals(object op1, object op2) {
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
	}
}