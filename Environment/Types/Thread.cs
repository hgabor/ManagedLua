
using System;

namespace ManagedLua.Environment.Types {
	/// <summary>
	/// Description of Thread.
	/// </summary>
	public abstract class Thread {
		public enum StatusType {
			Running, Suspended, Normal, Dead
		}
		public StatusType Status {get; set;}
		
		public Thread() { Status = StatusType.Suspended; }
	}
}
