using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedLua.Environment;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {

	/// <summary>
	/// Thrown when the byte stream cannot be interpreted as valid lua bytecode.
	/// </summary>
	public class MalformedChunkException: Exception {
		public MalformedChunkException(): base("The byte stream is not a valid lua chunk!") {}
		public MalformedChunkException(Exception innerException): base("The byte stream is not a valid lua chunk!", innerException) {}
	}

	/// <summary>
	/// Virtual machine for lua bytecode.
	/// </summary>
	public partial class VirtualMachine {
		
		Table globals = new ManagedLua.Environment.Types.Table();
		List<object> stack = new List<object>();
		
		/// <summary>
		/// Creates an instance of the interpreter.
		/// </summary>
		/// <param name="in_args">Arguments passed to the script (e.g. through the command line)</param>
		public VirtualMachine(string[] in_args) {
			LoadStdLib();
			
			//Set args
			Table arg = new Table();
			for (double i = 0; i < in_args.Length; ++i) {
				arg[i] = in_args[(int)i];
			}
			globals["arg"] = arg;
		}
		
		/// <summary>
		/// Loads the standard library functions into the lua environment.
		/// </summary>
		private void LoadStdLib() {
			var std = new StdLib();
			foreach(var m in typeof(StdLib).GetMethods()) {
				LibAttribute la = (LibAttribute)Array.Find(m.GetCustomAttributes(false), o => o is LibAttribute);
				if (la == null) continue;
				RegisterFunction(m, std, la.Table, la.PublicName);
			}
			Table io = (Table)globals["io"];
			io["stdout"] = std.StdOut;
		}
		
		private void RegisterFunction(MethodInfo m, object host, string tableName, string funcName) {
			if (string.IsNullOrEmpty(tableName)) {
				globals[funcName] = new InternalClosure(m, host);
			}
			else {
				if (!globals.IsSet(tableName)) {
					globals[tableName] = new Table();
				}
				Table t = (Table)globals[tableName];
				t[funcName] = new InternalClosure(m, host);
			}
		}

		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="code">The lua chunk</param>
		/// <exception cref="MalformedChunkException">The byte array is not a valid chunk</exception>
		public void Run(byte[] code) {
			Run(new MemoryStream(code, false));
		}

		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="s">The stream containing the chunk</param>
		/// <exception cref="MalformedChunkException">The stream's output is not a valid chunk</exception>
		public void Run(Stream s) {
			try {
				Headers h = ReadHeaders(s);
				if (Array.IndexOf(SUPPORTED_VERSIONS, h.Version) == -1 ||
				        h.Format != 0 ||
				        h.Endian == 0 && BitConverter.IsLittleEndian ||
				        h.Endian == 1 && !BitConverter.IsLittleEndian ||
				        h.IntSize != 4 ||
				        h.Size_tSize != 4 ||
				        h.InstructionSize != 4 ||
				        h.NumberSize != 8 ||
				        h.IsIntegral) {
					throw new NotSupportedException("The lua chunk format is not supported!");
				}

				var topFunction = ReadFunction(s);
				
				FunctionClosure topClosure = new FunctionClosure(globals, topFunction);
				
				topClosure.Prepare();
				topClosure.Run();

			}
			catch (System.IO.IOException ex) {
				throw new MalformedChunkException(ex);
			}
		}

		
		#region Chunk Declarations

		const int HEADER_SIG = 0x1B4C7561;
		static readonly int[] SUPPORTED_VERSIONS = {
			0x51
		};

		private struct Headers {
			public byte Version;
			public byte Format;
			public byte Endian;
			public byte IntSize;
			public byte Size_tSize;
			public byte InstructionSize;
			public byte NumberSize;
			public bool IsIntegral;
		}

		private struct Function {
			public byte UpValues;
			public byte Parameters;
			public byte IsVarargFlag;
			public byte MaxStackSize;

			public uint[] Code;
			public object[] Constants;
			public Function[] Functions;
		}

		const byte VARARG_HASARG = 1;
		const byte VARARG_ISVARARG = 2;
		const byte VARARG_NEEDSARG = 4;
		
		const byte LUA_TNIL = 0;
		const byte LUA_TBOOLEAN = 1;
		const byte LUA_TNUMBER = 3;
		const byte LUA_TSTRING = 4;

		#endregion

		#region Chunk Readers
		
		private Headers ReadHeaders(Stream s) {
			byte[] buffer = new byte[12];
			int sig;
			s.Read(buffer, 0, 12);

			//If the system is little endian, it will convert it wrong.
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(buffer, 0, 4);
			}
			sig = BitConverter.ToInt32(buffer, 0);
			if (sig != HEADER_SIG) {
				throw new MalformedChunkException();
			}
			return new Headers {
				Version = buffer[4],
				Format = buffer[5],
				Endian = buffer[6],
				IntSize = buffer[7],
				Size_tSize = buffer[8],
				InstructionSize = buffer[9],
				NumberSize = buffer[10],
				IsIntegral = buffer[11] == 1
			};
		}

		private Function ReadFunction(Stream s) {
			string srcName = ReadString(s);
			int lineDefined = ReadInt(s);
			int lastLineDefined = ReadInt(s);
			var ret = new Function() {
				UpValues = (byte)s.ReadByte(),
				Parameters = (byte)s.ReadByte(),
				IsVarargFlag = (byte)s.ReadByte(),
				MaxStackSize = (byte)s.ReadByte(),
				Code = ReadCode(s),
				Constants = ReadConstants(s),
				Functions = ReadFunctions(s),
			};
			
			//Source line positions
			ReadVoid(sizeof(int), s);

			ReadVoidLocals(s);
			
			ReadVoidUpvalues(s);
			
			return ret;
		}
		
		private void ReadVoid(int sizePerItem, Stream s) {
			int size = ReadInt(s);
			byte[] buffer = new byte[size*sizePerItem];
			s.Read(buffer, 0, size*sizePerItem);
		}
		private void ReadVoidLocals(Stream s) {
			int size = ReadInt(s);
			for (int i = 0; i < size; ++i) {
				ReadString(s);
				ReadInt(s);
				ReadInt(s);
			}
		}
		private void ReadVoidUpvalues(Stream s) {
			int size = ReadInt(s);
			for (int i = 0; i < size; ++i) {
				ReadString(s);
			}
		}
		
		private Function[] ReadFunctions(Stream s) {
			int size = ReadInt(s);
			Function[] f = new Function[size];
			for (int i = 0; i < size; ++i) {
				f[i] = ReadFunction(s);
			}
			return f;
		}

		private byte[] intBuffer = new byte[4];
		private int ReadInt(Stream s) {
			s.Read(intBuffer, 0, 4);
			return BitConverter.ToInt32(intBuffer, 0);
		}
		private uint ReadUInt(Stream s) {
			s.Read(intBuffer, 0, 4);
			return BitConverter.ToUInt32(intBuffer, 0);
		}

		private string ReadString(Stream s) {
			byte[] buffer = new byte[4];
			s.Read(buffer, 0, 4);
			int size = BitConverter.ToInt32(buffer, 0);

			if (size == 0) return null;

			buffer = new byte[size];
			s.Read(buffer, 0, size);

			char[] str = Array.ConvertAll(buffer, b => (char)b);
			return new string(str, 0, size - 1);
		}

		private byte[] numBuffer = new byte[8];
		private double ReadNum(Stream s) {
			s.Read(numBuffer, 0, 8);
			return BitConverter.ToDouble(numBuffer, 0);
		}

		private uint[] ReadCode(Stream s) {
			int size = ReadInt(s);
			uint[] code = new uint[size];
			for (int i = 0; i < size; ++i) {
				code[i] = ReadUInt(s);
			}
			return code;
		}
		
		private class UpValue {
			private object value = Nil.Value;
			private List<object> stack;
			private int stackIndex;
			private bool closed = false;
			
			public UpValue(List<object> stack, int stackIndex) {
				this.stack = stack;
				this.stackIndex = stackIndex;
			}
			
			public void CloseIfIndexGreaterThanOrEquals(int i) {
				if (!closed && stackIndex >= i) Close();
			}
			
			public void Close() {
				if (closed) return;
				value = stack[stackIndex];
				stack = null;
				closed = true;
			}
			
			public object Value {
				get {
					if (closed) return this.value;
					else return stack[stackIndex];
				}
				set {
					if (closed) this.value = value;
					else stack[stackIndex] = value;
				}
			}
		}

		private object[] ReadConstants(Stream s) {
			int size = ReadInt(s);
			object[] ret = new object[size];
			for (int i = 0; i < size; ++i) {
				byte t = (byte)s.ReadByte();
				object c = null;
				switch (t) {
				case LUA_TNIL:
					c = Nil.Value;
					break;
				case LUA_TBOOLEAN:
					byte b = (byte)s.ReadByte();
					c = b == 1;
					break;
				case LUA_TNUMBER:
					c = ReadNum(s);
					break;
				case LUA_TSTRING:
					c = ReadString(s);
					break;
				}
				ret[i] = c;
			}
			return ret;
		}
		
		#endregion
		
		/// <summary>
		/// Provides common operations for function/closure calls.
		/// To call a function, you first have to create a callable
		/// closure with the CreateCallableInstance() function.
		/// </summary>
		abstract class ClosureBase: Closure {
			//TODO: IsPrototype check
			private List<object> stack;
			
			protected List<object> Stack {
				get {
					return stack;
				}
			}
			protected int Top {get;set;}
			
			/// <summary>
			/// Prepares the closure for function call:
			/// Resets internal variables, clears the stack etc.
			/// </summary>
			public virtual void Prepare() {
				stack = new List<object>();
				Top = 0;
			}
			
			/// <summary>
			/// Pushes a parameter onto the function's stack.
			/// </summary>
			/// <param name="o">The object</param>
			public void AddParam(object o) {
				stack.Add(o);
				Top++;
			}
			
			/// <summary>
			/// Returns the return value(s) of the function.
			/// </summary>
			/// <returns>The list of return values</returns>
			public List<object> GetResults() {
				return stack;
			}
			
			/// <summary>
			/// Creates a callable closure of the prototype.
			/// Don't forget to call the Prepare method on the resulting Closure.
			/// </summary>
			/// <returns>The callable closure</returns>
			public ClosureBase CreateCallableInstance() {
				return (ClosureBase)this.MemberwiseClone();
			}
		}
		
		/// <summary>
		/// Adapter class for an internal (managed) function.
		/// </summary>
		class InternalClosure: ClosureBase {
			object host;
			MethodInfo method;
			
			/// <summary>
			/// Creates an InternalClosure instance.
			/// </summary>
			/// <param name="method">An StdLib method</param>
			/// <param name="lib">An instance of the standard library</param>
			public InternalClosure(MethodInfo method, object host) {
				this.host = host;
				this.method = method;
			}
			
			public override void Run() {
				var methodParams = method.GetParameters();
				
				var callParams = new List<object>();
				
				int i;
				for (i = 0; i < methodParams.Length-1; ++i) {
					Type t = methodParams[i].ParameterType;
					if (!t.IsInstanceOfType(Stack[i])) throw new ArgumentException();
					callParams.Add(Stack[i]);
				}
				if (i < methodParams.Length) {
					Type t = methodParams[i].ParameterType;
					if (methodParams[i].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0) {
						//This is a vararg function
						Type et = t.GetElementType();
						var paramArray = new List<object>();
						for(; i < Stack.Count; ++i) {
							if (!et.IsInstanceOfType(Stack[i])) throw new ArgumentException();
							paramArray.Add(Stack[i]);
						}
						callParams.Add(paramArray.ToArray());
					}
					else {
						if (!t.IsInstanceOfType(Stack[i])) throw new ArgumentException();
						callParams.Add(Stack[i]);
					}
				}
				
				object ret;
				ret = method.Invoke(host, callParams.ToArray());
				
				//TODO: multiple return values
				Stack.Clear();
				Stack.Add(ret);
			}
		}
		
		/// <summary>
		/// A user defined closure
		/// </summary>
		class FunctionClosure: ClosureBase {
			Function f;
			uint[] code;
			int cSize;
			int pc = 0;
			
			const int InstructionMask = 0x0000003F;
			const uint AMask = 0x00003FC0;
			const uint CMask = 0x007FC000;
			const uint BMask = 0xFF800000;
			const uint BxMask = 0xFFFFC000;
			const uint MSB = 0x00000100;
			const uint sBx0 = 131071;
			
			Table globals;
			
			public FunctionClosure(Table globals, Function f) {
				this.globals = globals;
				this.f = f;
				this.code = f.Code;
				cSize = code.Length;
			}
			
			public override void Prepare() {
				base.Prepare();
				pc = 0;
				newUpValues = new List<UpValue>();
			}
			
			List<UpValue> upValues = new List<UpValue>();
			public void AddUpValue(UpValue uv) {
				upValues.Add(uv);
			}
			public bool NeedsMoreUpValues {
				get {
					return upValues.Count != f.UpValues;
				}
			}
			
			List<UpValue> newUpValues;
			void AddNewUpValue(UpValue uv) {
				if (!newUpValues.Exists(o => o.Value.Equals(uv))) {
					newUpValues.Add(uv);
				}
			}

			
			FunctionClosure creatingClosure;
			const double LFIELDS_PER_FLUSH = 50;
			bool started = false;
			/// <summary>
			/// Starts or continues a function.
			/// </summary>
			public override void Run() {
				if (!started) {
					//Allocate stack
					while (Stack.Count < f.MaxStackSize) {
						Stack.Add(Nil.Value);
					}
					creatingClosure = null;
				}
				started = true;
				while (pc < cSize) {
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
								UpValue uv = new UpValue(this.Stack, iB);
								creatingClosure.AddUpValue(uv);
								this.AddNewUpValue(uv);
							}
							else if (opcode == OpCode.GETUPVAL) {
								creatingClosure.AddUpValue(this.newUpValues[iB]);
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
					
					switch(opcode) {
							
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
							Stack[iA] = Stack[iB];
							break;
						}
							
					/* 
					 * LOADK A Bx
					 * R(A) := K(Bx)
					 */
					case OpCode.LOADK:
							Stack[iA] = f.Constants[Bx];
							break;
					
					/*
					 * LOADBOOL A B C
					 * R(A) := (bool)B;   if (C != 0) PC++
					 */
					case OpCode.LOADBOOL:
							Stack[iA] = B != 0;
							if (C != 0) {
								++pc;
							}
							break;
					
					/*
					 * LOADNIL A B
					 * R(A) := ... := R(B) = nil
					 */
					case OpCode.LOADNIL:
							goto default;

					/*
					 * GETUPVAL A B
					 * R(A) := Up(B)
					 */
					case OpCode.GETUPVAL:
							Stack[iA] = upValues[iB].Value;
							break;

					/*
					 * GETGLOBAL A Bx
					 * R(A) := Gbl(K(Bx))
					 */
					case OpCode.GETGLOBAL: {
							object c = f.Constants[Bx];
							Stack[iA] = globals[c];
							break;
						}
					
					/*
					 * A B C
					 * R(A) := R(B)[RK(C)]
					 */
					case OpCode.GETTABLE: {
							Table t = (Table)Stack[iB];
							object index;
							if (C_const) {
								index = f.Constants[iC_RK];
							}
							else {
								index = Stack[iC_RK];
							}
							
							Stack[iA] = t[index];
							break;
						}
					
					/*
					 * SETGLOBAL A Bx
					 * Gbl(K(Bx)) := R(A)
					 */
					case OpCode.SETGLOBAL: {
							object c = f.Constants[Bx];
							globals[c] = Stack[iA];
							break;
						}
							
					/*
					 * SETUPVAL A B
					 * Up(B) := R(A)
					 */
					case OpCode.SETUPVAL:
							upValues[iB].Value = Stack[iA];
							break;
							
					/*
					 * SETTABLE A B C
					 * R(A)[RK(B)] := RK(C)
					 */
					case OpCode.SETTABLE: {
							Table t = (Table)Stack[iA];
							object index;
							if (B_const) {
								index = f.Constants[iB_RK];
							}
							else {
								index = Stack[iB_RK];
							}
							t[index] = C_const ? f.Constants[iC_RK] : Stack[iC_RK];
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
							Stack[iA] = t;
							break;
					}
					
					/*
					 * SELF A B C
					 * R(A+1) := R(B)
					 * R(A) := R(B)[RK(C)]
					 */
					case OpCode.SELF: {
						Table t = (Table)Stack[iB];
						var tKey = C_const ? f.Constants[iC_RK] : Stack[iC_RK];
						Stack[iA] = t[tKey];
						Stack[iA+1] = t;
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
							double op1 = (double)(B_const ? f.Constants[iB_RK] : Stack[iB_RK]);
							double op2 = (double)(C_const ? f.Constants[iC_RK] : Stack[iC_RK]);
							double result;
							switch(opcode) {
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
							Stack[iA] = result;
							break;
						}
							
					/*
					 * UNM A B
					 * R(A) := -R(B)
					 */
					case OpCode.UNM:
					/*
					 * NOT A B
					 * R(A) := not R(B)
					 */
					case OpCode.NOT:
					/*
					 * LEN A B
					 * R(A) := length of R(B)
					 */
					case OpCode.LEN:
					/*
					 * CONCAT A B C
					 * R(A) := R(B) .. (...) .. R(C), where R(B) ... R(C) are strings
					 */
					case OpCode.CONCAT:
							goto default;
							
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
						object op1 = (B_const ? f.Constants[iB_RK] : Stack[iB_RK]);
						object op2 = (C_const ? f.Constants[iC_RK] : Stack[iC_RK]);
						bool opA = A != 0;
						if (opcode == OpCode.LT && (LessThan(op1, op2) != opA)) ++pc;
						if (opcode == OpCode.LE && (LessThanEquals(op1, op2) != opA)) ++pc;
						if (opcode == OpCode.EQ && (VirtualMachine.Equals(op1, op2) != opA)) ++pc;
						break;
					}

					/*
					 * TEST A C
					 * if (bool)R(A) != C then PC++
					 */
					case OpCode.TEST: {
							bool bC = C != 0;
							bool bA = ToBool(Stack[iA]);
							if (bC != bA) {
								++pc;
							}
							//Next OpCode is JMP
							break;
						}

					/*
					 * TESTSET A B C
					 * if (bool)R(A) == C then R(A) := R(B) else PC++
					 */
					case OpCode.TESTSET:
							goto default;
						
					/*
					 * CALL A B C
					 * R(A), ..., R(A+C-2) := R(A)(R(A+1), ..., R(A+B-1))
					 */
					case OpCode.CALL: {
							ClosureBase c = ((ClosureBase)Stack[iA]).CreateCallableInstance();
							c.Prepare();
							//Push params
							if (B >= 1) {
								for (int i = 0; i < B-1; ++i) {
									c.AddParam(Stack[iA+i+1]);
								}
							}
							else {
								for (int i = 0; iA+i+1 < Top; ++i) {
									c.AddParam(Stack[iA+i+1]);
								}
							}
							c.Run();
							//Pop results
							var result = c.GetResults();
							if (C >= 1) {
								Top = iA + iC - 1;
								for (int i = 0; i < C-1; ++i) {
									Stack[iA+i] = result[i];
								}
							}
							else {
								Top = iA + result.Count;
								for (int i = 0; i < result.Count; ++i) {
									Stack[iA+i] = result[i];
								}
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
								for (int i = 0; i < B-1; ++i) {
									ret.Add(Stack[iA+i]);
								}
							}
							else {
								for (int i = 0; iA + i < Top; ++i) {
									ret.Add(Stack[iA+i]);
								}
							}
							foreach (var uv in newUpValues) {
								uv.Close();
							}
							
							Stack.Clear();
							Stack.AddRange(ret);
							return;
						}

					/*
					 * FORLOOP A sBx
					 * R(A) += R(A+2)
					 * if R(A) <= R(A+1) then
					 *   PC += sBx
					 *   R(A+3) := R(A)
					 */
					case OpCode.FORLOOP:
							Stack[iA] = (double)Stack[iA] + (double)Stack[iA+2];
							if ((double)Stack[iA] <= (double)Stack[iA+1]) {
								pc += sBx;
								Stack[iA+3] = Stack[iA];
							}
							break;

					/*
					 * FORPREP A sBx
					 * R(A) -= R(A+2)
					 * PC += sBx
					 */
					case OpCode.FORPREP:
							Stack[iA] = (double)Stack[iA] - (double)Stack[iA+2];
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
							goto default;
							
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
								block_start = (C-1)*LFIELDS_PER_FLUSH;
							}
							else {
								uint nextNum = code[pc++];
								//TODO: nextNum or nextNum-1?
								block_start = (nextNum-1)*LFIELDS_PER_FLUSH;
							}
							
							Table t = (Table)Stack[iA];
							
							if (B > 0) {
								for (int i = 1; i <= B; ++i) {
									t[(double)i] = Stack[iA+i];
								}
							}
							else {
								for (int i = 1; i < Top; ++i) {
									t[(double)i] = Stack[iA+i];
								}
							}
							break;
					}
							
					/*
					 * CLOSE A
					 * close all variables >= R(A)
					 */
					case OpCode.CLOSE: {
							foreach (var uv in newUpValues) {
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
							Function cl_f = f.Functions[iBx];
							FunctionClosure cl = new FunctionClosure(globals, cl_f);
							Stack[iA] = cl;
							
							//Set upvalues
							creatingClosure = cl;
							
							break;
					}
					
					/*
					 * VARARG A B
					 * R(A), ..., R(A+B-1) = vararg
					 */
					case OpCode.VARARG:
						goto default;
					
					#endregion
							
					default:
							throw new NotImplementedException(string.Format("OpCode {0} ({1}) is not supported", (int)opcode, opcode, opcode.ToString()));
					}
				}
			}
		}
	

		#region OPCODES

		private static OpCode[] ops = (OpCode[])OpCode.GetValues(typeof(OpCode));

		private enum OpCode {
			MOVE,
			LOADK,
			LOADBOOL,
			LOADNIL,
			GETUPVAL,
			GETGLOBAL,
			GETTABLE,
			SETGLOBAL,
			SETUPVAL,
			SETTABLE,
			NEWTABLE,
			SELF,
			ADD,
			SUB,
			MUL,
			DIV,
			MOD,
			POW,
			UNM,
			NOT,
			LEN,
			CONCAT,
			JMP,
			EQ,
			LT,
			LE,
			TEST,
			TESTSET,
			CALL,
			TAILCALL,
			RETURN,
			FORLOOP,
			FORPREP,
			TFORLOOP,
			SETLIST,
			CLOSE,
			CLOSURE,
			VARARG
		}

		#endregion
	}
}
