
using System;
using System.Diagnostics;

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
			
			string[] luacFiles = new string[] {
				"hello.luac", "echo.luac", "sort.luac", "closure.luac", "factorial.luac"
			};
			
			vm.Run(System.IO.File.ReadAllBytes(luacFiles[4]));
			
			Console.WriteLine("End");
			Console.ReadKey();
		}
	}
}
