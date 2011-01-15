
using System;
using System.Collections.Generic;
using ManagedLua.Environment;
using ManagedLua.Environment.Types;
using Function = ManagedLua.Interpreter.VirtualMachine.Function;

namespace ManagedLua.Interpreter {
	class LuaThread: Thread {
		internal FunctionClosure func;
		internal Stack<FunctionClosure> frames = new Stack<FunctionClosure>();

		public FunctionClosure PopFrame() {
			if (frames.Count == 0) return null;
			var oldFunc = func;
			func = frames.Pop();
			code = func.code;
			pc = func.pc;
			cycle = func.cycle;
			code = func.code;
			cSize = code.Length;
			return oldFunc;
		}

		public string ErrorString {
			get {
				if (pc - 1 < func.f.SourcePosition.Length) {
					return string.Intern(
					           string.Format("{0}:{1}",
					                         func.f.FileName,
					                         func.f.SourcePosition[pc-1]));
				}
				else {
					return string.Intern(func.f.FileName);
				}
			}
		}

		const int InstructionMask = 0x0000003F;
		const uint AMask = 0x00003FC0;
		const uint CMask = 0x007FC000;
		const uint BMask = 0xFF800000;
		const uint BxMask = 0xFFFFC000;
		const uint MSB = 0x00000100;
		const uint sBx0 = 131071;

		uint[] code;
		int cSize;
		int pc;
		int cycle = 0;

		public bool Running { get; private set; }

		VirtualMachine vm;
		public LuaThread(Table env, Function f, VirtualMachine vm) {
			func = new FunctionClosure(env, f);
			this.code = f.Code;
			cSize = code.Length;
			Running = false;
			this.vm = vm;
		}

		public LuaThread(Table env, FunctionClosure fc, VirtualMachine vm) {
			func = new FunctionClosure(env, fc);
			this.code = fc.f.Code;
			cSize = code.Length;
			Running = false;
			this.vm = vm;
		}

		#if LUA_DEBUG
		
		public bool ShouldBreak() {
			if (pc == 0) return false;
			else if (pc == 1 || (func.f.SourcePosition[pc-1] != func.f.SourcePosition[pc-2])) {
				return vm.ShouldBreak(func.f.FileName, func.f.SourcePosition[pc-1]);
			}
			else return false;
		}
		
		#endif

		FunctionClosure creatingClosure;
		const double LFIELDS_PER_FLUSH = 50;

		public ThreadResult Run() {
			Running = true;
			creatingClosure = null;
			while (pc < cSize) {
				if (pc == 0) {
					while (func.stack.Count < func.f.MaxStackSize) {
						func.stack.Add(Nil.Value);
					}
				}

				uint op = code[pc++];
				OpCode opcode = (OpCode)(op & InstructionMask);

				uint A = (op & AMask) >> 6;
				int iA = (int)A;
				uint B = (op & BMask) >> (6 + 8 + 9);
				int iB = (int)B;
				uint C = (op & CMask) >> (6 + 8);
				int iC = (int)C;
				uint Bx = (op & BxMask) >> (6 + 8);
				int iBx = (int)Bx;

				int sBx = iBx - (int)sBx0;

				bool B_const = (B & MSB) != 0;
				uint B_RK = B & ~MSB;
				int iB_RK = (int)B_RK;

				bool C_const = (C & MSB) != 0;
				uint C_RK = C & ~MSB;
				int iC_RK = (int)C_RK;



				if (creatingClosure != null) {
					if (creatingClosure.NeedsMoreUpValues) {
						if (opcode == OpCode.MOVE) {
							UpValue uv = new UpValue(this.func.stack, iB);
							creatingClosure.AddUpValue(uv);
							this.func.AddNewUpValue(uv);
						}
						else if (opcode == OpCode.GETUPVAL) {
							creatingClosure.AddUpValue(this.func.newUpValues[iB]);
						}
						else {
							throw new MalformedChunkException();
						}
						continue;
					}
					else {
						creatingClosure = null;
					}
				}

				#if LUA_DEBUG
				
				if (ShouldBreak()) {
					System.Diagnostics.Debug.WriteLine(string.Format("Break at {0}", this.ErrorString));
					System.Diagnostics.Debugger.Break();
				}
				
				#endif

				switch (opcode) {

				/*
				 * Notation:
				 * R(A)		Register A
				 * R(B)		Register B
				 * R(C)		Register C
				 * PC		Program Counter
				 * K(n)		Constant number n
				 * Up(n)	Upvalue number n
				 * Gbl(s)	Global symbol s
				 * RK(B)	Register B or constant
				 * RK(C)	Register C or constant
				 * sBx		Signed displacement, used for jumps
				 * xx[yy]	xx table's yy element
				 */

				#region Memory management

				/*
				 * MOVE A B
				 * R(A) := R(B)
				 *
				 * Secondary use (closures) is implemented at OpCode.CLOSURE
				 */
				case OpCode.MOVE: {
					func.stack[iA] = func.stack[iB];
					break;
				}

				/*
				 * LOADK A Bx
				 * R(A) := K(Bx)
				 */
				case OpCode.LOADK:
					func.stack[iA] = func.f.Constants[Bx];
					break;

				/*
				 * LOADBOOL A B C
				 * R(A) := (bool)B;   if (C != 0) PC++
				 */
				case OpCode.LOADBOOL:
					func.stack[iA] = B != 0;
					if (C != 0) {
						++pc;
					}
					break;

				/*
				 * LOADNIL A B
				 * R(A) := ... := R(B) = nil
				 */
				case OpCode.LOADNIL:
					for (int i = iA; i <= iB; ++i) {
						func.stack[i] = Nil.Value;
					}
					break;

				/*
				 * GETUPVAL A B
				 * R(A) := Up(B)
				 */
				case OpCode.GETUPVAL:
					func.stack[iA] = func.upValues[iB].Value;
					break;

				/*
				 * GETGLOBAL A Bx
				 * R(A) := Gbl(K(Bx))
				 */
				case OpCode.GETGLOBAL: {
					object c = func.f.Constants[Bx];
					func.stack[iA] = vm.GetElement(func.env, c);
					break;
				}

				/*
				 * GETTABLE A B C
				 * R(A) := R(B)[RK(C)]
				 */
				case OpCode.GETTABLE: {
					Table t = (Table)func.stack[iB];
					object index;
					if (C_const) {
						index = func.f.Constants[iC_RK];
					}
					else {
						index = func.stack[iC_RK];
					}

					func.stack[iA] = vm.GetElement(t, index);
					break;
				}

				/*
				 * SETGLOBAL A Bx
				 * Gbl(K(Bx)) := R(A)
				 */
				case OpCode.SETGLOBAL: {
					object c = func.f.Constants[Bx];
					vm.NewIndex(func.env, c, func.stack[iA]);
					break;
				}

				/*
				 * SETUPVAL A B
				 * Up(B) := R(A)
				 */
				case OpCode.SETUPVAL:
					func.upValues[iB].Value = func.stack[iA];
					break;

				/*
				 * SETTABLE A B C
				 * R(A)[RK(B)] := RK(C)
				 */
				case OpCode.SETTABLE: {
					Table t = (Table)func.stack[iA];
					object index;
					if (B_const) {
						index = func.f.Constants[iB_RK];
					}
					else {
						index = func.stack[iB_RK];
					}
					vm.NewIndex(t, index, C_const ? func.f.Constants[iC_RK] : func.stack[iC_RK]);
					break;
				}

				/*
				 * NEWTABLE A B C
				 * R(A) := new Table(with array size B, hash size C)
				 *
				 * B, C are floating point bytes: eeeeexxx
				 * 1xxx * 2^(eeeee-1) if eeeee > 0
				 * xxx                if eeeee == 0
				 */
				case OpCode.NEWTABLE: {
					//Size parameters are ignored, it is only an optimization
					Table t = new Table();
					func.stack[iA] = t;
					break;
				}

				/*
				 * SELF A B C
				 * R(A+1) := R(B)
				 * R(A) := R(B)[RK(C)]
				 */
				case OpCode.SELF: {
					object t = func.stack[iB];
					var tKey = C_const ? func.f.Constants[iC_RK] : func.stack[iC_RK];
					func.stack[iA] = vm.GetElement(t, tKey);
					func.stack[iA+1] = t;
					break;
				}

				#endregion

				#region Arithmetic and Logic

				/*
				 * OP A B C
				 * R(A) := RK(B) op RK(C)
				 *
				 * where: OP = ADD, SUB, MUL, DIV, MOD, POW
				 *        op = +    -    *    /    %    ^
				 */
				case OpCode.ADD:
				case OpCode.SUB:
				case OpCode.MUL:
				case OpCode.DIV:
				case OpCode.MOD:
				case OpCode.POW: {
					double op1 = Convert.ToDouble(B_const ? func.f.Constants[iB_RK] : func.stack[iB_RK], System.Globalization.CultureInfo.InvariantCulture);
					double op2 = Convert.ToDouble(C_const ? func.f.Constants[iC_RK] : func.stack[iC_RK], System.Globalization.CultureInfo.InvariantCulture);
					double result;
					switch (opcode) {
					case OpCode.ADD:
						result = op1 + op2;
						break;
					case OpCode.SUB:
						result = op1 - op2;
						break;
					case OpCode.MUL:
						result = op1 * op2;
						break;
					case OpCode.DIV:
						result = op1 / op2;
						break;
					case OpCode.MOD:
						result = op1 % op2;
						break;
					case OpCode.POW:
						result = Math.Pow(op1, op2);
						break;
					default:
						throw new Exception("Opcode mismatch");
					}
					func.stack[iA] = result;
					break;
				}

				/*
				 * UNM A B
				 * R(A) := -R(B)
				 */
				case OpCode.UNM:
					func.stack[iA] = -Convert.ToDouble(func.stack[iB]);
					break;
				/*
				 * NOT A B
				 * R(A) := not R(B)
				 */
				case OpCode.NOT: {
					object o = func.stack[iB];
					func.stack[iA] = (o == Nil.Value || false.Equals(o)) ? true : false;
					break;
				}
				/*
				 * LEN A B
				 * R(A) := length of R(B)
				 */
				case OpCode.LEN: {
					object o = func.stack[iB];
					if (o is Table) func.stack[iA] = ((Table)o).Length;
					else if (o is string) func.stack[iA] = ((string)o).Length;
					//TODO: Call metamethod
					else return new ThreadResult {
						Type = ThreadResult.ResultType.Error,
						Data = "Length operator can only be applied to tables and strings!",
					};
					break;
				}
				/*
				 * CONCAT A B C
				 * R(A) := R(B) .. (...) .. R(C), where R(B) ... R(C) are strings
				 */
				case OpCode.CONCAT:
					var sb = new System.Text.StringBuilder();
					for (int i = iB; i <= iC; ++i) {
						sb.Append(func.stack[i]);
					}
					func.stack[iA] = string.Intern(sb.ToString());
					break;

				#endregion

				#region Branching and jumping

				/*
				 * JMP sBx
				 * PC += sBx
				 */
				case OpCode.JMP:
					pc += sBx;
					break;

				/*
				 * OP A B C
				 * if (RK(B) op RK(C)) != A) then PC++
				 *
				 * where OP: EQ, LT, LE
				 *       op: ==, <,  <=
				 */
				case OpCode.EQ:
				case OpCode.LT:
				case OpCode.LE: {
					object op1 = (B_const ? func.f.Constants[iB_RK] : func.stack[iB_RK]);
					object op2 = (C_const ? func.f.Constants[iC_RK] : func.stack[iC_RK]);
					bool opA = A != 0u;
					if (opcode == OpCode.LT && (vm.LessThan(op1, op2) != opA)) ++pc;
					if (opcode == OpCode.LE && (vm.LessThanEquals(op1, op2) != opA)) ++pc;
					if (opcode == OpCode.EQ && (vm.Equals(op1, op2) != opA)) ++pc;
					break;
				}

				/*
				 * TEST A C
				 * if (bool)R(A) != C then PC++
				 */
				case OpCode.TEST: {
					bool bC = C != 0;
					bool bA = StdLib.ToBool(func.stack[iA]);
					if (bC != bA) {
						++pc;
					}
					//Next OpCode is JMP
					break;
				}

				/*
				 * TESTSET A B C
				 * if (bool)R(B) == C then R(A) := R(B) else PC++
				 */
				case OpCode.TESTSET: {
					bool bC = C != 0;
					bool bB = StdLib.ToBool(func.stack[iB]);
					if (bC == bB) {
						func.stack[iA] = func.stack[iB];
					}
					else {
						++pc;
					}
					//Next OpCode is JMP
					break;
				}

				/*
				 * CALL A B C
				 * R(A), ..., R(A+C-2) := R(A)(R(A+1), ..., R(A+B-1))
				 */
				case OpCode.CALL: {
					object o = func.stack[iA];
					if (!(o is ClosureBase)) {
						return new ThreadResult {
							Type = ThreadResult.ResultType.Error,
							Data = "Attempted to call a non-function",
						};
					}
					ClosureBase c = ((ClosureBase)o).CreateCallableInstance();
					//Push params
					if (B >= 1) {
						for (int i = 0; i < B - 1; ++i) {
							c.AddParam(func.stack[iA+i+1]);
						}
					}
					else {
						for (int i = 0; iA + i + 1 < func.Top; ++i) {
							c.AddParam(func.stack[iA+i+1]);
						}
					}
					if (c is InternalClosure) {
						func.returnRegister = iA;
						func.returnsSaved = iC;
						return new ThreadResult {
							Type = ThreadResult.ResultType.FunctionCall,
							Data = c
						};
					}
					else {
						FunctionClosure fc = (FunctionClosure)c;
						func.returnRegister = iA;
						func.returnsSaved = iC;
						func.pc = pc;
						func.cycle = 0;
						frames.Push(func);

						//Make the call:
						pc = 0;
						code = fc.code;
						cSize = code.Length;
						fc.env = func.env;
						func = fc;
					}
					break;
				}

				/*
				 * TAILCALL A B C
				 * return R(A)(R(A+1), ..., R(A+B-1))
				 *
				 * Luac compiler always generates C=0
				 */
				case OpCode.TAILCALL:
					//Optimization only: always followed by a RETURN.
					//TODO: optimize it
				goto case OpCode.CALL;

				/*
				 * RETURN A B
				 * return R(A), ..., R(A+B-2)
				 */
				case OpCode.RETURN: {
					List<object> ret = new List<object>();
					if (B > 0) {
						for (int i = 0; i < B - 1; ++i) {
							ret.Add(func.stack[iA+i]);
						}
					}
					else {
						for (int i = 0; iA + i < func.Top; ++i) {
							ret.Add(func.stack[iA+i]);
						}
					}
					foreach (var uv in func.newUpValues) {
						uv.Close();
					}

					func.stack.Clear();
					func.stack.AddRange(ret);

					if (frames.Count == 0) return new ThreadResult { Type = ThreadResult.ResultType.Dead };
					FunctionClosure retFunc = frames.Pop();

					//Copy results
					if (retFunc.returnsSaved >= 1) {
						retFunc.Top = retFunc.returnRegister + retFunc.returnsSaved - 1;
						for (int i = 0; i < retFunc.returnsSaved - 1; ++i) {
							retFunc.stack[retFunc.returnRegister+i] = i < func.stack.Count ? func.stack[i] : Nil.Value;
						}
					}
					else {
						retFunc.Top = retFunc.returnRegister + func.stack.Count;
						for (int i = 0; i < func.stack.Count; ++i) {
							retFunc.stack[retFunc.returnRegister+i] = func.stack[i];
						}
					}

					//Pop stack frame
					pc = retFunc.pc;
					cycle = retFunc.cycle;
					code = retFunc.code;
					cSize = code.Length;
					func = retFunc;
					break;
				}

				/*
				 * FORLOOP A sBx
				 * R(A) += R(A+2)
				 * if R(A) <= R(A+1) then
				 *   PC += sBx
				 *   R(A+3) := R(A)
				 */
				case OpCode.FORLOOP:
					func.stack[iA] = (double)func.stack[iA] + (double)func.stack[iA+2];
					if ((double)func.stack[iA] <= (double)func.stack[iA+1]) {
						pc += sBx;
						func.stack[iA+3] = func.stack[iA];
					}
					break;

				/*
				 * FORPREP A sBx
				 * R(A) -= R(A+2)
				 * PC += sBx
				 */
				case OpCode.FORPREP:
					func.stack[iA] = (double)func.stack[iA] - (double)func.stack[iA+2];
					pc += sBx;
					break;

				/*
				 * TFORLOOP A C
				 * R(A+3), ..., R(A+2+C) := R(A)(R(A+1), R(A+2))
				 * if R(A+3) != nil then
				 *   R(A+2) = R(A+3)
				 * else
				 *   PC++
				 */
				case OpCode.TFORLOOP:
					//This is the only opcode that uses more than one cycle to complete
					//We could break it up to multiple opcodes, but that could potentially break branch instructions.

					if (cycle == 0) {
						//In the first cycle, we call the function
						--pc;
						++cycle;
						ClosureBase c = ((ClosureBase)func.stack[iA]).CreateCallableInstance();
						//Push params
						c.AddParam(func.stack[iA+1]);
						c.AddParam(func.stack[iA+2]);
						if (c is InternalClosure) {
							func.returnRegister = iA + 3;
							func.returnsSaved = iC + 1;
							return new ThreadResult {
								Type = ThreadResult.ResultType.FunctionCall,
								Data = c
							};
						}
						else {
							FunctionClosure fc = (FunctionClosure)c;
							func.returnRegister = iA + 3;
							func.returnsSaved = iC + 1;
							func.pc = pc;
							func.cycle = 1;
							frames.Push(func);

							//Make the call:
							pc = 0;
							code = fc.code;
							cSize = code.Length;
							fc.env = func.env;
							func = fc;
						}
					}
					else {
						//In the second cycle, we execute the remaing instructions
						cycle = 0;
						if (func.stack[iA+3] != Nil.Value) {
							func.stack[iA+2] = func.stack[iA+3];
						}
						else {
							++pc;
						}
					}
					break;

				#endregion

				#region Misc

				/*
				 * SETLIST A B C
				 * if C = 0 C := PC++
				 * R(A)[(C-1)*FPF+i := R(A+i), 1 <= i <= B
				 * where FPF = LFIELDS_PER_FLUSH
				 */
				case OpCode.SETLIST: {
					double block_start;
					if (C > 0) {
						block_start = (C - 1) * LFIELDS_PER_FLUSH;
					}
					else {
						uint nextNum = code[pc++];
						//TODO: nextNum or nextNum-1?
						block_start = (nextNum - 1) * LFIELDS_PER_FLUSH;
					}

					Table t = (Table)func.stack[iA];

					if (B > 0) {
						for (int i = 1; i <= B; ++i) {
							t[block_start + (double)i] = func.stack[iA+i];
						}
					}
					else {
						for (int i = 1; iA + i < func.Top; ++i) {
							t[block_start + (double)i] = func.stack[iA+i];
						}
					}
					break;
				}

				/*
				 * CLOSE A
				 * close all variables >= R(A)
				 */
				case OpCode.CLOSE: {
					foreach (var uv in func.newUpValues) {
						uv.CloseIfIndexGreaterThanOrEquals(iA);
					}
					break;
				}

				/*
				 * CLOSURE A Bx
				 * R(A) := closure(Func(Bx), R(A), ..., R(A+n))
				 * Special, see documentation for details
				 */
				case OpCode.CLOSURE: {
					Function cl_f = func.f.Functions[iBx];
					FunctionClosure cl = new FunctionClosure(func.env, cl_f);
					func.stack[iA] = cl;

					//Set upvalues
					creatingClosure = cl;

					break;
				}

				/*
				 * VARARG A B
				 * R(A), ..., R(A+B-1) = vararg
				 */
				case OpCode.VARARG: {
					if (B == 0) {
						func.Top = iA + func.vararg.Count;
						//Grow stack till Top

						while (func.stack.Count < func.Top) func.stack.Add(Nil.Value);
						for (int i = 0; i < func.vararg.Count && iA + i < func.Top; ++i) {
							func.stack[iA+i] = func.vararg[i];
						}
					}
					else {
						for (int i = 0; i < iB; ++i) {
							func.stack[iA + i] = (i < func.vararg.Count) ? func.vararg[i] : Nil.Value;
						}
					}
					break;
				}

				#endregion

				default:
					throw new NotImplementedException(string.Format("OpCode {0} ({1}) is not supported", (int)opcode, opcode, opcode.ToString()));
				}
			}
			throw new MalformedChunkException();
		}
	}

}
