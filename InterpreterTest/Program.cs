
using System;

namespace ManagedLua.InterpreterTest {
	class Program {
		public static void Main(string[] args) {
			//Console.WriteLine(Environment.GetCommandLineArgs().Length);
			Interpreter.VirtualMachine vm = new Interpreter.VirtualMachine(Environment.GetCommandLineArgs());
			
			//vm.Run(System.IO.File.ReadAllBytes("hello.luac"));
			vm.Run(System.IO.File.ReadAllBytes("echo.luac"));
			
			Console.ReadKey();
		}
	}
}
