
using System;

namespace ManagedLua.InterpreterTest {
	class Program {
		public static void Main(string[] args) {
			Interpreter.VirtualMachine vm = new Interpreter.VirtualMachine();

			vm.Run(System.IO.File.ReadAllBytes("hello.luac"));
			
			Console.ReadKey();
		}
	}
}
