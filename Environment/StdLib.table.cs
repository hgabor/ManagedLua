

namespace ManagedLua.Environment {
	public partial class StdLib {
		[Lib("table", "getn")]
		public double getn(Types.Table t) {
			return t.Length;
		}
	}
}

