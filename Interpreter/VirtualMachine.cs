using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using ManagedLua.Environment;
using ManagedLua.Environment.Types;

namespace ManagedLua.Interpreter {


	public class MalformedChunkException: Exception {
		public MalformedChunkException(): base("The byte stream is not a valid lua chunk!") {}
		public MalformedChunkException(Exception innerException): base("The byte stream is not a valid lua chunk!", innerException) {}
	}

	/// <summary>
	/// Description of VirtualMachine.
	/// </summary>
	public class VirtualMachine {
		
		Table globals = new ManagedLua.Environment.Types.Table();
		List<object> stack = new List<object>();
		
		public VirtualMachine(string[] in_args) {
			LoadStdLib();
			
			//Set args
			Table arg = new Table();
			for (double i = 0; i < in_args.Length; ++i) {
				arg[i] = in_args[(int)i];
			}
			globals["arg"] = arg;
		}
		
		private void LoadStdLib() {
			var std = new StdLib();
			foreach(var m in typeof(StdLib).GetMethods()) {
				LibAttribute la = (LibAttribute)Array.Find(m.GetCustomAttributes(false), o => o is LibAttribute);
				if (la == null) continue;
				if (string.IsNullOrEmpty(la.Table)) {
					globals[la.PublicName] = new InternalClosure(m, std);
				}
				else {
					if (!globals.IsSet(la.Table)) {
						globals[la.Table] = new Table();
					}
					Table t = (Table)globals[la.Table];
					t[la.PublicName] = new InternalClosure(m, std);
				}
			}
		}

		public void Run(byte[] code) {
			Run(new MemoryStream(code, false));
		}

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

		private struct Function {
			public byte UpValues;
			public byte Parameters;
			public byte IsVarargFlag;
			public byte MaxStackSize;

			public uint[] Code;
			public Constant[] Constants;
			public Function[] Functions;
		}

		const byte VARARG_HASARG = 1;
		const byte VARARG_ISVARARG = 2;
		const byte VARARG_NEEDSARG = 4;
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

		const byte LUA_TNIL = 0;
		const byte LUA_TBOOLEAN = 1;
		const byte LUA_TNUMBER = 3;
		const byte LUA_TSTRING = 4;
		private struct Constant {
			public Type Type;
			public object Value;
		}

		private Constant[] ReadConstants(Stream s) {
			int size = ReadInt(s);
			Constant[] ret = new Constant[size];
			for (int i = 0; i < size; ++i) {
				byte t = (byte)s.ReadByte();
				Constant c = new Constant();
				switch (t) {
				case LUA_TNIL:
					c.Type = typeof(ManagedLua.Environment.Types.Nil);
					break;
				case LUA_TBOOLEAN:
					c.Type = typeof(bool);
					byte b = (byte)s.ReadByte();
					c.Value = b == 1;
					break;
				case LUA_TNUMBER:
					c.Type = typeof(double);
					c.Value = ReadNum(s);
					break;
				case LUA_TSTRING:
					c.Type = typeof(string);
					c.Value = ReadString(s);
					break;
				}
				ret[i] = c;
			}
			return ret;
		}
		
		abstract class ClosureBase: Closure {
			private List<object> stack;
			
			protected List<object> Stack {
				get {
					return stack;
				}
			}
			
			public virtual void Prepare() {
				stack = new List<object>();
			}
			
			public void AddParam(object o) {
				stack.Add(o);
			}
			
			public List<object> GetResults() {
				return stack;
			}
		}
		
		class InternalClosure: ClosureBase {
			StdLib lib;
			MethodInfo method;
			
			public InternalClosure(MethodInfo method, StdLib lib) {
				this.lib = lib;
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
				object ret = method.Invoke(lib, callParams.ToArray());
				//TODO: multiple return values
				Stack.Clear();
				Stack.Add(ret);
			}
		}
		
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
				
				//Allocate stack
				for (int i = 0; i < f.MaxStackSize; ++i) {
					Stack.Add(Nil.Value);
				}
			}
			
			public override void Run() {
				while (pc < cSize) {
					uint op = code[pc];
					++pc;
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
					
					switch(opcode) {
					case OpCode.MOVE: {
							Stack[iA] = Stack[iB];
							break;
						}
							
					case OpCode.GETGLOBAL: {
							Constant c = f.Constants[Bx];
							Stack[iA] = globals[c.Value];
							break;
						}
							
					case OpCode.GETTABLE: {
							Table t = (Table)Stack[iB];
							object index;
							if (C_const) {
								index = f.Constants[iC_RK].Value;
							}
							else {
								index = Stack[iC_RK];
							}
							
							Stack[iA] = t[index];
							break;
						}
							
					case OpCode.LOADK:
							Stack[iA] = f.Constants[Bx].Value;
							break;
							
					case OpCode.CALL: {
							ClosureBase c = (ClosureBase)Stack[iA];
							c.Prepare();
							//Push params
							if (B >= 1) {
								for (int i = 0; i < B-1; ++i) {
									c.AddParam(Stack[iA+i+1]);
								}
							}
							else {
								for (int i = 0; iA+i+1 < Stack.Count; ++i) {
									c.AddParam(Stack[iA+i+1]);
								}
							}
							c.Run();
							//Pop results
							var result = c.GetResults();
							if (C >= 1) {
								for (int i = 0; i < C-1; ++i) {
									Stack[iA+i] = result[i];
								}
							}
							else {
								for (int i = 0; iA+i < Stack.Count; ++i) {
									Stack[iA+i] = result[i];
								}
							}
							break;
						}
							
					case OpCode.RETURN: {
							List<object> ret = new List<object>();
							if (B >= 0) {
								for (int i = 0; i < B-1; ++i) {
									ret.Add(Stack[iA+i+1]);
								}
							}
							else {
								for (int i = 0; iA+i+1 < Stack.Count; ++i) {
									ret.Add(Stack[iA+i+1]);
								}
							}
							Stack.Clear();
							Stack.AddRange(ret);
							return;
						}
						
					case OpCode.FORPREP:
							Stack[iA] = (double)Stack[iA] - (double)Stack[iA+2];
							pc += sBx;
							break;
							
					case OpCode.FORLOOP:
							Stack[iA] = (double)Stack[iA] + (double)Stack[iA+2];
							if ((double)Stack[iA] <= (double)Stack[iA+1]) {
								pc += sBx;
								Stack[iA+3] = Stack[iA];
							}
							break;
							
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
