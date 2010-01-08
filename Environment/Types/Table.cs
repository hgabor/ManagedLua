
using System;
using System.Collections.Generic;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table {
		public Table() {
		}
		
		private Dictionary<object, object> d = new Dictionary<object, object>();
		
		public bool IsSet(object key) {
			return d.ContainsKey(key);
		}
		
		public object this[object key] {
			get {
				return d[key];
			}
			set {
				if (!IsSet(key)) {
					d.Add(key, value);
				}
				else {
					d[key] = value;
				}
			}
		}
	}
}
