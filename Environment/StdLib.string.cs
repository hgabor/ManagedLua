

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		
		[Lib("string", "format")]
		public string string_format(string format, params object[] args) {
			return string.Intern(printf.Printf.sprintf(format, args));
		}
	}
}