using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.Serialization;
using Iesi.Collections;
using Iesi.Collections.Generic;
using log4net;
using NHibernate.Mapping;
using NHibernate.Properties;
using NHibernate.Util;
using NHibernate.Validator.Constraints;
using NHibernate.Validator.Exceptions;
using NHibernate.Validator.Interpolator;
using NHibernate.Validator.Mappings;
using NHibernate.Validator.Util;
using Environment=NHibernate.Validator.Cfg.Environment;

namespace NHibernate.Validator.Engine
{
	/// <summary>
	/// Engine that take a object and check every expressed attribute restrictions
	/// </summary>
	[Serializable]
	public class ClassValidator : IClassValidator, IClassValidatorImplementor, ISerializable 
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(ClassValidator));
		private readonly System.Type entityType;
		private readonly IConstraintValidatorFactory constraintValidatorFactory;

		private readonly Dictionary<MemberInfo, List<Attribute>> membersAttributesDictionary =
			new Dictionary<MemberInfo, List<Attribute>>();

		private DefaultMessageInterpolatorAggregator defaultInterpolator;

		[NonSerialized]
		private readonly ResourceManager messageBundle;

		[NonSerialized]
		private readonly ResourceManager defaultMessageBundle;

		[NonSerialized]
		private readonly IMessageInterpolator userInterpolator;

		/// <summary>
		/// Used to create an instance when deserialize
		/// </summary>
		private readonly System.Type userInterpolatorType; 

		[NonSerialized]
		private readonly IClassValidatorFactory factory;

		private readonly IDictionary<System.Type, IClassValidator> childClassValidators;

		private IList<IValidator> entityValidators;

		private IList<IValidator> memberValidators;

		private List<MemberInfo> memberGetters;

		private List<MemberInfo> childGetters;

		public static readonly InvalidValue[] EMPTY_INVALID_VALUE_ARRAY = new InvalidValue[0];
		public static readonly IEnumerable<Attribute> EmptyConstraints = new Attribute[0];

		private readonly CultureInfo culture;

		private readonly ValidatorMode validatorMode;


		/// <summary>
		/// Create the validator engine for this entity type. 
		/// </summary>
		/// <remarks>
		/// Used in Unit Testing.
		/// </remarks>
		/// <param name="entityType"></param>
		public ClassValidator(System.Type entityType)
			: this(entityType, null, null, ValidatorMode.UseAttribute) {}

		/// <summary>
		/// Create the validator engine for a particular entity class, using a resource bundle
		/// for message rendering on violation
		/// </summary>
		/// <remarks>
		/// Used in Unit Testing.
		/// </remarks>
		/// <param name="entityType">entity type</param>
		/// <param name="resourceManager"></param>
		/// <param name="culture">The CultureInfo for the <paramref name="entityType"/>.</param>
		/// <param name="validatorMode">Validator definition mode</param>
		public ClassValidator(System.Type entityType, ResourceManager resourceManager, CultureInfo culture, ValidatorMode validatorMode)
			: this(entityType, new DefaultConstraintValidatorFactory(), new Dictionary<System.Type, IClassValidator>(), new JITClassValidatorFactory(new DefaultConstraintValidatorFactory(), resourceManager, culture, null, validatorMode)) {}

		/// <summary>
		/// Create the validator engine for a particular entity class, using a resource bundle
		/// for message rendering on violation
		/// </summary>
		/// <remarks>
		/// Used in Unit Testing.
		/// </remarks>
		/// <param name="entityType">entity type</param>
		/// <param name="resourceManager"></param>
		/// <param name="userInterpolator">Custom interpolator.</param>
		/// <param name="culture">The CultureInfo for the <paramref name="entityType"/>.</param>
		/// <param name="validatorMode">Validator definition mode</param>
		public ClassValidator(System.Type entityType, ResourceManager resourceManager, IMessageInterpolator userInterpolator, CultureInfo culture, ValidatorMode validatorMode)
			: this(entityType, new DefaultConstraintValidatorFactory(), new Dictionary<System.Type, IClassValidator>(), new JITClassValidatorFactory(new DefaultConstraintValidatorFactory(), resourceManager, culture, userInterpolator, validatorMode)) { }

		internal ClassValidator(System.Type clazz, IConstraintValidatorFactory constraintValidatorFactory, IDictionary<System.Type, IClassValidator> childClassValidators, IClassValidatorFactory factory)
		{
			if (!ShouldNeedValidation(clazz))
				throw new ArgumentOutOfRangeException("clazz", "Create a validator for a System class.");

			entityType = clazz;
			this.constraintValidatorFactory = constraintValidatorFactory;
			this.factory = factory;
			messageBundle = factory.ResourceManager ?? GetDefaultResourceManager();
			defaultMessageBundle = GetDefaultResourceManager();
			culture = factory.Culture;
			userInterpolator = factory.UserInterpolator;
			if (userInterpolator !=null) userInterpolatorType = factory.UserInterpolator.GetType();
			this.childClassValidators = childClassValidators;
			validatorMode = factory.ValidatorMode;
			
			//Initialize the ClassValidator
			InitValidator(entityType, childClassValidators);
		}

		internal static bool ShouldNeedValidation(System.Type clazz)
		{
			return (!clazz.FullName.StartsWith("System") &&
			        !clazz.IsValueType);
		}

		/// <summary>
		/// Return true if this <see cref="ClassValidator"/> contains rules for apply, false in other case. 
		/// </summary>
		public bool HasValidationRules
		{
			get
			{
				return memberValidators.Count != 0 || entityValidators.Count != 0 || childClassValidators.Count > 1;
			}
		}

		private static ResourceManager GetDefaultResourceManager()
		{
			return new ResourceManager(Environment.BaseNameOfMessageResource, Assembly.GetExecutingAssembly());
		}

		/// <summary>
		/// Initialize the <see cref="ClassValidator"/> type.
		/// </summary>
		/// <param name="clazz"></param>
		/// <param name="childClassValidators"></param>
		private void InitValidator(System.Type clazz, IDictionary<System.Type, IClassValidator> childClassValidators)
		{
			entityValidators = new List<IValidator>();
			memberValidators = new List<IValidator>();
			memberGetters = new List<MemberInfo>();
			childGetters = new List<MemberInfo>();
			defaultInterpolator = new DefaultMessageInterpolatorAggregator();
			defaultInterpolator.Initialize(messageBundle, defaultMessageBundle, culture);

			//build the class hierarchy to look for members in
			childClassValidators.Add(clazz, this);
			ISet<System.Type> classes = new HashedSet<System.Type>();
			AddSuperClassesAndInterfaces(clazz, classes);
			
			// Create the IClassMapping for each class of the validator
			List<IClassMapping> classesMaps = new List<IClassMapping>(classes.Count);
			foreach (System.Type type in classes)
			{
				IClassMapping mapping = factory.ClassMappingFactory.GetClassMapping(type, validatorMode);
				if (mapping != null)
					classesMaps.Add(mapping);
				else
					log.Warn("Validator not found in mode " + validatorMode + " for class " + clazz.AssemblyQualifiedName);
			}

			//Check on all selected classes
			foreach (IClassMapping map in classesMaps)
			{
				foreach (Attribute classAttribute in map.GetClassAttributes())
				{
					ValidateClassAtribute(classAttribute);
				}

				foreach (MemberInfo member in map.GetMembers())
				{
					var memberAttributes = map.GetMemberAttributes(member);
					CreateMemberAttributes(member, memberAttributes);
					CreateChildValidator(member, memberAttributes);

					foreach (Attribute memberAttribute in memberAttributes)
					{
						IValidator propertyValidator = CreateValidator(memberAttribute);

						if (propertyValidator != null)
						{
							memberValidators.Add(propertyValidator);
							memberGetters.Add(member);
						}
					}
				}
			}
		}

		private void AddAttributeToMember(MemberInfo currentMember, Attribute thisattribute)
		{
			log.Info(string.Format("Adding member {0} to dictionary with attribute {1}", currentMember.Name, thisattribute));
			if (!membersAttributesDictionary.ContainsKey(currentMember))
			{
				membersAttributesDictionary.Add(currentMember, new List<Attribute>());
			}

			membersAttributesDictionary[currentMember].Add(thisattribute);
		}

		private void ValidateClassAtribute(Attribute classAttribute)
		{
			IValidator validator = CreateValidator(classAttribute);

			if (validator != null)
			{
				entityValidators.Add(validator);
			}

			//Note: No need to handle Aggregate annotations, c# use Multiple Attribute declaration.
			//HandleAggregateAnnotations(classAttribute, null);
		}

		/// <summary>
		/// Apply constraints on a entity instance and return all the failures.
		/// if <paramref name="entity"/> is null, an empty array is returned 
		/// </summary>
		/// <param name="entity">object to apply the constraints</param>
		/// <returns></returns>
		public InvalidValue[] GetInvalidValues(object entity)
		{
			return GetInvalidValues(entity, new IdentitySet());
		}

		public InvalidValue[] GetInvalidValues(object entity, string propertyName)
		{
			if (entity == null)
			{
				return EMPTY_INVALID_VALUE_ARRAY;
			}
			if (string.IsNullOrEmpty(propertyName))
			{
				throw new ArgumentNullException("propertyName");
			}
			if (!entityType.IsInstanceOfType(entity))
			{
				throw new ArgumentException("not an instance of: " + entity.GetType());
			}

			return MembersValidation(entity, propertyName).ToArray();
		}

		private InvalidValue[] GetInvalidValues(object entity, ISet circularityState)
		{
			if (entity == null || circularityState.Contains(entity))
			{
				return EMPTY_INVALID_VALUE_ARRAY; //Avoid circularity
			}
			else
			{
				circularityState.Add(entity);
			}

			if (!entityType.IsInstanceOfType(entity))
			{
				throw new ArgumentException("not an instance of: " + entity.GetType());
			}

			List<InvalidValue> results = new List<InvalidValue>();

			//Entity Validation
			foreach (IValidator validator in entityValidators)
			{
				var constraintContext = new ConstraintValidatorContext(null,defaultInterpolator.GetAttributeMessage(validator));
				if (!validator.IsValid(entity, constraintContext))
				{
					new InvalidMessageTransformer(constraintContext, results, entityType, null, entity, entity, validator, defaultInterpolator, userInterpolator).Transform();
				}
			}

			results.AddRange(MembersValidation(entity, null));

			//Child validation
			for (int i = 0; i < childGetters.Count; i++)
			{
				MemberInfo member = childGetters[i];

				if (NHibernateUtil.IsPropertyInitialized(entity, member.Name))
				{
					object value = TypeUtils.GetMemberValue(entity, member);

					if (value != null && NHibernateUtil.IsInitialized(value))
					{
						MakeChildValidation(value, entity, member, circularityState, results);
					}
				}
			}
			return results.ToArray();
		}

		private List<InvalidValue> MembersValidation(object entity, string memberName)
		{
			//Property & Field Validation
			List<InvalidValue> results = new List<InvalidValue>();

			int getterFound = 0;
			for (int i = 0; i < memberValidators.Count; i++)
			{
				MemberInfo member = memberGetters[i];
				if (memberName == null || member.Name.Equals(memberName))
				{
					getterFound++;
					if (NHibernateUtil.IsPropertyInitialized(entity, member.Name))
					{
						object value = TypeUtils.GetMemberValue(entity, member);
						/* The implementation of NHibernateUtil.IsPropertyInitialized is not enough for us
						 * because NH call it only for some kind of propeties and especially only when NH need this check.
						 * We need to check if is itilialized its value to prevent the initialization of
						 * lazy-properties and lazy-collections.
						 */
						if (NHibernateUtil.IsInitialized(value))
						{
							IValidator validator = memberValidators[i];
							var constraintContext = new ConstraintValidatorContext(member.Name, defaultInterpolator.GetAttributeMessage(validator));
							if (!validator.IsValid(value, constraintContext))
							{
								new InvalidMessageTransformer(constraintContext, results, entityType, member.Name, value, entity, validator, defaultInterpolator,userInterpolator).Transform();
							}
						}
					}
				}
			}

			if (memberName != null && getterFound == 0 && TypeUtils.GetPropertyOrField(entityType, memberName) == null)
			{
				throw new TargetException(
					string.Format("The property or field '{0}' was not found in class {1}", memberName, entityType.FullName));
			}

			return results;
		}

		/// <summary>
		/// Validate the child validation to objects and collections
		/// </summary>
		/// <param name="value">value to validate</param>
		private void MakeChildValidation(object value, object entity, MemberInfo member, ISet circularityState, ICollection<InvalidValue> results)
		{
			IEnumerable valueEnum = value as IEnumerable;
			if (valueEnum != null)
			{
				MakeCollectionValidation(valueEnum, entity, member, circularityState, results);
			}
			else
			{
				//Simple Value, Non-Collection
				InvalidValue[] invalidValues = GetClassValidator(value.GetType()).GetInvalidValues(value, circularityState);

				foreach (InvalidValue invalidValue in invalidValues)
				{
					invalidValue.AddParentEntity(entity, member.Name);
					results.Add(invalidValue);
				}
			}
		}

		private void MakeCollectionValidation(IEnumerable value, object entity, MemberInfo member, ISet circularityState, ICollection<InvalidValue> results)
		{
			if (TypeUtils.IsGenericDictionary(value.GetType())) //Generic Dictionary
			{
				foreach (object item in value)
				{
					IGetter ValueProperty = new BasicPropertyAccessor().GetGetter(item.GetType(), "Value");
					IGetter KeyProperty = new BasicPropertyAccessor().GetGetter(item.GetType(), "Key");

					object valueValue = ValueProperty.Get(item);
					object keyValue = KeyProperty.Get(item);
					string indexedPropName = string.Format("{0}[{1}]", member.Name, keyValue);

					if (ShouldNeedValidation(ValueProperty.ReturnType))
					{
						InvalidValue[] invalidValuesKey =
							GetClassValidator(ValueProperty.ReturnType).GetInvalidValues(valueValue, circularityState);

						foreach (InvalidValue invalidValue in invalidValuesKey)
						{
							invalidValue.AddParentEntity(entity, indexedPropName);
							results.Add(invalidValue);
						}
					}

					if (ShouldNeedValidation(KeyProperty.ReturnType))
					{
						InvalidValue[] invalidValuesValue =
							GetClassValidator(KeyProperty.ReturnType).GetInvalidValues(keyValue, circularityState);
						foreach (InvalidValue invalidValue in invalidValuesValue)
						{
							invalidValue.AddParentEntity(entity, indexedPropName);
							results.Add(invalidValue);
						}
					}
				}
			}
			else //Generic collection
			{
				int index = 0;
				foreach (object item in value)
				{
					if (item == null)
					{
						index++;
						continue;
					}

					System.Type itemType = item.GetType();
					if (ShouldNeedValidation(itemType))
					{
						InvalidValue[] invalidValues = GetClassValidator(itemType).GetInvalidValues(item, circularityState);

						String indexedPropName = string.Format("{0}[{1}]", member.Name, index);

						foreach (InvalidValue invalidValue in invalidValues)
						{
							invalidValue.AddParentEntity(entity, indexedPropName);
							results.Add(invalidValue);
						}
					}
					index++;
				}
			}
		}

		/// <summary>
		/// Get the ClassValidator for the <paramref name="entityType"/>
		/// parametter  from <see cref="childClassValidators"/>. If doesn't exist, a 
		/// new <see cref="ClassValidator"/> is returned.
		/// </summary>
		/// <param name="entityType">type</param>
		/// <returns></returns>
		private IClassValidatorImplementor GetClassValidator(System.Type entityType)
		{
			IClassValidator result;
			if (!childClassValidators.TryGetValue(entityType, out result))
				return (factory.GetRootValidator(entityType) as IClassValidatorImplementor);
			else
				return (result as IClassValidatorImplementor);
		}

		/// <summary>
		/// Create a <see cref="IValidator"/> from a <see cref="ValidatorClassAttribute"/> attribute.
		/// If the attribute is not a <see cref="ValidatorClassAttribute"/> type return null.
		/// </summary>
		/// <param name="attribute">attribute</param>
		/// <returns>the validator for the attribute</returns>
		private IValidator CreateValidator(Attribute attribute)
		{
			try
			{
				ValidatorClassAttribute validatorClass = null;
				object[] AttributesInTheAttribute = attribute.GetType().GetCustomAttributes(typeof(ValidatorClassAttribute), false);

				if (AttributesInTheAttribute.Length > 0)
				{
					validatorClass = (ValidatorClassAttribute)AttributesInTheAttribute[0];
				}

				if (validatorClass == null)
				{
					return null;
				}

				IValidator entityValidator = constraintValidatorFactory.GetInstance(validatorClass.Value);

				InitializeValidator(attribute, validatorClass.Value, entityValidator);

				defaultInterpolator.AddInterpolator(attribute, entityValidator);
				return entityValidator;
			}
			catch (Exception ex)
			{
				throw new HibernateValidatorException("could not instantiate ClassValidator, maybe some validator is not well formed; check InnerException", ex);
			}
		}

		private static readonly System.Type baseInitializableType = typeof(IInitializableValidator<>);
		private static void InitializeValidator(Attribute attribute, System.Type validatorClass, IValidator entityValidator)
		{
			/* This method was added to supply major difference between JAVA and NET generics.
			 * So far in JAVA the generic type is something optional, in NET mean "strongly typed".
			 * In this case we don't know exactly wich is the generic version of "IInitializableValidator<>"
			 * so we create the type on the fly and invoke the Initialize method by reflection.
			 * All this work is only to give to the user the ability of implement IInitializableValidator<>
			 * without need to inherit from a base class or implement a generic and a no generic version of
			 * the method Initialize.
			 */
			System.Type[] args = {attribute.GetType()};
			System.Type concreteIvc = baseInitializableType.MakeGenericType(args);
			if (concreteIvc.IsAssignableFrom(validatorClass))
			{
				MethodInfo initMethod = concreteIvc.GetMethod("Initialize");
				initMethod.Invoke(entityValidator, new object[] {attribute});
			}
		}

		private void CreateMemberAttributes(MemberInfo member, IEnumerable<Attribute> memberAttributes)
		{
			foreach (Attribute memberAttribute in memberAttributes)
			{
				AddAttributeToMember(member, memberAttribute);
			}
		}

		/// <summary>
		/// Create the validator for the children, who got the <see cref="ValidAttribute"/>
		/// on the fields or properties
		/// </summary>
		private void CreateChildValidator(MemberInfo member, IEnumerable<Attribute> memberAttributes)
		{
			if (memberAttributes.OfType<ValidAttribute>().FirstOrDefault() == null) return;

			KeyValuePair<System.Type, System.Type> clazzDictionary;
			System.Type clazz;

			childGetters.Add(member);

			if (TypeUtils.IsGenericDictionary(TypeUtils.GetType(member)))
			{
				clazzDictionary = TypeUtils.GetGenericTypesOfDictionary(member);
				if (ShouldNeedValidation(clazzDictionary.Key) && !childClassValidators.ContainsKey(clazzDictionary.Key))
					factory.GetChildValidator(this, clazzDictionary.Key);
				if (ShouldNeedValidation(clazzDictionary.Value) && !childClassValidators.ContainsKey(clazzDictionary.Value))
					factory.GetChildValidator(this, clazzDictionary.Value);

				return;
			}
			else
			{
				clazz = TypeUtils.GetTypeOfMember(member);
			}

			if (ShouldNeedValidation(clazz) && !childClassValidators.ContainsKey(clazz))
			{
				factory.GetChildValidator(this, clazz);
			}
		}


		/// <summary>
		/// Add recursively the inheritance tree of types (Classes and Interfaces)
		/// to the parameter <paramref name="classes"/>
		/// </summary>
		/// <param name="clazz">Type to be analyzed</param>
		/// <param name="classes">Collections of types</param>
		private static void AddSuperClassesAndInterfaces(System.Type clazz, ISet<System.Type> classes)
		{
			//iterate for all SuperClasses
			for (System.Type currentClass = clazz; currentClass != null && currentClass != typeof(object); currentClass = currentClass.BaseType)
			{
				if (!classes.Add(currentClass))
				{
					return; //Base case for the recursivity
				}

				System.Type[] interfaces = currentClass.GetInterfaces();

				foreach (System.Type @interface in interfaces)
				{
					AddSuperClassesAndInterfaces(@interface, classes);
				}
			}
		}

		/// <summary>
		/// Assert a valid Object. A <see cref="InvalidStateException"/> 
		/// will be throw in a Invalid state.
		/// </summary>
		/// <param name="entity">Object to be asserted</param>
		public void AssertValid(object entity)
		{
			InvalidValue[] values = GetInvalidValues(entity);
			if (values.Length > 0)
			{
				throw new InvalidStateException(values);
			}
		}

		/// <summary>
		/// Apply constraints of a particular property value of a entity type and return all the failures.
		/// The InvalidValue objects returns return null for InvalidValue#Entity and InvalidValue#RootEntity.
		/// Note: this is not recursive.
		/// </summary>
		/// <param name="propertyName">Name of the property or field to validate</param>
		/// <param name="value">Real value to validate. Is not an entity instance.</param>
		/// <returns></returns>
		public InvalidValue[] GetPotentialInvalidValues(string propertyName, object value)
		{
			List<InvalidValue> results = new List<InvalidValue>();

			int getterFound = 0;
			for (int i = 0; i < memberValidators.Count; i++)
			{
				MemberInfo getter = memberGetters[i];
				if (getter.Name.Equals(propertyName))
				{
					getterFound++;
					IValidator validator = memberValidators[i];
					
					var constraintContext = new ConstraintValidatorContext(propertyName, defaultInterpolator.GetAttributeMessage(validator));
					
					if (!validator.IsValid(value, null))
						new InvalidMessageTransformer(constraintContext, results, entityType, propertyName, value, null, validator, defaultInterpolator, userInterpolator).Transform();

				}
			}

			if (getterFound == 0 && TypeUtils.GetPropertyOrField(entityType, propertyName) == null)
			{
				throw new TargetException(
					string.Format("The property or field '{0}' was not found in class {1}", propertyName, entityType.FullName));
			}

			return results.ToArray();
		}

		/// <summary>
		/// Apply the registred constraints rules on the hibernate metadata (to be applied on DB schema)
		/// </summary>
		/// <param name="persistentClass">hibernate metadata</param>
		public void Apply(PersistentClass persistentClass)
		{
			foreach (IValidator validator in entityValidators)
			{
				IPersistentClassConstraint pcc = validator as IPersistentClassConstraint;
				if (pcc != null)
					pcc.Apply(persistentClass);
			}

			for (int i = 0; i < memberValidators.Count; i++)
			{
				IValidator validator = memberValidators[i];
				MemberInfo getter = memberGetters[i];

				string propertyName = getter.Name;

				if (validator is IPropertyConstraint)
				{
					Property property = FindPropertyByName(persistentClass, propertyName);
					if (property != null)
						((IPropertyConstraint) validator).Apply(property);
				}
			}
		}

		public IEnumerable<Attribute> GetMemberConstraints(string propertyName)
		{
			foreach (var member in membersAttributesDictionary)
			{
				if (member.Key.Name.Equals(propertyName))
				{
					return member.Value.AsReadOnly();
				}
			}
			return EmptyConstraints;
		}

		private static Property FindPropertyByName(PersistentClass associatedClass, string propertyName)
		{
			Property property;
			Property idProperty = associatedClass.IdentifierProperty;
			string idName = idProperty != null ? idProperty.Name : null;
			try
			{
				//if it's a Id
				if (string.IsNullOrEmpty(propertyName) || propertyName.Equals(idName))
					property = idProperty;
				else //if it's a property
				{
					property = associatedClass.GetProperty(propertyName);
				}
			}
			catch (MappingException)
			{
				#region Unsupported future of NH2.0

				//try
				//{
				//if we do not find it try to check the identifier mapper
				//if (associatedClass.IdentifierMapper == null) return null;

				//StringTokenizer st = new StringTokenizer(propertyName, ".", false);

				//foreach (string element in st)
				//{
				//  if (property == null)
				//  {
				//    property = associatedClass.IdentifierMapper.GetProperty(element);
				//  }
				//  else
				//  {
				//    if (property.IsComposite)
				//      property = ((Component)property.Value).GetProperty(element);
				//    else
				//      return null;
				//  }
				//}
				//}
				//catch (MappingException)
				//{

				return null;
				//}

				#endregion
			}

			return property;
		}

		#region IClassValidatorImplementor Members

		InvalidValue[] IClassValidatorImplementor.GetInvalidValues(object entity, ISet circularityState)
		{
			return GetInvalidValues(entity, circularityState);
		}

		IDictionary<System.Type, IClassValidator> IClassValidatorImplementor.ChildClassValidators
		{
			get { return childClassValidators; }
		}

		#endregion

		/// <summary>
		/// Just In Time ClassValidatorFactory
		/// </summary>
		private class JITClassValidatorFactory : AbstractClassValidatorFactory
		{
			private readonly JITClassMappingFactory classMappingFactory;

			public JITClassValidatorFactory(IConstraintValidatorFactory constraintValidatorFactory,ResourceManager resourceManager, CultureInfo culture, IMessageInterpolator userInterpolator, ValidatorMode validatorMode) 
				: base(constraintValidatorFactory, resourceManager, culture, userInterpolator, validatorMode)
			{
				classMappingFactory = new JITClassMappingFactory();
			}

			public override IClassMappingFactory ClassMappingFactory
			{
				get { return classMappingFactory; }
			}

			#region IClassValidatorFactory Members

			public override IClassValidator GetRootValidator(System.Type type)
			{
				return new ClassValidator(type, ConstraintValidatorFactory, new Dictionary<System.Type, IClassValidator>(), this);
			}

			public override void GetChildValidator(IClassValidatorImplementor parentValidator, System.Type childType)
			{
				new ClassValidator(childType, ConstraintValidatorFactory, parentValidator.ChildClassValidators, this);
			}

			#endregion
		}

		//[OnDeserialized]
		//private void DeserializationCallBack(StreamingContext context)
		//{
		//    userInterpolator = (IMessageInterpolator)Activator.CreateInstance(userInterpolatorType);
			
		//    messageBundle = GetDefaultResourceManager();
		//    defaultMessageBundle = GetDefaultResourceManager();
		//    culture = factory.Culture;
		//    userInterpolator = factory.UserInterpolator;
		//    userInterpolatorType = factory.UserInterpolator.GetType();
		//    this.childClassValidators = childClassValidators;
		//    validatorMode = factory.ValidatorMode;
		//}


		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("interpolator", userInterpolatorType);
			info.AddValue("entityType", entityType);
			info.AddValue("entityValidators", entityValidators);
			info.AddValue("memberValidators", memberValidators);
			info.AddValue("childClassValidators", childClassValidators);
			info.AddValue("memberGetters", memberGetters);
			info.AddValue("childGetters", childGetters);
			info.AddValue("defaultInterpolator", defaultInterpolator);
			info.AddValue("membersAttributesDictionary", membersAttributesDictionary);
		}

		public ClassValidator(SerializationInfo info, StreamingContext ctxt)
		{
			System.Type interpolatorType = (System.Type)info.GetValue("interpolator", typeof(System.Type));
			if(interpolatorType != null) userInterpolator = (IMessageInterpolator)Activator.CreateInstance(interpolatorType);
			this.entityType = (System.Type)info.GetValue("entityType", typeof(System.Type));
			this.entityValidators = (IList<IValidator>)info.GetValue("entityValidators", typeof(IList<IValidator>));
			this.memberValidators = (IList<IValidator>)info.GetValue("memberValidators", typeof(IList<IValidator>));
			this.childClassValidators = (IDictionary<System.Type, IClassValidator>)info.GetValue("childClassValidators", typeof(IDictionary<System.Type, IClassValidator>));
			this.memberGetters = (List<MemberInfo>)info.GetValue("memberGetters", typeof(List<MemberInfo>));
			this.childGetters = (List<MemberInfo>)info.GetValue("childGetters", typeof(List<MemberInfo>));
			membersAttributesDictionary =
				(Dictionary<MemberInfo, List<Attribute>>)
				info.GetValue("membersAttributesDictionary", typeof (Dictionary<MemberInfo, List<Attribute>>));
			defaultMessageBundle = GetDefaultResourceManager();
			messageBundle = GetDefaultResourceManager();
			
			culture = CultureInfo.CurrentCulture;
			this.defaultInterpolator = (DefaultMessageInterpolatorAggregator)info.GetValue("defaultInterpolator", typeof(DefaultMessageInterpolatorAggregator));
			defaultInterpolator.Initialize(messageBundle,defaultMessageBundle,culture);

		}
	}
}
