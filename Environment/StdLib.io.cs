
using System;
using System.IO;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	public partial class StdLib {
		
		private Table defaultOutFile;
		private Table DefaultOutFile {
			get {
				if (defaultOutFile == null) {
					defaultOutFile = StdOut;
				}
				return defaultOutFile;
			}
			set {
				defaultOutFile = value;
			}
		}
		
		[Lib("io", "open")]
		public Table io_open(string fileName, string mode) {
			bool read = mode.Contains("r");
			bool write = mode.Contains("w");
			bool append = mode.Contains("a");
			bool plus = mode.Contains("+");
			bool binary = mode.Contains("b");
			FileStream f;
			if (binary && plus) {
				throw new ArgumentException("Invalid file mode");
			}
			if (read && !write && !append && !plus) {
				f = File.OpenRead(fileName);
			}
			else if (read && !write && !append && plus) {
				f = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
			}
			else if (!read && write && !append && !plus) {
				f = File.Create(fileName);
			}
			else if (!read && write && !append && plus) {
				f = File.Open(fileName, FileMode.Create, FileAccess.ReadWrite);
			}
			else if (!read && !write && append && !plus) {
				f = File.Open(fileName, FileMode.Append, FileAccess.Write);
			}
			else if (!read && !write && append && plus) {
				f = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite);
				f.Seek(0, SeekOrigin.End);
			}
			else {
				throw new ArgumentException("Invalid file mode!");
			}
			Table t = ((Table)vm.GetGlobalVar("file")).ShallowClone();
			t["__internal_read"] = read;
			t["__internal_write"] = write;
			t["__internal_text"] = !binary;
			t["__internal_filehandle"] = f;
			return t;
		}
		
		[Lib("io", "output")]
		public Table io_output(params object[] args ) {
			if (args.Length >= 2) throw new ArgumentException("More than one parameter");
			
			if (args.Length != 0) {
				if(args[0] is string) {
					DefaultOutFile = io_open((string)args[0], "w");
				}
				else if (args[0] is Table && IsFile((Table)args[0])) {
					DefaultOutFile = (Table)args[0];
				}
			}
			return DefaultOutFile;
		}
		
		[Lib("io", "tmpfile")]
		public Table io_tmpfile() {
			string tmpname = Path.GetTempFileName();
			Table handle = io_open(tmpname, "w+");
			vm.Shutdown += (sender, args) => {
				file_close(handle);
				File.Delete(tmpname);
			};
			return handle;
		}
		
		[Lib("io", "write")]
		public void io_write(params object[] args) {
			file_write(io_output(), args);
		}
	}
}

