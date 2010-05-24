
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Table.
	/// </summary>
	public class Table: IEnumerable, IList {
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

		public void Sort(IComparer comp) {
			var list = ArrayList.Adapter(this);
			try {
			list.Sort(comp);
			}
			catch {
				throw;
			}
		}

		public IEnumerator GetEnumerator() {
			foreach	(var k in a.Keys)
				yield return k;
			foreach (var k in h.Keys)
				yield return k;
		}

		object IList.this[int index] {
			get {
				return this[(double)index+1];
			}
			set {
				this[(double)index+1] = value;
			}
		}

		bool IList.IsReadOnly {
			get {
				return false;
			}
		}

		bool IList.IsFixedSize {
			get {
				return false;
			}
		}

		int ICollection.Count {
			get {
				return (int)this.Length;
			}
		}

		object syncroot = new object();
		object ICollection.SyncRoot {
			get {
				return this.syncroot;
			}
		}

		bool ICollection.IsSynchronized {
			get {
				return false;
			}
		}

		int IList.Add(object value) {
			double length = Length;
			this[length] = value;
			return (int)length;
		}

		bool IList.Contains(object value) {
			return (((IList)this).IndexOf(value)) != -1;
		}

		void IList.Clear() {
			double length = Length;
			for (double d = 1; d <= length; ++d) {
				this[d] = Nil.Value;
			}
		}

		int IList.IndexOf(object value) {
			double length = Length;
			for (double d = 1; d <= length; ++d) {
				if (object.Equals(a[d], value)) {
					return (int)d - 1;
				}
			}
			return -1;
		}

		void IList.Insert(int index, object value) {
			for (double d = Length; d >= index+1; --d) {
				a[d+1] = a[d];
			}
			a[(double)index] = value;
		}

		void IList.Remove(object value) {
			IList ithis = (IList)this;
			ithis.RemoveAt(ithis.IndexOf(value));
		}

		void IList.RemoveAt(int index) {
			double length = Length;
			for (double d = (double)index+1; d < length; ++d) {
				a[d] = a[d+1];
			}
			a[length] = Nil.Value;
		}

		void ICollection.CopyTo(Array array, int index) {
			int i = index;
			double d = 0;
			double length = Length;
			while(i < array.Length && d < length) {
				array.SetValue(a[d], i);
				++i;
				++d;
			}
		}
	}
}
