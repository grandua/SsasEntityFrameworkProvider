using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModelExample
{
    /// <remarks>
    /// Layer Supertype design pattern
    /// </remarks>
    public abstract class ModelBase
    {
        /// <summary>
        /// Auto-incremented Id
        /// Not visible to end users
        /// </summary> //TODO: add VersionId for production code, because ModifiedTime may be not unique
        public int Id { get; set; }
        /// <summary>
        /// Our internal unique code
        /// Visible to end users
        /// </summary>
        public string Code { get; set; }
        ///// <summary>
        ///// Who and when created or modified
        ///// </summary>
        public AuditTrail AuditTrail { get; set; }
    }
}
