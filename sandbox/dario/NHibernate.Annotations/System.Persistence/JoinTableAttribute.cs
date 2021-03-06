﻿namespace System.Persistence
{
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	public class JoinTableAttribute : Attribute
	{
		public JoinColumnAttribute[] JoinColumns { get; set; }
		public string Name { get; set; }
		public string Catalog { get; set; }
		public string Schema { get; set; }

		public UniqueConstraintAttribute[] UniqueConstraints { get; set; }
	}
}