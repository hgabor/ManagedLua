
using System;
using System.Collections.Generic;
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
		
		[Lib("file", "close")]
		public object file_close(Table t) {
			if (!IsFile(t)) {
				throw new ArgumentException("Argument is not a file!");
			}
			Stream s = (Stream)t["__internal_filehandle"];
			s.Close();
			return true;
		}
		
		[Lib("file", "read")]
		[MultiRet]
		public object[] file_read(Table t, params object[] args) {
			if (!IsFile(t)) {
				throw new ArgumentException("First argument is not a file!");
			}
			Stream s = (Stream)t["__internal_filehandle"];
			var sr = new StreamReader(s);
			if (args.Length == 0) {
				if (sr.EndOfStream) return new object[] { Nil.Value };
				else return new object[] { sr.ReadLine() };
			}
			var ret = new List<object>();
			foreach (object arg in args) {
				if (arg is double) {
					if (sr.EndOfStream) {
						ret.Add(Nil.Value);
					}
					else {
						char[] chars = new char[(int)(double)arg];
						sr.Read(chars, 0, (int)(double)arg);
						ret.Add(new string(chars));
					}
				}
				else if (arg is string) {
					string sarg = (string)arg;
					if (sarg.StartsWith("*a")) {
						ret.Add(sr.ReadToEnd());
					}
					else if (sarg.StartsWith("*l")) {
						if (sr.EndOfStream) {
							ret.Add(Nil.Value);
						}
						else {
							ret.Add(sr.ReadLine());
						}
					}
					else if (sarg.StartsWith("*n")) {
						if (sr.EndOfStream) {
							ret.Add(Nil.Value);
						}
						else {
							throw new NotImplementedException("Cannot read numbers");
						}
					}
					else {
						throw new ArgumentException("Invalid read format");
					}
				}
				else {
					throw new ArgumentException("Invalid read format");
				}
			}
			return ret.ToArray();
		}
		
		[Lib("file", "seek")]
		[MultiRet]
		public object[] file_seek(Table t, [Optional("cur")] string whence, [Optional(0d)] double offset) {
			if (!IsFile(t)) {
				throw new ArgumentException("First argument is not a file!");
			}
			Stream s = (Stream)t["__internal_filehandle"];
			if (!s.CanSeek) return new object[] { Nil.Value, "Stream does not support seeking" };
			long os = (int)offset;
			if (whence == "cur") {
				s.Seek(os, SeekOrigin.Current);
			}
			else if (whence == "end") {
				s.Seek(os, SeekOrigin.End);
			}
			else if (whence == "set") {
				s.Seek(os, SeekOrigin.Begin);
			}
			else {
				return new object[] { Nil.Value, "Invalid position" };
			}
			return new object[] { (double)s.Position };
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
			                                                	if (o is string || o is double) return Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture);
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