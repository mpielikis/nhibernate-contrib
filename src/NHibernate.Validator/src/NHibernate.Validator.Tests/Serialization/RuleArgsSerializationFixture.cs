using System;
using System.Collections.Generic;
using System.Reflection;
using NHibernate.Validator.Engine;
using NUnit.Framework;

namespace NHibernate.Validator.Tests.Serialization
{
	[TestFixture]
	public class RuleArgsSerializationFixture
	{
		[Test]
		public void AllRuleArgsAreSerializable()
		{
			var implementors = GetValidatorImplementors();
			implementors.ForEach(x => Assert.That(x, Has.Attribute<SerializableAttribute>()));
		}

		[Test]
		public void AllRuleArgsCanBeSerialized()
		{
			// Test that can be serialized after creation and test default constructor
			var implementors = GetValidatorImplementors();
			foreach (var implementor in implementors)
			{
				object validatorInstance = Activator.CreateInstance(implementor);
				Assert.That(validatorInstance, Is.BinarySerializable);
			}
		}

		private static List<System.Type> GetValidatorImplementors()
		{
			Assembly assembly = typeof(IRuleArgs).Assembly;
			var result = new List<System.Type>();
			if (assembly != null)
			{
				System.Type[] types = assembly.GetTypes();
				foreach (System.Type tp in types)
				{
					if (typeof(IRuleArgs).IsAssignableFrom(tp) && !tp.IsInterface)
						result.Add(tp);
				}
			}
			return result;
		}
	}
}
