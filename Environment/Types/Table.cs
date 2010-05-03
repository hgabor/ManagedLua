
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table {
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
				if (value == Nil.Value) {
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
			foreach(var k in a.Keys) {
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
	}
}
