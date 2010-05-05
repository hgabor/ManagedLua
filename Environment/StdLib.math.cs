
using System;

namespace ManagedLua.Environment {
	public partial class StdLib {
		private Random random = new Random();

		[Lib("math", "abs")]
		public double math_abs(double d) {
			return Math.Abs(d);
		}

		[Lib("math", "acos")]
		public double math_acos(double d) {
			return Math.Acos(d);
		}

		[Lib("math", "asin")]
		public double math_asin(double d) {
			return Math.Asin(d);
		}

		[Lib("math", "atan")]
		public double math_atan(double d) {
			return Math.Atan(d);
		}

		[Lib("math", "atan2")]
		public double math_atan2(double x, double y) {
			return Math.Atan2(x, y);
		}

		[Lib("math", "ceil")]
		public double math_ceil(double d) {
			return Math.Ceiling(d);
		}

		[Lib("math", "cos")]
		public double math_cos(double d) {
			return Math.Cos(d);
		}

		[Lib("math", "cosh")]
		public double math_cosh(double d) {
			return Math.Cosh(d);
		}

		[Lib("math", "deg")]
		public double math_deg(double d) {
			return d *(180 / Math.PI);
		}

		[Lib("math", "exp")]
		public double math_exp(double d) {
			return Math.Exp(d);
		}

		[Lib("math", "floor")]
		public double math_floor(double d) {
			return Math.Floor(d);
		}

		[Lib("math", "frexp")]
		[MultiRet]
		public object[] math_frexp(double d) {
			// Translate the double into sign, exponent and mantissa.
			long bits = BitConverter.DoubleToInt64Bits(d);
			// Note that the shift is sign-extended, hence the test against -1 not 1
			bool negative = (bits < 0);
			int exponent = (int)((bits >> 52) & 0x7ffL);
			long mantissa = bits & 0xfffffffffffffL;

			// Subnormal numbers; exponent is effectively one higher,
			// but there's no extra normalisation bit in the mantissa
			if (exponent == 0) {
				exponent++;
			}
			// Normal numbers; leave exponent as it is but add extra
			// bit to the front of the mantissa
			else {
				mantissa = mantissa | (1L << 52);
			}

			// Bias the exponent. It's actually biased by 1023, but we're
			// treating the mantissa as m.0 rather than 0.m, so we need
			// to subtract another 52 from it.
			exponent -= 1075;

			if (mantissa == 0) {
				exponent = 0;
			}
			else {
				/* Normalize */
				while ((mantissa & 1) == 0) {   /*  i.e., Mantissa is even */
					mantissa >>= 1;
					exponent++;
				}
			}

			return new object[] {(double)mantissa, (double)exponent };
		}

		[Lib("math", "fmod")]
		public double math_fmod(double x, double y) {
			return x % y;
		}

		[Lib("math", "huge")]
		public readonly double math_huge = double.MaxValue;
		
		
		[Lib("math", "ldexp")]
		public double math_ldexp(double m, double e) {
			return m * Math.Pow(2, e);
		}
		
		[Lib("math", "log")]
		public double math_log(double d) {
			return Math.Log(d);
		}
		
		[Lib("math", "log10")]
		public double math_log10(double d) {
			return Math.Log10(d);
		}

		[Lib("math", "max")]
		public double math_max(double first, params object[] args_o) {
			double[] args = Array.ConvertAll(args_o, o => Convert.ToDouble(o));
			double max = first;
			for (int i = 0; i < args.Length; ++i) {
				if (args[i] > max) max = args[i];
			}
			return max;
		}
		
		[Lib("math", "min")]
		public double math_min(double first, params object[] args_o) {
			double[] args = Array.ConvertAll(args_o, o => Convert.ToDouble(o));
			double min = first;
			for (int i = 0; i < args.Length; ++i) {
				if (args[i] < min) min = args[i];
			}
			return min;
		}

		[Lib("math", "mod")]
		public double math_mod(double x, double y) {
			return math_fmod(x, y);
		}

		[Lib("math", "modf")]
		[MultiRet]
		public object[] math_modf(double d) {
			double integral = Math.Truncate(d); // Integral part
			double fractional =  d - integral; // Fractional part
			return new object[] { integral, fractional };
		}

		[Lib("math", "pi")]
		public readonly double math_pi = Math.PI;
		
		
		[Lib("math", "pow")]
		public double math_pow(double x, double y) {
			return Math.Pow(x, y);
		}

		[Lib("math", "rad")]
		public double math_rad(double d) {
			return d *(Math.PI / 180d);
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
		
		[Lib("math", "randomseed")]
		public void math_randomseed(double d) {
			random = new Random((int)d);
		}

		[Lib("math", "sin")]
		public double math_sin(double d) {
			return Math.Sin(d);
		}

		[Lib("math", "sinh")]
		public double math_sinh(double d) {
			return Math.Sinh(d);
		}

		[Lib("math", "sqrt")]
		public double math_sqrt(double d) {
			return Math.Sqrt(d);
		}

		[Lib("math", "tan")]
		public double math_tan(double d) {
			return Math.Tan(d);
		}
		
		[Lib("math", "tanh")]
		public double math_tanh(double d) {
			return Math.Tanh(d);
		}
	}
}