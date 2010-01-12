

namespace ManagedLua.Environment {
	public partial class StdLib {
		[Lib("table", "getn")]
		public double table_getn(Types.Table t) {
			return t.Length;
		}
	}
}

