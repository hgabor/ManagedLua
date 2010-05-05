
using System;
using System.IO;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment {
	public partial class StdLib {
		[Lib("require")]
		public object require(string modname) {
			var loaded = package_loaded[modname];
			if (loaded != Nil.Value) {
				//It is already loaded
				return loaded;
			}
			//It is not loaded
			foreach(var key in package_loaders) {
				//First query all searchers
				var value = package_loaders[key];
				if (!(value is Closure)) continue;
				var searcher = (Closure)value;
				//Search for a loader
				var aloader = vm.Call(searcher, modname);
				if (aloader[0] is Closure) {
					var loader = (Closure)aloader[0];
					var ret = vm.Call(loader, modname);
					if (ret.Length == 0 && package_loaded[modname] == Nil.Value) {
						package_loaded[modname] = true;
						return true;
					}
					else {
						package_loaded[modname] = ret;
						return ret;
					}
				}
				else continue;
			}
			return null;
		}
		
		[Lib("package", "loaded")]
		public Table package_loaded = new Table();

		[Lib("package", "loaders")]
		public Table package_loaders = new Table();
		
		public object TestSearcher(string modname) {
			//Try to load a .luac file with the name "modname"
			if (File.Exists(modname + ".luac")) {
				using (FileStream fs = File.OpenRead(modname + ".luac")) {
					return vm.Load(fs, modname + ".luac");
				}
			}
			else return Nil.Value;
		}
		
		private void Init_package() {
			package_loaders[0] = vm.WrapFunction(typeof(StdLib).GetMethod("TestSearcher"), this);
		}
	}
}
