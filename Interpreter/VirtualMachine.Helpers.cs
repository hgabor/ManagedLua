
using System;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {
	public partial class VirtualMachine {
		private static bool ToBool(object value) {
			if (value.Equals(false) || value == Nil.Value || value == null) return false;
			else return true;
		}

		private static bool LessThan(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 < (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.Compare((string)op1, (string)op2) < 0;
			}
			throw new ArgumentException(string.Format("Cannot compare {0} and {1}", op1.GetType(), op2.GetType()));
		}

		private static bool LessThanEquals(object op1, object op2) {
			if (op1 is double && op2 is double) {
				return (double)op1 <= (double)op2;
			}
			if (op1 is string && op2 is string) {
				return string.Compare((string)op1, (string)op2) <= 0;
			}
			throw new ArgumentException(string.Format("Cannot compare {0} and {1}", op1.GetType(), op2.GetType()));
		}

		private static new bool Equals(object op1, object op2) {
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