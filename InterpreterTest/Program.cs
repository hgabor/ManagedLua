
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
			
			//vm.Run(System.IO.File.ReadAllBytes("hello.luac"));
			//vm.Run(System.IO.File.ReadAllBytes("echo.luac"));
			vm.Run(System.IO.File.ReadAllBytes("sort.luac"));
			
			Console.WriteLine("End");
			Console.ReadKey();
		}
	}
}
