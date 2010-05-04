
using System;
using System.Diagnostics;
using System.IO;

namespace ManagedLua.InterpreterTest {
	class Program {
		class MyListener: DefaultTraceListener {
			public override void WriteLine(string message)
			{
				Console.WriteLine(message);
			}
		}
		
		public static void Main(string[] args) {
			Trace.Listeners.Add(new MyListener());
			
			
			//Console.WriteLine(Environment.GetCommandLineArgs().Length);
			Interpreter.VirtualMachine vm = new Interpreter.VirtualMachine(Environment.GetCommandLineArgs());
			
			/*string[] luacFiles = new string[] {
				"hello.luac", "echo.luac", "sort.luac", "closure.luac", "factorial.luac", "obj.luac"
			};*/
			var luacFiles = System.IO.Directory.GetFiles("../../tests", "*.luac");
			
			//foreach (string s in luacFiles) {
			string s = luacFiles[2];
			{
				Console.WriteLine("Test {0}", s);
				
				try {
					vm.Run(System.IO.File.ReadAllBytes(s), Path.GetFileName(s));
				}
				catch(Interpreter.LuaScriptException ex) {
					Console.WriteLine("InterpreterTest: " + ex.Message);
				}
				Console.WriteLine("End of test {0}", s);
				Console.WriteLine();
			}
			
			Console.WriteLine("End");
			Console.ReadKey();
		}
	}
}
