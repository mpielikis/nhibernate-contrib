using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml;

using Iesi.Collections;

using log4net;

using NHibernate.Type;
using NHibernate.UserTypes;
using NHibernate.Util;

using CompositeUserType = NHibernate.UserTypes.ICompositeUserType;
using Type = NHibernate.Type.TypeType;
using Element = System.Xml.XmlElement;
using MultiMap = System.Collections.Hashtable;

namespace NHibernate.Tool.hbm2net
{
	public class ClassMapping : MappingElement
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private ClassName name = null;
		private ClassName generatedName = null;
		private string superClass = null;
		private ClassMapping superClassMapping = null;
		private string proxyClass = null;
		private SupportClass.ListCollectionSupport fields;
		private SupportClass.TreeSetSupport imports;
		private SupportClass.ListCollectionSupport subclasses;
		private static readonly IDictionary components = new Hashtable();
		private bool mustImplementEquals_Renamed_Field = false;

		private bool shouldBeAbstract_Renamed_Field = false;

		#region Constructors

		static ClassMapping()
		{
		}

		public ClassMapping(string classPackage, MappingElement parentElement, ClassName superClass,
		                    ClassMapping superClassMapping, Element classElement, MultiMap inheritedMeta)
			: this(classPackage, parentElement, superClass, classElement, inheritedMeta)
		{
			this.superClassMapping = superClassMapping;

			if (this.superClassMapping != null)
			{
				SupportClass.ListCollectionSupport l = this.superClassMapping.AllFieldsForFullConstructor;
				for (IEnumerator iter = l.GetEnumerator(); iter.MoveNext();)
				{
					FieldProperty element = (FieldProperty) iter.Current;
					ClassName ct = element.ClassType;
					if (ct != null)
					{
						// add imports for superclasses possible fields.
						//addImport(ct);
					}
					else
					{
						//addImport(element.FullyQualifiedTypeName);
					}
				}
			}
		}

		public ClassMapping(string classPackage, MappingElement parentElement, ClassName superClass, Element classElement,
		                    MultiMap inheritedMeta) : base(classElement, parentElement)
		{
			InitBlock();
			InitWith(classPackage, superClass, classElement, false, inheritedMeta);
		}

		public ClassMapping(string classPackage, Element classElement, MappingElement parentElement, MultiMap inheritedMeta)
			: base(classElement, parentElement)
		{
			InitBlock();
			InitWith(classPackage, null, classElement, false, inheritedMeta);
		}

		public ClassMapping(string classPackage, Element classElement, MappingElement parentElement, bool component,
		                    MultiMap inheritedMeta) : base(classElement, parentElement)
		{
			InitBlock();
			InitWith(classPackage, null, classElement, component, inheritedMeta);
		}

		#endregion

		#region Property methods

		public virtual SupportClass.ListCollectionSupport Fields
		{
			get { return fields; }
		}

		public virtual SupportClass.SetSupport Imports
		{
			get { return imports; }
		}

		/// <summary>shorthand method for getClassName().getFullyQualifiedName() </summary>
		public virtual string FullyQualifiedName
		{
			get { return ClassName.FullyQualifiedName; }
		}

		/// <summary>shorthand method for getClassName().getName() </summary>
		public virtual string Name
		{
			get { return ClassName.Name; }
		}

		/// <summary>shorthand method for getClassName().getPackageName() </summary>
		public virtual string PackageName
		{
			get { return ClassName.PackageName; }
		}

		public virtual ClassName ClassName
		{
			get { return name; }
		}

		public virtual string GeneratedName
		{
			get { return generatedName.Name; }
		}

		public virtual string GeneratedPackageName
		{
			get { return generatedName.PackageName; }
		}

		public virtual string Proxy
		{
			get { return proxyClass; }
		}

		public virtual SupportClass.ListCollectionSupport Subclasses
		{
			get { return subclasses; }
		}

		public virtual string SuperClass
		{
			get { return superClass; }
		}

		public virtual SupportClass.ListCollectionSupport LocalFieldsForFullConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = new SupportClass.ListCollectionSupport();
				for (IEnumerator myFields = Fields.GetEnumerator(); myFields.MoveNext();)
				{
					FieldProperty field = (FieldProperty) myFields.Current;
					if (!field.Identifier || (field.Identifier && !field.Generated))
					{
						result.Add(field);
					}
				}

				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport FieldsForSupersFullConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = new SupportClass.ListCollectionSupport();
				if (SuperClassMapping != null)
				{
					// The correct sequence is vital here, as the subclass should be
					// able to invoke the fullconstructor based on the sequence returned
					// by this method!
					result.AddAll(SuperClassMapping.FieldsForSupersFullConstructor);
					result.AddAll(SuperClassMapping.LocalFieldsForFullConstructor);
				}

				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport LocalFieldsForMinimalConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = new SupportClass.ListCollectionSupport();
				for (IEnumerator myFields = Fields.GetEnumerator(); myFields.MoveNext();)
				{
					FieldProperty field = (FieldProperty) myFields.Current;
					if ((!field.Identifier && !field.Nullable) || (field.Identifier && !field.Generated))
					{
						result.Add(field);
					}
				}
				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport AllFields
		{
			get
			{
				SupportClass.ListCollectionSupport result = new SupportClass.ListCollectionSupport();

				if (SuperClassMapping != null)
				{
					result.AddAll(SuperClassMapping.AllFields);
				}
				else
				{
					result.AddAll(Fields);
				}
				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport AllFieldsForFullConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = FieldsForSupersFullConstructor;
				result.AddAll(LocalFieldsForFullConstructor);
				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport FieldsForSupersMinimalConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = new SupportClass.ListCollectionSupport();
				if (SuperClassMapping != null)
				{
					// The correct sequence is vital here, as the subclass should be
					// able to invoke the fullconstructor based on the sequence returned
					// by this method!
					result.AddAll(SuperClassMapping.FieldsForSupersMinimalConstructor);
					result.AddAll(SuperClassMapping.LocalFieldsForMinimalConstructor);
				}

				return result;
			}
		}

		public virtual SupportClass.ListCollectionSupport AllFieldsForMinimalConstructor
		{
			get
			{
				SupportClass.ListCollectionSupport result = FieldsForSupersMinimalConstructor;
				result.AddAll(LocalFieldsForMinimalConstructor);
				return result;
			}
		}

		public static IEnumerator Components
		{
			get { return components.Values.GetEnumerator(); }
		}

		/// <summary> Returns the superClassMapping.</summary>
		/// <returns> ClassMapping
		/// </returns>
		public virtual ClassMapping SuperClassMapping
		{
			get { return superClassMapping; }
		}

		public virtual bool Interface
		{
			get { return GetMetaAsBool("interface"); }
		}

		/// <returns>
		/// </returns>
		public virtual string Scope
		{
			get
			{
				string classScope = "public";
				if (GetMeta("scope-class") != null)
				{
					classScope = GetMetaAsString("scope-class").Trim();
				}
				return classScope;
			}
		}

		/// <returns>
		/// </returns>
		public virtual string DeclarationType
		{
			get
			{
				if (Interface)
				{
					return "interface";
				}
				else
				{
					return "class";
				}
			}
		}

		/// <summary> Return the modifers for this class.
		/// Adds "abstract" if class should be abstract (but not if scope contains abstract)
		/// TODO: deprecate/remove scope-class and introduce class-modifier instead
		/// </summary>
		/// <returns>
		/// </returns>
		public virtual string Modifiers
		{
			get
			{
				if (ShouldBeAbstract() && (Scope.IndexOf("abstract") == - 1))
				{
					return "abstract";
				}
				else
				{
					return "";
				}
			}
		}

		/// <returns>
		/// </returns>
		public virtual bool SuperInterface
		{
			get { return SuperClassMapping == null ? false : SuperClassMapping.Interface; }
		}

		#endregion

		#region Private methods

		private void InitBlock()
		{
			fields = new SupportClass.ListCollectionSupport();
			imports = new SupportClass.TreeSetSupport();
			subclasses = new SupportClass.ListCollectionSupport();
		}

		private void DoCollections(string classPackage, Element classElement, string xmlName, string interfaceClass,
		                           string implementingClass, MultiMap inheritedMeta)
		{
			string originalInterface = interfaceClass;
			string originalImplementation = implementingClass;

			for (IEnumerator collections = classElement.SelectNodes("urn:" + xmlName, CodeGenerator.nsmgr).GetEnumerator();
			     collections.MoveNext();)
			{
				Element collection = (Element) collections.Current;
				MultiMap metaForCollection = MetaAttributeHelper.LoadAndMergeMetaMap(collection, inheritedMeta);
				string propertyName = (collection.Attributes["name"] == null ? string.Empty : collection.Attributes["name"].Value);

				//TODO: map and set in .net
				//		Small hack to switch over to sortedSet/sortedMap if sort is specified. (that is sort != unsorted)
				string sortValue = (collection.Attributes["sort"] == null ? null : collection.Attributes["sort"].Value);
				if ((object) sortValue != null && !"unsorted".Equals(sortValue) && !"".Equals(sortValue.Trim()))
				{
					if ("map".Equals(xmlName))
					{
						interfaceClass = typeof(IDictionary).FullName;
						implementingClass = typeof(IDictionary).FullName;
					}
					else if ("set".Equals(xmlName))
					{
						interfaceClass = typeof(ISet).FullName;
						implementingClass = typeof(ISet).FullName;
					}
				}
				else
				{
					interfaceClass = originalInterface;
					implementingClass = originalImplementation;
				}

				ClassName interfaceClassName = new ClassName(interfaceClass);
				ClassName implementationClassName = new ClassName(implementingClass);

				// add an import and field for this collection
				AddImport(interfaceClassName);
				// import implementingClassName should only be 
				// added if the initialisaiton code of the field 
				// is actually used - and currently it isn't!
				//addImport(implementingClassName);

				ClassName foreignClass = null;
				SupportClass.SetSupport foreignKeys = null;
				// Collect bidirectional data
				if (collection.SelectNodes("urn:one-to-many", CodeGenerator.nsmgr).Count != 0)
				{
					foreignClass = new ClassName(collection["one-to-many"].Attributes["class"].Value);
				}
				else if (collection.SelectNodes("urn:many-to-many", CodeGenerator.nsmgr).Count != 0)
				{
					foreignClass = new ClassName(collection["many-to-many"].Attributes["class"].Value);
				}

				// Do the foreign keys and import
				if (foreignClass != null)
				{
					// Collect the keys
					foreignKeys = new SupportClass.HashSetSupport();
					if (collection["key"].Attributes["column"] != null)
						foreignKeys.Add(collection["key"].Attributes["column"].Value);

					for (IEnumerator iter = collection["key"].SelectNodes("urn:column", CodeGenerator.nsmgr).GetEnumerator();
					     iter.MoveNext();)
					{
						if (((Element) iter.Current).Attributes["name"] != null)
							foreignKeys.Add(((Element) iter.Current).Attributes["name"].Value);
					}

					AddImport(foreignClass);
				}
				FieldProperty cf =
					new FieldProperty(collection, this, propertyName, interfaceClassName, implementationClassName, false, foreignClass,
					                  foreignKeys, metaForCollection);
				AddFieldProperty(cf);
				if (collection.SelectNodes("urn:composite-element", CodeGenerator.nsmgr).Count != 0)
				{
					for (
						IEnumerator compositeElements =
							collection.SelectNodes("urn:composite-element", CodeGenerator.nsmgr).GetEnumerator();
						compositeElements.MoveNext();)
					{
						Element compositeElement = (Element) compositeElements.Current;
						string compClass = compositeElement.Attributes["class"].Value;

						try
						{
							ClassMapping mapping = new ClassMapping(classPackage, compositeElement, this, true, MetaAttribs);
							ClassName classType = new ClassName(compClass);
							// add an import and field for this property
							AddImport(classType);
							object tempObject;
							tempObject = mapping;
							components[mapping.FullyQualifiedName] = tempObject;
						}
						catch (Exception e)
						{
							log.Error("Error building composite-element " + compClass, e);
						}
					}
				}
			}
		}

		private void DoArrays(Element classElement, string type, MultiMap inheritedMeta)
		{
			for (IEnumerator arrays = classElement.SelectNodes("urn:" + type, CodeGenerator.nsmgr).GetEnumerator();
			     arrays.MoveNext();)
			{
				Element array = (Element) arrays.Current;
				MultiMap metaForArray = MetaAttributeHelper.LoadAndMergeMetaMap(array, inheritedMeta);
				string role = array.Attributes["name"].Value;
				string elementClass = (array.Attributes["element-class"] == null ? null : array.Attributes["element-class"].Value);
				if ((Object) elementClass == null)
				{
					Element elt = array["element"];
					if (elt == null)
						elt = array["one-to-many"];
					if (elt == null)
						elt = array["many-to-many"];
					if (elt == null)
						elt = array["composite-element"];
					if (elt == null)
					{
						log.Warn("skipping collection with subcollections");
						continue;
					}
					elementClass = (elt.Attributes["type"] == null ? null : elt.Attributes["type"].Value);
					if ((Object) elementClass == null)
						elementClass = (elt.Attributes["class"] == null ? string.Empty : elt.Attributes["class"].Value);
				}
				ClassName cn = GetFieldType(elementClass, false, true);

				AddImport(cn);
				FieldProperty af = new FieldProperty(array, this, role, cn, false, metaForArray);
				AddFieldProperty(af);
			}
		}

		private ClassName GetFieldType(string hibernateType)
		{
			return GetFieldType(hibernateType, false, false);
		}

		/// <summary> Return a ClassName for a hibernatetype.
		/// 
		/// </summary>
		/// <param name="hibernateType">Name of the hibernatetype (e.g. "binary")
		/// </param>
		/// <param name="isArray">if the type should be postfixed with array brackes ("[]")
		/// </param>
		/// <param name="mustBeNullable"></param>
		/// <returns>
		/// </returns>
		private ClassName GetFieldType(string hibernateType, bool mustBeNullable, bool isArray)
		{
			string postfix = isArray ? "[]" : "";
			// deal with hibernate binary type
			ClassName cn = null;
			if (hibernateType.Equals("binary"))
			{
				cn = new ClassName("byte[]" + postfix);
				return cn;
			}
			else
			{
				// Transform it if it's a basic .net type
				hibernateType = GetTypeForJavaType(hibernateType);

				IType basicType = TypeFactory.Basic(hibernateType);
				if (basicType != null)
				{
					cn = new ClassName(basicType.ReturnedClass.Name + postfix);
					return cn;
				}
				else
				{
					// check and resolve correct type if it is an usertype
					hibernateType = GetTypeForUserType(hibernateType);
					cn = new ClassName(hibernateType + postfix);
					// add an import and field for this property
					AddImport(cn);
					return cn;
				}
			}
		}

		/// <summary>
		/// Substitute basic Hibernate types for the .net ones
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		private string GetTypeForJavaType(string type)
		{
			if (type.StartsWith("java"))
			{
				switch (type)
				{
					case "java.lang.Integer":
						return "Int32";

					case "java.lang.Boolean":
						return "bool";

					case "java.util.Date":
						return "Date";

					default:
						return type.Replace("java.lang.", "").Replace("java.util", "");
				}
			}

			return type;
		}

		/// <summary>Returns name of returnedclass if type is an UserType *</summary>
		private string GetTypeForUserType(string type)
		{
			System.Type clazz = null;
			try
			{
				if (type.IndexOf("(") > 0)
				{
					type = type.Substring(0, type.IndexOf("("));
				}

				clazz = ReflectHelper.ClassForName(type);

				if (typeof(IUserType).IsAssignableFrom(clazz))
				{
					IUserType ut = (IUserType) SupportClass.CreateNewInstance(clazz);
					log.Debug("Resolved usertype: " + type + " to " + ut.ReturnedType.Name);
					string t = ClazzToName(ut.ReturnedType);
					return t;
				}

				if (typeof(CompositeUserType).IsAssignableFrom(clazz))
				{
					CompositeUserType ut = (CompositeUserType) SupportClass.CreateNewInstance(clazz);
					log.Debug("Resolved composite usertype: " + type + " to " + ut.ReturnedClass.Name);
					string t = ClazzToName(ut.ReturnedClass);
					return t;
				}
			}
			catch (FileNotFoundException e)
			{
				if (type.IndexOf(",") > 0)
					type = type.Substring(0, type.IndexOf(","));
				log.Warn("Could not find UserType: " + type + ". Using the type '" + type + "' directly instead. (" + e.ToString() +
				         ")");
			}
			catch (UnauthorizedAccessException iae)
			{
				log.Warn("Error while trying to resolve UserType. Using the type '" + type + "' directly instead. (" +
				         iae.ToString() + ")");
			}
			catch (Exception e)
			{
				log.Warn("Error while trying to resolve UserType. Using the type '" + type + "' directly instead. (" + e.ToString() +
				         ")");
			}

			return type;
		}

		private string ClazzToName(System.Type cl)
		{
			string s = null;

			if (cl.IsArray)
			{
				s = ClazzToName(cl.GetElementType()) + "[]";
			}
			else
			{
				s = cl.FullName;
			}

			return s;
		}

		#endregion

		#region Protected methods

		protected internal virtual void InitWith(string classPackage, ClassName mySuperClass, Element classElement,
		                                         bool component, MultiMap inheritedMeta)
		{
			string fullyQualifiedName = (classElement.Attributes[component ? "class" : "name"] == null
			                             	? string.Empty : classElement.Attributes[component ? "class" : "name"].Value);

			if (fullyQualifiedName.IndexOf('.') < 0 && (object) classPackage != null && classPackage.Trim().Length > 0)
			{
				fullyQualifiedName = classPackage + "." + fullyQualifiedName;
			}

			log.Debug("Processing mapping for class: " + fullyQualifiedName);

			MetaAttribs = MetaAttributeHelper.LoadAndMergeMetaMap(classElement, inheritedMeta);

			//    class & package names
			name = new ClassName(fullyQualifiedName);

			if (GetMeta("generated-class") != null)
			{
				generatedName = new ClassName(GetMetaAsString("generated-class").Trim());
				shouldBeAbstract_Renamed_Field = true;
				log.Warn("Generating " + generatedName + " instead of " + name);
			}
			else
			{
				generatedName = name;
			}

			if (mySuperClass != null)
			{
				this.superClass = mySuperClass.Name;
				AddImport(mySuperClass); // can only be done AFTER this class gets its own name.
			}

			// get the properties defined for this class
			SupportClass.ListCollectionSupport propertyList = new SupportClass.ListCollectionSupport();
			propertyList.AddAll(classElement.SelectNodes("urn:property", CodeGenerator.nsmgr));
			propertyList.AddAll(classElement.SelectNodes("urn:version", CodeGenerator.nsmgr));
			propertyList.AddAll(classElement.SelectNodes("urn:timestamp", CodeGenerator.nsmgr));
			propertyList.AddAll(classElement.SelectNodes("urn:key-property", CodeGenerator.nsmgr));
			propertyList.AddAll(classElement.SelectNodes("urn:any", CodeGenerator.nsmgr));

			// get all many-to-one associations defined for the class
			SupportClass.ListCollectionSupport manyToOneList = new SupportClass.ListCollectionSupport();
			manyToOneList.AddAll(classElement.SelectNodes("urn:many-to-one", CodeGenerator.nsmgr));
			manyToOneList.AddAll(classElement.SelectNodes("urn:key-many-to-one", CodeGenerator.nsmgr));

			XmlAttribute att = classElement.Attributes["proxy"];
			if (att != null)
			{
				proxyClass = att.Value;
				if (proxyClass.IndexOf(",") > 0)
					proxyClass = proxyClass.Substring(0, proxyClass.IndexOf(","));
			}

			Element id = classElement["id"];

			if (id != null)
			{
				propertyList.Insert(0, id);
				// implementEquals();
			}

			// composite id
			Element cmpid = classElement["composite-id"];
			if (cmpid != null)
			{
				ImplementEquals();
				string cmpname = (cmpid.Attributes["name"] == null ? null : cmpid.Attributes["name"].Value);
				string cmpclass = (cmpid.Attributes["class"] == null ? null : cmpid.Attributes["class"].Value);
				if ((Object) cmpclass == null || cmpclass.Equals(string.Empty))
				{
					//Embedded composite id
					//implementEquals();
					propertyList.AddAll(0, cmpid.SelectNodes("urn:key-property", CodeGenerator.nsmgr));
					manyToOneList.AddAll(0, cmpid.SelectNodes("urn:key-many-to-one", CodeGenerator.nsmgr));
				}
				else
				{
					//Composite id class
					ClassMapping mapping = new ClassMapping(classPackage, cmpid, this, true, MetaAttribs);
					MultiMap metaForCompositeid = MetaAttributeHelper.LoadAndMergeMetaMap(cmpid, MetaAttribs);
					mapping.ImplementEquals();
					ClassName classType = new ClassName(cmpclass);
					// add an import and field for this property
					AddImport(classType);
					FieldProperty cmpidfield =
						new FieldProperty(cmpid, this, cmpname, classType, false, true, false, metaForCompositeid);
					AddFieldProperty(cmpidfield);
					object tempObject;
					tempObject = mapping;
					components[mapping.FullyQualifiedName] = tempObject;
				}
			}

			// checked after the default sets of implement equals.
			if (GetMetaAsBool("implement-equals"))
			{
				ImplementEquals();
			}

			// derive the class imports and fields from the properties
			for (IEnumerator properties = propertyList.GetEnumerator(); properties.MoveNext();)
			{
				Element property = (Element) properties.Current;

				MultiMap metaForProperty = MetaAttributeHelper.LoadAndMergeMetaMap(property, MetaAttribs);
				string propertyName = (property.Attributes["name"] == null ? null : property.Attributes["name"].Value);
				if ((Object) propertyName == null || propertyName.Trim().Equals(string.Empty))
				{
					continue; //since an id doesn't necessarily need a name
				}

				// ensure that the type is specified
				string type = (property.Attributes["type"] == null ? null : property.Attributes["type"].Value);
				if ((Object) type == null && cmpid != null)
				{
					// for composite-keys
					type = (property.Attributes["class"] == null ? null : property.Attributes["class"].Value);
				}
				if ("timestamp".Equals(property.LocalName))
				{
					type = "System.DateTime";
				}

				if ("any".Equals(property.LocalName))
				{
					type = "System.Object";
				}

				if ((Object) type == null || type.Trim().Equals(string.Empty))
				{
					if (property == id)
						type = "Int32";
					else
						type = "String";
					log.Warn("property \"" + propertyName + "\" in class " + Name + " is missing a type attribute, guessing " + type);
				}


				// handle in a different way id and properties...
				// ids may be generated and may need to be of object type in order to support
				// the unsaved-value "null" value.
				// Properties may be nullable (ids may not)
				if (property == id)
				{
					Element generator = property["generator"];
					string unsavedValue = (property.Attributes["unsaved-value"] == null
					                       	? null : property.Attributes["unsaved-value"].Value);
					bool mustBeNullable = ((Object) unsavedValue != null && unsavedValue.Equals("null"));
					bool generated =
						!(generator.Attributes["class"] == null ? string.Empty : generator.Attributes["class"].Value).Equals("assigned");
					ClassName rtype = GetFieldType(type, mustBeNullable, false);
					AddImport(rtype);
					FieldProperty idField =
						new FieldProperty(property, this, propertyName, rtype, false, true, generated, metaForProperty);
					AddFieldProperty(idField);
				}
				else
				{
					string notnull = (property.Attributes["not-null"] == null ? null : property.Attributes["not-null"].Value);
					// if not-null property is missing lets see if it has been
					// defined at column level
					if ((Object) notnull == null)
					{
						Element column = property["column"];
						if (column != null)
							notnull = (column.Attributes["not-null"] == null ? null : column.Attributes["not-null"].Value);
					}
					bool nullable = ((Object) notnull == null || notnull.Equals("false"));
					bool key = property.LocalName.StartsWith("key-"); //a composite id property
					ClassName t = GetFieldType(type);
					AddImport(t);
					FieldProperty stdField =
						new FieldProperty(property, this, propertyName, t, nullable && !key, key, false, metaForProperty);
					AddFieldProperty(stdField);
				}
			}

			// one to ones
			for (IEnumerator onetoones = classElement.SelectNodes("urn:one-to-one", CodeGenerator.nsmgr).GetEnumerator();
			     onetoones.MoveNext();)
			{
				Element onetoone = (Element) onetoones.Current;

				MultiMap metaForOneToOne = MetaAttributeHelper.LoadAndMergeMetaMap(onetoone, MetaAttribs);
				string propertyName = (onetoone.Attributes["name"] == null ? string.Empty : onetoone.Attributes["name"].Value);

				// ensure that the class is specified
				string clazz = (onetoone.Attributes["class"] == null ? string.Empty : onetoone.Attributes["class"].Value);
				if (clazz.Length == 0)
				{
					log.Warn("one-to-one \"" + name + "\" in class " + Name + " is missing a class attribute");
					continue;
				}
				ClassName cn = GetFieldType(clazz);
				AddImport(cn);
				FieldProperty fm = new FieldProperty(onetoone, this, propertyName, cn, true, metaForOneToOne);
				AddFieldProperty(fm);
			}

			// many to ones - TODO: consolidate with code above
			for (IEnumerator manytoOnes = manyToOneList.GetEnumerator(); manytoOnes.MoveNext();)
			{
				Element manyToOne = (Element) manytoOnes.Current;

				MultiMap metaForManyToOne = MetaAttributeHelper.LoadAndMergeMetaMap(manyToOne, MetaAttribs);
				string propertyName = (manyToOne.Attributes["name"] == null ? string.Empty : manyToOne.Attributes["name"].Value);

				// ensure that the type is specified
				string type = (manyToOne.Attributes["class"] == null ? string.Empty : manyToOne.Attributes["class"].Value);
				if (type.Length == 0)
				{
					log.Warn("many-to-one \"" + propertyName + "\" in class " + Name + " is missing a class attribute");
					continue;
				}
				ClassName classType = new ClassName(type);

				// is it nullable?
				string notnull = (manyToOne.Attributes["not-null"] == null ? null : manyToOne.Attributes["not-null"].Value);
				bool nullable = ((Object) notnull == null || notnull.Equals("false"));
				bool key = manyToOne.LocalName.StartsWith("key-"); //a composite id property

				// add an import and field for this property
				AddImport(classType);
				FieldProperty f =
					new FieldProperty(manyToOne, this, propertyName, classType, nullable && !key, key, false, metaForManyToOne);
				AddFieldProperty(f);
			}

			// collections
			DoCollections(classPackage, classElement, "list", "System.Collections.IList", "System.Collections.ArrayList",
			              MetaAttribs);
			DoCollections(classPackage, classElement, "map", "System.Collections.IDictionary", "System.Collections.Hashtable",
			              MetaAttribs);
			DoCollections(classPackage, classElement, "set", "Iesi.Collections.ISet", "Iesi.Collections.HashedSet", MetaAttribs);
			DoCollections(classPackage, classElement, "bag", "System.Collections.IList", "System.Collections.ArrayList",
			              MetaAttribs);
			DoCollections(classPackage, classElement, "idbag", "System.Collections.IList", "System.Collections.ArrayList",
			              MetaAttribs);
			DoArrays(classElement, "array", MetaAttribs);
			DoArrays(classElement, "primitive-array", MetaAttribs);


			//components
			for (IEnumerator iter = classElement.SelectNodes("urn:component", CodeGenerator.nsmgr).GetEnumerator();
			     iter.MoveNext();)
			{
				Element cmpe = (Element) iter.Current;
				MultiMap metaForComponent = MetaAttributeHelper.LoadAndMergeMetaMap(cmpe, MetaAttribs);
				string cmpname = (cmpe.Attributes["name"] == null ? null : cmpe.Attributes["name"].Value);
				string cmpclass = (cmpe.Attributes["class"] == null ? null : cmpe.Attributes["class"].Value);
				if ((Object) cmpclass == null || cmpclass.Equals(string.Empty))
				{
					log.Warn("component \"" + cmpname + "\" in class " + Name + " does not specify a class");
					continue;
				}
				ClassMapping mapping = new ClassMapping(classPackage, cmpe, this, true, MetaAttribs);

				ClassName classType = new ClassName(cmpclass);
				// add an import and field for this property
				AddImport(classType);
				FieldProperty ff = new FieldProperty(cmpe, this, cmpname, classType, false, metaForComponent);
				AddFieldProperty(ff);
				object tempObject2;
				tempObject2 = mapping;
				components[mapping.FullyQualifiedName] = tempObject2;
			}

			//    subclasses (done last so they can access this superclass for info)
			for (IEnumerator iter = classElement.SelectNodes("urn:subclass", CodeGenerator.nsmgr).GetEnumerator();
			     iter.MoveNext();)
			{
				Element subclass = (Element) iter.Current;
				ClassMapping subclassMapping = new ClassMapping(classPackage, this, name, this, subclass, MetaAttribs);
				AddSubClass(subclassMapping);
			}

			for (IEnumerator iter = classElement.SelectNodes("urn:joined-subclass", CodeGenerator.nsmgr).GetEnumerator();
			     iter.MoveNext();)
			{
				Element subclass = (Element) iter.Current;
				ClassMapping subclassMapping = new ClassMapping(classPackage, this, name, this, subclass, MetaAttribs);
				AddSubClass(subclassMapping);
			}

			ValidateMetaAttributes();
		}

		#endregion

		private void AddFieldProperty(FieldProperty fieldProperty)
		{
			if (fieldProperty.ParentClass == null)
			{
				fields.Add(fieldProperty);
			}
			else
			{
				throw new SystemException("Field " + fieldProperty + " is already associated with a class: " +
				                          fieldProperty.ParentClass);
			}
		}

		#region Public methods

		public virtual void ImplementEquals()
		{
			log.Info("Flagging class to implement Equals() and GetHashCode().");
			mustImplementEquals_Renamed_Field = true;
		}

		public virtual bool MustImplementEquals()
		{
			return (!Interface) && mustImplementEquals_Renamed_Field;
		}

		// We need a minimal constructor only if it's different from
		// the full constructor or the no-arg constructor.
		// A minimal construtor is one that lets
		// you specify only the required fields.
		public virtual bool NeedsMinimalConstructor()
		{
			return
				(AllFieldsForFullConstructor.Count != AllFieldsForMinimalConstructor.Count) &&
				AllFieldsForMinimalConstructor.Count > 0;
		}

		public virtual void AddImport(ClassName className)
		{
			// if the package is java.lang or our own package don't add
			if (!className.InJavaLang() && !className.InSamePackage(generatedName) && !className.Primitive &&
			    className.PackageName != null && className.PackageName.Length > 0)
			{
				/*
				if (className.Array)
				{
					//TODO: fix imports
					imports.Add(className.PackageName.Substring(0, (className.FullyQualifiedName.Length - 2) - (0))); // remove []
				}
				else
				{
				*/
				//TODO: fix imports
				imports.Add(className.PackageName);
				//}
			}
		}

		public virtual void AddImport(string className)
		{
			ClassName cn = new ClassName(className);
			AddImport(cn);
		}

		#endregion

		/// <summary> Method shouldBeAbstract.</summary>
		/// <returns> boolean
		/// </returns>
		public virtual bool ShouldBeAbstract()
		{
			return shouldBeAbstract_Renamed_Field;
		}

		// Based on some raw heuristics the following method validates the provided metaattribs.
		internal virtual void ValidateMetaAttributes()
		{
			// Inform that "extends" is not used if this one is a genuine subclass
			if ((Object) SuperClass != null && GetMeta("extends") != null)
			{
				log.Warn("Warning: meta attribute extends='" + GetMetaAsString("extends") + "' will be ignored for subclass " + name);
			}
		}

		public override string ToString()
		{
			return "ClassMapping: " + name.FullyQualifiedName;
		}

		public virtual void AddSubClass(ClassMapping subclassMapping)
		{
			subclasses.Add(subclassMapping);
		}

		public virtual void AddImport(System.Type clazz)
		{
			AddImport(clazz.FullName);
		}
	}
}