
using System;
using System.Text;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	public partial class StdLib {
		private int CorrectIndex(double i_d, string s) {
			int i = (int)i_d;
			if (i < 0) {
				return i + s.Length;
			}
			else {
				return i - 1;
			}
		}
		
		[Lib("string", "find")]
		[MultiRet]
		public object[] string_find(string s, string pattern, [Optional(1d)] double init, [Optional(false)] bool plain) {
			//TODO: pattern matching
			int i = CorrectIndex(init, s);
			string news = s.Substring(i);
			if (true) {
				int startid = news.IndexOf(pattern);
				if (startid == -1) {
					return new object[] { Nil.Value };
				}
				else {
					return new object[] {
						(double)i + startid + 1,
						(double)i + startid + pattern.Length,
					};
				}
			}
			else {
				
			}
		}

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

		[Lib("string", "sub")]
		public string string_sub(string s, double i_d, [Optional(-1d)] double j_d) {
			int i = CorrectIndex(i_d, s);
			int j = CorrectIndex(j_d, s);
			if (i >= s.Length || j < 0 || i > j) return "";
			if (i < 0) i = 0;
			if (j >= s.Length) j = s.Length - 1;
			return s.Substring(i, j - i + 1);
		}
	}
}