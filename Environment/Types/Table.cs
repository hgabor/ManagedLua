
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table: IEnumerable {
		public Table() {
		}

		//We need a container that can:
		// Store i/v pairs, where i is double
		// Store k/v pairs, where k is refernce type
		// Order of i and k need to be consistent

		//private OrderedDictionary d = new OrderedDictionary();
		//Too slow

		private class MyEqualityComparer: IEqualityComparer {
			public int GetHashCode(object a) {
				return a.GetHashCode();
			}

			public new bool Equals(object a, object b) {
				return object.Equals(a, b);
			}
		}

		private Hashtable h = new Hashtable(new MyEqualityComparer());
		private Dictionary<double, object> a = new Dictionary<double, object>();

		public bool IsSet(object key) {
			if (key is double)
				return a.ContainsKey((double)key);
			else
				return h.ContainsKey(key);
		}

		public object this[object key] {
			get {
				if (key is double) return this[(double)key];
				if (h.ContainsKey(key)) {
					return h[key];
				}
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
				if (key is double) this[(double)key] = value;
				else if (key == Nil.Value) {
					throw new ArgumentOutOfRangeException("Key cannot be nil");
				}
				else if (value == Nil.Value) {
					h.Remove(key);
				}
				else {
					h[key] = value;
				}
			}
		}

		public object this[double key] {
			get {
				if (a.ContainsKey(key)) {
					return a[key];
				}
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
				if (double.IsNaN(key)) {
					throw new ArgumentOutOfRangeException("Key cannot be NaN");
				}
				if (value == Nil.Value) {
					a.Remove(key);
				}
				else {
					if (a.ContainsKey(key)) {
						a[key] = value;
					}
					else {
						a.Add(key, value);
					}
				}
			}
		}

		public object RawGet(object key) {
			if (key is double) return this.RawGet((double)key);
			if (h.ContainsKey(key)) {
				return h[key];
			}
			else
				return Nil.Value;
		}


		public void RawSet(object key, object value) {
			if (key is double) this.RawSet((double)key, value);
			if (value == Nil.Value) {
				h.Remove(key);
			}
			else {
				h[key] = value;
			}
		}

		public object RawGet(double key) {
			if (a.ContainsKey(key)) {
				return a[key];
			}
			else
				return Nil.Value;
		}

		public void RawSet(double key, object value) {
			if (value == Nil.Value) {
				a.Remove(key);
			}
			else {
				if (a.ContainsKey(key)) {
					a[key] = value;
				}
				else {
					a.Add(key, value);
				}
			}
		}

		public Table ShallowClone() {
			Table t = new Table();
			foreach (DictionaryEntry v in this.h) {
				t.h.Add(v.Key, v.Value);
			}
			foreach (var v in this.a) {
				t.a.Add(v.Key, v.Value);
			}
			t.metatable = this.metatable;
			return t;
		}


		public double Length {
			get {
				double i = 1;
				while (true) {
					if (this[i] == Nil.Value) {
						return i - 1;
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

		private object NextIndex(double? d) {
			foreach (var k in a.Keys) {
				if (d == null) return k;
				else if (d == k) {
					//return the next value
					d = k;
				}
			}
			return null;
		}

		public object NextKey(object o) {
			if (o == null) throw new ArgumentNullException("o");
			if (o == Nil.Value || o is double) {
				var ret = NextIndex(o == Nil.Value ? null : (double?)o);
				if (ret != null) return ret;
				else o = null;
			}
			foreach (var k in h.Keys) {
				if (o == null) return k;
				else if (k.Equals(o)) {
					//return the next value
					o = null;
				}
			}
			return Nil.Value;
		}

		public IEnumerator GetEnumerator() {
			foreach	(var k in a.Keys)
				yield return k;
			foreach (var k in h.Keys)
				yield return k;
		}
	}
}
