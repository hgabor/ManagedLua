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
	
	public class LuaScriptException: Exception {
		public LuaScriptException(string msg, string[] luaStack) : base(msg + "\nstack traceback:\n" + string.Join("\n", luaStack)) {}
	}

	/// <summary>
	/// Virtual machine for lua bytecode.
	/// </summary>
	public partial class VirtualMachine: IDisposable {
		
		Table globals = new ManagedLua.Environment.Types.Table();
		List<object> stack = new List<object>();
		
		/// <summary>
		/// Creates an instance of the interpreter.
		/// </summary>
		/// <param name="in_args">Arguments passed to the script (e.g. through the command line)</param>
		public VirtualMachine(string[] in_args) {
			vminterface = new VirtualMachine.VMInterface(this);
			LoadStdLib();
			
			//Set args
			Table arg = new Table();
			for (double i = 0; i < in_args.Length; ++i) {
				arg[i] = in_args[(int)i];
			}
			globals["arg"] = arg;
			globals["_VERSION"] = "Lua 5.1";
		}
		
		#region Internal helper functions
		
		private VMCommand _setuperrorhandler() {
			return VMCommand.PCALL;
		}
		
		#endregion
		
		/// <summary>
		/// Loads the standard library functions into the lua environment.
		/// </summary>
		private void LoadStdLib() {
			var std = new StdLib(vminterface);
			foreach(var m in typeof(StdLib).GetMethods()) {
				LibAttribute la = (LibAttribute)Array.Find(m.GetCustomAttributes(false), o => o is LibAttribute);
				if (la == null) continue;
				RegisterFunction(m, std, la.Table, la.PublicName);
			}
			foreach(var f in typeof(StdLib).GetFields()) {
				LibAttribute la = (LibAttribute)Array.Find(f.GetCustomAttributes(false), o => o is LibAttribute);
				if (la == null) continue;
				RegisterGlobal(f, std, la.Table, la.PublicName);
			}
			Table io = (Table)globals["io"];
			io["stdout"] = std.StdOut;
			
			RegisterFunction("_setfenv", this, null, "setfenv");
			RegisterFunction("_getmetatable", this, null, "getmetatable");
			RegisterFunction("_tostring", this, null, "tostring");
			
			Table stringMetatable = new Table();
			stringMetatable["__index"] = globals["string"];
			metatables.Add(typeof(string), stringMetatable);
			
			RegisterFunction("_setuperrorhandler", this, null, "__internal_setuperrorhandler");
			
			using (FileStream fs = File.OpenRead("stdlib.luac")) {
				Run(fs);
			}
		}
		
		private void RegisterFunction(string methodName, object host, string tableName, string funcName) {
			RegisterFunction(host.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance), host, tableName, funcName);
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
		
		private void RegisterGlobal(FieldInfo f, object host, string tableName, string fieldName) {
			if (string.IsNullOrEmpty(tableName)) {
				globals[fieldName] = f.GetValue(host);
			}
			else {
				if (!globals.IsSet(tableName)) {
					globals[tableName] = new Table();
				}
				Table t = (Table)globals[tableName];
				t[fieldName] = f.GetValue(host);
			}
		}

		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="code">The lua chunk</param>
		/// <exception cref="MalformedChunkException">The byte array is not a valid chunk</exception>
		public void Run(byte[] code) {
			Run(new MemoryStream(code, false), "<unknown>");
		}
		
		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="code">The lua chunk</param>
		/// <param name="filename">The original file name of the chunk</param>
		/// <exception cref="MalformedChunkException">The byte array is not a valid chunk</exception>
		public void Run(byte[] code, string filename) {
			Run(new MemoryStream(code, false), filename);
		}

		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="s">The stream containing the chunk</param>
		/// <exception cref="MalformedChunkException">The stream's output is not a valid chunk</exception>
		public void Run(Stream s) {
			Run(s, "<unknown>");
		}

		delegate void EmptyFunc();

		/// <summary>
		/// Loads and runs a lua chunk.
		/// </summary>
		/// <param name="s">The stream containing the chunk</param>
		/// <param name="filename">The original file name of the chunk</param>
		/// <exception cref="MalformedChunkException">The stream's output is not a valid chunk</exception>
		public void Run(Stream s, string filename) {
			Run(ReadChunk(s, filename));
		}
		
		private object[] Run(Function topFunction) {
			return Run(new LuaThread(globals, topFunction, this));
		}
		
		private object[] Run(FunctionClosure topFunction) {
			return Run(new LuaThread(globals, topFunction, this));
		}
		
		LuaThread currentThread = null;
		private object[] Run(LuaThread mainThread) {
			LuaThread thread = currentThread = mainThread;
			thread.Status = Thread.StatusType.Running;
			List<object> result = new List<object>();
			
			ThreadResult tResult;
			Dictionary<LuaThread, LuaThread> calledBy = new Dictionary<LuaThread, LuaThread>();
			do {
				EmptyFunc pushResults = delegate() {
					if (thread.func.returnsSaved >= 1) {
						thread.func.Top = thread.func.returnRegister + thread.func.returnsSaved - 1;
						for (int i = 0; i < thread.func.returnsSaved-1; ++i) {
							thread.func.stack[thread.func.returnRegister+i] = i < result.Count ? result[i] : Nil.Value;
						}
					}
					else {
						thread.func.Top = thread.func.returnRegister + result.Count;
						for (int i = 0; i < result.Count; ++i) {
							thread.func.stack[thread.func.returnRegister+i] = result[i];
						}
					}
				};
				
				do {
					try {
						tResult = thread.Run();
					}
					catch(Exception ex) {
						tResult = new ThreadResult {
							Type = ThreadResult.ResultType.Error,
							Data = ex.ToString(),
						};
					}
					
					if (tResult.Type == ThreadResult.ResultType.FunctionCall || tResult.Type == ThreadResult.ResultType.Error) {
						if (tResult.Type == ThreadResult.ResultType.FunctionCall) {
							try {
								var tResultF = (InternalClosure)tResult.Data;
								tResultF.Run();
								currentThread = thread;
								
								//Pop results
								result = tResultF.GetResults();
							}
							catch (Exception ex) {
								result = new List<object> { VMCommand.ERROR, ex.ToString() /*Error msg*/ };
							}
						}
						else { //It was an error
							result = new List<object> { VMCommand.ERROR, tResult.Data /*Error msg*/ };
						}
						
						if (result.Count != 0) {
							if (result[0] is System.Enum) {
								switch((VMCommand)result[0]) {
									//1: The closure
									case VMCommand.CO_CREATE:
										var newFunction = (FunctionClosure)((FunctionClosure)result[1]).CreateCallableInstance();
										LuaThread newThread = new LuaThread(thread.func.env, newFunction, this);
										result.Clear();
										result.Add(newThread);
										break;
										
									//1: the thread to resume
									//2+: args to the thread
									case VMCommand.CO_RESUME:
										calledBy.Add((LuaThread)result[1], thread);
										thread.Status = Thread.StatusType.Normal;
										thread = currentThread = (LuaThread)result[1];
										thread.Status = Thread.StatusType.Running;
										if (!thread.Running) {
											for (int i = 2; i < result.Count; ++i) {
												thread.func.AddParam(result[i]);
											}
											continue;
										}
										//Else just pass them as return values to yield
										result.RemoveRange(0, 2); // Remove the command and the thread
										break;
										
									case VMCommand.CO_RUNNING:
										result.Clear();
										if (calledBy.ContainsKey(thread)) {
											result.Add(thread);
										}
										else {
											result.Add(Nil.Value);
										}
										break;
										
									//1+: return values to resume
									case VMCommand.CO_YIELD:
										var caller = calledBy[thread];
										calledBy.Remove(thread);
										thread.Status = Thread.StatusType.Suspended;
										thread = currentThread = caller;
										thread.Status = Thread.StatusType.Running;
										result.RemoveAt(0); // Remove command
										break;
										
									case VMCommand.PCALL:
										thread.func.errorHandler = true;
										break;
										
									//1: error msg
									case VMCommand.ERROR:
										//Pop all stacks until an error handler is found
										var stackTrace = new List<string>();
										string errorMsg = string.Intern(thread.ErrorString + ": " + (string)result[1]);
										result[1] = errorMsg;
										FunctionClosure fc;
										do {
											fc = thread.PopFrame();
											if (fc == null) {
												//Thread finished without handling the error, start unwinding the calling thread
												if (calledBy.Count == 0) {
													stackTrace.Add("\t" + thread.ErrorString + ": in main chunk");
													//Last thread finished without handling the error, bailing out
													throw new LuaScriptException(errorMsg, stackTrace.ToArray());
												}
												
												var callerThread = calledBy[thread];
												calledBy.Remove(thread);
												thread = currentThread = callerThread;
												stackTrace.Add("\t" + thread.ErrorString + ": in lua thread");
												continue;
											}
											else if (fc.errorHandler == true) break;
											stackTrace.Add("\t"+ thread.ErrorString +": in lua function");
										} while (true);
										result[0] = false;
										break;
								}
							}
						}
						pushResults();
					}
				} while(tResult.Type != ThreadResult.ResultType.Dead);
				thread.Status = Thread.StatusType.Dead;
				
				if (calledBy.Count == 0) break;
				
				result = thread.func.GetResults();
				var prevThread = calledBy[thread];
				calledBy.Remove(thread);
				thread = currentThread = prevThread;
				pushResults();
				
			} while(true);
			return thread.func.stack.ToArray();
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
		
		internal struct LocalVar {
			public string Name;
			public uint StartPc;
			public uint EndPc;
		}

		internal struct Function {
			public byte UpValues;
			public byte Parameters;
			public byte IsVarargFlag;
			public byte MaxStackSize;

			public uint[] Code;
			public object[] Constants;
			public Function[] Functions;
			public string FileName;
			
			public uint[] SourcePosition;
			public LocalVar[] LocalVars;
		}

		internal const byte VARARG_HASARG = 1;
		internal const byte VARARG_ISVARARG = 2;
		internal const byte VARARG_NEEDSARG = 4;
		
		const byte LUA_TNIL = 0;
		const byte LUA_TBOOLEAN = 1;
		const byte LUA_TNUMBER = 3;
		const byte LUA_TSTRING = 4;

		#endregion

		#region Chunk Readers
		
		private Function ReadChunk(Stream s, string fileName) {
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
				return ReadFunction(s, fileName);
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

		private Function ReadFunction(Stream s, string fileName) {
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
				Functions = ReadFunctions(s, fileName),
				FileName = fileName,
				
				SourcePosition = ReadSourcePosition(s),
				LocalVars = ReadLocalVars(s),
			};
			
			ReadVoidUpvalues(s);
			
			return ret;
		}
		
		private void ReadVoidUpvalues(Stream s) {
			int size = ReadInt(s);
			for (int i = 0; i < size; ++i) {
				ReadString(s);
			}
		}
		
		private Function[] ReadFunctions(Stream s, string fileName) {
			int size = ReadInt(s);
			Function[] f = new Function[size];
			for (int i = 0; i < size; ++i) {
				f[i] = ReadFunction(s, fileName);
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
			return string.Intern(new string(str, 0, size - 1));
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

		private uint[] ReadSourcePosition(Stream s) {
			return ReadCode(s); // Implementation is the same
		}
		
		private LocalVar[] ReadLocalVars(Stream s) {
			int size = ReadInt(s);
			LocalVar[] vars = new LocalVar[size];
			for (int i = 0; i < size; ++i) {
				vars[i] = new LocalVar {
					Name = ReadString(s),
					StartPc = ReadUInt(s),
					EndPc = ReadUInt(s),
				};
			}
			return vars;
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
		

		
		private static OpCode[] ops = (OpCode[])OpCode.GetValues(typeof(OpCode));

		
		private class VMInterface : LuaVM, IDisposable {
			private VirtualMachine vm;
			public VMInterface(VirtualMachine vm) { this.vm = vm; }
			
			public Closure WrapFunction(MethodInfo method, object obj) {
				return new InternalClosure(method, obj);
			}
			
			public Closure CompileString(string str, string filename) {
				#if PINVOKE_ENABLED
				string luaFileName = Path.GetTempFileName();
				string luacFileName = Path.GetTempFileName();
				try {
					File.WriteAllText(luaFileName, str);
					var psinfo = new System.Diagnostics.ProcessStartInfo();
					psinfo.FileName = "luac";
					psinfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
					psinfo.Arguments = string.Format("-o \"{0}\" \"{1}\"", luacFileName, luaFileName);
					System.Diagnostics.Process.Start(psinfo).WaitForExit();
					Function f;
					using (FileStream fs = File.OpenRead(luacFileName)) {
						f = vm.ReadChunk(fs, filename);
					}
					FunctionClosure fc = new FunctionClosure(vm.globals, f);
					return fc;
				}
				finally {
					File.Delete(luacFileName);
					File.Delete(luaFileName);
				}
				
				#else
				throw new NotSupportedException("Cannot compile lua strings without the native luac.");
				#endif
			}
			
			public Closure Load(Stream s, string filename) {
				return new FunctionClosure(vm.globals, vm.ReadChunk(s, filename));
			}
			
			public object[] Call(Closure c, params object[] args) {
				var cb = ((ClosureBase)c).CreateCallableInstance();
				foreach(var arg in args) {
					cb.AddParam(arg);
				}
				if (cb is InternalClosure) {
					((InternalClosure)cb).Run();
					return cb.GetResults().ToArray();
				}
				else { //cb is FunctionClosure
					return vm.Run((FunctionClosure)cb);
				}
			}
			
			public object GetGlobalVar(object key) {
				return vm.globals[key];
			}
			
			public void SetGlobalVar(object key, object value) {
				vm.globals[key] = value;
			}
			
			public event EventHandler Shutdown;
			
			public void Dispose() {
				if (Shutdown != null) Shutdown(this, new EventArgs());
			}
		}
		private VMInterface vminterface;
		
		void IDisposable.Dispose() {
			vminterface.Dispose();
		}
		
		#if LUA_DEBUG
		
		private struct Breakpoint {
			public string Filename;
			public uint Sourcepos;
		}
		
		private Dictionary<Breakpoint, object> breakpoints = new Dictionary<Breakpoint, object>();
		
		public void SetBreakpoint(string filename, uint sourcepos) {
			var bp = new Breakpoint {
				Filename = filename,
				Sourcepos = sourcepos,
			};
			if (!breakpoints.ContainsKey(bp)) {
				breakpoints.Add(bp, null);
			}
		}
		
		internal bool ShouldBreak(string filename, uint sourcepos) {
			return breakpoints.ContainsKey(new Breakpoint {
			                               	Filename = filename,
			                               	Sourcepos = sourcepos,
			                               });
		}
		
		#else
		
		public void SetBreakpoint(string filename, uint sourcepos) {}
		
		#endif
	}
}
