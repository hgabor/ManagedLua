
using System;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		//Will run into problems with silverlight...
		public static System.Diagnostics.Process cur = System.Diagnostics.Process.GetCurrentProcess();
		[Lib("os", "clock")]
		public double os_clock() {
			return System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
		}
	}
}
