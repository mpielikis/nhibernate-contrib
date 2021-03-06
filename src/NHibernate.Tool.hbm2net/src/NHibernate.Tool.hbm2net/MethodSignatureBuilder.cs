using System;
using System.Collections;
using System.Text;

namespace NHibernate.Tool.hbm2net
{
	/// <summary> Build method signatures given lots of parameters
	/// Date: Apr 15, 2003
	/// Time: 7:30:09 PM
	/// </summary>
	/// <author>  Matt Hall (matt2k(at)users.sf.net)
	/// </author>
	public class MethodSignatureBuilder
	{
		private void InitBlock()
		{
			//UPGRADE_ISSUE: Class hierarchy differences between 'java.util.ArrayList' and 'System.Collections.ArrayList' may cause compilation errors. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1186"'
			paramList = new ArrayList();
			//UPGRADE_ISSUE: Class hierarchy differences between 'java.util.ArrayList' and 'System.Collections.ArrayList' may cause compilation errors. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1186"'
			throwsList = new ArrayList();
		}

		public virtual string Name
		{
			get { return name; }

			set { this.name = value; }
		}

		public virtual string ReturnType
		{
			get { return returnType; }

			set { this.returnType = value; }
		}

		public virtual string AccessModifier
		{
			get { return accessModifier; }

			set { this.accessModifier = value; }
		}

		//UPGRADE_ISSUE: Class hierarchy differences between 'java.util.ArrayList' and 'System.Collections.ArrayList' may cause compilation errors. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1186"'
		public virtual ArrayList ParamList
		{
			get { return paramList; }

			set { this.paramList = value; }
		}

		private string name = "";
		private string returnType = "";
		private string accessModifier = "";
		//UPGRADE_ISSUE: Class hierarchy differences between 'java.util.ArrayList' and 'System.Collections.ArrayList' may cause compilation errors. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1186"'
		//UPGRADE_NOTE: The initialization of  'paramList' was moved to method 'InitBlock'. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1005"'
		private ArrayList paramList;
		//UPGRADE_ISSUE: Class hierarchy differences between 'java.util.ArrayList' and 'System.Collections.ArrayList' may cause compilation errors. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1186"'
		//UPGRADE_NOTE: The initialization of  'throwsList' was moved to method 'InitBlock'. 'ms-help://MS.VSCC.2003/commoner/redir/redirect.htm?keyword="jlca1005"'
		private ArrayList throwsList;

		public MethodSignatureBuilder(string methodName, string returnType, string accessModifier)
		{
			InitBlock();
			name = methodName;
			this.returnType = returnType;
			this.accessModifier = accessModifier;
		}

		public virtual string BuildMethodSignature()
		{
			StringBuilder sb = new StringBuilder(accessModifier + " " + returnType + " " + name + "(");

			for (int i = 0; i < paramList.Count; i++)
			{
				string param = (String) paramList[i];
				sb.Append(param);

				if (i < paramList.Count - 1)
				{
					sb.Append(", ");
				}
			}
			sb.Append(") ");

			for (int i = 0; i < throwsList.Count; i++)
			{
				if (i == 0)
				{
					sb.Append(" throws ");
				}
				string thr = (String) throwsList[i];
				sb.Append(thr);

				if (i < throwsList.Count - 1)
				{
					sb.Append(", ");
				}
			}

			sb.Append(" {");
			return sb.ToString();
		}

		public virtual void AddParameter(string param)
		{
			this.paramList.Add(param);
		}

		public virtual void AddThrows(string throwsString)
		{
			this.throwsList.Add(throwsString);
		}
	}
}