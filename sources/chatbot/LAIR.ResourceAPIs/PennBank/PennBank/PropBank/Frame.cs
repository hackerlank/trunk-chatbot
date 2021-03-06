using System;
using System.Collections.Generic;
using System.Text;

namespace LAIR.ResourceAPIs.PennBank.PropBank
{
    /// <summary>
    /// Represents a PropBank frame
    /// </summary>
    public class Frame
    {
        private string _verb;
        private Dictionary<int, RoleSet> _idRoleSet;

        /// <summary>
        /// Gets or sets the verb. This is the lemma form, which is used in the verb.id role set notation
        /// </summary>
        public string Verb
        {
            get { return _verb; }
        }

        /// <summary>
        /// Gets an enumerator over role sets in this frame
        /// </summary>
        public IEnumerable<RoleSet> RoleSets
        {
            get { return _idRoleSet.Values; }
        }

        /// <summary>
        /// Gets the number of role sets in this frame
        /// </summary>
        public int RoleSetCount
        {
            get { return _idRoleSet.Count; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="verb">Verb for this Frame</param>
        public Frame(string verb)
        {
            _verb = verb;

            _idRoleSet = new Dictionary<int, RoleSet>();
        }

        /// <summary>
        /// Gets nicely formatted string representation of this frame
        /// </summary>
        /// <returns>Frame description</returns>
        public override string ToString()
        {
            StringBuilder s = new StringBuilder(_verb + " role sets:" + Environment.NewLine + Environment.NewLine);

            foreach (RoleSet roleSet in _idRoleSet.Values)
                s.Append(roleSet + Environment.NewLine + Environment.NewLine);

            return s.ToString().Trim();
        }

        /// <summary>
        /// Adds a role set to this frame
        /// </summary>
        /// <param name="roleSet">Role set to add</param>
        public void AddRoleSet(RoleSet roleSet)
        {
            _idRoleSet.Add(roleSet.ID, roleSet);
        }

        /// <summary>
        /// Gets role set by ID
        /// </summary>
        /// <param name="roleSetID">ID of role set to get</param>
        /// <returns>Role set</returns>
        public RoleSet GetRoleSet(int roleSetID)
        {
            return _idRoleSet[roleSetID];
        }

        /// <summary>
        /// Gets whether or not this frame contains a role set
        /// </summary>
        /// <param name="roleSetID">ID of role set to check for</param>
        /// <returns>True if role set is contained, false otherwise</returns>
        public bool Contains(int roleSetID)
        {
            return _idRoleSet.ContainsKey(roleSetID);
        }
    }
}
