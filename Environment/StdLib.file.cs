
using System;
using ManagedLua.Environment.Types;
using System.IO;

namespace ManagedLua.Environment {
	public partial class StdLib {
		private bool IsFile(Table t) {
			return !(t["__internal_write"] == Nil.Value ||
				t["__internal_read"] == Nil.Value ||
				t["__internal_text"] == Nil.Value ||
				!(t["__internal_filehandle"] is Stream));
		}
		
		private Table stdOut;
		public Table StdOut {
			get {
				if (stdOut == null) {
					stdOut = new Table();
					stdOut["__internal_write"] = true;
					stdOut["__internal_read"] = false;
					stdOut["__internal_text"] = true;
					stdOut["__internal_filehandle"] = System.Console.OpenStandardOutput();
				}
				return stdOut;
			}
		}
		
		[Lib("file", "write")]
		public void file_write(Table t, params object[] args) {
			if (!IsFile(t)) {
				throw new ArgumentException("First argument is not a file!");
			}
			
			bool writable = (bool)t["__internal_write"];
			if (!writable) throw new InvalidOperationException("File is not writable");
			
			
			Stream f = (Stream)t["__internal_filehandle"];
			bool isText = (bool)t["__internal_text"];
			
			//Check params
			string toWrite = string.Concat(Array.ConvertAll(args, o => {
			                                                	if (o is string || o is double) return o.ToString();
			                                                	else throw new ArgumentException("Only strings and numbers");
			                                                }));
			
			if (isText) {
				StreamWriter sr = new StreamWriter(f, System.Text.Encoding.ASCII);
				sr.Write(toWrite);
				sr.Flush();
			}
			else {
				byte[] bytes = System.Text.Encoding.Default.GetBytes(toWrite);
				f.Write(bytes, 0, bytes.Length);
			}
			f.Flush();
		}
	}
}