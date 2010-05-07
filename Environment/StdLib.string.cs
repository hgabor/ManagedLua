
using System;
using System.Text;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		
		[Lib("string", "format")]
		public string string_format(string format, params object[] args) {
			printf.FormatObject formatObject = new printf.FormatObject(format);
			formatObject.AddFormatter('q', (fsp, obj) => {
			                          	StringBuilder sb = new StringBuilder((string)obj);
			                          	sb.Replace(@"\", @"\\").Replace("\n", @"\n").Replace("\r", @"\r").Replace("\0", @"\0").Replace("\"", "\\\"");
			                          	sb.Insert(0, '"').Append('"');
			                          	return sb.ToString();
			                          });
			formatObject.SetArgs(args);
			return string.Intern(formatObject.ToString());
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