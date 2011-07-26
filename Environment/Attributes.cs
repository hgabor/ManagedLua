using System;
using ManagedLua.Environment.Types;

namespace ManagedLua.Environment
{
	/// <summary>
	/// Marked methods and fields will be available to the lua interpreter automatically.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field)]
	public class LibAttribute: Attribute {
		
		public String PublicName { get; private set; }
		public String Table { get; private set; }
		
		/// <summary>
		/// The method/field will be available as a public variable.
		/// </summary>
		/// <param name="PublicName">The name the lua interpreter will use to access the method/field.</param>
		public LibAttribute(string PublicName) : this("", PublicName) {}
		
		/// <summary>
		/// The method/field will be available nested inside the specified table.
		/// </summary>
		/// <param name="Table">The table the method will be nested in.</param>
		/// <param name="PublicName">The name of the method/field.</param>
		public LibAttribute(string Table, string PublicName) {
			this.PublicName = PublicName;
			this.Table = Table;
		}
	}

	/// <summary>
	/// Lua will interpret the return value as multiple return values.
	/// The function must return an array of objects.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public class MultiRetAttribute: Attribute {}

	/// <summary>
	/// Marks a parameter on the parameter list as optional.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public class OptionalAttribute: Attribute {
		public object DefaultValue { get; private set; }
		
		/// <summary>
		/// If the caller does not specify a value for this parameter, nil will be used.
		/// </summary>
		public OptionalAttribute() : this(Nil.Value) {}
		
		/// <summary>
		/// If the caller does not specify a value for this parameter, this value be used.
		/// </summary>
		/// <param name="defaultValue">The default value</param>
		public OptionalAttribute(object defaultValue) {
			this.DefaultValue = defaultValue;
		}
	}

}
