
using System;
using System.Collections.Specialized;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table {
		public Table() {
		}
		
		private OrderedDictionary d = new OrderedDictionary();
		//private Dictionary<object, object> d = new Dictionary<object, object>();
		
		public bool IsSet(object key) {
			return d.Contains(key);
		}
		
		public object this[object key] {
			get {
				if (d.Contains(key))
					return d[key];
				else
					return Nil.Value;
			}
			set {
				d[key] = value;
			}
		}
		
		public double Length {
			get {
				double i = 1;
				while (true) {
					if (this[i] == Nil.Value) {
						return i-1;
					}
					i++;
				}
			}
		}
	}
}
