using System;
using System.Reflection;

namespace NHibernate.Search.Backend
{
    /// <summary>
    /// work unit. Only make sense inside the same session since it uses the scope principle
    /// </summary>
    public class Work
    {
        private readonly object entity;
        private readonly object id;
        private readonly MemberInfo idGetter;
        private readonly WorkType workType;

        public Work(object entity, Object id, WorkType type)
        {
            this.entity = entity;
            this.id = id;
            workType = type;
        }

        public Work(object entity, MemberInfo idGetter, WorkType type)
        {
            this.entity = entity;
            this.idGetter = idGetter;
            workType = type;
        }

        public object Entity
        {
            get { return entity; }
        }

        public object Id
        {
            get { return id; }
        }

        public MemberInfo IdGetter
        {
            get { return idGetter; }
        }

        public WorkType WorkType
        {
            get { return workType; }
        }
    }
}