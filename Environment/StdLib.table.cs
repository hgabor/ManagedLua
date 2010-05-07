
using System;
using System.Collections;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	public partial class StdLib {
		[Lib("table", "getn")]
		public double table_getn(Types.Table t) {
			return t.Length;
		}
		
		

		class TableComparer: IComparer {
			public Comparison<object> ComparerFunc;

			public int Compare(object x, object y) {
				return this.ComparerFunc(x, y);
			}
		}

		[Lib("table", "sort")]
		public void table_sort(Table t, [Optional] object comp) {
			Closure compFunc;
			
			if (comp == Nil.Value) {
				compFunc = (Closure)vm.GetGlobalVar("__internal_lessthan");
			}
			else {
				compFunc = (Closure)comp;
			}
			t.Sort(new TableComparer { ComparerFunc = (x, y) => {
				bool xLessY = ToBool(vm.Call(compFunc, x, y)[0]);
				bool yLessX = ToBool(vm.Call(compFunc, y, x)[0]);
				if (yLessX) return +1;
				if (xLessY) return -1;
				else return 0;
			}});
		}
	}
}

