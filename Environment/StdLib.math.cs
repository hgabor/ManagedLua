
using System;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		private Random random = new Random();
			
		[Lib("math", "abs")]
		public double math_abs(double d) {
			return Math.Abs(d);
		}
		
		[Lib("math", "fmod")]
		public double math_fmod(double x, double y) {
			return x % y;
		}
		
		[Lib("math", "mod")]
		public double math_mod(double x, double y) {
			return math_fmod(x, y);
		}
		
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