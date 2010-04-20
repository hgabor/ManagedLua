
using System;
using System.Collections;
using System.Collections.Specialized;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table {
		public Table() {
		}
		
		private OrderedDictionary d = new OrderedDictionary();
		
		public bool IsSet(object key) {
			return d.Contains(key);
			
		}
		
		public object this[object key] {
			get {
				if (d.Contains(key))
					return d[key];
				else if (metatable != null) {
					object o = metatable["__index"];
					if (o is Table) {
						return ((Table)o)[key];
					}
					else {
						return Nil.Value;
					}
				}
				else
					return Nil.Value;
			}
			set {
				if (value == Nil.Value) {
					d.Remove(key);
				}
				else {
					d[key] = value;
				}
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
		
		private Table metatable;
		public Table Metatable {
			get {
				return metatable;
			}
			set {
				if (metatable != null) {
					throw new InvalidOperationException("Table already has a metatable");
				}
				metatable = value;
			}
		}
		
		public object NextKey(object o) {
			if (o == null) throw new ArgumentNullException("o");
			int i = 0;
			foreach(DictionaryEntry v in d) {
				if (o == Nil.Value) return v.Key;
				if (o.Equals(v.Key)) {
					if (i == d.Count-1) {
						return Nil.Value;
					}
					else {
						//return the next value
						o = Nil.Value;
					}
				}
				++i;
			}
			return null;
		}
	}
}
