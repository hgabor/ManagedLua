
using System;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		private Random random = new Random();
			
		[Lib("math", "random")]
		public double math_random(params object[] d) {
			if (d.Length >= 3) throw new ArgumentException("To many arguments!");
			if (d.Length == 0) {
				return random.NextDouble();
			}
			else if (d.Length == 1) {
				return random.Next((int)(double)(d[0])) + 1;;
			}
			else {
				return random.Next((int)(double)(d[0]), (int)(double)(d[1]) + 1);
			}
		}
	}
}