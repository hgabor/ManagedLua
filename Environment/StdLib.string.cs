
using System;
using System.Text;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		
		[Lib("string", "format")]
		public string string_format(string format, params object[] args) {
			return string.Intern(printf.Printf.sprintf(format, args));
		}
		
		[Lib("string", "rep")]
		public string string_rep(string s, double d) {
			if (d < 0) throw new ArgumentOutOfRangeException("Repeat count cannot be negative");
			var sb = new StringBuilder();
			for (double i = 0; i < d; ++i) {
				sb.Append(s);
			}
			return sb.ToString();
		}
	}
}